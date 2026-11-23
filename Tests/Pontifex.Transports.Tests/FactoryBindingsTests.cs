using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Factory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.Raw.Reliable.Reconnectable;
using Pontifex.Raw.Reliable.Logger;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.Raw.Reliable.Zip;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Raw.Unreliable.Udp;
using Pontifex.RR;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.RR.Unreliable.Udp;
using Pontifex.TransportFormula;
using Pontifex.Utils;
using Scriba;
using FormulaRegistry = Pontifex.TransportFormula.Registry;
using Registration = Pontifex.Factory.Registration;

namespace Pontifex.Tests;

[TestFixture]
public sealed class FactoryBindingsTests
{
    private static readonly object[] CompileCases =
    {
        Case(Registration.TransportRole.Server, "direct(\"node-1\")", TransportType.RawReliable),
        Case(Registration.TransportRole.Server, "direct-raw-unreliable(\"node-1\")", TransportType.RawUnreliable),
        Case(Registration.TransportRole.Server, "direct-rr-unreliable(\"node-1\")", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Server, "direct-rr-unreliable(\"node-1\", response_ttl = 5)", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Server, "tcp(\"127.0.0.1\", 9000)", TransportType.RawReliable),
        Case(Registration.TransportRole.Server, "tcp(\"127.0.0.1\", 9000, disconnection_timeout = 10)", TransportType.RawReliable),
        Case(Registration.TransportRole.Server, "udp(\"127.0.0.1\", 9000)", TransportType.RawUnreliable),
        Case(Registration.TransportRole.Server, "udp_rr(\"127.0.0.1\", 9000)", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Server, "udp_rr(\"127.0.0.1\", 9000, response_ttl = 5)", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Server, "zip(tcp(\"127.0.0.1\", 9000), compression_level = 9)", TransportType.RawReliable),
        Case(Registration.TransportRole.Server, "reconnectable(30, tcp(\"127.0.0.1\", 9000))", TransportType.RawReliable),
        Case(Registration.TransportRole.Server, "log(tcp(\"127.0.0.1\", 9000))", TransportType.RawReliable),
        Case(Registration.TransportRole.Client, "direct(\"node-1\")", TransportType.RawReliable),
        Case(Registration.TransportRole.Client, "direct-raw-unreliable(\"node-1\")", TransportType.RawUnreliable),
        Case(Registration.TransportRole.Client, "direct-raw-unreliable(\"node-1\", delivery = { type = \"perfect\" })", TransportType.RawUnreliable),
        Case(Registration.TransportRole.Client, "direct-rr-unreliable(\"node-1\")", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Client, "direct-rr-unreliable(\"node-1\", request_ttl = 5)", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Client, "direct-rr-unreliable(\"node-1\", delivery = { type = \"perfect\" })", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Client, "tcp(\"127.0.0.1\", 9000)", TransportType.RawReliable),
        Case(Registration.TransportRole.Client, "tcp(\"127.0.0.1\", 9000, disconnection_timeout = 10)", TransportType.RawReliable),
        Case(Registration.TransportRole.Client, "udp(\"127.0.0.1\", 9000)", TransportType.RawUnreliable),
        Case(Registration.TransportRole.Client, "udp_rr(\"127.0.0.1\", 9000)", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Client, "udp_rr(\"127.0.0.1\", 9000, request_ttl = 5)", TransportType.RRUnreliable),
        Case(Registration.TransportRole.Client, "zip(tcp(\"127.0.0.1\", 9000))", TransportType.RawReliable),
        Case(Registration.TransportRole.Client, "reconnectable(30, tcp(\"127.0.0.1\", 9000))", TransportType.RawReliable),
        Case(Registration.TransportRole.Client, "log(tcp(\"127.0.0.1\", 9000))", TransportType.RawReliable)
    };

    [TestCaseSource(nameof(CompileCases))]
    public void Compile_WithRegisteredBinding_ProducesTransportOfExpectedType(
        Registration.TransportRole role,
        string formula,
        TransportType expectedType)
    {
        var factory = BuildFactory();
        var compiler = factory.CreateCompiler(
            role,
            new Logger([]),
            MemoryRental.Shared);

        var transport = factory.Compile(formula, compiler);

        Assert.That(transport, Is.Not.Null);
        Assert.That(transport.Type, Is.EqualTo(expectedType));
    }

    [Test]
    public void Canonicalize_WithEveryBinding_ReturnsCanonicalFormula()
    {
        var factory = BuildFactory();

        Assert.That(
            factory.Canonicalize("DIRECT( \"node-1\" )"),
            Is.EqualTo("direct(node-1)"));
        Assert.That(
            factory.Canonicalize("direct-raw-unreliable(\"node-1\", delivery = { type = \"netem\", latencyns = 1000, correlation = { delayrho = 1, lossrho = 2 }, delaydistribution = { values = [1, 2, 3] } })"),
            Is.EqualTo("direct-raw-unreliable(node-1, delivery={type=netem, latencyns=1000, correlation={delayrho=1, lossrho=2}, delaydistribution={values=[1, 2, 3]}})"));
        Assert.That(
            factory.Canonicalize("DIRECT-RR-UNRELIABLE( \"node-1\", request_ttl = 5, delivery = { type = \"perfect\" } )"),
            Is.EqualTo("direct-rr-unreliable(node-1, request_ttl=5, delivery={type=perfect})"));
        Assert.That(
            factory.Canonicalize("direct-rr-unreliable(\"node-1\", response_ttl = 5)"),
            Is.EqualTo("direct-rr-unreliable(node-1, response_ttl=5)"));
        Assert.That(
            factory.Canonicalize("TCP(\"127.0.0.1\", 9000)"),
            Is.EqualTo("tcp(127.0.0.1, 9000)"));
        Assert.That(
            factory.Canonicalize("UDP(\"127.0.0.1\", 9000) AS RRUnreliable"),
            Is.EqualTo("udp(127.0.0.1, 9000) as RRUnreliable"));
        Assert.That(
            factory.Canonicalize("UDP_RR(\"127.0.0.1\", 9000, request_ttl = 5)"),
            Is.EqualTo("udp_rr(127.0.0.1, 9000, request_ttl=5)"));
        Assert.That(
            factory.Canonicalize("ZIP(TCP(\"127.0.0.1\", 9000), compression_level = 1)"),
            Is.EqualTo("zip(tcp(127.0.0.1, 9000), compression_level=1)"));
        Assert.That(
            factory.Canonicalize("RECONNECTABLE(30, TCP(\"127.0.0.1\", 9000))"),
            Is.EqualTo("reconnectable(30, tcp(127.0.0.1, 9000))"));
        Assert.That(
            factory.Canonicalize("LOG(TCP(\"127.0.0.1\", 9000))"),
            Is.EqualTo("log(tcp(127.0.0.1, 9000))"));
    }

    [Test]
    public void Compile_ServerDelivery_Throws()
    {
        var factory = BuildFactory();
        var compiler = factory.CreateCompiler(
            Registration.TransportRole.Server,
            new Logger([]),
            MemoryRental.Shared);

        var exception = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-raw-unreliable(\"node-1\", delivery = { type = \"perfect\" })",
            compiler));
        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void Compile_UnknownDeliveryType_Throws()
    {
        var factory = BuildFactory();
        var compiler = factory.CreateCompiler(
            Registration.TransportRole.Client,
            new Logger([]),
            MemoryRental.Shared);

        var exception = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-raw-unreliable(\"node-1\", delivery = { type = \"bogus\" })",
            compiler));
        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void Compile_FourStateWithoutParameters_Throws()
    {
        var factory = BuildFactory();
        var compiler = factory.CreateCompiler(
            Registration.TransportRole.Client,
            new Logger([]),
            MemoryRental.Shared);

        var exception = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-raw-unreliable(\"node-1\", delivery = { type = \"netem\", lossmodel = \"fourState\" })",
            compiler));
        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void Compile_PartialNetemConfiguration_ProducesAClient()
    {
        var factory = BuildFactory();
        var compiler = factory.CreateCompiler(
            Registration.TransportRole.Client,
            new Logger([]),
            MemoryRental.Shared);

        var transport = factory.Compile(
            "direct-raw-unreliable(\"node-1\", delivery = { type = \"netem\", latencyns = 1000, correlation = { delayrho = 1 } })",
            compiler);
        Assert.That(transport, Is.InstanceOf<IRawUnreliableClient>());

        var client = (IRawUnreliableClient)transport;
        Assert.That(client.Init(new NoopUnreliableHandler()), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        Assert.That(client.Stop(), Is.True);
    }

    [Test]
    [Category("Integration")]
    public void TcpRawReliableBinding_CompilesAndConnects()
    {
        var factory = BuildFactory();
        int port = GetFreeLoopbackPort();

        var serverHandler = new RecordingRawReliableServerHandler();
        var serverHandlerFactory = new RecordingRawReliableServerHandlerFactory(serverHandler);
        var clientHandler = new RecordingRawReliableClientHandler();

        var serverTransport = factory.Compile(
            $"tcp(\"127.0.0.1\", {port})",
            factory.CreateCompiler(
                Registration.TransportRole.Server, new Logger([]), MemoryRental.Shared));
        Assert.That(serverTransport, Is.InstanceOf<IRawReliableServer>(),
            "the server role must compile to a reliable server transport");

        var clientTransport = factory.Compile(
            $"tcp(\"127.0.0.1\", {port})",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared));
        Assert.That(clientTransport, Is.InstanceOf<IRawReliableClient>(),
            "the client role must compile to a reliable client transport");

        var server = (IRawReliableServer)serverTransport;
        var client = (IRawReliableClient)clientTransport;
        try
        {
            Assert.That(server.Init(serverHandlerFactory), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(serverHandler.Connected.Wait(Timeout), Is.True,
                "the server handler must connect through the compiled binding");
            Assert.That(clientHandler.Connected.Wait(Timeout), Is.True,
                "the client handler must connect through the compiled binding");

            var toServer = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            toServer.PutLast(11);
            Assert.That(clientHandler.Endpoint!.Send(toServer), Is.EqualTo(SendResult.Ok));
            Assert.That(serverHandler.Received.Wait(Timeout), Is.True,
                "the server must receive the message sent by the compiled client");
            Assert.That(serverHandler.ReceivedValues, Is.EqualTo(new[] { 11 }));

            var toClient = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            toClient.PutLast(22);
            Assert.That(serverHandler.Endpoint!.Send(toClient), Is.EqualTo(SendResult.Ok));
            Assert.That(clientHandler.Received.Wait(Timeout), Is.True,
                "the client must receive the message sent by the compiled server");
            Assert.That(clientHandler.ReceivedValues, Is.EqualTo(new[] { 22 }));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Test]
    public void Compile_RRUnreliable_ExposesByteLimitAndTtlDefaults()
    {
        var factory = BuildFactory();

        var serverTransport = factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\")",
            factory.CreateCompiler(
                Registration.TransportRole.Server, new Logger([]), MemoryRental.Shared));
        var clientTransport = factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\")",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared));

        var server = (IRRUnreliableServerTransport)serverTransport;
        var client = (IRRUnreliableClientTransport)clientTransport;

        Assert.Multiple(() =>
        {
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(1024 * 1024));
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(1024 * 1024));
            Assert.That(server.ResponseTtl, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(client.RequestTtl, Is.EqualTo(TimeSpan.FromSeconds(3)));
        });
    }

    [Test]
    public void Compile_RRUnreliable_ExposesExplicitTtls()
    {
        var factory = BuildFactory();

        var server = (IRRUnreliableServerTransport)factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", response_ttl = 7)",
            factory.CreateCompiler(
                Registration.TransportRole.Server, new Logger([]), MemoryRental.Shared));
        var client = (IRRUnreliableClientTransport)factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", request_ttl = 9)",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared));

        Assert.Multiple(() =>
        {
            Assert.That(server.ResponseTtl, Is.EqualTo(TimeSpan.FromSeconds(7)));
            Assert.That(client.RequestTtl, Is.EqualTo(TimeSpan.FromSeconds(9)));
        });
    }

    [Test]
    public void Compile_RRUnreliableWithWrongRoleTtl_Throws()
    {
        var factory = BuildFactory();

        var serverException = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", request_ttl = 5)",
            factory.CreateCompiler(
                Registration.TransportRole.Server, new Logger([]), MemoryRental.Shared)));
        var clientException = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", response_ttl = 5)",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared)));

        Assert.Multiple(() =>
        {
            Assert.That(serverException.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(clientException.InnerException, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public void Compile_RRUnreliableServerDelivery_Throws()
    {
        var factory = BuildFactory();

        var exception = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", delivery = { type = \"perfect\" })",
            factory.CreateCompiler(
                Registration.TransportRole.Server, new Logger([]), MemoryRental.Shared)));

        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void Compile_RRUnreliableFourStateWithoutParameters_Throws()
    {
        var factory = BuildFactory();

        var exception = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", delivery = { type = \"netem\", lossmodel = \"fourState\" })",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared)));

        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void Compile_RRUnreliableGilbertElliotWithoutParameters_Throws()
    {
        var factory = BuildFactory();

        var exception = Assert.Throws<BuildException>(() => factory.Compile(
            "direct-rr-unreliable(\"rr-node-1\", delivery = { type = \"netem\", lossmodel = \"gilbertElliot\" })",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared)));

        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    [Category("Integration")]
    public void DirectRRUnreliableBinding_CompilesAndRoundTrips()
    {
        var factory = BuildFactory();
        var serverId = "rr-bind-" + Guid.NewGuid().ToString("N");

        var serverTransport = factory.Compile(
            $"direct-rr-unreliable(\"{serverId}\")",
            factory.CreateCompiler(
                Registration.TransportRole.Server, new Logger([]), MemoryRental.Shared));
        var clientTransport = factory.Compile(
            $"direct-rr-unreliable(\"{serverId}\")",
            factory.CreateCompiler(
                Registration.TransportRole.Client, new Logger([]), MemoryRental.Shared));

        var server = (IRRUnreliableServerTransport)serverTransport;
        var client = (IRRUnreliableClientTransport)clientTransport;
        using var serverHandler = new RecordingRRServerHandler();
        using var clientHandler = new RecordingRRClientHandler();

        try
        {
            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(41);
            Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));

            Assert.That(serverHandler.Requested.Wait(Timeout), Is.True,
                "the server must receive the request through the compiled binding");
            Assert.That(clientHandler.Responded.Wait(Timeout), Is.True,
                "the client must receive the response through the compiled binding");
            Assert.That(clientHandler.ResponseValue, Is.EqualTo(42));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    private static ITransportFactory BuildFactory()
    {
        var builder = TransportFactoryBootstrap.CreateBuilder();

        foreach (TransportType type in Enum.GetValues<TransportType>())
        {
            builder.Registry.RegisterType(
                new FormulaRegistry.TransportTypeInfo(type, type.ToString()));
        }

        builder.RegisterTransport(RawReliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RRUnreliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableTcpBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableUdpBinding.CreateRegistration());
        builder.RegisterTransport(RRUnreliableUdpBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableZipBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableReconnectableBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableLoggerBinding.CreateRegistration());

        return builder.Build();
    }

    private static object[] Case(
        Registration.TransportRole role,
        string formula,
        TransportType expectedType)
    {
        return new object[] { role, formula, expectedType };
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private sealed class NoopUnreliableHandler : IRawUnreliableHandler
    {
        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
        }
    }

    private sealed class RecordingRawReliableServerHandlerFactory
        : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly IRawReliableServerHandler _handler;

        public RecordingRawReliableServerHandlerFactory(IRawReliableServerHandler handler)
        {
            _handler = handler;
        }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            return _handler;
        }
    }

    private sealed class RecordingRawReliableServerHandler : IRawReliableServerHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Received = new(false);

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public IReadOnlyList<int> ReceivedValues
        {
            get
            {
                lock (_lock)
                {
                    return _receivedValues.ToArray();
                }
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            Connected.Set();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                lock (_lock)
                {
                    if (receivedBuffer.Elements.Count > 0)
                    {
                        _receivedValues.Add(receivedBuffer.Elements[0].Alias.IntValue);
                    }
                }

                Received.Set();
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
        }

        public void OnStopped(StopReason reason)
        {
        }
    }

    private sealed class RecordingRRClientHandler : RRRequestHandler, IRRUnreliableRequestHandler,
        IDisposable
    {
        public ManualResetEventSlim Responded { get; } = new(false);

        public int ResponseValue { get; private set; }

        public void Response(UnionDataList response)
        {
            try
            {
                ResponseValue = response.Elements[0].Alias.IntValue;
                Responded.Set();
            }
            finally
            {
                response.Release();
            }
        }

        public void Dispose()
        {
            Responded.Dispose();
        }
    }

    private sealed class RecordingRRServerHandler : IRRUnreliableServerHandler, IDisposable
    {
        public ManualResetEventSlim Requested { get; } = new(false);

        public bool OnRequested(IEndPoint client, UnionDataList request, IRRUnreliableResponse response)
        {
            try
            {
                int value = request.Elements[0].Alias.IntValue;
                var payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
                payload.PutLast(value + 1);
                Assert.That(response.Response(payload), Is.EqualTo(SendResult.Ok));
                Requested.Set();
                return true;
            }
            finally
            {
                request.Release();
            }
        }

        public void Dispose()
        {
            Requested.Dispose();
        }
    }

    private sealed class RecordingRawReliableClientHandler : IRawReliableClientHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Received = new(false);

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public IReadOnlyList<int> ReceivedValues
        {
            get
            {
                lock (_lock)
                {
                    return _receivedValues.ToArray();
                }
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            Connected.Set();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                lock (_lock)
                {
                    if (receivedBuffer.Elements.Count > 0)
                    {
                        _receivedValues.Add(receivedBuffer.Elements[0].Alias.IntValue);
                    }
                }

                Received.Set();
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
