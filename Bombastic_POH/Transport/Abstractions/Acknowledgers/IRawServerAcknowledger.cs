using Shared;

namespace Transport.Abstractions.Acknowledgers
{
    /// <summary>
    /// Реализует бизнесс-логика
    /// </summary>
    public interface IRawServerAcknowledger<out THandler>
        where THandler : Handlers.Server.IAckRawServerHandler
    {
        /// <summary>
        /// Идентифицирует входящего клиента.
        /// Создаёт новую сессию для взаимодействия с ним.
        /// !!! Гарантируется, что вызовы TryAck() конкуррентно не пересекаются друг с другом по времени.
        /// </summary>
        /// <param name="ackData"> Данные клиента для его идентификации </param>
        /// <returns> null если клиент не признан, иначе хэндлер клиентской сессии </returns>
        THandler TryAck(ByteArraySegment ackData, ILogger logger);
    }
}
