using System.Collections.Generic;
using Actuarius.Memory;

namespace Pontifex.Abstractions.Servers
{
    public interface INoAckRRServer : ITransport
    {
        /// <summary>
        /// Максимальный допустимый размер еденичного сообщения для отправки (и получения)
        /// </summary>
        int MessageMaxByteSize { get; }
    }

    public interface INoAckUnreliableRRServer : INoAckRRServer, Flags.IUnreliable
    {// UDP-like

        bool Init(Handlers.Server.INoAckUnreliableRRServerHandler handler);

        /// <summary>
        /// Отправляет сообщение на клиент.
        /// Сообщение может потеряться
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns> != SendResult.Ok - всё плохо, ничего доставлено не будет </returns>
        SendResult Send(IEndPoint client, Message message);

        /// <summary>
        /// Отправляет группу сообщений на клиент.
        /// Вся группа или её часть может потеряться, сообщения могут прийти в произвольном порядке и более одного раза
        /// </summary>
        /// <param name="client"></param>
        /// <param name="messages"></param>
        /// <returns> != SendResult.Ok - всё плохо, ничего доставлено не будет </returns>
        //SendResult Send(IEndPoint client, IMacroOwner<Message> messages);
    }

    public interface INoAckReliableRRServer : INoAckRRServer, Flags.IReliable
    { // HTTP-like

        bool Init(Handlers.Server.INoAckReliableRRServerHandler handler);
    }
}
