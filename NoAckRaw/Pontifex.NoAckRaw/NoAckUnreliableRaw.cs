using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Abstractions.Handlers;
using Pontifex.Utils;

namespace Pontifex.Abstractions
{
    public interface INoAckUnreliableRawEndpoint
    {
        int MessageMaxByteSize { get; }
    }

    public interface INoAckUnreliableRawServerEndpoint : INoAckUnreliableRawEndpoint
    {
        IEndPoint ServerEndPoint { get; }
        SendResult Send(UnionDataList message);
    }

    public interface INoAckUnreliableRawClientEndpoint : INoAckUnreliableRawEndpoint
    {
        SendResult Send(IEndPoint destination, UnionDataList message);
    }

    public interface INoAckUnreliableRawClientHandler : IHandler
    {
        /// <summary>
        /// Вызывается после полной инициализации транспорта
        /// </summary>
        /// <param name="endpoint"> Настороенный и готовый к работе эндпоинт для отправки сообщений </param>
        void OnStarted(INoAckUnreliableRawServerEndpoint endpoint);

        /// <summary>
        /// Информирует об пришедшем сообщении от сервера.
        /// Начинает работать после Started()
        /// </summary>
        /// <param name="message"> Данные, присланные сервером. Отдаются во владение обработчику </param>
        void OnReceived(UnionDataList message);
        /// <summary>
        /// Информирует о разрушении транспорта. Приходит строго после Started()
        /// Эндпоинт становится невалидным
        /// </summary>
        void OnStopped();
    }

    public interface INoAckUnreliableRawServerHandler : IHandler
    {
        void OnStarted(INoAckUnreliableRawClientEndpoint endpoint);
        void OnStopped();
        void OnReceived(IEndPoint sender, UnionDataList message);
    }

    public interface INoAckUnreliableRawClient : ITransport
    {
        /// <summary>
        /// Максимальный допустимый размер еденичного сообщения для отправки (и получения)
        /// </summary>
        //int MessageMaxByteSize { get; }

        bool Init(INoAckUnreliableRawClientHandler handler);
    }

    public interface INoAckUnreliableRawServer : ITransport
    {
        bool Init(INoAckUnreliableRawServerHandler handler);
    }
}