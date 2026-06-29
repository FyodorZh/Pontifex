using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.NoAckRR
{
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
        /// <param name="message"></param>
        /// <returns></returns>
        SendResult Send(UnionDataList message);
    }

    public interface INoAckReliableRRServerEndpoint : INoAckRRServerEndpoint
    {
        SendResult Send(UnionDataList data, INoAckReliableRRCallbackOnClient callback);
    }
}