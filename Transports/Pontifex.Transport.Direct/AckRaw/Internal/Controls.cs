using System;
using System.Collections.Generic;
using Pontifex.Abstractions;
using Pontifex.Utils;

namespace Pontifex.Transports.Direct
{
    internal interface IAnyDirectCtl
    {
        void OnReceived(UnionDataList buffer);
        void OnDisconnected(StopReason reason);
    }

    internal interface IClientDirectCtl : IAnyDirectCtl
    {
        void GetAckData(UnionDataList ackData);
        void GetTransportControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }

    internal interface IServerDirectCtl : IAnyDirectCtl
    {
        void Init(DirectTransport transport);
        void OnClientPrepared();
    }
}