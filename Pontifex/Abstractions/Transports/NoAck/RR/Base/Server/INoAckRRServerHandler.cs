namespace Pontifex.NoAck.RR
{
    public interface INoAckRRServerHandler : IHandler
    {
        void Started();
        void Stopped();
    }
}
