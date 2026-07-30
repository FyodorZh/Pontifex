using Actuarius.Memory;
using Pontifex.Ack.Raw.Reliable.Direct;
using Pontifex.Ack.Raw.Reliable.Tcp;
using Pontifex.Converters;
using Pontifex.Factory;
//using Pontifex.NoAck.Raw.Reliable.Direct;
using Pontifex.NoAck.Raw.Unreliable.Direct;
using Pontifex.NoAck.Raw.Unreliable.Udp;
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

        Builder.RegisterTransport(new AckRawDirectConstructor());
        Builder.RegisterTransport(new AckRawTcpConstructor());
        //Builder.RegisterTransport(new NoAckRawReliableDirectConstructor());
        Builder.RegisterTransport(new NoAckRawUnreliableDirectConstructor());
        Builder.RegisterTransport(new NoAckRawUdpConstructor());
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
