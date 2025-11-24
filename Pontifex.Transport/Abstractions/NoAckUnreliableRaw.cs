using Actuarius.Memory;
using Transport.Abstractions.Flags;
using Transport.Abstractions.Handlers;

namespace Transport.Abstractions
{
    public interface INoAckUnreliableRawEndpoint
    {
        int MessageMaxByteSize { get; }
    }

    public interface INoAckUnreliableRawServerEndpoint : INoAckUnreliableRawEndpoint
    {
        IEndPoint ServerEndPoint { get; }
        //SendResult Send(Message messages);
        SendResult Send(IMacroOwner<Message> messages);
    }

    public interface INoAckUnreliableRawClientEndpoint : INoAckUnreliableRawEndpoint
    {
        //SendResult Send(IEndPoint destination, Message messages);
        SendResult Send(IEndPoint destination, IMacroOwner<Message> messages);
    }

    public interface INoAckUnreliableRawClientHandler : IHandler
    {
        /// <summary>
        /// Вызывается после полной инициализации транспорта
        /// </summary>
        /// <param name="endpoint"> Настороенный и готовый к работе эндпоинт для отправки сообщений </param>
        void OnStarted(INoAckUnreliableRawServerEndpoint endpoint);

        /// <summary>
        /// Информирует об пришедшем ответе от сервера.
        /// Начинает работать после Started()
        /// </summary>
        /// <param name="message"> Данные присланные сервером </param>
        void OnReceived(Message message);
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
        void OnReceived(IEndPoint sender, Message message);
    }

    public interface INoAckUnreliableRawClient : ITransport, IUnreliable
    {
        /// <summary>
        /// Максимальный допустимый размер еденичного сообщения для отправки (и получения)
        /// </summary>
        //int MessageMaxByteSize { get; }

        bool Init(INoAckUnreliableRawClientHandler handler);
    }

    public interface INoAckUnreliableRawServer : ITransport, IUnreliable
    {
        bool Init(INoAckUnreliableRawServerHandler handler);
    }
}