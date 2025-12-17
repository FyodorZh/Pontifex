using Actuarius.PeriodicLogic;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Controls;
using Pontifex.Utils;
using TimeSpan = System.TimeSpan;

namespace Transport.Utils
{
    public class TrafficCollector : ITrafficCollector, ITrafficCollectorSink
    {
        private readonly string mName;

        private readonly SumCounter mInTraffic;
        private readonly SumCounter mOutTraffic;

        public TrafficCollector(IDateTimeProvider timeProvider, string name)
        {
            mName = name;
            mInTraffic = new SumCounter(timeProvider, TimeSpan.FromSeconds(1));
            mOutTraffic = new SumCounter(timeProvider, TimeSpan.FromSeconds(1));
        }

        public void IncInTraffic(int value)
        {
            mInTraffic.Inc(value);
        }

        public void IncOutTraffic(int value)
        {
            mOutTraffic.Inc(value);
        }

        string IControl.Name => mName;

        long ITrafficCollector.InTraffic => mInTraffic.Total;

        long ITrafficCollector.OutTraffic => mOutTraffic.Total;

        int ITrafficCollector.InSpeed => (int)mInTraffic.SumInRange;

        int ITrafficCollector.OutSpeed => (int)mOutTraffic.SumInRange;

        int ITrafficCollector.InPacketsSpeed => mInTraffic.ValuesInRange;

        int ITrafficCollector.OutPacketsSpeed => mOutTraffic.ValuesInRange;
    }
}
