using System;
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
    /// Gets or sets an exception thrown from <see cref="CreateEndpoint"/> before an endpoint is created.
    /// </summary>
    public Exception? CreateEndpointException { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the created endpoint rejects its start by returning
    /// <see langword="false"/>.
    /// </summary>
    public bool EndpointStartReturnsFalse { get; set; }

    /// <summary>
    /// Gets or sets an exception thrown from the created endpoint's start.
    /// </summary>
    public Exception? EndpointStartException { get; set; }

    /// <summary>
    /// Gets whether the endpoint saw itself attached to the transport when its start was invoked, or
    /// <see langword="null"/> when no probe ran.
    /// </summary>
    public bool? EndpointWasAttachedDuringStart { get; private set; }

    /// <summary>
    /// Gets the endpoint returned by the most recent <see cref="CreateEndpoint"/> call.
    /// </summary>
    public TestRawReliableHandshakeClientEndpoint? LastCreatedEndpoint { get; private set; }

    /// <summary>
    /// Gets the start-failure fake returned by the most recent <see cref="CreateEndpoint"/> call, or
    /// <see langword="null"/> when the normal endpoint double was returned.
    /// </summary>
    public FakeRawReliableClientEndpoint? LastFakeEndpoint { get; private set; }

    /// <summary>
    /// Gets the endpoint currently attached to the transport, or <see langword="null"/> when none is
    /// attached.
    /// </summary>
    public new IRawReliableClientEndpointImp? AttachedEndpoint => base.AttachedEndpoint;

    protected override IRawReliableClientEndpointImp CreateEndpoint(IRawReliableClientHandler handler)
    {
        CreateEndpointCalls++;

        if (CreateEndpointException != null)
        {
            throw CreateEndpointException;
        }

        if (EndpointStartReturnsFalse || EndpointStartException != null)
        {
            LastFakeEndpoint = new FakeRawReliableClientEndpoint
            {
                StartResult = !EndpointStartReturnsFalse,
                StartException = EndpointStartException,
                StartProbe = () => EndpointWasAttachedDuringStart = AttachedEndpoint != null,
            };

            return LastFakeEndpoint;
        }

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
