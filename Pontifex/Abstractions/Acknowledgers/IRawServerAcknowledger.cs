using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Transport.Abstractions.Acknowledgers
{
    /// <summary>
    /// Реализует бизнесс-логика
    /// </summary>
    public interface IRawServerAcknowledger<out THandler>
        where THandler : Handlers.Server.IAckRawServerHandler
    {
        void Setup(IMemoryRental memory, ILogger logger);
        
        /// <summary>
        /// Идентифицирует входящего клиента.
        /// Создаёт новую сессию для взаимодействия с ним.
        /// !!! Гарантируется, что вызовы TryAck() конкуррентно не пересекаются друг с другом по времени.
        /// </summary>
        /// <param name="ackData"> Данные клиента для его идентификации </param>
        /// <param name="logger"></param>
        /// <returns> null если клиент не признан, иначе хэндлер клиентской сессии </returns>
        THandler? TryAck(UnionDataList ackData);
    }
}
