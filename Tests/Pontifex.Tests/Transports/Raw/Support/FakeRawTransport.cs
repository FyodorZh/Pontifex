using Actuarius.Memory;
using Pontifex.Raw;
using Scriba;

namespace Pontifex.Tests.Transports.Raw.Support;

/// <summary>
/// Minimal <see cref="IRawTransport"/> test double. RawEndpoint only consumes
/// Name/Log/Memory; the remaining members are implemented trivially so the same
/// fake can be reused by future tests of derived endpoint/transport classes.
/// </summary>
public sealed class FakeRawTransport : IRawTransport
{
    public FakeRawTransport(string? name = null)
    {
        Name = name ?? "fake-transport";
    }

    public string Name { get; set; }

    public TransportType Type => TransportType.RawReliableAck;

    public bool IsValid { get; set; } = true;

    public bool IsStarted { get; private set; }

    public ILogger Log { get; set; } = VoidLogger.Instance;

    public IMemoryRental Memory { get; set; } = MemoryRental.Shared;

    public int MessageMaxByteSize { get; set; } = 1 << 20;

    public bool Start(Action<StopReason> onStopped)
    {
        IsStarted = true;
        return true;
    }

    public bool Stop(StopReason? reason = null)
    {
        IsStarted = false;
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }
}

/// <summary>
/// Identity endpoint test double. Only equality/identity is ever observed by the
/// transport layer, so a unique string id is sufficient.
/// </summary>
public sealed class FakeEndPoint : IEndPoint
{
    private readonly string _id;

    public FakeEndPoint(string? id = null)
    {
        _id = id ?? Guid.NewGuid().ToString("N");
    }

    public bool Equals(IEndPoint? other)
    {
        return other is FakeEndPoint f && string.Equals(f._id, _id, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as IEndPoint);
    }

    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

    public override string ToString()
    {
        return $"fake-endpoint[{_id}]";
    }
}
