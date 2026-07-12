using System.Collections;

namespace Pontifex.Tests;

public class NoAckRawReliableStacks : IEnumerable<ITransportStack>
{
    public IEnumerator<ITransportStack> GetEnumerator()
    {
        yield return new DynamicTransportStack("direct-noack-raw-reliable", 
            (100, 100), (5000, 100), 
            () => $"transport://direct-noack-raw-reliable|srv-{Guid.NewGuid()}");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
