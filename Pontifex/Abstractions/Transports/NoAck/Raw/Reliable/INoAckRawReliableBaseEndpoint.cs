using Pontifex.Utils;

namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableBaseEndpoint : IBaseEndpoint
    {
        IEndPoint? RemoteEndPoint { get; }

        bool IsConnected { get; }

        int MessageMaxByteSize { get; }

        bool Disconnect(StopReason reason);

        SendResult Send(UnionDataList bufferToSend);
    }
}
