using System.Linq;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Semantics;
using Pontifex.Factory.Tests.Semantics.Harness;

namespace Pontifex.Factory.Tests.Semantics;

[TestFixture]
[Category("Factory.Semantics")]
public sealed class ParameterValidatorTests
{
    [Test]
    public void Validate_ForStringParameterWithValidText_ProducesScalarValue()
    {
        var constructor = Constructor("validated");
        var provided = Validate("validated(ok)", constructor);

        Assert.That(provided, Has.Count.EqualTo(1));
        Assert.That(provided[0].Param, Is.SameAs(SemanticsTestData.FindParam(constructor, "value")));
        Assert.That(provided[0].Value, Is.InstanceOf<ScalarValue>());
        Assert.That(((ScalarValue)provided[0].Value).Text, Is.EqualTo("ok"));
    }

    [Test]
    public void Validate_ForStringParameterWithNullValidator_AcceptsAnyText()
    {
        var provided = Validate("beta(\"\")", Constructor("beta"));

        Assert.That(((ScalarValue)provided[0].Value).Text, Is.Empty);
    }

    [Test]
    public void Validate_ForStringParameterReceivingTransport_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "beta(alpha(beta(x)))",
            Constructor("beta"),
            "E_SEM_EXPECTED_SCALAR",
            "parameter 'value' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForStringParameterReceivingStruct_ReportsExpectedScalarWithStructShape()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "beta({a=1})",
            Constructor("beta"),
            "E_SEM_EXPECTED_SCALAR",
            "parameter 'value' expects a scalar value, got struct");
    }

    [Test]
    public void Validate_ForTransportParameterReceivingList_ReportsExpectedTransport()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "alpha([beta(x)])",
            Constructor("alpha"),
            "E_SEM_EXPECTED_TRANSPORT",
            "parameter 'input' expects a transport, got transport list");
    }

    [Test]
    public void Validate_ForTransportParameterReceivingStruct_ReportsExpectedTransport()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "alpha({a=1})",
            Constructor("alpha"),
            "E_SEM_EXPECTED_TRANSPORT",
            "parameter 'input' expects a transport, got struct");
    }

    [Test]
    public void Validate_ForListParameterReceivingScalar_ReportsExpectedTransportList()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma(1)",
            Constructor("gamma"),
            "E_SEM_EXPECTED_TRANSPORT_LIST",
            "parameter 'items' expects a transport list, got scalar");
    }

    [Test]
    public void Validate_ForStructParameterReceivingList_ReportsExpectedStruct()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "structy([beta(x)])",
            Constructor("structy"),
            "E_SEM_EXPECTED_STRUCT",
            "parameter 'config' expects a struct, got transport list");
    }

    [Test]
    public void Validate_ForListParameterReceivingStruct_ReportsExpectedTransportList()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma({a=1})",
            Constructor("gamma"),
            "E_SEM_EXPECTED_TRANSPORT_LIST",
            "parameter 'items' expects a transport list, got struct");
    }

    [Test]
    public void Validate_ForStructParameterReceivingTransport_ReportsExpectedStruct()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "structy(beta(x))",
            Constructor("structy"),
            "E_SEM_EXPECTED_STRUCT",
            "parameter 'config' expects a struct, got transport");
    }

    [Test]
    public void Validate_ForListBelowMinimum_ReportsMinimumAndCount()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([])",
            Constructor("gamma"),
            "E_SEM_LIST_MIN",
            "transport list for 'items' requires at least 2 element(s), got 0");
    }

    [Test]
    public void Validate_ForListWithUnboundedMaximum_AcceptsLargeList()
    {
        var provided = Validate(
            "bag([beta(x), beta(x), beta(x), beta(x), beta(x)])",
            Constructor("bag"));

        Assert.That(
            ((TransportListValue)provided[0].Value).Elements,
            Has.Count.EqualTo(5));
    }

    [Test]
    public void Validate_ForListElementsWithExpectedSet_ResolvesElementAgainstParameterExpectedType()
    {
        var provided = Validate("listB([alpha(x)])", Constructor("listB"));
        var element = (ResolvedExpr)((TransportListValue)provided[0].Value).Elements[0];

        Assert.That(((ResolvedTransport)element).Constructor.Name, Is.EqualTo("beta"));
        Assert.That(element.Type, Is.EqualTo(SemanticsTestData.TypeB));
    }

    [Test]
    public void Validate_ForHomogeneousListWithThreeTypes_ReportsAllDistinctTypesInElementOrder()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([alpha(beta(x)), beta(y), gamma([alpha(beta(x)), alpha(beta(x))])])",
            Constructor("gamma"),
            "E_SEM_LIST_HOMOGENEITY",
            "transport list for 'items' must contain one transport type; found TypeA, TypeB, TypeC");
    }

    [Test]
    public void Validate_ChecksListArityBeforeResolvingElements()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([alpha(x)])",
            Constructor("gamma"),
            "E_SEM_LIST_MIN",
            "transport list for 'items' requires at least 2 element(s), got 1");
    }

    [Test]
    public void Validate_ChecksListMaximumBeforeResolvingElements()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([alpha(x), alpha(x), alpha(x), alpha(x)])",
            Constructor("gamma"),
            "E_SEM_LIST_MAX",
            "transport list for 'items' requires at most 3 element(s), got 4");
    }

    [Test]
    public void Validate_ResolvesListElementsInOrderBeforeCheckingHomogeneity()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([alpha(x), beta(y)])",
            Constructor("gamma"),
            "E_SEM_EXPECTED_TRANSPORT",
            "parameter 'input' expects a transport, got scalar");
    }

    [Test]
    public void Validate_VisitsBoundParametersInDeclarationOrder()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "alpha(1, [beta(x)])",
            Constructor("alpha"),
            "E_SEM_EXPECTED_TRANSPORT",
            "parameter 'input' expects a transport, got scalar");
    }

    [Test]
    public void Validate_DelegatesStructParametersToStructValidation()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "pair({x=1})",
            Constructor("pair"),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'x' in struct for parameter 'config'; one of: 'a', 'b'");
    }

    [Test]
    public void Validate_ForStructParameterWithValidFields_ProducesStructValue()
    {
        var provided = Validate("structy({name=x})", Constructor("structy"));

        Assert.That(provided[0].Value, Is.InstanceOf<StructValue>());
        Assert.That(((StructValue)provided[0].Value).Fields, Has.Count.EqualTo(1));
    }

    [Test]
    public void Validate_ForTransportParameter_ProducesResolvedTransportValue()
    {
        var provided = Validate("alpha(beta(x))", Constructor("alpha"));

        Assert.That(provided, Has.Count.EqualTo(1));
        Assert.That(provided[0].Value, Is.InstanceOf<TransportValue>());
        Assert.That(((TransportValue)provided[0].Value).Expr, Is.InstanceOf<ResolvedTransport>());
    }

    [Test]
    public void Validate_SkipsUnboundOptionalParameters()
    {
        var provided = Validate("alpha(beta(x))", Constructor("alpha"));

        Assert.That(provided.Select(p => p.Param.Name), Is.EqualTo(new[] { "input" }));
    }

    private static IRegistry Registry()
    {
        return SemanticsTestData.StandardRegistry();
    }

    private static TransportConstructorInfo Constructor(string name)
    {
        return SemanticsTestData.Canonical(name);
    }

    private static System.Collections.Generic.IReadOnlyList<ParamBinding> Validate(
        string source,
        TransportConstructorInfo constructor)
    {
        return SemanticsAssertions.ValidateParams(Registry(), source, constructor);
    }
}
