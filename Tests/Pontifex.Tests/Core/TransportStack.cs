namespace Pontifex.Test;

public class TransportStack
{
    public string Id { get; }
    public string TransportUri { get; }

    public TransportStack(string id, string transportUri)
    {
        Id = id;
        TransportUri = transportUri;
    }

    public override string ToString() => Id;
}
