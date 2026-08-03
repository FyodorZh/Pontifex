namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRServerHandler : IHandler
    {
        void Started();
        void Stopped();
    }
}
