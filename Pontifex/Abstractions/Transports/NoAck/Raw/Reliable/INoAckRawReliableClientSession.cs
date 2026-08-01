using System;
using Pontifex.Utils;

namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableClientSession
    {
        event Action<UnionDataList>? OnReceived;
        event Action<StopReason>? OnClosed;
        
        IEndPoint RemoteEndPoint { get; }
        SendResult Send(UnionDataList message);
        void Close();
    }
}