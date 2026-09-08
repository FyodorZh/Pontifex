using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Tests.Transports.Raw.Support;

namespace Pontifex.Tests.Transports.Raw;

/// <summary>
/// Wires a complete, isolated RawEndpoint unit under test: fake transport, fake
/// handler, fake non-periodic driver and the endpoint harness. The endpoint is not
/// started until <see cref="Start"/> is called so tests can exercise pre-activation
/// behaviour.
/// </summary>
public sealed class RawEndpointTestContext
{
    public const int DefaultMessageMaxByteSize = 1 << 20;
    public const long DefaultBufferCapacityBytes = 1 << 20;

    public RawEndpointTestContext(int messageMaxByteSize = DefaultMessageMaxByteSize, long bufferCapacityBytes = DefaultBufferCapacityBytes)
    {
        Transport = new FakeRawTransport();
        RemoteEndPoint = new FakeEndPoint();
        Handler = new RecordingRawHandler();
        Driver = new FakeNonPeriodicDriver();
        Endpoint = new RawEndpointHarness(Transport, RemoteEndPoint, Handler, messageMaxByteSize, bufferCapacityBytes);
        Handler.Endpoint = Endpoint;
    }

    public FakeRawTransport Transport { get; }

    public FakeEndPoint RemoteEndPoint { get; }

    public RecordingRawHandler Handler { get; }

    public FakeNonPeriodicDriver Driver { get; }

    public RawEndpointHarness Endpoint { get; }

    public IMemoryRental Memory => Transport.Memory;

    public bool Start()
    {
        return Driver.Start(Endpoint);
    }

    /// <summary>Starts the endpoint through the driver and fails the test if startup failed.</summary>
    public void Activate()
    {
        Assert.That(Start(), Is.True);
    }
}
