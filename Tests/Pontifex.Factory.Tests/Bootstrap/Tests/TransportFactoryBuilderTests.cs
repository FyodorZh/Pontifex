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
