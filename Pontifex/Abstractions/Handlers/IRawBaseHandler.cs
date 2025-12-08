using Actuarius.Memory;
using Pontifex.Utils;

namespace Transport.Abstractions.Handlers
{
    public interface IRawBaseHandler : IHandler
    {
        /// <summary>
        /// Логический дисконнект. Информирует бизнесс-логику, что логическая связь между сервером и клиентом более не поддерживается.
        /// Фактически транспорт может быть ещё жив.
        /// OnReceived() более не вызываются
        /// </summary>
        /// <param name="reason"></param>
        void OnDisconnected(StopReason reason);

        /// <summary>
        /// После OnConnected() начинают приходить данные.
        /// После OnDisconnected() данные НЕ приходят
        /// </summary>
        /// <param name="receivedBuffer"> Логика вправе пользоваться данными отложенным образом </param>
        void OnReceived(UnionDataList receivedBuffer);
    }
}
