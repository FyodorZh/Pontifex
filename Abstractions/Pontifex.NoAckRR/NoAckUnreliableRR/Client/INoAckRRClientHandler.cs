using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.NoAckRR
{
    public interface INoAckUnreliableRRClientHandler : IHandler
    {
        /// <summary>
        /// Вызывается после полной инициализации транспорта
        /// </summary>
        /// <param name="endpoint"> Настороенный и готовый к работе эндпоинт для отправки сообщений </param>
        void Started(INoAckUnreliableRRServerEndpoint endpoint);
        /// <summary>
        /// Информирует об пришедшем ответе от сервера.
        /// Начинает работать после Started()
        /// </summary>
        /// <param name="message"> Данные присланные сервером </param>
        void Received(UnionDataList message);
        /// <summary>
        /// Информирует о разрушении транспорта. Приходит строго после Started()
        /// Эндпоинт становится невалидным
        /// </summary>
        void Stopped();
    }

    public enum NoAckReliableRRFailReason
    {
        /// <summary>
        /// Данные не приняты к отправке
        /// </summary>
        Rejected,

        /// <summary>
        /// Буффер сообщений для отправки переполнен
        /// </summary>
        BufferOverflow,

        /// <summary>
        /// Отправляемые данные не могут быть доставлены за отведённый период времени
        /// </summary>
        Timeout
    }

    public interface INoAckReliableRRCallbackOnClient
    {
        //void Response(DeliveryId id, IMultiRefByteArray data);
        void Failed(NoAckReliableRRFailReason reason);
    }

    public interface INoAckReliableRRClientHandler : IHandler
    {
        void Started(INoAckReliableRRServerEndpoint endpoint);
        void Stopped();
    }
}
