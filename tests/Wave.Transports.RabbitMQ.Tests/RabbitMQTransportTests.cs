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
using System.Runtime.Serialization;
using System.Threading;
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

        private static readonly Version priorityQueuesMinimumServerVersion = new Version(3, 5, 0);

        private ConcurrentBag<Guid> usedGuids = new ConcurrentBag<Guid>();

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
                        channel.QueueDelete(guid.ToString() + "_Delay", ifUnused: false, ifEmpty: false);
                        channel.QueueDelete(guid.ToString() + "_Error", ifUnused: false, ifEmpty: false);
                    }              
                }
            #endif
        }

        public override ITransport GetTransport()
        {
            return GetTransportWithMaxPriority(maxPriority: 0);
        }

        [Test]
        public void Send_Puts_Message_With_Priority_In_Front_Of_Primary_Queue()
        {
            var transport = this.GetTransportWithMaxPriority(maxPriority: 1);

            var lowPriorityMessage = new PriorityTestMessage { Priority = 0 };
            var highPriorityMessage = new PriorityTestMessage { Priority = 1 };
            var messagesReturned = new List<RawMessage>();

            transport.RegisterSubscription(typeof(TestMessage).Name);

            transport.Send(typeof(TestMessage).Name, lowPriorityMessage, lowPriorityMessage.Priority);
            transport.Send(typeof(TestMessage).Name, lowPriorityMessage, lowPriorityMessage.Priority);
            transport.Send(typeof(TestMessage).Name, highPriorityMessage, highPriorityMessage.Priority);

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
        }

        [Test]
        public void Send_Does_Not_Put_Message_With_Priority_In_Front_Of_Primary_Queue_When_MaxPriority_Is_Not_Set()
        {
            var transport = this.GetTransportWithMaxPriority(maxPriority: 0);

            var lowPriorityMessage = new PriorityTestMessage { Priority = 0 };
            var highPriorityMessage = new PriorityTestMessage { Priority = 1 };
            var messagesReturned = new List<RawMessage>();

            transport.RegisterSubscription(typeof(TestMessage).Name);

            transport.Send(typeof(TestMessage).Name, lowPriorityMessage, lowPriorityMessage.Priority);
            transport.Send(typeof(TestMessage).Name, lowPriorityMessage, lowPriorityMessage.Priority);
            transport.Send(typeof(TestMessage).Name, highPriorityMessage, highPriorityMessage.Priority);

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
            VerifyMessagePriority(messagesReturned.Last(), highPriorityMessage.Priority);
        }

        private ITransport GetTransportWithMaxPriority(byte maxPriority)
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
                    r.WithMaxPriority(maxPriority);
                });
            });

            var transport = new RabbitMQTransport(transportGuid.ToString(), config.ConfigurationContext);

            if ((maxPriority > 0) && (transport.ServerVersion < priorityQueuesMinimumServerVersion))
            {
                Assert.Inconclusive("Priority queues are only supported on version {0} and higher.", priorityQueuesMinimumServerVersion);
            }

            transport.InitializeForConsuming();
            transport.InitializeForPublishing();
            return transport;
        }

        private static void VerifyMessagePriority(RawMessage rawMessage, byte expectedPriority)
        {
            Assert.AreEqual(expectedPriority, rawMessage.Priority);

            var testMessage = (PriorityTestMessage)ConfigurationContext.Current.Serializer
                .Deserialize(rawMessage.Data, typeof(PriorityTestMessage));

            Assert.AreEqual(expectedPriority, testMessage.Priority);
        }

        [DataContract]
        private class PriorityTestMessage : TestMessage
        {
            [DataMember]
            public byte Priority { get; set; }
        }
    }
}