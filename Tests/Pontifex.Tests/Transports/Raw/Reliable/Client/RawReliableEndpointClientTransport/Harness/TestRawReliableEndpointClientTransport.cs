using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Tests.Endpoints;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="RawReliableEndpointClientTransport{TClientHandler}"/>. It supplies the endpoint
/// factory, reuses the reliable endpoint double from the Endpoints suite, and exposes the endpoint
/// creation and attachment surface used by the start/stop delta tests.
/// </summary>
internal sealed class TestRawReliableEndpointClientTransport
    : RawReliableEndpointClientTransport<IRawReliableClientHandler>, IRawReliableClient
{
    public TestRawReliableEndpointClientTransport(
        string typeName = "TestRawReliableEndpointClientTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(typeName, logger ?? new Logger([]), memory ?? MemoryRental.Shared)
    {
    }

    public override int MessageMaxByteSize => 1024;

    /// <summary>
    /// Gets the number of times <see cref="CreateEndpoint"/> was invoked.
    /// </summary>
    public int CreateEndpointCalls { get; private set; }

    /// <summary>
    /// Gets the endpoint returned by the most recent <see cref="CreateEndpoint"/> call.
    /// </summary>
    public TestRawReliableHandshakeClientEndpoint? LastCreatedEndpoint { get; private set; }

    /// <summary>
    /// Gets the endpoint currently attached to the transport, or <see langword="null"/> when none is
    /// attached.
    /// </summary>
    public new IRawReliableClientEndpointImp? AttachedEndpoint => base.AttachedEndpoint;

    protected override IRawReliableClientEndpointImp CreateEndpoint(IRawReliableClientHandler handler)
    {
        CreateEndpointCalls++;
        var endpoint = new TestRawReliableHandshakeClientEndpoint(
            handler,
            transportName: Name,
            memory: Memory,
            log: Log,
            handshakeName: "test-transport");
        LastCreatedEndpoint = endpoint;
        return endpoint;
    }
}
