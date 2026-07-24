namespace Pontifex.NoAck.Raw.Unreliable.Tests;

public static class ConformanceAdapterSource
{
    private static readonly List<INoAckRawUnreliableConformanceTestAdapter> _adapters = new();

    public static void Register(INoAckRawUnreliableConformanceTestAdapter adapter)
    {
        _adapters.Add(adapter);
    }

    public static IEnumerable<TestFixtureData> GetAdapters()
    {
        return _adapters.Select(a => new TestFixtureData(a));
    }
}
