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

using RabbitMQ.Client;
using System;

namespace Wave.Transports.RabbitMQ
{
    using global::RabbitMQ.Client.Exceptions;
    using global::RabbitMQ.Client.Framing.Impl;

    internal class RabbitConnectionManager
    {
        private readonly ConnectionFactory connectionFactory;        
        private Lazy<IConnection> connection;

        internal RabbitConnectionManager(Uri connectionString)
        {            
            this.connectionFactory = new ConnectionFactory { Uri = connectionString.AbsoluteUri, RequestedHeartbeat = 30 };
            this.connectionFactory.AutomaticRecoveryEnabled = this.connectionFactory.TopologyRecoveryEnabled = true;
            this.connection = new Lazy<IConnection>(CreateConnection);                        
        }

        internal IModel GetChannel(bool autorecovering = true)
        {
            var autorecoveringConnection = this.connection.Value as AutorecoveringConnection;
            return autorecovering ? autorecoveringConnection.CreateModel() : autorecoveringConnection.CreateNonRecoveringModel();
        }

        internal void Shutdown()
        {
            Console.WriteLine("ShutdownConnection");
            //this.connection.Value.ConnectionShutdown -= OnConnectionShutDown;
            this.connection.Value.Close();
        }

        private IConnection CreateConnection()
        {
            Console.WriteLine("CreateConnection");

            IConnection conn;
            try
            {
                conn = this.connectionFactory.CreateConnection();
            }
            catch (Exception)
            {
                //this.InitializeLazyConnection();
                throw;
            }

            //conn.ConnectionShutdown += OnConnectionShutDown;
            return conn;
        }

        /*private void OnConnectionShutDown(object connection, ShutdownEventArgs reason)
        {
            Console.WriteLine("OnConnectionShutDown - " + reason.ReplyText);
            // If the connection is aborted, reinit the lazy connection so that next access will reconnect.
            this.InitializeLazyConnection();
        }

        private void InitializeLazyConnection()
        {
            Console.WriteLine("InitializeLazyConnection");
            this.connection = new Lazy<IConnection>(this.CreateConnection);
        }*/
    }
}