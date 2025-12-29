using Pontifex.Abstractions.Controls;

namespace Pontifex.Transports.Tcp
{
    internal class PingCollector : IPingCollector
    {
        private volatile int _ping;

        public bool CollectPing
        {
            get => true;
            set { }
        }

        public string Name => "Tcp.Ping";

        public bool GetPing(out int minPing, out int maxPing, out int avgPing)
        {
            var ping = _ping;
            minPing = ping;
            maxPing = ping;
            avgPing = ping;
            return true;
        }

        public void SetPing(int pingMs)
        {
            _ping = pingMs;
        }
    }
}