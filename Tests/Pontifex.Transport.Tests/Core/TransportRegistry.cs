using Actuarius.Memory;
using Pontifex.Factory;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Raw.Unreliable.Udp;
using Pontifex.RR.Reliable.Direct;
using Pontifex.RR.Unreliable.Direct;
using Scriba;
using FormulaRegistry = Pontifex.TransportFormula.Registry;

namespace Pontifex.Tests;

public static class TransportRegistry
{
    public static IMemoryRental Memory { get; }
    public static ITransportFactory Factory { get; }

    public static RecordingLogger GetLogger(bool failIfError, bool writeToConsole = true) =>
        new(failIfError, writeToConsole);

    static TransportRegistry()
    {
        Memory = MemoryRental.Shared;

        var builder = TransportFactoryBootstrap.CreateBuilder();
        foreach (TransportType type in Enum.GetValues(typeof(TransportType)))
        {
            builder.Registry.RegisterType(
                new FormulaRegistry.TransportTypeInfo(type, type.ToString()));
        }

        builder.RegisterTransport(RawReliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawReliableTcpBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RRUnreliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RRReliableDirectBinding.CreateRegistration());
        builder.RegisterTransport(RawUnreliableUdpBinding.CreateRegistration());

        Factory = builder.Build();
    }
}
