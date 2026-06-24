// using Shared.Utils;
// using Pontifex.Abstractions;
//
// namespace Pontifex.Transports.RUdp
// {
//     public class BaseAckRawRUdpProducer
//     {
//         public string Name
//         {
//             get { return RUdpInfo.TransportName; }
//         }
//
//         // host:ip/timeout
//         protected static bool Parse(string @params, out System.TimeSpan disconnectionTimeout, out string ipAndPort)
//         {
//             try
//             {
//                 string[] list = @params.Split('/');
//                 ipAndPort = list[0];
//                 disconnectionTimeout = System.TimeSpan.FromSeconds(int.Parse(list[1]));
//                 return true;
//             }
//             catch
//             {
//                 ipAndPort = "";
//                 disconnectionTimeout = new System.TimeSpan();
//                 return false;
//             }
//         }
//     }
//
//     public class AckRawRUdpClientProducer : BaseAckRawRUdpProducer, ITransportProducer
//     {
//         public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
//         {
//             System.TimeSpan disconnectionTimeout;
//             string ipAndPort;
//             if (Parse(@params, out disconnectionTimeout, out ipAndPort))
//             {
//                 return factory.Construct("reliable1|reliable2|" + (int)disconnectionTimeout.TotalSeconds +":udp_rr|" + ipAndPort);
//             }
//             return null;
//         }
//     }
//
//     public class AckRawRUdpServerProducer : BaseAckRawRUdpProducer, ITransportProducer
//     {
//         public ITransport Produce(string @params, ITransportFactory factory, IPeriodicLogicRunner logicRunner)
//         {
//             System.TimeSpan disconnectionTimeout;
//             string ipAndPort;
//             if (Parse(@params, out disconnectionTimeout, out ipAndPort))
//             {
//                 return factory.Construct("reliable1|reliable2|" + (int)disconnectionTimeout.TotalSeconds + ":udp_rr|" + ipAndPort);
//             }
//             return null;
//         }
//     }
// }