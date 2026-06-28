namespace Pontifex.Abstractions.Controls
{
    public interface ITrafficCollector : IControl
    {
        long InTraffic { get; }
        long OutTraffic { get; }
        int InSpeed { get; }
        int OutSpeed { get; }
        int InPacketsSpeed { get; }
        int OutPacketsSpeed { get; }
    }

    public interface ITrafficCollectorSink
    {
        void IncInTraffic(int value);
        void IncOutTraffic(int value);
    }
}