using System;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Protocols;

namespace Pontifex.Raw.Reliable.Zip
{
    /// <summary>
    /// Factory binding for the zip reliable wrapper.
    /// </summary>
    public static class RawReliableZipBinding
    {
        public const string SchemeName = ZipInfo.TransportName;

        private const short NegotiationVersion = 1;
        private const int DefaultCompressionLevel = 9;

        private sealed class ZipClient : RawReliableWrapperClient
        {
            public ZipClient(IRawReliableClient transportToWrap, int compressionLevel)
                : base(ZipInfo.TransportName, ZipInfo.TransportName, transportToWrap,
                    new RawReliableZipClientLogic(
                        transportToWrap.Log,
                        transportToWrap.Memory,
                        compressionLevel),
                    NegotiationVersion)
            {
            }
        }

        private sealed class ZipServer : RawReliableWrapperServer
        {
            public ZipServer(IRawReliableServer transportToWrap, int compressionLevel)
                : base(ZipInfo.TransportName, ZipInfo.TransportName, transportToWrap,
                    // A fresh logic per session: the zip logic releases its compressor/decompressor in
                    // OnDisconnected, so a shared instance would break every later session.
                    () => new RawReliableZipServerLogic(
                        transportToWrap.Log,
                        transportToWrap.Memory,
                        compressionLevel),
                    NegotiationVersion)
            {
            }
        }

        public static ITransportRegistration CreateRegistration()
        {
            return new TransportRegistration(
                SchemeName,
                TransportType.RawReliable,
                new TransportParameter[]
                {
                    new NestedTransportParameter(
                        "nested",
                        required: true,
                        positional: true,
                        TransportTypeSet.Exactly(TransportType.RawReliable)),
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
                    (IRawReliableServer)nested(),
                    compressionLevel)
                : new ZipClient(
                    (IRawReliableClient)nested(),
                    compressionLevel);
        }
    }
}
