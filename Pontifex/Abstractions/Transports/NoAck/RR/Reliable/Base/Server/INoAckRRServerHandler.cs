namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRServerHandler : IHandler
    {
        void Started();
        void Stopped();
    }
}
