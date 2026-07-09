using Pontifex.Ack.Raw;
using Pontifex.StopReasons;

namespace Pontifex
{
    public interface IAckRawClientControl : IControl
    {
        void Stop();
    }
    
    public class AckRawClientControl : IAckRawClientControl
    {
        private readonly IAckRawClient _transport;

        public string Name => _transport.Name + ".Control";
        
        public AckRawClientControl(IAckRawClient transport)
        {
            _transport = transport;
        }

        public void Stop()
        {
            _transport.Stop(new UserIntention("AckRawClientControl", "Stop() invocation"));
        }
    }
}