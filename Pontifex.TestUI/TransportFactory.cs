using System;
using Actuarius.Memory;
using Pontifex.Factory;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Direct;
using Pontifex.Raw.Reliable.Ack.Logger;
using Pontifex.Raw.Reliable.Ack.Reconnectable;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.Raw.Reliable.Ack.Zip;
using Scriba;
using FormulaRegistry = Pontifex.TransportFormula.Registry;

namespace Pontifex.Test
{
    public class TransportFactory
    {
        private readonly ITransportFactory _factory;

        public TransportFactory()
        {
            var builder = TransportFactoryBootstrap.CreateBuilder();

            foreach (TransportType type in Enum.GetValues(typeof(TransportType)))
            {
                builder.Registry.RegisterType(
                    new FormulaRegistry.TransportTypeInfo(type, type.ToString()));
            }

            builder.RegisterTransport(RawReliableAckDirectBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableAckTcpBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableAckZipBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableAckReconnectableBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableAckLoggerBinding.CreateRegistration());

            _factory = builder.Build();
        }

        public IRawReliableAckServer? ConstructServer(
            string formula,
            ILogger logger,
            IMemoryRental memoryRental)
        {
            var compiler = _factory.CreateCompiler(
                TransportRole.Server,
                logger,
                memoryRental);
            return _factory.Compile(formula, compiler) as IRawReliableAckServer;
        }

        public IRawReliableAckClient? ConstructClient(
            string formula,
            ILogger logger,
            IMemoryRental memoryRental)
        {
            var compiler = _factory.CreateCompiler(
                TransportRole.Client,
                logger,
                memoryRental);
            return _factory.Compile(formula, compiler) as IRawReliableAckClient;
        }
    }
}
