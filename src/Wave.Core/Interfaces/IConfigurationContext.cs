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
using System.Linq;
using System.Threading;

namespace Wave
{
    public interface IConfigurationContext
    {
        IAssemblyLocator AssemblyLocator { get; set; }

        IContainer Container { get; set; }

        ILogger Logger { get; set; }

        int MaxWorkers { get; set; }

        int PrefetchCount { get; set; }

        int DelayPrefetchCount { get; set; }

        bool IsAutoRecoveryEnabled { get; set; }

        TimeSpan AutoRecoveryInterval { get; set; }

        ILookup<Type, IInboundMessageFilter> InboundMessageFilters { get; set; }

        ILookup<Type, IOutboundMessageFilter> OutboundMessageFilters { get; set; }

        int MessageRetryLimit { get; set; }

        IQueueNameResolver QueueNameResolver { get; set; }

        ISerializer Serializer { get; set; }

        ISubscriptionKeyResolver SubscriptionKeyResolver { get; set; }

        ILookup<Type, Func<object, IHandlerResult>> Subscriptions { get; set; }

        CancellationTokenSource TokenSource { get; set; }

        ITransport Transport { get; set; }

        object this[string name] { get; set; }
    }
}