using System;
using Pontifex;
using Pontifex.TransportFormula.Compilation;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Core = Pontifex.Factory.Registration;

namespace Pontifex.Factory.Tests.Bootstrap.Harness
{
    // Hand-written construction helpers for the bootstrap suite. No mocking
    // library is used anywhere in this repository. Each suite keeps its own
    // harness, so the metadata helpers are local.
    internal static class BootstrapTestData
    {
        public static readonly TransportType TypeA = TransportType.RawReliableAck;
        public static readonly TransportType TypeB = TransportType.RawReliableNoAck;

        public static ITransportFactoryBuilder NewBuilder()
        {
            var builder = TransportFactoryBootstrap.CreateBuilder();
            RegisterStandardTypes(builder);
            return builder;
        }

        public static void RegisterStandardTypes(ITransportFactoryBuilder builder)
        {
            builder.Registry.RegisterType(new TransportTypeInfo(TypeA, "TypeA"));
            builder.Registry.RegisterType(new TransportTypeInfo(TypeB, "TypeB"));
        }

        public static TransportRegistration AlphaRegistration()
        {
            return Registration(
                "alpha",
                TypeA,
                new[] { "a" },
                new StringParam("value", false, true, null));
        }

        public static TransportRegistration GammaRegistration()
        {
            return Registration(
                "gamma",
                TypeB,
                Array.Empty<string>(),
                new StringParam("name", true, true, null));
        }

        public static TransportRegistration Registration(
            string name,
            TransportType type,
            string[] aliases,
            params Param[] parameters)
        {
            return new TransportRegistration(
                new TransportConstructorInfo(name, type, parameters, Array.Empty<string>()),
                aliases);
        }

        public static TransportTypeSet Exactly(TransportType type)
        {
            return new TransportTypeSet(TransportTypeSetKind.Exactly, new[] { type });
        }

        public static Core.TransportParameter CoreIntegerParameter(
            string name,
            long min,
            long max,
            bool required = false,
            bool positional = false)
        {
            return new Core.IntegerParameter(
                name,
                required,
                positional,
                new Core.IntegerType(min, max));
        }

        public static Core.TransportParameter CoreDecimalParameter(
            string name,
            decimal min,
            decimal max,
            bool required = false,
            bool positional = false)
        {
            return new Core.DecimalParameter(
                name,
                required,
                positional,
                new Core.DecimalType(min, max));
        }

        public static Core.StructField CoreIntegerField(
            string name,
            long min,
            long max,
            bool required = false)
        {
            return new Core.IntegerField(name, required, new Core.IntegerType(min, max));
        }

        public static Core.StructField CoreDecimalField(
            string name,
            decimal min,
            decimal max,
            bool required = false)
        {
            return new Core.DecimalField(name, required, new Core.DecimalType(min, max));
        }

        public static Core.StructField CoreArrayField(
            string name,
            Core.SingleType elementType,
            int min = 0,
            int? max = null,
            bool required = false)
        {
            return new Core.ArrayField(name, required, elementType, min, max);
        }

        // Core registration helper: the name and metadata deliberately pass
        // through unchanged so tests can assert the facade's normalization.
        public static Core.TransportRegistration CoreRegistration(
            string name,
            TransportType type,
            Func<Core.ITransportBuildContext, ITransport> create,
            Core.TransportParameter[]? parameters = null,
            string[]? aliases = null)
        {
            return new Core.TransportRegistration(
                name,
                type,
                parameters ?? Array.Empty<Core.TransportParameter>(),
                create,
                aliases);
        }
    }

    // Captures the resolved tree and returns a deterministic marker. Exposes only
    // what the bootstrap tests inspect.
    internal sealed class RecordingCompiler : ICompiler<string>
    {
        public int Calls { get; private set; }

        public ResolvedFormula? Formula { get; private set; }

        public string Compile(ResolvedFormula formula)
        {
            Calls++;
            Formula = formula;
            return "compiled:" + formula.Root.Type;
        }
    }

    // Throws a caller-supplied exception unchanged, so the suite can prove that
    // bootstrap does not relabel compiler failures.
    internal sealed class ThrowingCompiler : ICompiler<string>
    {
        private readonly Exception _exception;

        public ThrowingCompiler(Exception exception)
        {
            _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public string Compile(ResolvedFormula formula)
        {
            throw _exception;
        }
    }
}
