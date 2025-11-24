using Pontifex.Utils;
using Shared;
using Shared.Buffer;

namespace Transport.Transports.Direct
{
    internal interface IAnyDirectCtl
    {
        void OnReceived(IMemoryBufferHolder buffer);
        void OnDisconnected(StopReason reason);
    }

    internal interface IClientDirectCtl : IAnyDirectCtl
    {
        void GetAckData(UnionDataList ackData);
    }

    internal interface IServerDirectCtl : IAnyDirectCtl
    {
        void Init(DirectTransport transport);
        void OnClientPrepared();
    }
}