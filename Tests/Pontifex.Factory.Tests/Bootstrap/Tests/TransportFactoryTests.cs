using System;
using Pontifex.Factory.Semantics;
using Pontifex.Factory.Tests.Bootstrap.Harness;

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
    }
}
