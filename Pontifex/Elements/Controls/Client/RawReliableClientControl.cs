using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;

namespace Pontifex
{
    public interface IRawReliableClientControl : IControl
    {
        void Stop();
    }
    
    public class RawReliableClientControl : IRawReliableClientControl
    {
        private readonly IRawReliableClient _transport;

        public string Name => _transport.Name + ".Control";
        
        public RawReliableClientControl(IRawReliableClient transport)
        {
            _transport = transport;
        }

        public void Stop()
        {
            _transport.Stop(new UserIntention("RawReliableClientControl", "Stop() invocation"));
        }
    }
}
