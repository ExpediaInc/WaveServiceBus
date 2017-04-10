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

using System.Configuration;

namespace Wave.Transports.RabbitMQ.Configuration
{
    public class ConfigurationSection : System.Configuration.ConfigurationSection
    {
        [ConfigurationProperty("connectionStringName", IsRequired = false)]
        public string ConnectionStringName
        {
            get { return (string)base["connectionStringName"]; }
            set { base["connectionStringName"] = value; }
        }

        [ConfigurationProperty("exchange", IsRequired = false)]
        public string Exchange
        {
            get { return (string)base["exchange"]; }
            set { base["exchange"] = value; }
        }

        [ConfigurationProperty("autoDeleteQueues", IsRequired = false)]
        public bool AutoDeleteQueues
        {
            get { return bool.Parse((string)base["autoDeleteQueues"]); }
            set { base["autoDeleteQueues"] = value.ToString(); }
        }

        [ConfigurationProperty("prefetchCount", IsRequired = false)]
        public int PrefetchCount
        {
            get { return (int)base["prefetchCount"]; }
            set { base["prefetchCount"] = value; }
        }

        [ConfigurationProperty("delayPrefetchCount", IsRequired = false)]
        public int DelayPrefetchCount
        {
            get { return (int)base["delayPrefetchCount"]; }
            set { base["delayPrefetchCount"] = value; }
        }
    }
}
