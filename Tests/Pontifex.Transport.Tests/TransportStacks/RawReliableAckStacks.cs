using System.Collections;

namespace Pontifex.Tests;

public class RawReliableAckStacks : IEnumerable<ITransportStack>
{
    public IEnumerator<ITransportStack> GetEnumerator()
    {
        yield return new DynamicTransportStack("direct", 
            (100, 100), (5000, 100),
            () => $"direct(\"srv-{Guid.NewGuid()}\")");
        yield return new DynamicTransportStack("tcp", 
            (10, 10), (1000, 50), 
            () => $"tcp(\"127.0.0.1\", {DynamicPortAllocator.GetRandomPort()}, disconnection_timeout = 60)");
        // yield return new DynamicTransportStack("r-udp", 
        //     (10, 10), (1000, 50), 
        //     () => $"udp(\"127.0.0.1\", {DynamicPortAllocator.GetRandomPort()}) as RawReliableAck");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
