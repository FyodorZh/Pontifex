using System.Linq;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.Factory.Tests.Semantics.Harness;

namespace Pontifex.Factory.Tests.Semantics;

[TestFixture]
[Category("Factory.Semantics")]
public sealed class ParameterValidatorTests
{
    [Test]
    public void Validate_ForStringParameterWithValidText_ProducesStringValue()
    {
        var constructor = Constructor("validated");
        var provided = Validate("validated(ok)", constructor);

        Assert.That(provided, Has.Count.EqualTo(1));
        Assert.That(provided[0].Param, Is.SameAs(SemanticsTestData.FindParam(constructor, "value")));
        Assert.That(provided[0].Value, Is.InstanceOf<StringValue>());
        Assert.That(((StringValue)provided[0].Value).Text, Is.EqualTo("ok"));
    }

    [Test]
    public void Validate_ForStringParameterWithNullValidator_AcceptsAnyText()
    {
        var provided = Validate("beta(\"\")", Constructor("beta"));

        Assert.That(((StringValue)provided[0].Value).Text, Is.Empty);
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
            "parameter 'input' expects a transport, got list");
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
            "parameter 'config' expects a struct, got list");
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
    public void Validate_ForListWithScalarElement_ReportsListElementType()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([alpha(beta(x)), 1])",
            Constructor("gamma"),
            "E_SEM_LIST_ELEMENT_TYPE",
            "transport list for 'items' expects transport elements, got scalar");
    }

    [Test]
    public void Validate_ForListWithStructElement_ReportsListElementType()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "gamma([alpha(beta(x)), {}])",
            Constructor("gamma"),
            "E_SEM_LIST_ELEMENT_TYPE",
            "transport list for 'items' expects transport elements, got struct");
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
    public void Validate_ForTransportParameter_ProducesResolvedExpressionValue()
    {
        var provided = Validate("alpha(beta(x))", Constructor("alpha"));

        Assert.That(provided, Has.Count.EqualTo(1));
        Assert.That(provided[0].Value, Is.InstanceOf<ResolvedExpr>());
        Assert.That((ResolvedExpr)provided[0].Value, Is.InstanceOf<ResolvedTransport>());
    }

    [Test]
    public void Validate_SkipsUnboundOptionalParameters()
    {
        var provided = Validate("alpha(beta(x))", Constructor("alpha"));

        Assert.That(provided.Select(p => p.Param.Name), Is.EqualTo(new[] { "input" }));
    }

    [Test]
    public void Validate_ForIntegerParameterReceivingTransport_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(beta(x))",
            Constructor("numbers"),
            "E_SEM_EXPECTED_SCALAR",
            "parameter 'count' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForIntegerParameterReceivingList_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers([beta(x)])",
            Constructor("numbers"),
            "E_SEM_EXPECTED_SCALAR",
            "parameter 'count' expects a scalar value, got list");
    }

    [Test]
    public void Validate_ForDecimalParameterReceivingTransport_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(0, beta(x))",
            Constructor("numbers"),
            "E_SEM_EXPECTED_SCALAR",
            "parameter 'ratio' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForDecimalParameterReceivingList_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(0, [beta(x)])",
            Constructor("numbers"),
            "E_SEM_EXPECTED_SCALAR",
            "parameter 'ratio' expects a scalar value, got list");
    }

    [Test]
    public void Validate_ForIntegerAndDecimalParameters_ProducesTypedValues()
    {
        var provided = Validate("numbers(5, 1.25)", Constructor("numbers"));

        Assert.That(provided, Has.Count.EqualTo(2));
        Assert.That(provided[0].Value, Is.InstanceOf<IntegerValue>());
        Assert.That(((IntegerValue)provided[0].Value).Value, Is.EqualTo(5L));
        Assert.That(provided[1].Value, Is.InstanceOf<DecimalValue>());
        Assert.That(((DecimalValue)provided[1].Value).Value, Is.EqualTo(1.25m));
    }

    [Test]
    public void Validate_ForIntegerAndDecimalBoundaries_AcceptsInclusiveLimits()
    {
        var provided = Validate("numbers(-10, -1.5)", Constructor("numbers"));

        Assert.That(((IntegerValue)provided[0].Value).Value, Is.EqualTo(-10L));
        Assert.That(((DecimalValue)provided[1].Value).Value, Is.EqualTo(-1.5m));
    }

    [Test]
    public void Validate_ForIntegerAboveRange_ReportsIntegerOutOfRange()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(11)",
            Constructor("numbers"),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'count' is invalid: out of range [-10, 10]");
    }

    [Test]
    public void Validate_ForIntegerBelowRange_ReportsIntegerOutOfRange()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(-11)",
            Constructor("numbers"),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'count' is invalid: out of range [-10, 10]");
    }

    [Test]
    public void Validate_ForDecimalAboveRange_ReportsDecimalOutOfRange()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(0, 1.6)",
            Constructor("numbers"),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'ratio' is invalid: out of range [-1.5, 1.5]");
    }

    [Test]
    public void Validate_ForNonIntegerText_ReportsExpectedInteger()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(1.5)",
            Constructor("numbers"),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'count' is invalid: expected an integer");
    }

    [Test]
    public void Validate_ForIntegerOverflow_ReportsExpectedInteger()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(9223372036854775808)",
            Constructor("numbers"),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'count' is invalid: expected an integer");
    }

    [Test]
    public void Validate_ForQuotedInteger_AcceptsIt()
    {
        var provided = Validate("numbers(\"5\")", Constructor("numbers"));

        Assert.That(((IntegerValue)provided[0].Value).Value, Is.EqualTo(5L));
    }

    [Test]
    public void Validate_ForIntegerWithLeadingWhitespace_ReportsExpectedInteger()
    {
        SemanticsAssertions.ValidateParamsError(
            Registry(),
            "numbers(\" 5\")",
            Constructor("numbers"),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'count' is invalid: expected an integer");
    }

    [Test]
    public void Validate_ForDecimalWithTrailingWhitespace_ReportsExpectedDecimal()
    {
        var registry = FullRangeRegistry();

        SemanticsAssertions.ValidateParamsError(
            registry,
            "full(\"1 \")",
            FullRangeConstructor(registry),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'value' is invalid: expected a decimal");
    }

    [TestCase("1e3", "1000")]
    [TestCase(".5", "0.5")]
    [TestCase("1.", "1")]
    [TestCase("+1", "1")]
    [TestCase("-0", "0")]
    [TestCase("1.0e-28", "0.0000000000000000000000000001")]
    public void Validate_ForRepresentableDecimalForm_ProducesDecimalValue(string text, string expected)
    {
        var registry = FullRangeRegistry();
        var provided = SemanticsAssertions.ValidateParams(
            registry,
            "full(" + text + ")",
            FullRangeConstructor(registry));

        Assert.That(
            ((DecimalValue)provided[0].Value).Value,
            Is.EqualTo(decimal.Parse(expected, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Test]
    public void Validate_ForQuotedDecimal_AcceptsIt()
    {
        var registry = FullRangeRegistry();
        var provided = SemanticsAssertions.ValidateParams(
            registry,
            "full(\"1e3\")",
            FullRangeConstructor(registry));

        Assert.That(((DecimalValue)provided[0].Value).Value, Is.EqualTo(1000m));
    }

    [TestCase("1e-28")]
    [TestCase("7922816251426433759354395033.5")]
    [TestCase("79228162514264337593543950335")]
    [TestCase("-79228162514264337593543950335")]
    public void Validate_ForExactlyRepresentableDecimalBoundary_AcceptsIt(string text)
    {
        var registry = FullRangeRegistry();
        var provided = SemanticsAssertions.ValidateParams(
            registry,
            "full(" + text + ")",
            FullRangeConstructor(registry));

        Assert.That(provided[0].Value, Is.InstanceOf<DecimalValue>());
    }

    [TestCase("1e29")]
    [TestCase("1e-29")]
    [TestCase("-1e-29")]
    [TestCase("5e-29")]
    [TestCase("6e-29")]
    [TestCase("0.00000000000000000000000000001")]
    [TestCase("0.12345678901234567890123456789")]
    public void Validate_ForInexactDecimal_ReportsExpectedDecimal(string text)
    {
        var registry = FullRangeRegistry();

        SemanticsAssertions.ValidateParamsError(
            registry,
            "full(" + text + ")",
            FullRangeConstructor(registry),
            "E_SEM_VALIDATOR_FAILED",
            "parameter 'value' is invalid: expected a decimal");
    }

    [TestCase("0e-29")]
    [TestCase("0e29")]
    public void Validate_ForZeroWithAnyExponent_AcceptsZero(string text)
    {
        var registry = FullRangeRegistry();
        var provided = SemanticsAssertions.ValidateParams(
            registry,
            "full(" + text + ")",
            FullRangeConstructor(registry));

        Assert.That(((DecimalValue)provided[0].Value).Value, Is.EqualTo(0m));
    }

    private static IRegistry Registry()
    {
        return SemanticsTestData.StandardRegistry();
    }

    private static IRegistry FullRangeRegistry()
    {
        var builder = SemanticsTestData.NewBuilder();
        SemanticsTestData.Register(
            builder,
            "full",
            SemanticsTestData.TypeA,
            System.Array.Empty<string>(),
            SemanticsTestData.CreateDecimalParam(
                "value",
                decimal.MinValue,
                decimal.MaxValue,
                required: true,
                positional: true));
        return builder.Build();
    }

    private static TransportConstructorInfo FullRangeConstructor(IRegistry registry)
    {
        return registry.FindCanonical("full")!;
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
