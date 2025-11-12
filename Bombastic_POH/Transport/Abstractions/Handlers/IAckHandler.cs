namespace Transport.Abstractions.Handlers
{
    public interface IAckHandler : IHandler
    {
        byte[] GetAckData();
    }
}
