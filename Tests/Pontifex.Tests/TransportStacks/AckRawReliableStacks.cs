using System.Collections;

namespace Pontifex.Tests;

public class AckRawReliableStacks : IEnumerable<ITransportStack>
{
    public IEnumerator<ITransportStack> GetEnumerator()
    {
        yield return new DynamicTransportStack("direct", () => $"transport://direct|srv-{Guid.NewGuid()}");
        yield return new DynamicTransportStack("tcp", () => $"transport://tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/10");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
