using Actuarius.Memory;
using Pontifex;
using Pontifex.Factory;
using Pontifex.Raw.Reliable.Ack.Direct;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Raw.Reliable.Ack.Reconnectable;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.Raw.Reliable.Ack.Zip;
using Pontifex.Raw.Unreliable.Ack.Direct;
using Pontifex.Raw.Unreliable.Ack.Udp;
using Pontifex.Raw.Unreliable.NoAck.Direct;
using Pontifex.Raw.Unreliable.NoAck.Udp;
using Pontifex.RR.Unreliable.NoAck.Udp;
using Scriba;
using FormulaRegistry = Pontifex.TransportFormula.Registry;
using Registration = Pontifex.Factory.Registration;

namespace Pontifex.Tests;

[TestFixture]
public sealed class FactoryBindingsTests
{
    private static readonly object[] CompileCases =
    {
        Case(Registration.TransportRole.Server, "direct(\"node-1\")", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Server, "direct-raw-unreliable-ack(\"node-1\")", TransportType.RawUnreliableAck),
        Case(Registration.TransportRole.Server, "direct-raw-unreliable-noack(\"node-1\", delivery = { type = \"perfect\" })", TransportType.RawUnreliableNoAck),
        Case(Registration.TransportRole.Server, "tcp(\"127.0.0.1\", 9000)", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Server, "tcp(\"127.0.0.1\", 9000, disconnection_timeout = 10)", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Server, "udp-raw-unreliable-ack(\"127.0.0.1\", 9000)", TransportType.RawUnreliableAck),
        Case(Registration.TransportRole.Server, "udp(\"127.0.0.1\", 9000)", TransportType.RawUnreliableNoAck),
        Case(Registration.TransportRole.Server, "udp_rr(\"127.0.0.1\", 9000)", TransportType.RRUnreliableNoAck),
        Case(Registration.TransportRole.Server, "zip(tcp(\"127.0.0.1\", 9000), compression_level = 9)", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Server, "reconnectable(30, tcp(\"127.0.0.1\", 9000))", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Server, "log(tcp(\"127.0.0.1\", 9000))", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Client, "direct(\"node-1\")", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Client, "direct-raw-unreliable-ack(\"node-1\", delivery = { type = \"perfect\" })", TransportType.RawUnreliableAck),
        Case(Registration.TransportRole.Client, "direct-raw-unreliable-noack(\"node-1\")", TransportType.RawUnreliableNoAck),
        Case(Registration.TransportRole.Client, "tcp(\"127.0.0.1\", 9000)", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Client, "udp-raw-unreliable-ack(\"127.0.0.1\", 9000)", TransportType.RawUnreliableAck),
        Case(Registration.TransportRole.Client, "udp(\"127.0.0.1\", 9000)", TransportType.RawUnreliableNoAck),
        Case(Registration.TransportRole.Client, "udp_rr(\"127.0.0.1\", 9000)", TransportType.RRUnreliableNoAck),
        Case(Registration.TransportRole.Client, "zip(tcp(\"127.0.0.1\", 9000))", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Client, "reconnectable(30, tcp(\"127.0.0.1\", 9000))", TransportType.RawReliableAck),
        Case(Registration.TransportRole.Client, "log(tcp(\"127.0.0.1\", 9000))", TransportType.RawReliableAck)
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
            factory.Canonicalize("direct-raw-unreliable-noack(\"node-1\", delivery = { type = \"netem\", latencyns = 1000, correlation = { delayrho = 1, lossrho = 2 }, delaydistribution = { values = [1, 2, 3] } })"),
            Is.EqualTo("direct-raw-unreliable-noack(node-1, delivery={type=netem, latencyns=1000, correlation={delayrho=1, lossrho=2}, delaydistribution={values=[1, 2, 3]}})"));
        Assert.That(
            factory.Canonicalize("TCP(\"127.0.0.1\", 9000)"),
            Is.EqualTo("tcp(127.0.0.1, 9000)"));
        Assert.That(
            factory.Canonicalize("UDP(\"127.0.0.1\", 9000) AS RRUnreliableNoAck"),
            Is.EqualTo("udp(127.0.0.1, 9000) as RRUnreliableNoAck"));
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

    private static ITransportFactory BuildFactory()
    {
        var builder = TransportFactoryBootstrap.CreateBuilder();

        foreach (TransportType type in Enum.GetValues<TransportType>())
        {
            builder.Registry.RegisterType(
                new FormulaRegistry.TransportTypeInfo(type, type.ToString()));
        }

        builder.RegisterTransport(RawReliableAckDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableAckDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableNoAckDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableAckTcpBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableAckUdpBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableNoAckUdpBinding.CreateRegistration());
        builder.RegisterTransport(RRUnreliableNoAckUdpBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableAckZipBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableAckReconnectableBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableAckLoggerBinding.CreateRegistration());

        return builder.Build();
    }

    private static object[] Case(
        Registration.TransportRole role,
        string formula,
        TransportType expectedType)
    {
        return new object[] { role, formula, expectedType };
    }
}
