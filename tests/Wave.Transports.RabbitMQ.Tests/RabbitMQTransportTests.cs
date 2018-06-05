/* Copyright 2014 Jonathan Holland.
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

using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RabbitMQ.Client;
using Wave.Configuration;
using Wave.Tests;
using Wave.Tests.Internal;

namespace Wave.Transports.RabbitMQ.Tests
{
    /// <summary>
    /// These tests require a running RabbitMQ broker - These are integration test and should not be ran by the build agent    
    /// 
    /// To run these tests the INTEGRATION directive will need to be set on the build
    /// </summary>
    [Category("Transport: RabbitMQ")]
    [TestFixture]    
    public class RabbitMQTransportTests : TransportTestBase
    {
        private const string connectionString = "amqp://guest:guest@localhost:5672/";
        private const string exchange = "Wave";
        private const string PriorityKey = "Priority";

        private static readonly Version priorityQueuesMinimumServerVersion = new Version(3, 5, 0);

        private ConcurrentBag<Guid> usedGuids = new ConcurrentBag<Guid>();

        public override ITransport GetTransport()
        {
            return GetTransport(primaryQueueArguments: null);
        }

        [SetUp]
        public void Setup()
        {
            #if !INTEGRATION
                Assert.Inconclusive("RabbitMQ test is only ran under integration profile");
            #endif
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            #if INTEGRATION
                // Removes all the queues used for the tests
                var connection = new RabbitConnectionManager(new Uri(connectionString));
                using (var channel = connection.GetChannel())
                {
                    foreach (var guid in usedGuids)
                    {
                        channel.QueueDelete(guid.ToString(), ifUnused: false, ifEmpty: false);
                        channel.QueueDelete(guid + "_Delay", ifUnused: false, ifEmpty: false);
                        channel.QueueDelete(guid + "_Error", ifUnused: false, ifEmpty: false);
                    }              
                }
            #endif
        }

        [Test]
        public void Lets_Exception_Bubble_Out_If_Primary_Queue_Arguments_Are_Invalid()
        {
            IReadOnlyDictionary<string, object> primaryQueueArguments = new Dictionary<string, object>
            {
                { "x-max-length", 10000 },
                { "x-message-ttl", "invalid" }
            };

            try
            {
                this.GetTransport(primaryQueueArguments);
            }
            catch (Exception)
            {
                return; // expected result, but don't care what the exception is.
            }

            Assert.Fail("Exception did not bubble out for invalid primary queue argument.");
        }


        [Test]
        public void Send_Puts_Message_With_Priority_In_Front_Of_Primary_Queue()
        {
            IReadOnlyDictionary<string, object> primaryQueueArguments = new Dictionary<string, object>
            {
                { "x-max-priority", 2 }
            };

            var transport = this.GetTransport(primaryQueueArguments);
            if (transport.ServerVersion < priorityQueuesMinimumServerVersion)
            {
                Assert.Inconclusive("Priority queues are only supported on version {0} and higher.", priorityQueuesMinimumServerVersion);
            }

            var lowPriorityMessage = new PriorityTestMessage(priority: 0);
            var mediumPriorityMessage = new PriorityTestMessage(priority: 1);
            var highPriorityMessage = new PriorityTestMessage(priority: 2);
            var messagesReturned = new List<RawMessage>();

            transport.RegisterSubscription(typeof(TestMessage).Name);

            transport.Send(typeof(TestMessage).Name, lowPriorityMessage);
            transport.Send(typeof(TestMessage).Name, mediumPriorityMessage);
            transport.Send(typeof(TestMessage).Name, highPriorityMessage);

            this.RunBlocking((unblockEvent) =>
            {
                transport.GetMessages(new CancellationToken(),
                    (message, ack, reject) =>
                    {
                        messagesReturned.Add(message);
                        ack();
                        if (messagesReturned.Count == 3)
                        {
                            unblockEvent.Set();
                        }
                    });
            }, TimeSpan.FromSeconds(15));

            Assert.AreEqual(3, messagesReturned.Count);
            VerifyMessagePriority(messagesReturned.First(), highPriorityMessage.Priority);
            VerifyMessagePriority(messagesReturned.Last(), lowPriorityMessage.Priority);
        }

        [Test]
        public void Send_Does_Not_Put_Message_With_Priority_In_Front_Of_Primary_Queue_When_MaxPriority_Is_Not_Set()
        {
            IReadOnlyDictionary<string, object> primaryQueueArguments = new Dictionary<string, object>
            {
                { "x-max-priority", 0 }
            };

            var transport = this.GetTransport(primaryQueueArguments);
            if (transport.ServerVersion < priorityQueuesMinimumServerVersion)
            {
                Assert.Inconclusive("Priority queues are only supported on version {0} and higher.", priorityQueuesMinimumServerVersion);
            }

            var lowPriorityMessage = new PriorityTestMessage(priority: 0);
            var mediumPriorityMessage = new PriorityTestMessage(priority: 1);
            var highPriorityMessage = new PriorityTestMessage(priority: 2);
            var messagesReturned = new List<RawMessage>();

            transport.RegisterSubscription(typeof(TestMessage).Name);

            transport.Send(typeof(TestMessage).Name, lowPriorityMessage);
            transport.Send(typeof(TestMessage).Name, mediumPriorityMessage);
            transport.Send(typeof(TestMessage).Name, highPriorityMessage);

            this.RunBlocking((unblockEvent) =>
            {
                transport.GetMessages(new CancellationToken(),
                    (message, ack, reject) =>
                    {
                        messagesReturned.Add(message);
                        ack();
                        if (messagesReturned.Count == 3)
                        {
                            unblockEvent.Set();
                        }
                    });
            }, TimeSpan.FromSeconds(15));

            Assert.AreEqual(3, messagesReturned.Count);
            VerifyMessagePriority(messagesReturned.First(), lowPriorityMessage.Priority);
            VerifyMessagePriority(messagesReturned.Last(), highPriorityMessage.Priority);
        }

        private RabbitMQTransport GetTransport(IReadOnlyDictionary<string, object> primaryQueueArguments)
        {
            // Each Transport uses a unique Guid as the queue base to ensure the tests are isolated            
            var transportGuid = Guid.NewGuid();
            usedGuids.Add(transportGuid);

            var config = new ConfigurationBuilder();
            config.ConfigureAndCreateContext(x =>
            {
                x.UsingAssemblyLocator<TestAssemblyLocator>();
                x.UseRabbitMQ(r =>
                {
                    r.UseConnectionString(connectionString);
                    r.UseExchange(exchange);
                    r.WithPrimaryQueueArguments(primaryQueueArguments);
                    r.WithOnSendingMessageAction(OnSendingMessage);
                });
            });

            var transport = new RabbitMQTransport(transportGuid.ToString(), config.ConfigurationContext);
            transport.InitializeForConsuming();
            transport.InitializeForPublishing();
            return transport;
        }

        private void OnSendingMessage(IBasicProperties properties, IDictionary<string, string> metadata)
        {
            byte priority;
            string priorityValue;
            if (metadata.TryGetValue(PriorityKey, out priorityValue) && byte.TryParse(priorityValue, out priority))
            {
                properties.Priority = priority;
            }
        }

        private static void VerifyMessagePriority(RawMessage rawMessage, ushort expectedPriority)
        {
            string priorityHeader;
            Assert.IsTrue(rawMessage.Headers.TryGetValue(PriorityKey, out priorityHeader));

            byte actualPriority;
            Assert.IsTrue(byte.TryParse(priorityHeader, out actualPriority));

            Assert.AreEqual(expectedPriority, actualPriority);
        }

        private class PriorityTestMessage : IMessage<TestMessage>
        {
            public PriorityTestMessage(ushort priority)
            {
                Priority = priority;
                Content = new TestMessage();
                Headers = new Dictionary<string, string>
                {
                    { PriorityKey, priority.ToString() }
                };
            }

            public ushort Priority { get; }

            public TestMessage Content { get; }

            public IReadOnlyDictionary<string, string> Headers { get; }
        }
    }
}