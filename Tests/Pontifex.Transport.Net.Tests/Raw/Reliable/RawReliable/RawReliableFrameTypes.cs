using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.RawReliable;

/// <summary>
/// Exposes the protected <see cref="RawReliableEndpoint.MessageType"/> frame constants by symbol so the raw-peer
/// tests survive frame-type renumbering. The type is abstract and never instantiated; it exists only to reach
/// the inherited protected enum.
/// </summary>
internal abstract class RawReliableFrameTypes : RawReliableEndpoint
{
    protected RawReliableFrameTypes()
        : base("frame-types", null!, null!, 1, 1, MemoryRental.Shared, new Logger([]))
    {
    }

    public static byte Payload => (byte)MessageType.PayloadMessage;

    public static byte Disconnect => (byte)MessageType.DisconnectMessage;

    public static byte HandshakeRequest => (byte)MessageType.HandshakeRequestMessage;

    public static byte HandshakeResponse => (byte)MessageType.HandshakeResponseMessage;

    public static byte HandshakeReject => (byte)MessageType.HandshakeRejectMessage;
}
