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

        // TODO: This just ensures that queue creation doesn't fail and send works normally.
        //       When message-level manipulation is added, an end-to-end test will be added using priority,
        //       that will test primary queue arguments at the same time.
        [Test]
        public void SendToPrimary_With_Valid_Primary_Queue_Arguments_Puts_Message_In_Primary_Queue()
        {
            IReadOnlyDictionary<string, object> validPrimaryQueueArguments = new Dictionary<string, object>
            {
                { "x-max-length", 10000 },
                { "x-message-ttl", 86400000 }
            };

            var transport = this.GetTransport(validPrimaryQueueArguments);
            var testMessage = new TestMessage();
            var returnedMessage = (RawMessage)null;

            transport.RegisterSubscription(typeof(TestMessage).Name);
            transport.Send(typeof(TestMessage).Name, testMessage);

            this.RunBlocking(unblockEvent =>
            {
                transport.GetMessages(new CancellationToken(),
                    (message, ack, reject) =>
                    {
                        returnedMessage = message;
                        ack();
                        unblockEvent.Set();
                    });
            }, TimeSpan.FromSeconds(15));

            Assert.IsNotNull(returnedMessage);
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

        private ITransport GetTransport(IReadOnlyDictionary<string, object> primaryQueueArguments)
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
                });
            });

            var transport = new RabbitMQTransport(transportGuid.ToString(), config.ConfigurationContext);
            transport.InitializeForConsuming();
            transport.InitializeForPublishing();
            return transport;
        }
    }
}