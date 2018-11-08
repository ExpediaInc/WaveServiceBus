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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Wave.Core.Tests")]
[assembly: InternalsVisibleTo("Wave.IoC.Autofac.Tests")]
[assembly: InternalsVisibleTo("Wave.IoC.Unity.Tests")]
[assembly: InternalsVisibleTo("Wave.IoC.NInject.Tests")]
[assembly: InternalsVisibleTo("Wave.IoC.StructureMap.Tests")]
[assembly: InternalsVisibleTo("Wave.IoC.CastleWindsor.Tests")]
[assembly: InternalsVisibleTo("Wave.Logging.Log4Net.Tests")]
[assembly: InternalsVisibleTo("Wave.Logging.NLog.Tests")]
[assembly: InternalsVisibleTo("Wave.Logging.CommonsLogging.Tests")]
[assembly: InternalsVisibleTo("Wave.Serialization.JsonDotNet.Tests")]
[assembly: InternalsVisibleTo("Wave.Serialization.ServiceStack.Tests")]
[assembly: InternalsVisibleTo("Wave.Serialization.Xml.Tests")]
[assembly: InternalsVisibleTo("Wave.ServiceHosting.TopShelf.Tests")]
[assembly: InternalsVisibleTo("Wave.Transports.RabbitMQ.Tests")]
[assembly: InternalsVisibleTo("Wave.Transports.MSMQ.Tests")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
