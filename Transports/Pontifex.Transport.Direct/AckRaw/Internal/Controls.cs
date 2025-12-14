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
    }

    internal interface IServerDirectCtl : IAnyDirectCtl
    {
        void Init(DirectTransport transport);
        void OnClientPrepared();
    }
}