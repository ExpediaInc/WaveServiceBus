﻿/* Copyright 2014 Jonathan Holland.
*
*  Licensed under the Apache License, Version 2.0 (the "License");
*  you may not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
*  http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing, software
*  distributed under the License is distributed on an "AS IS" BASIS,
*  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*  See the License for the specific language governing permissions and
*  limitations under the License.
*/

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using Wave.Transports.RabbitMQ.Extensions;
using Wave.Utility;

namespace Wave.Transports.RabbitMQ
{    
    public class RabbitMQTransport : ITransport, IDisposable
    {
        private static readonly TimeSpan SendChannelMonitorInterval = TimeSpan.FromMinutes(5.0);

        private static readonly Action<IBasicProperties, IDictionary<string, string>> DoNothingOnSend = (properties, metadata) => { };

        private readonly ConcurrentDictionary<Thread, IModel> sendChannelsByThread = new ConcurrentDictionary<Thread, IModel>();

        private readonly RabbitConnectionManager connectionManager;        
        private readonly String delayQueueName;
        private readonly String errorQueueName;
        private readonly String primaryQueueName;
        private readonly IConfigurationContext configuration;

        private readonly Lazy<string> encodingName;

        private readonly ThreadLocal<IModel> sendChannel;

        private readonly Action<IBasicProperties, IDictionary<string, string>> onSend;

        private readonly Timer sendChannelMonitor;

        public RabbitMQTransport(IConfigurationContext configuration)
            : this(configuration.QueueNameResolver.GetPrimaryQueueName(), configuration)
        {            
        }

        internal RabbitMQTransport(string baseQueueName, IConfigurationContext configuration)
        {
            this.configuration = MergeConfiguration(configuration);

            // Cache encoding name as looking it from the Encoding class is expensive.
            this.encodingName = new Lazy<string>(() => configuration.Serializer.Encoding.EncodingName);

            this.connectionManager = new RabbitConnectionManager(new Uri(configuration.GetConnectionString()));
            this.primaryQueueName = baseQueueName;
            this.delayQueueName = String.Format("{0}_Delay", this.primaryQueueName);
            this.errorQueueName = String.Format("{0}_Error", this.primaryQueueName);

            this.DeclareExchange();

            this.sendChannel = new ThreadLocal<IModel>(valueFactory: CreateSendChannel);

            this.onSend = this.configuration.GetOnSendingMessageAction() ?? DoNothingOnSend;

            this.sendChannelMonitor = new Timer(
                MonitorSendChannels,
                state: null,
                dueTime: SendChannelMonitorInterval,
                period: SendChannelMonitorInterval);
        }

        public void GetDelayMessages(CancellationToken token, Action<RawMessage, Action, Action> onMessageReceived)
        {
            // NOTE:  Messages from the delay queue can be acked in an order different than the order of message delivery, since
            //        the order of acks depends on the DelayUntil DateTime value.
            //        If AckMultiple=true and a message with a later delivery tag is acked, then the channel throws an error
            //        when trying to ack a message with a previous delivery tag since it's considered a duplicate ack.
            const bool AckMultiple = false;
            this.GetMessages(this.delayQueueName, AckMultiple, token, onMessageReceived, this.configuration.GetDelayQueuePrefetchCount().Value);
        }

        public void GetMessages(CancellationToken token, Action<RawMessage, Action, Action> onMessageReceived)
        {
            const bool AckMultiple = true;
            this.GetMessages(this.primaryQueueName, AckMultiple, token, onMessageReceived, this.configuration.GetPrefetchCountPerWorker().Value);
        }

        public void InitializeForConsuming()
        {
            this.DeclareExchange(); // exchange may not exist at this point in an autorecovery event

            using (var channel = this.connectionManager.GetChannel())
            {
                var autoDelete = this.configuration.GetAutoDeleteQueues();
                var primaryQueueArguments = this.configuration.GetPrimaryQueueArguments();

                var workQueue = channel.QueueDeclare(this.primaryQueueName, true, autoDelete, autoDelete, primaryQueueArguments);
                var delayQueue = channel.QueueDeclare(this.delayQueueName, true, autoDelete, autoDelete, null);
                var errorQueue = channel.QueueDeclare(this.errorQueueName, true, autoDelete, autoDelete, null);

                // Create a routing key for the work queue name for direct sends
                channel.QueueBind(workQueue, this.configuration.GetExchange(), this.primaryQueueName);

                // Create a routing key for the delay queue
                channel.QueueBind(delayQueue, this.configuration.GetExchange(), this.delayQueueName);

                // Create a routing key for the error queue
                channel.QueueBind(errorQueue, this.configuration.GetExchange(), this.errorQueueName);
            }
        }

        public void InitializeForPublishing()
        {
            // No Op for RabbitMQ Transport
        }

        public void RegisterSubscription(string subscription)
        {
            // Create a binding on work queue, set the routing key to the susbcription
            using (var channel = this.connectionManager.GetChannel())
            {
                channel.QueueBind(this.primaryQueueName, this.configuration.GetExchange(), subscription);
            }
        }

        public void Send(string subscription, object message)
        {
            this.Send(subscription, RawMessage.Create(message));
        }

        public void Send<T>(string subscription, IMessage<T> message)
        {
            this.Send(subscription, RawMessage.Create(message));
        }

        public void Send(string subscription, RawMessage message)
        {
            if (this.sendChannel.Value.IsClosed)
            {
                this.sendChannel.Value.Dispose();
                DestroySendChannel(Thread.CurrentThread);
                this.sendChannel.Value = CreateSendChannel();
            }

            this.sendChannel.Value.BasicPublish(
                this.configuration.GetExchange(),
                subscription,
                this.CreateProperties(message, this.sendChannel.Value),
                this.configuration.Serializer.Encoding.GetBytes(message.Data));
        }

        public void SendToDelay(RawMessage message)
        {
            this.Send(this.delayQueueName, message);
        }

        public void SendToError(RawMessage message)
        {
            this.Send(this.errorQueueName, message);
        }

        public void SendToPrimary(RawMessage message)
        {
            this.Send(this.primaryQueueName, message);
        }

        public void Shutdown()
        {
            Dispose();

            // Force the RabbitMQ connection to shutdown.
            this.connectionManager.Shutdown();
        }

        public void Dispose()
        {
            this.sendChannel.Dispose();

            foreach (Thread thread in AllSendChannelThreads)
            {
                DestroySendChannel(thread);
            }

            this.sendChannelMonitor.Dispose();
        }

        internal Version ServerVersion
        {
            get { return this.connectionManager.ServerVersion; }
        }

        private IEnumerable<Thread> AllSendChannelThreads
            => this.sendChannelsByThread.Keys;

        private IEnumerable<Thread> DeadSendChannelThreads
            => AllSendChannelThreads.Where(thread => !thread.IsAlive);

        private void DeclareExchange()
        {
            using (var channel = this.connectionManager.GetChannel())
            {
                // Create exchange if it doesn't already exist
                channel.ExchangeDeclare(this.configuration.GetExchange(), "direct", true);
            }
        }

        private IBasicProperties CreateProperties(RawMessage message, IModel channel)
        {
            var properties = channel.CreateBasicProperties();
            properties.MessageId = message.Id.ToString();
            properties.AppId = this.primaryQueueName;
            properties.ContentType = this.configuration.Serializer.ContentType;
            properties.ContentEncoding = this.encodingName.Value;
            properties.SetPersistent(true);
            properties.Timestamp = new AmqpTimestamp((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            properties.Headers = new Dictionary<String,Object>();

            var messageHeaders = message.Headers;
            foreach (var pair in messageHeaders)
            {
                properties.Headers[pair.Key] = pair.Value;
            }

            this.onSend(properties, messageHeaders);

            return properties;
        }

        private void GetMessages(
            string queueName,
            bool ackMultiple,
            CancellationToken token,
            Action<RawMessage, Action, Action> onMessageReceived,
            ushort prefetchCount)
        {
            using (var channel = this.connectionManager.GetChannel())
            {
                var consumer = new QueueingBasicConsumer(channel);

                channel.BasicQos(0, prefetchCount, false);
                channel.BasicConsume(queueName, false, consumer);

                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        channel.Dispose();
                        token.ThrowIfCancellationRequested();
                    }

                    BasicDeliverEventArgs rabbitMessage;
                    if (consumer.Queue.Dequeue(1500, out rabbitMessage))
                    {
                        if (rabbitMessage == null)
                        {
                            continue;
                        }

                        var rawMessage = new RawMessage
                        {
                            Data = this.configuration.Serializer.Encoding.GetString(rabbitMessage.Body),
                            Id = new Guid(rabbitMessage.BasicProperties.MessageId)
                        };

                        foreach (var header in rabbitMessage.BasicProperties.Headers)
                        {
                            var key = header.Key;
                            rawMessage.Headers[key] = this.configuration.Serializer.Encoding.GetString((Byte[])header.Value);
                        }

                        // Callback and provide an accept and reject callback to the consumer                        
                        onMessageReceived(
                            rawMessage,
                            () => channel.BasicAck(rabbitMessage.DeliveryTag, ackMultiple),
                            () => channel.BasicNack(rabbitMessage.DeliveryTag, false, true));
                    }
                }
            }
        }

        private IModel CreateSendChannel()
        {
            return this.sendChannelsByThread.GetOrAdd(
                key: Thread.CurrentThread,
                valueFactory: thread => this.connectionManager.GetChannel());
        }

        private void MonitorSendChannels(object state)
        {
            foreach (Thread thread in DeadSendChannelThreads)
            {
                DestroySendChannel(thread);
            }
        }

        private void DestroySendChannel(Thread thread)
        {
            if (this.sendChannelsByThread.TryRemove(thread, out IModel channel))
            {
                DestroySendChannel(channel);
            }
        }

        private static void DestroySendChannel(IModel channel)
        {
            try
            {
                channel.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // continue if already disposed
            }
        }

        private IConfigurationContext MergeConfiguration(IConfigurationContext context)
        {
            var defaultSettings = new Configuration.ConfigurationSettings();
            var configSection = ConfigurationHelper.GetConfigSection<Configuration.ConfigurationSection>();

            // If the value is in the config use that
            // Otherwise, if the value is already set via fluent config use that
            // Otherwise use the default value
            if (configSection != null)
            {
                if (!String.IsNullOrWhiteSpace(configSection.ConnectionStringName))
                {
                    context.SetConnectionString(ConfigurationManager.ConnectionStrings[configSection.ConnectionStringName].ConnectionString);
                }
                else if (context.GetConnectionString() == null)
                {
                    context.SetConnectionString(defaultSettings.ConnectionString);
                }

                if (!String.IsNullOrWhiteSpace(configSection.Exchange))
                {
                    context.SetExchange(configSection.Exchange);
                }
                else if (context.GetExchange() == null)
                {
                    context.SetExchange(defaultSettings.Exchange);
                }

                if (!String.IsNullOrWhiteSpace(configSection.PrefetchCountPerWorker))
                {
                    context.SetPrefetchCountPerWorker(Convert.ToUInt16(configSection.PrefetchCountPerWorker));
                }
                else if (context.GetPrefetchCountPerWorker() == null)
                {
                    context.SetPrefetchCountPerWorker(Convert.ToUInt16(defaultSettings.PrefetchCountPerWorker));
                }

                if (!String.IsNullOrWhiteSpace(configSection.DelayQueuePrefetchCount))
                {
                    context.SetDelayQueuePrefetchCount(Convert.ToUInt16(configSection.DelayQueuePrefetchCount));
                }
                else if (context.GetDelayQueuePrefetchCount() == null)
                {
                    context.SetDelayQueuePrefetchCount(Convert.ToUInt16(defaultSettings.DelayQueuePrefetchCount));
                }
            }

            return context;
        }
    }
}
