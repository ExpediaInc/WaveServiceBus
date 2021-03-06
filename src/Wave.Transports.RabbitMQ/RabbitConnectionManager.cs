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
using System;
using System.Linq;

namespace Wave.Transports.RabbitMQ
{
    internal class RabbitConnectionManager
    {
        private readonly Uri connectionString;

        private ConnectionFactory connectionFactory;

        private Lazy<IConnection> connection;

        internal RabbitConnectionManager(Uri connectionString)
        {
            this.connectionString = connectionString;
            this.ResetConnection();
        }

        internal Version ServerVersion
        {
            get
            {
                Version version;
                return Version.TryParse(GetServerPropertyAsString("version"), out version)
                    ? version
                    : new Version(); // 0.0
            }
        }

        internal IModel GetChannel()
        {
            this.EnsureConnectionIsOpen();
            return this.connection.Value.CreateModel();
        }

        internal void Shutdown()
        {
            this.connection.Value.ConnectionShutdown -= OnConnectionShutDown;
            this.connection.Value.Close();
        }

        private void EnsureConnectionIsOpen()
        {
            if (!this.connection.Value.IsOpen)
            {
                this.ResetConnection();
            }
        }

        private IConnection CreateConnection()
        {
            try
            {
                var conn = this.connectionFactory.CreateConnection();
                conn.ConnectionShutdown += OnConnectionShutDown;

                return conn;
            }
            catch (Exception)
            {
                this.ResetConnection();
                throw;
            }
        }

        private void ResetConnection()
        {
            this.connectionFactory = new ConnectionFactory { Uri = new Uri(this.connectionString.AbsoluteUri, UriKind.Absolute), RequestedHeartbeat = 30 };
            if (this.connection != null && this.connection.IsValueCreated)
            {
                this.connection.Value.ConnectionShutdown -= OnConnectionShutDown;
            }

            this.connection = new Lazy<IConnection>(this.CreateConnection);
        }

        private void OnConnectionShutDown(object sender, ShutdownEventArgs reason)
        {
            // If the connection is aborted, reinit the lazy connection so that next access will reconnect.
            this.ResetConnection();
        }

        private string GetServerPropertyAsString(string name)
        {
            var serverPropertyChars = GetServerPropertyAsBytes(name)
                .Select(Convert.ToChar);
            return new string(serverPropertyChars.ToArray());
        }

        private byte[] GetServerPropertyAsBytes(string name)
        {
            object serverProperty;
            return this.connection.Value.ServerProperties
                .TryGetValue(name, out serverProperty)
                ? (byte[])serverProperty
                : Enumerable.Empty<byte>().ToArray();
        }

    }
}