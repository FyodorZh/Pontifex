using System.Collections;

namespace Pontifex.Tests;

public class RawReliableStacks : IEnumerable<ITransportStack>
{
    public IEnumerator<ITransportStack> GetEnumerator()
    {
        yield return new DynamicTransportStack("direct", 
            (100, 100), (5000, 100),
            () => $"direct-noack(\"srv-{Guid.NewGuid()}\")");
        yield return new DynamicTransportStack("tcp", 
            (10, 10), (1000, 50), 
            () => $"tcp-noack(\"127.0.0.1\", {DynamicPortAllocator.GetRandomPort()}, disconnection_timeout = 60)");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
