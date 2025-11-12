using Shared.Buffer;

namespace Transport.Abstractions.Endpoints
{
    /// <summary>
    /// Реализует транспортная система.
    /// </summary>
    public interface IAckRawBaseEndpoint
    {
        IEndPoint RemoteEndPoint { get; }

        bool IsConnected { get; }
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Отправляет сообщение.
        /// Если неуспех произойдёт ассинхронно после возращения успеха методом - транспорт будет разрушен.
        /// </summary>
        /// <param name="bufferToSend"></param>
        /// <returns> Вернёт SendResult.Ok в случае успеха. </returns>
        SendResult Send(IMemoryBufferHolder bufferToSend);

        bool Disconnect(StopReason reason);
    }
}