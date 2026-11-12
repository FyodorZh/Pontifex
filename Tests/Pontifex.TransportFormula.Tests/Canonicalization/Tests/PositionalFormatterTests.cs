using NUnit.Framework;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Tests.Canonicalization.Harness;

namespace Pontifex.TransportFormula.Tests.Canonicalization;

[TestFixture]
[Category("TransportFormula.Canonicalization")]
public sealed class PositionalFormatterTests
{
    [Test]
    public void Arrange_WhenAllPositionalParametersAreProvided_EmitsThemPositionally()
    {
        var constructor = Alpha();
        var input = Param(constructor, "input");
        var mode = Param(constructor, "mode");
        var timeout = Param(constructor, "timeout");

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[]
            {
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("i")),
                CanonicalizationTestData.Bind(mode, CanonicalizationTestData.Scalar("m")),
                CanonicalizationTestData.Bind(timeout, CanonicalizationTestData.Scalar("t"))
            });

        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries[0].Binding.Param.Name, Is.EqualTo("input"));
        Assert.That(entries[0].IsNamed, Is.False);
        Assert.That(entries[1].Binding.Param.Name, Is.EqualTo("mode"));
        Assert.That(entries[1].IsNamed, Is.False);
        Assert.That(entries[2].Binding.Param.Name, Is.EqualTo("timeout"));
        Assert.That(entries[2].IsNamed, Is.True);
    }

    [Test]
    public void Arrange_WhenAnOptionalPositionalIsAbsent_EmitsLaterPositionalsByName()
    {
        var constructor = Alpha();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[]
            {
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("i")),
                CanonicalizationTestData.Bind(timeout, CanonicalizationTestData.Scalar("t"))
            });

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].Binding.Param.Name, Is.EqualTo("input"));
        Assert.That(entries[0].IsNamed, Is.False);
        Assert.That(entries[1].Binding.Param.Name, Is.EqualTo("timeout"));
        Assert.That(entries[1].IsNamed, Is.True);
    }

    [Test]
    public void Arrange_WhenTheFirstPositionalIsAbsent_EmitsALaterPositionalByName()
    {
        var constructor = Opt();
        var b = Param(constructor, "b");

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[] { CanonicalizationTestData.Bind(b, CanonicalizationTestData.Scalar("two")) });

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Binding.Param.Name, Is.EqualTo("b"));
        Assert.That(entries[0].IsNamed, Is.True);
    }

    [Test]
    public void Arrange_WhenBothOptionalPositionalsAreProvided_EmitsBothPositionally()
    {
        var constructor = Opt();
        var a = Param(constructor, "a");
        var b = Param(constructor, "b");

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[]
            {
                CanonicalizationTestData.Bind(a, CanonicalizationTestData.Scalar("one")),
                CanonicalizationTestData.Bind(b, CanonicalizationTestData.Scalar("two"))
            });

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].Binding.Param.Name, Is.EqualTo("a"));
        Assert.That(entries[0].IsNamed, Is.False);
        Assert.That(entries[1].Binding.Param.Name, Is.EqualTo("b"));
        Assert.That(entries[1].IsNamed, Is.False);
    }

    [Test]
    public void Arrange_WhenANamedOnlyParameterPrecedesAPositionalParameter_EmitsPositionalFirst()
    {
        var constructor = Mixed();
        var flag = Param(constructor, "flag");
        var input = Param(constructor, "input");

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[]
            {
                CanonicalizationTestData.Bind(flag, CanonicalizationTestData.Scalar("fast")),
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("i"))
            });

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].Binding.Param.Name, Is.EqualTo("input"));
        Assert.That(entries[0].IsNamed, Is.False);
        Assert.That(entries[1].Binding.Param.Name, Is.EqualTo("flag"));
        Assert.That(entries[1].IsNamed, Is.True);
    }

    [Test]
    public void Arrange_WhenOnlyANamedOnlyParameterIsProvided_EmitsItNamed()
    {
        var constructor = Mixed();
        var flag = Param(constructor, "flag");

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[] { CanonicalizationTestData.Bind(flag, CanonicalizationTestData.Scalar("fast")) });

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Binding.Param.Name, Is.EqualTo("flag"));
        Assert.That(entries[0].IsNamed, Is.True);
    }

    [Test]
    public void Arrange_WhenNothingIsProvided_ReturnsNoEntries()
    {
        var entries = PositionalFormatter.Arrange(
            Alpha(),
            Array.Empty<ParamBinding>());

        Assert.That(entries, Is.Empty);
    }

    [Test]
    public void Arrange_WhenBindingUsesAStructurallyEqualClone_DoesNotTreatItAsProvided()
    {
        var constructor = Opt();
        var clone = CanonicalizationTestData.StringParam("a", positional: true);

        var entries = PositionalFormatter.Arrange(
            constructor,
            new[] { CanonicalizationTestData.Bind(clone, CanonicalizationTestData.Scalar("one")) });

        Assert.That(entries, Is.Empty);
    }

    private static TransportConstructorInfo Alpha()
    {
        return CanonicalizationTestData.Constructor(
            "alpha",
            CanonicalizationTestData.TypeA,
            CanonicalizationTestData.TransportParam(
                "input",
                CanonicalizationTestData.AnySet(),
                required: true,
                positional: true),
            CanonicalizationTestData.StringParam("mode", positional: true),
            CanonicalizationTestData.StringParam("timeout"));
    }

    private static TransportConstructorInfo Opt()
    {
        return CanonicalizationTestData.Constructor(
            "opt",
            CanonicalizationTestData.TypeA,
            CanonicalizationTestData.StringParam("a", positional: true),
            CanonicalizationTestData.StringParam("b", positional: true));
    }

    private static TransportConstructorInfo Mixed()
    {
        return CanonicalizationTestData.Constructor(
            "mixed",
            CanonicalizationTestData.TypeA,
            CanonicalizationTestData.StringParam("flag"),
            CanonicalizationTestData.TransportParam(
                "input",
                CanonicalizationTestData.AnySet(),
                required: true,
                positional: true));
    }

    private static Param Param(
        TransportConstructorInfo constructor,
        string name)
    {
        return CanonicalizationTestData.FindParam(constructor, name);
    }
}
