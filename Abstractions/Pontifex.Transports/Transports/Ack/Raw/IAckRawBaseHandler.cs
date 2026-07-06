using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Base handler for raw-connection lifecycle events.
    /// Implemented by business logic to receive logical disconnect notifications
    /// and incoming data from the transport.
    /// </summary>
    public interface IAckRawBaseHandler : IHandler
    {
        /// <summary>
        /// Logical disconnect. Informs business logic that the logical connection
        /// between server and client is no longer maintained.
        /// The transport may still be physically alive.
        /// OnReceived() will no longer be called after this point.
        /// </summary>
        /// <original>Логический дисконнект. Информирует бизнесс-логику, что логическая связь между сервером и клиентом более не поддерживается. Фактически транспорт может быть ещё жив. OnReceived() более не вызываются</original>
        /// <param name="reason">The reason for the disconnection.</param>
        void OnDisconnected(StopReason reason);

        /// <summary>
        /// Called when data arrives from the remote peer.
        /// After OnConnected(), data starts arriving.
        /// After OnDisconnected(), data does NOT arrive.
        /// </summary>
        /// <original>После OnConnected() начинают приходить данные. После OnDisconnected() данные НЕ приходят</original>
        /// <param name="receivedBuffer">
        /// The received data. The buffer may be reused after the call returns;
        /// copy the data if you need it beyond the scope of this method.
        /// <original>Логика вправе пользоваться данными отложенным образом</original>
        /// </param>
        void OnReceived(UnionDataList receivedBuffer);
    }
}
