using System.Linq;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Tests.Semantics.Harness;

namespace Pontifex.Factory.Tests.Semantics;

[TestFixture]
[Category("Factory.Semantics")]
public sealed class ArgumentBinderTests
{
    [Test]
    public void Bind_ForPositionalArguments_FillsPositionalCapableParametersInDeclarationOrder()
    {
        var constructor = Alpha();
        var bound = SemanticsAssertions.Bind("alpha(beta(x), fast)", constructor);

        Assert.That(bound.Select(b => b.Param.Name), Is.EqualTo(new[] { "input", "mode" }));
        Assert.That(bound.Select(b => b.Param), Is.EqualTo(new[]
        {
            SemanticsTestData.FindParam(constructor, "input"),
            SemanticsTestData.FindParam(constructor, "mode")
        }));
    }

    [Test]
    public void Bind_ForNamedArguments_BindsByNormalizedName()
    {
        var bound = SemanticsAssertions.Bind(
            "alpha(beta(x), MODE=fast, timeout=5s)",
            Alpha());

        Assert.That(
            bound.Select(b => b.Param.Name),
            Is.EqualTo(new[] { "input", "mode", "timeout" }));
    }

    [Test]
    public void Bind_ForPositionalPrefixThenNamedSuffix_ProducesDeclarationOrder()
    {
        var bound = SemanticsAssertions.Bind(
            "alpha(beta(x), fast, timeout=5s)",
            Alpha());

        Assert.That(
            bound.Select(b => b.Param.Name),
            Is.EqualTo(new[] { "input", "mode", "timeout" }));
    }

    [Test]
    public void Bind_ExcludesNamedOnlyParametersFromPositionalSlots()
    {
        var bound = SemanticsAssertions.Bind("mixed(beta(x))", Mixed());

        Assert.That(bound.Select(b => b.Param.Name), Is.EqualTo(new[] { "input" }));
        Assert.That(bound.Any(b => b.Param.Name == "flag"), Is.False);
    }

    [Test]
    public void Bind_ForNamedOnlyParameter_IncludesIt()
    {
        var bound = SemanticsAssertions.Bind("alpha(beta(x), timeout=5s)", Alpha());

        Assert.That(bound.Select(b => b.Param.Name), Is.EqualTo(new[] { "input", "timeout" }));
    }

    [Test]
    public void Bind_ForRepeatedNamedArgument_ReportsDuplicateAtSecondName()
    {
        SemanticsAssertions.BindError(
            "alpha(beta(x), timeout=a, timeout=b)",
            Alpha(),
            "E_SEM_DUPLICATE_PARAM",
            26,
            "parameter 'timeout' is specified more than once");
    }

    [Test]
    public void Bind_ForEmptyArgumentsAndNoRequiredParameters_ReturnsEmptyBindings()
    {
        var bound = SemanticsAssertions.Bind("empty()", Empty());

        Assert.That(bound, Is.Empty);
    }

    [Test]
    public void Bind_ChecksNamedArgumentsInSourceOrder()
    {
        SemanticsAssertions.BindError(
            "alpha(beta(x), q=1, input=beta(y))",
            Alpha(),
            "E_SEM_UNKNOWN_PARAM",
            15,
            "unknown parameter 'q' for scheme 'alpha'; one of: 'input', 'mode', 'timeout'");
    }

    [Test]
    public void Bind_ChecksTooManyPositionalArgumentsBeforeNamedArguments()
    {
        SemanticsAssertions.BindError(
            "alpha(beta(x), fast, extra, q=1)",
            Alpha(),
            "E_SEM_TOO_MANY_POSITIONAL",
            21,
            "too many positional arguments for scheme 'alpha'; expected at most 2");
    }

    [Test]
    public void Bind_ForMultipleMissingRequiredParameters_ReportsFirstInDeclarationOrder()
    {
        var constructor = SemanticsTestData.Constructor(
            "gamma",
            SemanticsTestData.TypeA,
            SemanticsTestData.CreateStringParam("zeta", required: true),
            SemanticsTestData.CreateStringParam("alpha", required: true));

        SemanticsAssertions.BindError(
            "gamma()",
            constructor,
            "E_SEM_MISSING_REQUIRED_PARAM",
            6,
            "missing required parameter 'zeta' for scheme 'gamma'");
    }

    [Test]
    public void Bind_ForUnknownNamedArgument_ReportsParametersInDeclarationOrder()
    {
        var constructor = SemanticsTestData.Constructor(
            "gamma",
            SemanticsTestData.TypeA,
            SemanticsTestData.CreateStringParam("zeta"),
            SemanticsTestData.CreateStringParam("alpha"));

        SemanticsAssertions.BindError(
            "gamma(q=1)",
            constructor,
            "E_SEM_UNKNOWN_PARAM",
            6,
            "unknown parameter 'q' for scheme 'gamma'; one of: 'zeta', 'alpha'");
    }

    [Test]
    public void Bind_ForDuplicateWithUppercaseName_PreservesRawSpelling()
    {
        SemanticsAssertions.BindError(
            "alpha(beta(x), INPUT=beta(y))",
            Alpha(),
            "E_SEM_DUPLICATE_PARAM",
            15,
            "parameter 'INPUT' is specified more than once");
    }

    private static TransportConstructorInfo Alpha()
    {
        return SemanticsTestData.Canonical("alpha");
    }

    private static TransportConstructorInfo Mixed()
    {
        return SemanticsTestData.Canonical("mixed");
    }

    private static TransportConstructorInfo Empty()
    {
        return SemanticsTestData.Canonical("empty");
    }
}
