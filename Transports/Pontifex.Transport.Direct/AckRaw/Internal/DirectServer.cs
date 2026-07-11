using System;
using System.Collections.Generic;
using System.Linq;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Direct
{
    internal class DirectServer
    {
        private readonly IEndPoint _serverEp;
        private readonly Func<UnionDataList, IServerDirectCtl?> _onConnecting;

        private readonly Dictionary<IEndPoint, DirectTransport> _connectedClients = new ();
        
        private readonly IMemoryRental _memoryRental;

        private readonly object _locker = new();

        private bool _stopped;

        public DirectServer(IEndPoint serverEp, Func<UnionDataList, IServerDirectCtl?> onConnecting, IMemoryRental memoryRental)
        {
            _serverEp = serverEp;
            _onConnecting = onConnecting;
            _memoryRental = memoryRental;
        }

        public void Stop()
        {
            lock (_locker)
            {
                if (!_stopped)
                {
                    _stopped = true;

                    foreach (var client in _connectedClients.Values.ToArray())
                    {
                        client.Disconnect(new GracefulRemoteIntention(_serverEp.ToString()));
                    }
                    _connectedClients.Clear();
                }
            }
        }

        public DirectTransport? NewTransport(IEndPoint clientAddress, IClientDirectCtl clientCtl)
        {
            lock (_locker)
            {
                if (_stopped)
                {
                    return null;
                }
                
                if (_connectedClients.TryGetValue(clientAddress, out var transport))
                {
                    return null;
                }

                UnionDataList ackData = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                clientCtl.GetAckData(ackData);
                IServerDirectCtl? serverCtl = _onConnecting(ackData);
                if (serverCtl != null)
                {
                    transport = new DirectTransport(_serverEp, clientAddress, serverCtl, clientCtl, (clientEp) =>
                    {
                        lock (_locker)
                        {
                            _connectedClients.Remove(clientEp.ClientEp);
                        }
                    });
                    serverCtl.Init(transport);

                    _connectedClients.Add(clientAddress, transport);

                    return transport;
                }

                return null;
            }
        }
    }
}