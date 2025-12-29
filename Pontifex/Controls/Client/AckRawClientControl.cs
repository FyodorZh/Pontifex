using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
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

        public string Name => _transport.Type + ".Control";
        
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