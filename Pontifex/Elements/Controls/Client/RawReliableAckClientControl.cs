using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;

namespace Pontifex
{
    public interface IRawReliableAckClientControl : IControl
    {
        void Stop();
    }
    
    public class RawReliableAckClientControl : IRawReliableAckClientControl
    {
        private readonly IRawAckClient _transport;

        public string Name => _transport.Name + ".Control";
        
        public RawReliableAckClientControl(IRawAckClient transport)
        {
            _transport = transport;
        }

        public void Stop()
        {
            _transport.Stop(new UserIntention("RawReliableAckClientControl", "Stop() invocation"));
        }
    }
}