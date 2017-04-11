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

namespace Wave.Transports.RabbitMQ.Extensions
{
    internal static class ConfigurationContextExtensions
    {
        internal static bool GetAutoDeleteQueues(this IConfigurationContext context)
        {            
            return bool.Parse(((string)context["rabbitmq.autodeletequeues"]) ?? "False");
        }

        internal static void SetAutoDeleteQueues(this IConfigurationContext context, bool autoDelete)
        {
            context["rabbitmq.autodeletequeues"] = autoDelete.ToString();
        }

        internal static string GetConnectionString(this IConfigurationContext context)
        {
            return (string)context["rabbitmq.connectionstring"];
        }

        internal static void SetConnectionString(this IConfigurationContext context, string connectionString)
        {
            context["rabbitmq.connectionstring"] = connectionString;
        }

        internal static string GetExchange(this IConfigurationContext context)
        {
            return (string)context["rabbitmq.exchange"];
        }

        internal static void SetExchange(this IConfigurationContext context, string exchange)
        {
            context["rabbitmq.exchange"] = exchange;
        }
        
        internal static ushort? GetPrefetchCountPerWorker(this IConfigurationContext context)
        {
            ushort result;
            return ushort.TryParse((string)context["rabbitmq.prefetchCountPerWorker"], out result)
                ? result
                : (ushort?)null;
        }

        internal static void SetPrefetchCountPerWorker(this IConfigurationContext context, ushort prefetchCountPerWorker)
        {
            context["rabbitmq.prefetchCountPerWorker"] = prefetchCountPerWorker.ToString();
        }
        
        internal static ushort? GetDelayQueuePrefetchCount(this IConfigurationContext context)
        {
            ushort result;
            return ushort.TryParse((string)context["rabbitmq.delayQueuePrefetchCount"], out result)
                ? result
                : (ushort?)null;
        }

        internal static void SetDelayQueuePrefetchCount(this IConfigurationContext context, ushort delayQueuePrefetchCount)
        {
            context["rabbitmq.delayQueuePrefetchCount"] = delayQueuePrefetchCount.ToString();
        }
    }
}
