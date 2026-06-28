namespace Pontifex.NoAckRaw
{
    public interface INoAckRawClient : ITransport
    {
        /// <summary>
        /// Максимальный допустимый размер еденичного сообщения для отправки (и получения)
        /// </summary>
        //int MessageMaxByteSize { get; }

        bool Init(INoAckRawClientSideHandler handler);
    }
}