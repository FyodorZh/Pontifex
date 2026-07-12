using System.Collections;

namespace Pontifex.Tests;

public class AckRawReliableStacks : IEnumerable<ITransportStack>
{
    public IEnumerator<ITransportStack> GetEnumerator()
    {
        yield return new DynamicTransportStack("direct", 
            (100, 100), (5000, 100),
            () => $"transport://direct|srv-{Guid.NewGuid()}");
        yield return new DynamicTransportStack("tcp", 
            (10, 10), (1000, 50), 
            () => $"transport://tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/60");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
