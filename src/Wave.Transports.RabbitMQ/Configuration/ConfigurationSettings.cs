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

using System;
using System.Configuration;

namespace Wave.Transports.RabbitMQ.Configuration
{
    public class ConfigurationSettings
    {
        private const ushort DefaultPrefetchCountPerWorker = 2;
        private const ushort DefaultDelayQueuePrefetchCount = 1800;

        private bool autoDeleteQueues = false;
        private string connectionString = null;
        private string exchange = "Wave";

        // These are stored internally as ushort because that is what RabbitMQ requires.
        // They are exposed externally as int because this is a public class and I didn't
        // want to break CLS compliance without permission.
        private ushort prefetchCountPerWorker = DefaultPrefetchCountPerWorker;
        private ushort delayQueuePrefetchCount = DefaultDelayQueuePrefetchCount;

        public string ConnectionString 
        {
            get
            {
                if (connectionString == null)
                {
                    // If not set, check to see if a connection string for "RabbitMQ" 
                    // is defined.
                    if (ConfigurationManager.ConnectionStrings["RabbitMQ"] != null)
                    {
                        connectionString = ConfigurationManager.ConnectionStrings["RabbitMQ"].ConnectionString;
                    }
                    else
                    {
                        connectionString = "amqp://guest:guest@localhost:5672/";
                    }
                }

                return connectionString;
            }
        }

        public string Exchange
        {
            get
            {
                return this.exchange;
            }
        }

        public bool AutoDeleteQueues
        {
            get
            {
                return this.autoDeleteQueues;
            }
        }

        public int PrefetchCountPerWorker
        {
            get
            {
                return this.prefetchCountPerWorker;
            }
        }

        public int DelayQueuePrefetchCount
        {
            get
            {
                return this.delayQueuePrefetchCount;
            }
        }

        public ConfigurationSettings UseAutoDeleteQueues()
        {
            this.autoDeleteQueues = true;
            return this;
        }

        public ConfigurationSettings UseConnectionString(string connectionString)
        {
            this.connectionString = connectionString;
            return this;
        }

        public ConfigurationSettings UseExchange(string exchange)
        {
            this.exchange = exchange;
            return this;
        }
        
        // These take int arguments because I didn't want to break CLS compliance.
        // They convert to ushort immediately to fail as early as possible if invalid.
        public ConfigurationSettings WithPrefetchCountPerWorker(int prefetchCountPerWorker)
        {
            this.prefetchCountPerWorker = Convert.ToUInt16(prefetchCountPerWorker);
            return this;
        }

        public ConfigurationSettings WithDelayQueuePrefetchCount(int delayQueuePrefetchCount)
        {
            this.delayQueuePrefetchCount = Convert.ToUInt16(delayQueuePrefetchCount);
            return this;
        }
    }
}
