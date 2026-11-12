using System;
using Pontifex;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;
using Pontifex.Factory.Tests.Bootstrap.Harness;

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
    }
}
