namespace Transport.Abstractions.Clients
{
    public interface INoAckRRClient : ITransport
    {
    }

    public interface INoAckUnreliableRRClient : INoAckRRClient, Flags.IUnreliable
    {
        bool Init(Handlers.Client.INoAckUnreliableRRClientHandler handler);
        
        /// <summary>
        /// Максимальный допустимый размер еденичного сообщения для отправки (и получения)
        /// </summary>
        int MessageMaxByteSize { get; }
    }

    public interface INoAckReliableRRClient : INoAckRRClient, Flags.IReliable
    {
        bool Init(Handlers.Client.INoAckReliableRRClientHandler handler);
    }
}
