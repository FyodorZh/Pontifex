namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRNoAckServerHandler : IHandler
    {
        void Started();
        void Stopped();
    }
}
