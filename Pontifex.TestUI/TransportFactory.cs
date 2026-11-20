using System;
using Actuarius.Memory;
using Pontifex.Factory;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.Raw.Reliable.Logger;
using Pontifex.Raw.Reliable.Reconnectable;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.Raw.Reliable.Zip;
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

            builder.RegisterTransport(RawReliableDirectBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableTcpBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableZipBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableReconnectableBinding.CreateRegistration());
            builder.RegisterTransport(RawReliableLoggerBinding.CreateRegistration());

            _factory = builder.Build();
        }

        public IRawReliableServer? ConstructServer(
            string formula,
            ILogger logger,
            IMemoryRental memoryRental)
        {
            var compiler = _factory.CreateCompiler(
                TransportRole.Server,
                logger,
                memoryRental);
            return _factory.Compile(formula, compiler) as IRawReliableServer;
        }

        public IRawReliableClient? ConstructClient(
            string formula,
            ILogger logger,
            IMemoryRental memoryRental)
        {
            var compiler = _factory.CreateCompiler(
                TransportRole.Client,
                logger,
                memoryRental);
            return _factory.Compile(formula, compiler) as IRawReliableClient;
        }
    }
}
