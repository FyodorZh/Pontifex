using Actuarius.Memory;
using Pontifex.Factory;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.Raw.Unreliable.Udp;
using Scriba;
using Scriba.Consumers;
using FormulaRegistry = Pontifex.TransportFormula.Registry;

namespace Pontifex.Tests;

public static class TransportRegistry
{
    public static IMemoryRental Memory { get; }
    public static ITransportFactory Factory { get; }

    public static ILogger GetLogger(bool failIfError)
    {
        var logger = new Logger([new ConsoleConsumer(), new LogConsumer(msg =>
        {
            if (failIfError)
            {
                Assert.That(msg.Severity > Severity.ERROR);
            }
        })]);
        logger.LogFor = Severity.WARN;
        return logger;
    }

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
        builder.RegisterTransport(RawUnreliableUdpBinding.CreateRegistration());

        Factory = builder.Build();
    }

    private class LogConsumer : ILogConsumer
    {
        private readonly Action<MessageData> _processor;
        
        public LogConsumer(Action<MessageData> processor)
        {
            _processor = processor;
        }
        
        public void Message(MessageData logMessage)
        {
            _processor.Invoke(logMessage);
        }

        public void AddRef()
        {
        }

        public void Release()
        {
        }
    }
}
