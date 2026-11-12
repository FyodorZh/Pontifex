using NUnit.Framework;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.Factory.Tests.Canonicalization.Harness;

namespace Pontifex.Factory.Tests.Canonicalization;

[TestFixture]
[Category("Factory.Canonicalization")]
public sealed class FormulaCanonicalizerTests
{
    #region Hand-built resolved trees

    [Test]
    public void Canonicalize_ForTransportWithNoProvidedParameters_PrintsEmptyParentheses()
    {
        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(CanonicalizationTestData.Canonical("bag"))));

        Assert.That(output, Is.EqualTo("bag()"));
    }

    [Test]
    public void Canonicalize_ForLongestPositionalPrefix_PrintsItPositionally()
    {
        var constructor = CanonicalizationTestData.Canonical("opt");
        var a = Param(constructor, "a");
        var b = Param(constructor, "b");

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(a, CanonicalizationTestData.Scalar("one")),
                CanonicalizationTestData.Bind(b, CanonicalizationTestData.Scalar("two")))));

        Assert.That(output, Is.EqualTo("opt(one, two)"));
    }

    [Test]
    public void Canonicalize_ForOmittedFirstPositional_PrintsLaterParameterByName()
    {
        var constructor = CanonicalizationTestData.Canonical("opt");
        var b = Param(constructor, "b");

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(b, CanonicalizationTestData.Scalar("two")))));

        Assert.That(output, Is.EqualTo("opt(b=two)"));
    }

    [Test]
    public void Canonicalize_ForNamedOnlyParameterDeclaredBeforePositional_PrintsPositionalFirst()
    {
        var constructor = CanonicalizationTestData.Canonical("mixed");
        var flag = Param(constructor, "flag");
        var input = Param(constructor, "input");

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(flag, CanonicalizationTestData.Scalar("fast")),
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(BetaTransport())))));

        Assert.That(output, Is.EqualTo("mixed(beta(x), flag=fast)"));
    }

    [Test]
    public void Canonicalize_ForAbsentOptionalParameters_OmitsThem()
    {
        var constructor = CanonicalizationTestData.Canonical("alpha");
        var input = Param(constructor, "input");

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(BetaTransport())))));

        Assert.That(output, Is.EqualTo("alpha(beta(x))"));
    }

    [Test]
    public void Canonicalize_ForTransportList_PreservesElementOrder()
    {
        var constructor = CanonicalizationTestData.Canonical("bag");
        var items = Param(constructor, "items");

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.List(BetaTransport("one"), BetaTransport("two"))))));

        Assert.That(output, Is.EqualTo("bag([beta(one), beta(two)])"));
    }

    [Test]
    public void Canonicalize_ForEmptyListAndEmptyStruct_PrintsEmptyContainers()
    {
        var bag = CanonicalizationTestData.Canonical("bag");
        var bagItems = Param(bag, "items");
        var emptyStruct = CanonicalizationTestData.Canonical("emptyStruct");
        var emptyConfig = Param(emptyStruct, "config");

        var listOutput = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                bag,
                CanonicalizationTestData.Bind(bagItems, CanonicalizationTestData.List()))));
        var structOutput = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                emptyStruct,
                CanonicalizationTestData.Bind(
                    emptyConfig,
                    CanonicalizationTestData.Struct()))));

        Assert.That(listOutput, Is.EqualTo("bag([])"));
        Assert.That(structOutput, Is.EqualTo("emptystruct({})"));
    }

    [Test]
    public void Canonicalize_ForNestedStruct_PrintsFieldsInSchemaOrder()
    {
        var constructor = CanonicalizationTestData.Canonical("structy");
        var config = (StructParam)Param(constructor, "config");
        var name = config.Schema.Fields[0];
        var nested = (NestedStructField)config.Schema.Fields[1];
        var nestedA = nested.Schema.Fields[0];

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue(
                                CanonicalizationTestData.Field(
                                    nestedA,
                                    CanonicalizationTestData.StringValue("1")))))))));

        Assert.That(output, Is.EqualTo("structy({name=x, nested={a=1}})"));
    }

    [Test]
    public void Canonicalize_ForNestedStructWithEmptyNestedStruct_PrintsBraces()
    {
        var constructor = CanonicalizationTestData.Canonical("structy");
        var config = (StructParam)Param(constructor, "config");
        var name = config.Schema.Fields[0];
        var nested = (NestedStructField)config.Schema.Fields[1];

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue()))))));

        Assert.That(output, Is.EqualTo("structy({name=x, nested={}})"));
    }

    [Test]
    public void Canonicalize_ForNonIdentityCast_PrintsCanonicalTarget()
    {
        var formula = CanonicalizationTestData.Formula(
            new ResolvedCast(
                BetaTransport(),
                CanonicalizationTestData.TypeA,
                new SourceSpan(0, 0)));

        Assert.That(Print(formula), Is.EqualTo("beta(x) as TypeA"));
    }

    [Test]
    public void Canonicalize_ForCastNestedInAnArgument_PrintsTheCastInsideTheArgument()
    {
        var constructor = CanonicalizationTestData.Canonical("alpha");
        var input = Param(constructor, "input");
        var cast = new ResolvedCast(
            BetaTransport(),
            CanonicalizationTestData.TypeA,
            new SourceSpan(0, 0));

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(cast)))));

        Assert.That(output, Is.EqualTo("alpha(beta(x) as TypeA)"));
    }

    [Test]
    public void Canonicalize_ForCastInsideTransportList_PrintsTheCast()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.Exactly(CanonicalizationTestData.TypeA),
            min: 0,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "listb",
            CanonicalizationTestData.TypeC,
            items);
        var cast = new ResolvedCast(
            BetaTransport(),
            CanonicalizationTestData.TypeA,
            new SourceSpan(0, 0));

        var output = Print(CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.List(cast)))));

        Assert.That(output, Is.EqualTo("listb([beta(x) as TypeA])"));
    }

    [Test]
    public void Canonicalize_ForScalarVariants_UsesMinimalQuoting()
    {
        var constructor = CanonicalizationTestData.Canonical("beta");
        var value = Param(constructor, "value");

        var outputs = new[]
        {
            Print(BetaFormula(value, "plain")),
            Print(BetaFormula(value, "a, b")),
            Print(BetaFormula(value, "a\"b")),
            Print(BetaFormula(value, "a\\b")),
            Print(BetaFormula(value, string.Empty)),
            Print(BetaFormula(value, "café"))
        };

        Assert.That(outputs, Is.EqualTo(new[]
        {
            "beta(plain)",
            "beta(\"a, b\")",
            "beta(\"a\\\"b\")",
            "beta(a\\b)",
            "beta(\"\")",
            "beta(café)"
        }));
    }

    [Test]
    public void Canonicalize_ForPrintedOutput_HasNoTrailingWhitespaceOrNewline()
    {
        var output = Print(CanonicalizationTestData.Formula(BetaTransport()));

        Assert.That(output, Is.EqualTo("beta(x)"));
    }

    #endregion

    #region parsing.md §14.4 printer examples

    [TestCaseSource(nameof(PrinterExamples))]
    public void Canonicalize_ForParsingMdPrinterExamples_ProducesExpectedOutput(
        string source,
        string expected)
    {
        Assert.That(CanonicalizationTestData.Canonicalize(source), Is.EqualTo(expected));
    }

    private static System.Collections.Generic.IEnumerable<TestCaseData> PrinterExamples()
    {
        yield return Case("alpha(input=beta(x))", "alpha(beta(x))");
        yield return Case("alpha(input=beta(x), mode=fast)", "alpha(beta(x), fast)");
        yield return Case("alpha(input=beta(x), timeout=5s)", "alpha(beta(x), timeout=5s)");
        yield return Case("alpha(timeout=5s, input=beta(x))", "alpha(beta(x), timeout=5s)");
        yield return Case(
            "alpha(input=beta(x), mode=fast, timeout=5s)",
            "alpha(beta(x), fast, timeout=5s)");
        yield return Case("alpha(input=beta(x) as TypeA)", "alpha(beta(x) as TypeA)");
        yield return Case(
            "gamma(items=[alpha(beta(x)), alpha(beta(x))])",
            "gamma([alpha(beta(x)), alpha(beta(x))])");
        yield return Case("structy(config={name=x, nested={a=1}})", "structy({name=x, nested={a=1}})");
        yield return Case("beta(value=\"a, b\")", "beta(\"a, b\")");
        yield return Case("beta(value=\"\")", "beta(\"\")");
        yield return Case("beta(value=\"x=y\")", "beta(\"x=y\")");
        yield return Case("beta(value=\"plain\")", "beta(plain)");
        yield return Case("beta(value=\"a\\\"b\")", "beta(\"a\\\"b\")");
        yield return Case("beta(value=\"a\\\\b\")", "beta(a\\b)");
        yield return Case("beta(value=\"a\tb\")", "beta(\"a\tb\")");
        yield return Case("beta(value=\"  x  \")", "beta(\"  x  \")");
        yield return Case("alpha(input=alpha(beta(x)))", "alpha(alpha(beta(x)))");
        yield return Case("mixed(input=beta(x), flag=fast)", "mixed(beta(x), flag=fast)");
        yield return Case("opt(b=two)", "opt(b=two)");
        yield return Case("bag(items=[])", "bag([])");
        yield return Case("emptyStruct(config={})", "emptystruct({})");
        yield return Case("single(item=[beta(x)])", "single([beta(x)])");
        yield return Case("numbers(count=5, ratio=1.50)", "numbers(5, 1.5)");
        yield return Case("numbers(count=+5, ratio=-0.0)", "numbers(5, 0)");
        yield return Case("wide(value=1e3)", "wide(1000)");
        yield return Case("wide(value=1e-28)", "wide(0.0000000000000000000000000001)");
        yield return Case(
            "numberStruct(config={count=5, ratio=1.50})",
            "numberstruct({count=5, ratio=1.5})");
        yield return Case(
            "arrays(config={counts=[-1, 1], ratios=[1.50]})",
            "arrays({counts=[-1, 1], ratios=[1.5]})");
        yield return Case(
            "arrays(config={counts=[0], names=[]})",
            "arrays({names=[], counts=[0]})");
    }

    #endregion

    #region parsing.md §11.3 canonicalization differences

    [TestCaseSource(nameof(CanonicalizationEquivalencePairs))]
    public void Canonicalize_ForEquivalentSources_ProducesTheSameOutputAndTree(
        string first,
        string second,
        string expected)
    {
        var canonicalizer = CanonicalizationTestData.Canonicalizer();

        var firstTree = CanonicalizationTestData.Resolve(first);
        var secondTree = CanonicalizationTestData.Resolve(second);
        var firstOutput = canonicalizer.Canonicalize(firstTree);
        var secondOutput = canonicalizer.Canonicalize(secondTree);

        Assert.That(firstOutput, Is.EqualTo(expected));
        Assert.That(secondOutput, Is.EqualTo(expected));
        CanonicalizationAssertions.AssertTreesEqual(firstTree, secondTree);
    }

    private static System.Collections.Generic.IEnumerable<TestCaseData> CanonicalizationEquivalencePairs()
    {
        yield return Pair("ALPHA(beta(x))", "alpha(beta(x))", "alpha(beta(x))");
        yield return Pair("alpha( beta(x) ,  x )", "alpha(beta(x), x)", "alpha(beta(x), x)");
        yield return Pair(
            "alpha(beta(x), mode=x)",
            "alpha(beta(x), x)",
            "alpha(beta(x), x)");
        yield return Pair(
            "alpha(beta(x), timeout=1s)",
            "alpha(input=beta(x), timeout=1s)",
            "alpha(beta(x), timeout=1s)");
        yield return Pair(
            "alpha(beta(x)) as TypeA",
            "alpha(beta(x))",
            "alpha(beta(x))");
        yield return Pair("outer(alpha(x))", "outer(beta(x))", "outer(beta(x))");
        yield return Pair(
            "structy({nested={a=1}, name=x})",
            "structy({name=x, nested={a=1}})",
            "structy({name=x, nested={a=1}})");
        yield return Pair("beta(\"plain\")", "beta(plain)", "beta(plain)");
        yield return Pair(
            "  alpha(\r\n beta(x) ,\t x \r\n)  ",
            "alpha(beta(x), x)",
            "alpha(beta(x), x)");
    }

    #endregion

    #region Cross-subsystem round-trip

    [TestCaseSource(nameof(RoundTripSources))]
    public void Canonicalize_ForRepresentativeFormula_IsIdempotentAndPreservesTheTree(string source)
    {
        var canonicalizer = CanonicalizationTestData.Canonicalizer();

        var firstTree = CanonicalizationTestData.Resolve(source);
        var firstOutput = canonicalizer.Canonicalize(firstTree);
        CanonicalizationAssertions.AssertNoRawNewlines(firstOutput);

        var secondTree = CanonicalizationTestData.Resolve(firstOutput);
        var secondOutput = canonicalizer.Canonicalize(secondTree);

        Assert.That(secondOutput, Is.EqualTo(firstOutput));
        CanonicalizationAssertions.AssertTreesEqual(firstTree, secondTree);
    }

    [Test]
    public void Canonicalize_ForTopLevelNonIdentityCast_ProducesExpectedOutput()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("alpha(beta(x)) as TypeB"),
            Is.EqualTo("alpha(beta(x)) as TypeB"));
    }

    [Test]
    public void Canonicalize_ForCrLfAndTabWhitespace_ProducesNoRawNewlines()
    {
        var output = CanonicalizationTestData.Canonicalize(
            "\r\n\talpha(\r\n beta(x)\t,\r\n x\r\n)\r\n");

        Assert.That(output, Is.EqualTo("alpha(beta(x), x)"));
        CanonicalizationAssertions.AssertNoRawNewlines(output);
    }

    [Test]
    public void Canonicalize_ForCaseInsensitiveCastType_UsesCatalogCanonicalSpelling()
    {
        var output = CanonicalizationTestData.Canonicalize("alpha(beta(x)) as typeb");

        Assert.That(output, Is.EqualTo("alpha(beta(x)) as TypeB"));
    }

    [Test]
    public void Canonicalize_ForDeepNestedStruct_RoundTripsInSchemaOrder()
    {
        var source = "deep({outer={inner={value=v}}})";
        var canonicalizer = CanonicalizationTestData.Canonicalizer();

        var firstTree = CanonicalizationTestData.Resolve(source);
        var firstOutput = canonicalizer.Canonicalize(firstTree);
        var secondTree = CanonicalizationTestData.Resolve(firstOutput);
        var secondOutput = canonicalizer.Canonicalize(secondTree);

        Assert.That(firstOutput, Is.EqualTo("deep({outer={inner={value=v}}})"));
        Assert.That(secondOutput, Is.EqualTo(firstOutput));
        CanonicalizationAssertions.AssertTreesEqual(firstTree, secondTree);
    }

    [Test]
    public void Canonicalize_ForListOrder_PreservesTheOriginalElementOrderAcrossRoundTrip()
    {
        var source = "bag([beta(one), beta(two), beta(three)])";
        var canonicalizer = CanonicalizationTestData.Canonicalizer();

        var firstTree = CanonicalizationTestData.Resolve(source);
        var firstOutput = canonicalizer.Canonicalize(firstTree);
        var secondTree = CanonicalizationTestData.Resolve(firstOutput);

        Assert.That(firstOutput, Is.EqualTo("bag([beta(one), beta(two), beta(three)])"));
        CanonicalizationAssertions.AssertTreesEqual(firstTree, secondTree);
    }

    [Test]
    public void Canonicalize_ForIntegerAndDecimalParameters_PrintsCanonicalScalars()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("numeric(10, 1.50)"),
            Is.EqualTo("numeric(10, 1.5)"));
    }

    [Test]
    public void Canonicalize_ForPositiveSignedInteger_PrintsUnsignedCanonicalText()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("numeric(+5, 0)"),
            Is.EqualTo("numeric(5, 0)"));
    }

    [Test]
    public void Canonicalize_ForExponentInput_PrintsPlainInteger()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("wide(1e3)"),
            Is.EqualTo("wide(1000)"));
    }

    [Test]
    public void Canonicalize_ForSmallestScaleDecimal_PrintsPlainForm()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("wide(1e-28)"),
            Is.EqualTo("wide(0.0000000000000000000000000001)"));
    }

    [Test]
    public void Canonicalize_ForDecimalMinAndMax_PrintsPlainForm()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("wide(-79228162514264337593543950335)"),
            Is.EqualTo("wide(-79228162514264337593543950335)"));
        Assert.That(
            CanonicalizationTestData.Canonicalize("wide(79228162514264337593543950335)"),
            Is.EqualTo("wide(79228162514264337593543950335)"));
    }

    [Test]
    public void Canonicalize_ForNegativeZeroAndTrailingZeros_NormalizesTheText()
    {
        Assert.That(CanonicalizationTestData.Canonicalize("wide(-0)"), Is.EqualTo("wide(0)"));
        Assert.That(CanonicalizationTestData.Canonicalize("wide(-0.0)"), Is.EqualTo("wide(0)"));
        Assert.That(CanonicalizationTestData.Canonicalize("wide(1.000)"), Is.EqualTo("wide(1)"));
    }

    [Test]
    public void Canonicalize_ForQuotedNumericScalar_PrintsCanonicalNumber()
    {
        Assert.That(CanonicalizationTestData.Canonicalize("wide(\"1e3\")"), Is.EqualTo("wide(1000)"));
        Assert.That(CanonicalizationTestData.Canonicalize("wide(\"1.50\")"), Is.EqualTo("wide(1.5)"));
    }

    [Test]
    public void Canonicalize_ForIntegerAndDecimalStructFields_PrintsCanonicalScalars()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("numericStruct({count=5, ratio=1.50})"),
            Is.EqualTo("numericstruct({count=5, ratio=1.5})"));
    }

    [Test]
    public void Canonicalize_ForArrays_PrintsCanonicalElementsInSchemaOrder()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize(
                "arrays({counts=[-1, 1], ratios=[1.50, -0.5], names=[\"a, b\", c]})"),
            Is.EqualTo("arrays({names=[\"a, b\", c], counts=[-1, 1], ratios=[1.5, -0.5]})"));
    }

    [Test]
    public void Canonicalize_ForEmptyArrays_PrintsEmptyBrackets()
    {
        Assert.That(
            CanonicalizationTestData.Canonicalize("arrays({counts=[0], names=[], ratios=[]})"),
            Is.EqualTo("arrays({names=[], counts=[0], ratios=[]})"));
    }

    private static System.Collections.Generic.IEnumerable<TestCaseData> RoundTripSources()
    {
        foreach (var source in new[]
                 {
                     "alpha(beta(x))",
                     "alpha(beta(x), fast)",
                     "alpha(beta(x), timeout=5s)",
                     "alpha(beta(x), fast, timeout=5s)",
                     "alpha(beta(x)) as TypeB",
                     "alpha(beta(x) as TypeA)",
                     "gamma([alpha(beta(x)), alpha(beta(x))])",
                     "structy({name=x, nested={a=1}})",
                     "beta(\"a, b\")",
                     "beta(\"\")",
                     "beta(\"x=y\")",
                     "beta(plain)",
                     "beta(\"a\\\"b\")",
                     "beta(\"  x  \")",
                     "mixed(input=beta(x), flag=fast)",
                     "opt(b=two)",
                     "bag(items=[])",
                     "bag([beta(one), beta(two)])",
                     "emptyStruct(config={})",
                     "single(item=[beta(x)])",
                     "outer(alpha(x))",
                     "ALPHA( BETA(x) , FAST )",
                     "numeric(10, 1.5)",
                     "numericStruct({count=5, ratio=1.5})",
                     "wide(1e3)",
                     "wide(1e-28)",
                     "wide(1.50)",
                     "wide(-0.0)",
                     "wide(79228162514264337593543950335)",
                     "arrays({counts=[-1, 1], ratios=[1.5], names=[a, b]})",
                     "arrays({counts=[0], names=[], ratios=[]})",
                     "nestedArrays({nested={values=[0, 1]}})"
                 })
        {
            yield return new TestCaseData(source).SetName("RoundTrip_" + source);
        }
    }

    #endregion

    private static string Print(ResolvedFormula formula)
    {
        return CanonicalizationTestData.Canonicalizer().Canonicalize(formula);
    }

    private static ResolvedFormula BetaFormula(Param value, string text)
    {
        var constructor = CanonicalizationTestData.Canonical("beta");
        return CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(value, CanonicalizationTestData.Scalar(text))));
    }

    private static ResolvedTransport BetaTransport(string text = "x")
    {
        var constructor = CanonicalizationTestData.Canonical("beta");
        var value = Param(constructor, "value");
        return CanonicalizationTestData.Transport(
            constructor,
            CanonicalizationTestData.Bind(value, CanonicalizationTestData.Scalar(text)));
    }

    private static Param Param(TransportConstructorInfo constructor, string name)
    {
        return CanonicalizationTestData.FindParam(constructor, name);
    }

    private static TestCaseData Case(string source, string expected)
    {
        return new TestCaseData(source, expected).SetName("Printer_" + source);
    }

    private static TestCaseData Pair(string first, string second, string expected)
    {
        return new TestCaseData(first, second, expected).SetName("Equivalent_" + first);
    }
}
