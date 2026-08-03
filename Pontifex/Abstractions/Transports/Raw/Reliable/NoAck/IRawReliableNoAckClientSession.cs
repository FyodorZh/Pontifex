using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.NoAck
{
    public interface IRawReliableNoAckClientSession
    {
        event Action<UnionDataList>? OnReceived;
        event Action<StopReason>? OnClosed;
        
        IEndPoint RemoteEndPoint { get; }
        SendResult Send(UnionDataList message);
        void Close();
    }
}