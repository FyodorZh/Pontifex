using System;
using Pontifex.Factory.Tests.Compilation.Harness;

namespace Pontifex.Factory.Tests.Compilation;

[TestFixture]
[Category("Factory.Compilation")]
public sealed class TransportConstructorRegistryTests
{
    [Test]
    public void Register_WithNewKey_ReturnsSameRegistrationFromFind()
    {
        var registry = new TransportConstructorRegistry();
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);

        registry.Register(registration);

        Assert.That(
            registry.Find("alpha", CompilationTestData.TypeA),
            Is.SameAs(registration));
    }

    [Test]
    public void Register_WithUppercaseScheme_IsFoundByAnyCase()
    {
        var registry = new TransportConstructorRegistry();
        var registration = new RecordingRegistration("Alpha", CompilationTestData.TypeA);

        registry.Register(registration);

        Assert.That(
            registry.Find("alpha", CompilationTestData.TypeA),
            Is.SameAs(registration));
        Assert.That(
            registry.Find("ALPHA", CompilationTestData.TypeA),
            Is.SameAs(registration));
    }

    [Test]
    public void Register_WithSameSchemeDifferentType_KeepsBothEntries()
    {
        var registry = new TransportConstructorRegistry();
        var reliable = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var unreliable = new RecordingRegistration("alpha", CompilationTestData.TypeC);

        registry.Register(reliable);
        registry.Register(unreliable);

        Assert.That(
            registry.Find("alpha", CompilationTestData.TypeA),
            Is.SameAs(reliable));
        Assert.That(
            registry.Find("alpha", CompilationTestData.TypeC),
            Is.SameAs(unreliable));
    }

    [Test]
    public void Register_WithDuplicateKey_ThrowsArgumentException()
    {
        var registry = new TransportConstructorRegistry();
        registry.Register(new RecordingRegistration("alpha", CompilationTestData.TypeA));

        Assert.Throws<ArgumentException>(
            () => registry.Register(
                new RecordingRegistration("alpha", CompilationTestData.TypeA)));
    }

    [Test]
    public void Register_WithDuplicateKeyDifferentCase_ThrowsArgumentException()
    {
        var registry = new TransportConstructorRegistry();
        registry.Register(new RecordingRegistration("Alpha", CompilationTestData.TypeA));

        Assert.Throws<ArgumentException>(
            () => registry.Register(
                new RecordingRegistration("alpha", CompilationTestData.TypeA)));
    }

    [Test]
    public void Register_WithNullRegistration_ThrowsArgumentNullException()
    {
        var registry = new TransportConstructorRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Test]
    public void Register_AfterSeal_ThrowsInvalidOperationException()
    {
        var registry = new TransportConstructorRegistry();
        registry.Seal();

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(
                new RecordingRegistration("alpha", CompilationTestData.TypeA)));
    }

    [Test]
    public void Find_AfterSeal_StillReturnsRegistration()
    {
        var registry = new TransportConstructorRegistry();
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        registry.Register(registration);
        registry.Seal();

        Assert.That(
            registry.Find("alpha", CompilationTestData.TypeA),
            Is.SameAs(registration));
    }

    [Test]
    public void Find_WithUnknownKey_ReturnsNull()
    {
        var registry = new TransportConstructorRegistry();
        registry.Register(new RecordingRegistration("alpha", CompilationTestData.TypeA));

        Assert.That(
            registry.Find("beta", CompilationTestData.TypeA),
            Is.Null);
        Assert.That(
            registry.Find("alpha", CompilationTestData.TypeB),
            Is.Null);
    }
}
