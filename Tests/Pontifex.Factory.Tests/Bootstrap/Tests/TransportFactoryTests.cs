using System;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Factory.Compilation;
using Pontifex.Factory.Registration;
using Pontifex.Factory.Tests.Bootstrap.Harness;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Scriba;
using Core = Pontifex.Factory.Registration;

namespace Pontifex.Factory.Tests.Bootstrap
{
    [TestFixture]
    [Category("Factory.Bootstrap")]
    public sealed class TransportFactoryTests
    {
        private static ITransportFactory NewFactory()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.Registry.RegisterTransport(BootstrapTestData.AlphaRegistration());
            builder.Registry.RegisterTransport(BootstrapTestData.GammaRegistration());
            return builder.Build();
        }

        private static ITransportCompiler NewCompiler(ITransportFactory factory)
        {
            return factory.CreateCompiler(
                TransportRole.Server,
                VoidLogger.Instance,
                MemoryRental.Shared);
        }

        private static ITransportFactory NewCoreFactory(
            RecordingConvertersGraph? converters = null,
            params Core.TransportRegistration[] registrations)
        {
            var builder = TransportFactoryBootstrap.CreateBuilder(converters);
            BootstrapTestData.RegisterStandardTypes(builder);
            for (var i = 0; i < registrations.Length; i++)
            {
                builder.RegisterTransport(registrations[i]);
            }

            return builder.Build();
        }

        [Test]
        public void Resolve_WithValidFormula_ReturnsResolvedTransport()
        {
            var result = NewFactory().Resolve("alpha(1)");

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Formula, Is.Not.Null);

            var transport = result.Formula!.Root as ResolvedTransport;
            Assert.That(transport, Is.Not.Null);
            Assert.That(transport!.Constructor.Name, Is.EqualTo("alpha"));
            Assert.That(transport.Type, Is.EqualTo(BootstrapTestData.TypeA));
        }

        [Test]
        public void Resolve_WithParseError_ReturnsResultInsteadOfThrowing()
        {
            var result = NewFactory().Resolve("alpha(");

            Assert.That(result.Formula, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SYN_UNEXPECTED_TOKEN"));
            Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Parsing));
        }

        [Test]
        public void Resolve_WithSemanticError_ReturnsResultInsteadOfThrowing()
        {
            var result = NewFactory().Resolve("nosuch()");

            Assert.That(result.Formula, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SEM_UNKNOWN_SCHEME"));
            Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        }

        [Test]
        public void Resolve_WithNullFormula_ReturnsEmptyInputError()
        {
            var result = NewFactory().Resolve(null!);

            Assert.That(result.Formula, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SYN_EMPTY_INPUT"));
            Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Parsing));
        }

        [Test]
        public void Resolve_WithNullExpectedType_UsesAny()
        {
            var result = NewFactory().Resolve("alpha(1)", null);

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Formula, Is.Not.Null);
        }

        [Test]
        public void Resolve_WithExpectedTypeThatExcludesTheCandidate_ReturnsNoVariantError()
        {
            var expected = BootstrapTestData.Exactly(BootstrapTestData.TypeB);

            var result = NewFactory().Resolve("alpha(1)", expected);

            Assert.That(result.Formula, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(
                result.Error!.Code,
                Is.EqualTo("E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE"));
            Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        }

        [Test]
        public void Resolve_WithMissingRequiredParameter_ReturnsMissingParameterError()
        {
            var result = NewFactory().Resolve("gamma()");

            Assert.That(result.Formula, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SEM_MISSING_REQUIRED_PARAM"));
            Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        }

        [Test]
        public void Canonicalize_WithValidFormula_ReturnsCanonicalOutput()
        {
            Assert.That(NewFactory().Canonicalize("ALPHA(1)"), Is.EqualTo("alpha(1)"));
        }

        [Test]
        public void Canonicalize_WithParseError_ThrowsFactoryExceptionWithParsingPhase()
        {
            var exception = Assert.Throws<FactoryException>(
                () => NewFactory().Canonicalize("alpha("));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_SYN_UNEXPECTED_TOKEN"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Parsing));
        }

        [Test]
        public void Canonicalize_WithSemanticError_ThrowsFactoryExceptionWithResolutionPhase()
        {
            var exception = Assert.Throws<FactoryException>(
                () => NewFactory().Canonicalize("nosuch()"));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_SEM_UNKNOWN_SCHEME"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        }

        [Test]
        public void Compile_WithValidFormula_PassesResolvedTreeToTheCompiler()
        {
            var compiler = new RecordingCompiler();

            var output = NewFactory().Compile("alpha(1)", compiler);

            Assert.That(output, Is.EqualTo("compiled:" + BootstrapTestData.TypeA));
            Assert.That(compiler.Calls, Is.EqualTo(1));
            Assert.That(compiler.Formula, Is.Not.Null);
            Assert.That(compiler.Formula!.Root, Is.InstanceOf<ResolvedTransport>());
        }

        [Test]
        public void Compile_WithParseError_DoesNotInvokeTheCompiler()
        {
            var compiler = new RecordingCompiler();

            var exception = Assert.Throws<FactoryException>(
                () => NewFactory().Compile("alpha(", compiler));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_SYN_UNEXPECTED_TOKEN"));
            Assert.That(compiler.Calls, Is.Zero);
        }

        [Test]
        public void Compile_WithSemanticError_DoesNotInvokeTheCompiler()
        {
            var compiler = new RecordingCompiler();

            var exception = Assert.Throws<FactoryException>(
                () => NewFactory().Compile("nosuch()", compiler));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_SEM_UNKNOWN_SCHEME"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
            Assert.That(compiler.Calls, Is.Zero);
        }

        [Test]
        public void Compile_WithNullCompiler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => NewFactory().Compile<string>("alpha(1)", null!));
        }

        [Test]
        public void Compile_WhenTheCompilerThrows_PropagatesTheExceptionUnchanged()
        {
            var expected = new CompilerException(
                "E_CMP_INCONSISTENT_TREE",
                "compiler rejected the tree");

            var thrown = Assert.Throws<CompilerException>(
                () => NewFactory().Compile("alpha(1)", new ThrowingCompiler(expected)));

            Assert.That(thrown, Is.SameAs(expected));
            Assert.That(thrown!.Error.Phase, Is.EqualTo(FactoryPhase.Compilation));
        }

        [Test]
        public void CreateCompiler_WithServerRole_ReturnsCompilerForThatRole()
        {
            var compiler = NewCompiler(NewFactory());

            Assert.That(compiler.Role, Is.EqualTo(TransportRole.Server));
        }

        [Test]
        public void CreateCompiler_WithClientRole_ReturnsCompilerForThatRole()
        {
            var compiler = NewFactory().CreateCompiler(
                TransportRole.Client,
                VoidLogger.Instance,
                MemoryRental.Shared);

            Assert.That(compiler.Role, Is.EqualTo(TransportRole.Client));
        }

        [Test]
        public void CreateCompiler_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => NewFactory().CreateCompiler(
                    TransportRole.Server,
                    null!,
                    MemoryRental.Shared));
        }

        [Test]
        public void CreateCompiler_WithNullMemory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => NewFactory().CreateCompiler(
                    TransportRole.Server,
                    VoidLogger.Instance,
                    null!));
        }

        [Test]
        public void Compile_WithCoreRegistration_ReturnsLiveTransportAndPassesStringParameter()
        {
            string? captured = null;
            var factory = NewCoreFactory(
                null,
                BootstrapTestData.CoreRegistration(
                    "Alpha",
                    BootstrapTestData.TypeA,
                    context =>
                    {
                        context.TryGetString("value", out var value);
                        captured = value;
                        return new FakeTransport(BootstrapTestData.TypeA, value);
                    },
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.StringParameter("Value", required: true, positional: true)
                    }));

            var transport = factory.Compile(
                "alpha(hello)",
                NewCompiler(factory));

            Assert.That(transport.Type, Is.EqualTo(BootstrapTestData.TypeA));
            Assert.That(transport.Name, Is.EqualTo("hello"));
            Assert.That(captured, Is.EqualTo("hello"));
        }

        [Test]
        public void Compile_WithFormulaOnlyRegistration_ReportsConstructorNotFound()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.Registry.RegisterTransport(BootstrapTestData.AlphaRegistration());
            var factory = builder.Build();

            var exception = Assert.Throws<BuildException>(
                () => factory.Compile("alpha(1)", NewCompiler(factory)));

            Assert.That(
                exception!.Error.Code,
                Is.EqualTo("E_BLD_CONSTRUCTOR_NOT_FOUND"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Building));
        }

        [Test]
        public void Compile_WithCoreRegistration_MapsNestedListAndStructParameters()
        {
            string? childName = null;
            string? itemName = null;
            string? host = null;
            string? mode = null;

            var optionsSchema = new Core.StructSchema(
                new Core.StructField[]
                {
                    new Core.StringField("Host", required: true),
                    new Core.NestedStructField(
                        "Tls",
                        required: true,
                        new Core.StructSchema(
                            new Core.StructField[]
                            {
                                new Core.StringField("Mode", required: true)
                            }))
                });

            var factory = NewCoreFactory(
                null,
                BootstrapTestData.CoreRegistration(
                    "beta",
                    BootstrapTestData.TypeB,
                    _ => new FakeTransport(BootstrapTestData.TypeB, "beta")),
                BootstrapTestData.CoreRegistration(
                    "combo",
                    BootstrapTestData.TypeA,
                    context =>
                    {
                        if (context.TryGetTransport("child", out var child))
                        {
                            childName = child().Name;
                        }

                        if (context.TryGetTransportList("items", out var items))
                        {
                            itemName = items[0]().Name;
                        }

                        if (context.TryGetStruct("options", out var options))
                        {
                            options.TryGetString("host", out host);
                            if (options.TryGetStruct("tls", out var tls))
                            {
                                tls.TryGetString("mode", out mode);
                            }
                        }

                        return new FakeTransport(BootstrapTestData.TypeA, "combo");
                    },
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.NestedTransportParameter(
                            name: "Child",
                            required: true,
                            positional: true,
                            expected: Core.TransportTypeSet.Any),
                        new Core.TransportListParameter(
                            name: "Items",
                            required: true,
                            positional: false,
                            expected: Core.TransportTypeSet.Any,
                            homogeneous: true,
                            min: 1),
                        new Core.StructParameter(
                            name: "Options",
                            required: true,
                            positional: false,
                            schema: optionsSchema)
                    }));

            var transport = factory.Compile(
                "combo(beta(), items=[beta()], options={host=\"h\", tls={mode=\"m\"}})",
                NewCompiler(factory));

            Assert.That(transport.Name, Is.EqualTo("combo"));
            Assert.That(childName, Is.EqualTo("beta"));
            Assert.That(itemName, Is.EqualTo("beta"));
            Assert.That(host, Is.EqualTo("h"));
            Assert.That(mode, Is.EqualTo("m"));
        }

        [Test]
        public void Resolve_WithCoreExactlyTypeSet_RejectsDifferentNestedType()
        {
            var factory = NewCoreFactory(
                null,
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA)),
                BootstrapTestData.CoreRegistration(
                    "beta",
                    BootstrapTestData.TypeB,
                    _ => new FakeTransport(BootstrapTestData.TypeB)),
                BootstrapTestData.CoreRegistration(
                    "parent",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.NestedTransportParameter(
                            "child",
                            required: true,
                            positional: true,
                            expected: Core.TransportTypeSet.Exactly(
                                BootstrapTestData.TypeA))
                    }));

            var accepted = factory.Resolve("parent(alpha())");
            var rejected = factory.Resolve("parent(beta())");

            Assert.That(accepted.Error, Is.Null);
            Assert.That(rejected.Error, Is.Not.Null);
            Assert.That(
                rejected.Error!.Code,
                Is.EqualTo("E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE"));
            Assert.That(rejected.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        }

        [Test]
        public void Resolve_WithCoreOneOfTypeSet_AcceptsEitherNestedType()
        {
            var factory = NewCoreFactory(
                null,
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA)),
                BootstrapTestData.CoreRegistration(
                    "beta",
                    BootstrapTestData.TypeB,
                    _ => new FakeTransport(BootstrapTestData.TypeB)),
                BootstrapTestData.CoreRegistration(
                    "parent",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.NestedTransportParameter(
                            "child",
                            required: true,
                            positional: true,
                            expected: Core.TransportTypeSet.OneOf(
                                BootstrapTestData.TypeA,
                                BootstrapTestData.TypeB))
                    }));

            Assert.That(factory.Resolve("parent(alpha())").Error, Is.Null);
            Assert.That(factory.Resolve("parent(beta())").Error, Is.Null);
        }

        [Test]
        public void Resolve_WithCoreTransportListRangeAndHomogeneity_EnforcesAll()
        {
            var factory = NewCoreFactory(
                null,
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA)),
                BootstrapTestData.CoreRegistration(
                    "beta",
                    BootstrapTestData.TypeB,
                    _ => new FakeTransport(BootstrapTestData.TypeB)),
                BootstrapTestData.CoreRegistration(
                    "parent",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.TransportListParameter(
                            "items",
                            required: true,
                            positional: false,
                            expected: Core.TransportTypeSet.Any,
                            homogeneous: true,
                            min: 2,
                            max: 2)
                    }));

            var tooFew = factory.Resolve("parent(items=[alpha()])");
            var tooMany = factory.Resolve(
                "parent(items=[alpha(), alpha(), alpha()])");
            var mixed = factory.Resolve("parent(items=[alpha(), beta()])");

            Assert.That(tooFew.Error!.Code, Is.EqualTo("E_SEM_LIST_MIN"));
            Assert.That(tooMany.Error!.Code, Is.EqualTo("E_SEM_LIST_MAX"));
            Assert.That(mixed.Error!.Code, Is.EqualTo("E_SEM_LIST_HOMOGENEITY"));
        }

        [Test]
        public void Compile_WithInjectedConverterGraph_UsesTheGraphForTheCast()
        {
            ITransport? innerTransport = null;
            var graph = new RecordingConvertersGraph(
                inner =>
                {
                    innerTransport = inner();
                    return new FakeTransport(BootstrapTestData.TypeB, "converted");
                });

            var factory = NewCoreFactory(
                graph,
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA, "alpha")));

            var transport = factory.Compile(
                "alpha() as TypeB",
                NewCompiler(factory));

            Assert.That(transport.Type, Is.EqualTo(BootstrapTestData.TypeB));
            Assert.That(transport.Name, Is.EqualTo("converted"));
            Assert.That(innerTransport, Is.Not.Null);
            Assert.That(innerTransport!.Type, Is.EqualTo(BootstrapTestData.TypeA));

            Assert.That(graph.Calls, Has.Count.EqualTo(1));
            Assert.That(graph.Calls[0].FromType, Is.EqualTo(BootstrapTestData.TypeA));
            Assert.That(graph.Calls[0].ToType, Is.EqualTo(BootstrapTestData.TypeB));
            Assert.That(graph.Calls[0].MemoryRental, Is.Not.Null);
            Assert.That(graph.Calls[0].Logger, Is.Not.Null);
        }

        [Test]
        public void Compile_WithDefaultConverterGraph_UsesTheBuiltInConversion()
        {
            var builder = TransportFactoryBootstrap.CreateBuilder();
            builder.Registry.RegisterType(
                new TransportTypeInfo(TransportType.RawUnreliableNoAck, "TypeC"));
            builder.Registry.RegisterType(
                new TransportTypeInfo(BootstrapTestData.TypeA, "TypeA"));
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    TransportType.RawUnreliableNoAck,
                    _ => new FakeRawUnreliableNoAckClient()));

            var factory = builder.Build();
            var compiler = factory.CreateCompiler(
                TransportRole.Server,
                new Logger([]),
                MemoryRental.Shared);
            var transport = factory.Compile("alpha() as TypeA", compiler);

            Assert.That(transport.Type, Is.EqualTo(BootstrapTestData.TypeA));
        }

        [Test]
        public void Compile_WithParseError_DoesNotInvokeTheRuntimeConstructor()
        {
            var calls = 0;
            var factory = NewCoreFactory(
                null,
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ =>
                    {
                        calls++;
                        return new FakeTransport(BootstrapTestData.TypeA);
                    }));

            var exception = Assert.Throws<FactoryException>(
                () => factory.Compile("alpha(", NewCompiler(factory)));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_SYN_UNEXPECTED_TOKEN"));
            Assert.That(calls, Is.Zero);
        }
    }
}
