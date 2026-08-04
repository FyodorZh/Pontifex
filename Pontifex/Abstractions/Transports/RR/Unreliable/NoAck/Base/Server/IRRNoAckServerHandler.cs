namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRNoAckServerHandler : IHandler
    {
        void Started();
        void Stopped();
    }
}
