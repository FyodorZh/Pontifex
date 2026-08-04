using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack.Direct;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.Converters;
using Pontifex.Factory;
//using Pontifex.Raw.Reliable.NoAck.Direct;
using Pontifex.Raw.Unreliable.NoAck.Direct;
using Pontifex.Raw.Unreliable.NoAck.Udp;
using Scriba;
using Scriba.Consumers;

namespace Pontifex.Tests;

public static class TransportRegistry
{
    public static IMemoryRental Memory { get; }
    public static TransportBuilder Builder { get; }
    public static IDescriptionFactory DescriptionFactory => Builder.DescriptionFactory;

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
        Builder = new TransportBuilder(ConvertersGraph.Default);

        Builder.RegisterTransport(new RawReliableAckDirectConstructor());
        Builder.RegisterTransport(new RawReliableAckTcpConstructor());
        Builder.RegisterTransport(new RawUnreliableNoAckDirectConstructor());
        Builder.RegisterTransport(new RawUnreliableNoAckUdpConstructor());
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
