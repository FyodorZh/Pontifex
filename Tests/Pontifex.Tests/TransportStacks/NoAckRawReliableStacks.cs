using System.Collections;

namespace Pontifex.Test;

public class NoAckRawReliableStacks : IEnumerable<TransportStack>
{
    public static readonly TransportStack Direct = new(
        id: "direct-noack",
        transportUri: "transport://direct-noack-raw-reliable|test-srv"
    );

    public IEnumerator<TransportStack> GetEnumerator()
    {
        yield return Direct;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
