using Shared;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
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

        string IControl.Name
        {
            get { return mName; }
        }

        long ITrafficCollector.InTraffic
        {
            get { return mInTraffic.Total; }
        }

        long ITrafficCollector.OutTraffic
        {
            get { return mOutTraffic.Total; }
        }

        int ITrafficCollector.InSpeed
        {
            get { return (int)mInTraffic.SumInRange; }
        }

        int ITrafficCollector.OutSpeed
        {
            get { return (int)mOutTraffic.SumInRange; }
        }

        int ITrafficCollector.InPacketsSpeed
        {
            get { return mInTraffic.ValuesInRange; }
        }

        int ITrafficCollector.OutPacketsSpeed
        {
            get { return mOutTraffic.ValuesInRange; }
        }
    }
}
