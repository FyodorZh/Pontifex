using System.Collections;

namespace Pontifex.Test;

public class AckRawReliableStacks : IEnumerable<TransportStack>
{
    public static readonly TransportStack Direct = new(
        id: "direct",
        transportUri: "transport://direct|test-srv"
    );

    public static readonly TransportStack Tcp = new(
        id: "tcp",
        transportUri: $"transport://tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/10"
    );

    public IEnumerator<TransportStack> GetEnumerator()
    {
        yield return Direct;
        yield return Tcp;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
