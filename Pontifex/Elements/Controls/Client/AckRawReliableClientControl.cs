using Pontifex.Ack.Raw;
using Pontifex.StopReasons;

namespace Pontifex
{
    public interface IAckRawReliableClientControl : IControl
    {
        void Stop();
    }
    
    public class AckRawReliableClientControl : IAckRawReliableClientControl
    {
        private readonly IAckRawClient _transport;

        public string Name => _transport.Name + ".Control";
        
        public AckRawReliableClientControl(IAckRawClient transport)
        {
            _transport = transport;
        }

        public void Stop()
        {
            _transport.Stop(new UserIntention("AckRawReliableClientControl", "Stop() invocation"));
        }
    }
}