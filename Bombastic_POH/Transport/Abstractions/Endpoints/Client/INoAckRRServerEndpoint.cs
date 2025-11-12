using Shared;

namespace Transport.Abstractions.Endpoints.Client
{
    using Handlers.Client;

    public interface INoAckRRServerEndpoint
    {
        IEndPoint EndPoint { get; }
        int MessageMaxByteSize { get; }
    }

    public interface INoAckUnreliableRRServerEndpoint : INoAckRRServerEndpoint
    {
        /// <summary>
        /// Отправка данных в порядке перечисления.
        /// Данные передаются во владение
        /// </summary>
        /// <param name="messages"></param>
        /// <returns></returns>
        SendResult Send(IMacroOwner<Message> messages);
    }

    public interface INoAckReliableRRServerEndpoint : INoAckRRServerEndpoint
    {
        SendResult Send(IMultiRefByteArray data, INoAckReliableRRCallback callback);
    }
}