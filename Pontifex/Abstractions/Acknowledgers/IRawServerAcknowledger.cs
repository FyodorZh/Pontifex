using Pontifex.Utils;

namespace Pontifex.Abstractions.Acknowledgers
{
    /// <summary>
    /// Реализует бизнес-логика
    /// </summary>
    public interface IRawServerAcknowledger<out THandler>
        where THandler : Handlers.Server.IAckRawServerHandler
    {
        /// <summary>
        /// Идентифицирует входящего клиента.
        /// Создаёт новую сессию для взаимодействия с ним.
        /// !!! Гарантируется, что вызовы TryAck() конкурентно не пересекаются друг с другом по времени.
        /// </summary>
        /// <param name="ackData"> Данные клиента для его идентификации </param>
        /// <returns> Null если клиент не признан, иначе хэндлер клиентской сессии </returns>
        THandler? TryAck(UnionDataList ackData);
    }
}
