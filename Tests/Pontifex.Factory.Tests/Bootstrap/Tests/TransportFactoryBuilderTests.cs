using System;
using Actuarius.Memory;
using Pontifex;
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
    public sealed class TransportFactoryBuilderTests
    {
        [Test]
        public void CreateBuilder_FromPublicSeam_ReturnsBuilderWithEmptyRegistry()
        {
            var builder = TransportFactoryBootstrap.CreateBuilder();

            Assert.That(builder, Is.Not.Null);
            Assert.That(builder.Registry, Is.Not.Null);
            Assert.That(builder.Build(), Is.Not.Null);
        }

        [Test]
        public void CreateBuilder_WithInjectedConverterGraph_ReturnsBuilderThatBuilds()
        {
            var builder = TransportFactoryBootstrap.CreateBuilder(
                new RecordingConvertersGraph(null));

            Assert.That(builder.Build(), Is.Not.Null);
        }

        [Test]
        public void CreateBuilder_CalledTwice_ReturnsIndependentBuilders()
        {
            var first = BootstrapTestData.NewBuilder();
            var second = TransportFactoryBootstrap.CreateBuilder();

            first.Build();

            // The second builder never registered the types, so its factory must
            // not see them; this proves there is no hidden static registry.
            var result = second.Build().Resolve("alpha()");

            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SEM_UNKNOWN_SCHEME"));
        }

        [Test]
        public void Build_WithRegisteredTransports_ReturnsWorkingFactory()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.Registry.RegisterTransport(BootstrapTestData.AlphaRegistration());

            var result = builder.Build().Resolve("alpha(1)");

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Formula, Is.Not.Null);
        }

        [Test]
        public void Build_CalledTwice_ReturnsTheSameFactory()
        {
            var builder = BootstrapTestData.NewBuilder();

            var first = builder.Build();
            var second = builder.Build();

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void Build_SealsTheRegistry_SoLaterRegistrationThrows()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.Build();

            var exception = Assert.Throws<RegistrationException>(
                () => builder.Registry.RegisterType(
                    new TransportTypeInfo(BootstrapTestData.TypeA, "TypeX")));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_REG_BUILDER_SEALED"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Registration));
        }

        [Test]
        public void Build_WithoutRegistrations_ReturnsFactoryThatReportsUnknownScheme()
        {
            var factory = TransportFactoryBootstrap.CreateBuilder().Build();

            var result = factory.Resolve("alpha()");

            Assert.That(result.Formula, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SEM_UNKNOWN_SCHEME"));
            Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_RegistersFormulaMetadataAndRuntimeConstructor()
        {
            var builder = BootstrapTestData.NewBuilder();
            var transport = new FakeTransport(
                BootstrapTestData.TypeA,
                "core-alpha");
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "Alpha",
                    BootstrapTestData.TypeA,
                    _ => transport));

            var factory = builder.Build();
            var result = factory.Resolve("alpha()");

            Assert.That(result.Error, Is.Null);
            var resolved = result.Formula!.Root as ResolvedTransport;
            Assert.That(resolved, Is.Not.Null);
            Assert.That(resolved!.Constructor.Name, Is.EqualTo("alpha"));
            Assert.That(resolved.Type, Is.EqualTo(BootstrapTestData.TypeA));

            var compiler = factory.CreateCompiler(
                TransportRole.Server,
                VoidLogger.Instance,
                MemoryRental.Shared);
            Assert.That(factory.Compile("alpha()", compiler), Is.SameAs(transport));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_NormalizesTheScheme()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "ALPHA",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA)));

            var factory = builder.Build();
            var result = factory.Resolve("Alpha()");

            Assert.That(result.Error, Is.Null);
            var resolved = (ResolvedTransport)result.Formula!.Root;
            Assert.That(resolved.Constructor.Name, Is.EqualTo("alpha"));
            Assert.That(factory.Canonicalize("ALPHA()"), Is.EqualTo("alpha()"));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_PreservesAliases()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    aliases: new[] { "A", "old-alpha" }));

            var factory = builder.Build();
            var result = factory.Resolve("a()");

            Assert.That(result.Error, Is.Null);
            Assert.That(
                ((ResolvedTransport)result.Formula!.Root).Constructor.Name,
                Is.EqualTo("alpha"));
            Assert.That(factory.Canonicalize("OLD-ALPHA()"), Is.EqualTo("alpha()"));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_MapsStringParameterMetadata()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.StringParameter("Value", required: true, positional: true)
                    }));

            var factory = builder.Build();

            var missing = factory.Resolve("alpha()");
            Assert.That(missing.Error, Is.Not.Null);
            Assert.That(missing.Error!.Code, Is.EqualTo("E_SEM_MISSING_REQUIRED_PARAM"));
            Assert.That(missing.Error.Phase, Is.EqualTo(FactoryPhase.Resolution));

            var provided = factory.Resolve("alpha(1)");
            Assert.That(provided.Error, Is.Null);
            Assert.That(provided.Formula, Is.Not.Null);
        }

        [Test]
        public void RegisterTransport_WithNullRegistration_ThrowsArgumentNullException()
        {
            var builder = BootstrapTestData.NewBuilder();

            Assert.Throws<ArgumentNullException>(
                () => builder.RegisterTransport(null!));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_MapsIntegerAndDecimalParameterMetadata()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        BootstrapTestData.CoreIntegerParameter(
                            "Count",
                            -5,
                            5,
                            required: true,
                            positional: true),
                        BootstrapTestData.CoreDecimalParameter(
                            "Ratio",
                            -1.5m,
                            1.5m,
                            positional: true)
                    }));

            var factory = builder.Build();
            var result = factory.Resolve("alpha(3, 1.25)");

            Assert.That(result.Error, Is.Null);
            var resolved = (ResolvedTransport)result.Formula!.Root;
            Assert.That(
                ((IntegerValue)resolved.Provided[0].Value).Value,
                Is.EqualTo(3L));
            Assert.That(
                ((DecimalValue)resolved.Provided[1].Value).Value,
                Is.EqualTo(1.25m));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_MapsIntegerAndDecimalFieldMetadata()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.StructParameter(
                            "config",
                            required: true,
                            positional: true,
                            new Core.StructSchema(new Core.StructField[]
                            {
                                BootstrapTestData.CoreIntegerField(
                                    "Count",
                                    0,
                                    10,
                                    required: true),
                                BootstrapTestData.CoreDecimalField(
                                    "Ratio",
                                    -1.5m,
                                    1.5m)
                            }))
                    }));

            var factory = builder.Build();
            var result = factory.Resolve("alpha({count=3, ratio=1.25})");

            Assert.That(result.Error, Is.Null);
            var resolved = (ResolvedTransport)result.Formula!.Root;
            var config = (StructValue)resolved.Provided[0].Value;
            Assert.That(((IntegerValue)config.Fields[0].Value).Value, Is.EqualTo(3L));
            Assert.That(((DecimalValue)config.Fields[1].Value).Value, Is.EqualTo(1.25m));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_MapsArrayFieldMetadata()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.StructParameter(
                            "config",
                            required: true,
                            positional: true,
                            new Core.StructSchema(new Core.StructField[]
                            {
                                BootstrapTestData.CoreArrayField(
                                    "Names",
                                    new Core.StringType(value => null),
                                    0,
                                    2),
                                BootstrapTestData.CoreArrayField(
                                    "Counts",
                                    new Core.IntegerType(-1, 1),
                                    1,
                                    3,
                                    required: true),
                                BootstrapTestData.CoreArrayField(
                                    "Ratios",
                                    new Core.DecimalType(-1.5m, 1.5m))
                            }))
                    }));

            var factory = builder.Build();
            var result = factory.Resolve(
                "alpha({names=[a, b], counts=[-1, 1], ratios=[1.50]})");

            Assert.That(result.Error, Is.Null);
            var resolved = (ResolvedTransport)result.Formula!.Root;
            var config = (StructValue)resolved.Provided[0].Value;

            var names = (ArrayValue)config.Fields[0].Value;
            Assert.That(((StringValue)names.Elements[0]).Text, Is.EqualTo("a"));
            var counts = (ArrayValue)config.Fields[1].Value;
            Assert.That(((IntegerValue)counts.Elements[0]).Value, Is.EqualTo(-1L));
            var ratios = (ArrayValue)config.Fields[2].Value;
            Assert.That(((DecimalValue)ratios.Elements[0]).Value, Is.EqualTo(1.5m));
        }

        [Test]
        public void RegisterTransport_WithUnsortedCoreArrayRange_ThrowsInvalidArrayRange()
        {
            var builder = BootstrapTestData.NewBuilder();

            var exception = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "alpha",
                        BootstrapTestData.TypeA,
                        _ => new FakeTransport(BootstrapTestData.TypeA),
                        parameters: new Core.TransportParameter[]
                        {
                            new Core.StructParameter(
                                "config",
                                required: true,
                                positional: true,
                                new Core.StructSchema(new Core.StructField[]
                                {
                                    BootstrapTestData.CoreArrayField(
                                        "values",
                                        new Core.IntegerType(0, 1),
                                        min: -1)
                                }))
                        })));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_REG_INVALID_ARRAY_RANGE"));
            Assert.That(
                exception.Error.Message,
                Is.EqualTo("invalid array range: min -1, max unbounded"));
        }

        [Test]
        public void RegisterTransport_WithUnsortedCoreArrayElementRange_ThrowsInvalidIntegerRange()
        {
            var builder = BootstrapTestData.NewBuilder();

            var integerException = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "alpha",
                        BootstrapTestData.TypeA,
                        _ => new FakeTransport(BootstrapTestData.TypeA),
                        parameters: new Core.TransportParameter[]
                        {
                            new Core.StructParameter(
                                "config",
                                required: true,
                                positional: true,
                                new Core.StructSchema(new Core.StructField[]
                                {
                                    BootstrapTestData.CoreArrayField(
                                        "values",
                                        new Core.IntegerType(5, 1))
                                }))
                        })));

            Assert.That(integerException!.Error.Code, Is.EqualTo("E_REG_INVALID_INTEGER_RANGE"));
            Assert.That(
                integerException.Error.Message,
                Is.EqualTo("invalid integer range: min 5, max 1"));
        }

        [Test]
        public void RegisterTransport_WithUnsortedCoreDecimalArrayElementRange_ThrowsInvalidDecimalRange()
        {
            var builder = BootstrapTestData.NewBuilder();

            var exception = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "alpha",
                        BootstrapTestData.TypeA,
                        _ => new FakeTransport(BootstrapTestData.TypeA),
                        parameters: new Core.TransportParameter[]
                        {
                            new Core.StructParameter(
                                "config",
                                required: true,
                                positional: true,
                                new Core.StructSchema(new Core.StructField[]
                                {
                                    BootstrapTestData.CoreArrayField(
                                        "values",
                                        new Core.DecimalType(2.5m, 1.5m))
                                }))
                        })));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_REG_INVALID_DECIMAL_RANGE"));
            Assert.That(
                exception.Error.Message,
                Is.EqualTo("invalid decimal range: min 2.5, max 1.5"));
        }

        [Test]
        public void RegisterTransport_WithCoreRegistration_PreservesArrayStringValidator()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA),
                    parameters: new Core.TransportParameter[]
                    {
                        new Core.StructParameter(
                            "config",
                            required: true,
                            positional: true,
                            new Core.StructSchema(new Core.StructField[]
                            {
                                BootstrapTestData.CoreArrayField(
                                    "names",
                                    new Core.StringType(
                                        value => value == "bad" ? "name must not be 'bad'" : null))
                            }))
                    }));

            var factory = builder.Build();
            var result = factory.Resolve("alpha({names=[bad]})");

            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo("E_SEM_VALIDATOR_FAILED"));
            Assert.That(
                result.Error.Message,
                Is.EqualTo("field 'names[0]' of parameter 'config' is invalid: name must not be 'bad'"));
        }

        [Test]
        public void RegisterTransport_WithUnsortedCoreIntegerRange_ThrowsInvalidIntegerRange()
        {
            var builder = BootstrapTestData.NewBuilder();

            var exception = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "alpha",
                        BootstrapTestData.TypeA,
                        _ => new FakeTransport(BootstrapTestData.TypeA),
                        parameters: new Core.TransportParameter[]
                        {
                            BootstrapTestData.CoreIntegerParameter("count", 5, 1)
                        })));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_REG_INVALID_INTEGER_RANGE"));
            Assert.That(
                exception.Error.Message,
                Is.EqualTo("invalid integer range: min 5, max 1"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Registration));
        }

        [Test]
        public void RegisterTransport_WithUnsortedCoreDecimalFieldRange_ThrowsInvalidDecimalRange()
        {
            var builder = BootstrapTestData.NewBuilder();

            var exception = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "alpha",
                        BootstrapTestData.TypeA,
                        _ => new FakeTransport(BootstrapTestData.TypeA),
                        parameters: new Core.TransportParameter[]
                        {
                            new Core.StructParameter(
                                "config",
                                required: true,
                                positional: true,
                                new Core.StructSchema(new Core.StructField[]
                                {
                                    BootstrapTestData.CoreDecimalField("ratio", 2.5m, 1.5m)
                                }))
                        })));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_REG_INVALID_DECIMAL_RANGE"));
            Assert.That(
                exception.Error.Message,
                Is.EqualTo("invalid decimal range: min 2.5, max 1.5"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Registration));
        }

        [Test]
        public void RegisterTransport_WithDuplicateCanonicalScheme_ThrowsRegistrationException()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA)));

            var exception = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "ALPHA",
                        BootstrapTestData.TypeB,
                        _ => new FakeTransport(BootstrapTestData.TypeB))));

            Assert.That(
                exception!.Error.Code,
                Is.EqualTo("E_REG_DUPLICATE_CANONICAL_NAME"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Registration));
        }

        [Test]
        public void Build_SealsTheRuntimeRegistry_SoLaterCoreRegistrationThrows()
        {
            var builder = BootstrapTestData.NewBuilder();
            builder.RegisterTransport(
                BootstrapTestData.CoreRegistration(
                    "alpha",
                    BootstrapTestData.TypeA,
                    _ => new FakeTransport(BootstrapTestData.TypeA)));
            builder.Build();

            var exception = Assert.Throws<RegistrationException>(
                () => builder.RegisterTransport(
                    BootstrapTestData.CoreRegistration(
                        "beta",
                        BootstrapTestData.TypeB,
                        _ => new FakeTransport(BootstrapTestData.TypeB))));

            Assert.That(exception!.Error.Code, Is.EqualTo("E_REG_BUILDER_SEALED"));
            Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Registration));
        }
    }
}
