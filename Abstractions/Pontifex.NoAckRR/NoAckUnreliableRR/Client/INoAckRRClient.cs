namespace Pontifex.NoAckRR
{
    public interface INoAckRRClient : ITransport
    {
    }

    public interface INoAckUnreliableRRClient : INoAckRRClient
    {
        bool Init(INoAckUnreliableRRClientHandler handler);
        
        /// <summary>
        /// Максимальный допустимый размер еденичного сообщения для отправки (и получения)
        /// </summary>
        int MessageMaxByteSize { get; }
    }

    public interface INoAckReliableRRClient : INoAckRRClient
    {
        bool Init(INoAckReliableRRClientHandler handler);
    }
}
