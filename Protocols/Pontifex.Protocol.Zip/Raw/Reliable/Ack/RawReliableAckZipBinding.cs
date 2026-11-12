using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Protocols;

namespace Pontifex.Raw.Reliable.Ack.Zip
{
    /// <summary>
    /// Factory binding for the zip reliable Ack wrapper.
    /// </summary>
    public static class RawReliableAckZipBinding
    {
        public const string SchemeName = ZipInfo.TransportName;

        private const int DefaultCompressionLevel = 9;

        private sealed class ZipClient
            : RawReliableAckWrapperClient<RawReliableAckZipClientLogic>, IRawReliableAckClient
        {
            public ZipClient(IRawReliableAckClient transportToWrap, int compressionLevel)
                : base(ZipInfo.TransportName, transportToWrap,
                    (logger, memoryRental) =>
                        new RawReliableAckZipClientLogic(logger, memoryRental, compressionLevel))
            {
            }
        }

        private sealed class ZipServer
            : RawReliableAckWrapperServer<
                AcknowledgerWrapper<HandlerWrapper<RawReliableAckZipServerLogic>>>,
                IRawReliableAckServer
        {
            public ZipServer(IRawReliableAckServer transportToWrap, int compressionLevel)
                : base(
                    ZipInfo.TransportName,
                    transportToWrap,
                    (logger, memory) =>
                        new AcknowledgerWrapper<HandlerWrapper<RawReliableAckZipServerLogic>>(
                            () => new HandlerWrapper<RawReliableAckZipServerLogic>(
                                () => new RawReliableAckZipServerLogic(
                                    logger,
                                    memory,
                                    compressionLevel))))
            {
            }
        }

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliableAck,
                new TransportParameter[]
                {
                    new NestedTransportParameter(
                        "nested",
                        required: true,
                        positional: true,
                        TransportTypeSet.Exactly(TransportType.RawReliableAck)),
                    new IntegerParameter(
                        "compression_level",
                        required: false,
                        positional: false,
                        new IntegerType(0, 9))
                },
                Create);
        }

        private static ITransport Create(ITransportBuildContext context)
        {
            if (!context.TryGetTransport("nested", out var nested))
            {
                throw new InvalidOperationException(
                    "The resolver guarantees the required 'nested' parameter.");
            }

            var compressionLevel = DefaultCompressionLevel;
            if (context.TryGetInt64("compression_level", out var level))
            {
                compressionLevel = (int)level;
            }

            return context.Role == TransportRole.Server
                ? new ZipServer(
                    (IRawReliableAckServer)nested(),
                    compressionLevel)
                : new ZipClient(
                    (IRawReliableAckClient)nested(),
                    compressionLevel);
        }
    }
}
