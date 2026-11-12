using System;
using Pontifex;
using Pontifex.TransportFormula.Compilation;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;

namespace Pontifex.Factory.Tests.Bootstrap.Harness
{
    // Hand-written construction helpers for the bootstrap suite. No mocking
    // library is used anywhere in this repository. Sibling suites do not share
    // harness (Tests/RULES.md HARNESS-05), so the metadata helpers are local.
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
