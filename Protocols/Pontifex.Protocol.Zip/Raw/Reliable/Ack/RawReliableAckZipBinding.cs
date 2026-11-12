using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Raw.Reliable.Ack.Zip
{
    /// <summary>
    /// Factory binding for the zip reliable Ack wrapper.
    /// </summary>
    public static class RawReliableAckZipBinding
    {
        public const string SchemeName = ZipInfo.TransportName;

        private const int DefaultCompressionLevel = 9;

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
                ? new RawReliableAckZipConstructor.ZipServer(
                    (IRawReliableAckServer)nested(),
                    compressionLevel)
                : new RawReliableAckZipConstructor.ZipClient(
                    (IRawReliableAckClient)nested(),
                    compressionLevel);
        }
    }
}
