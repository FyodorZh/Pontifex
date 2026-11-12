using System.Linq;
using Pontifex;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.Factory.Tests.Semantics.Harness;

namespace Pontifex.Factory.Tests.Semantics;

[TestFixture]
[Category("Factory.Semantics")]
public sealed class FormulaResolverTests
{
    [Test]
    public void Resolve_ForCanonicalScheme_SelectsCanonicalConstructorAndTransport()
    {
        var formula = Resolve("alpha(beta(x))");
        var root = SemanticsAssertions.AsTransport(formula.Root);

        Assert.That(root.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(root.Type, Is.EqualTo(SemanticsTestData.TypeA));
        Assert.That(root.Type, Is.EqualTo(root.Constructor.Type));

        var input = SemanticsAssertions.ProvidedTransport(root, "input");
        Assert.That(input.Constructor.Name, Is.EqualTo("beta"));
        Assert.That(input.Type, Is.EqualTo(SemanticsTestData.TypeB));
    }

    [Test]
    public void Resolve_ForTwoPositionalArguments_BindsPositionalPrefixInDeclarationOrder()
    {
        var root = ResolveTransport("alpha(beta(x), fast)");

        Assert.That(root.Provided.Select(b => b.Param.Name), Is.EqualTo(new[] { "input", "mode" }));
        Assert.That(
            SemanticsAssertions.ProvidedScalar(root, "mode").Text,
            Is.EqualTo("fast"));
    }

    [Test]
    public void Resolve_ForNamedOnlyParameter_BindsItByName()
    {
        var root = ResolveTransport("alpha(beta(x), timeout=5s)");

        Assert.That(root.Provided.Select(b => b.Param.Name), Is.EqualTo(new[] { "input", "timeout" }));
        Assert.That(
            SemanticsAssertions.ProvidedScalar(root, "timeout").Text,
            Is.EqualTo("5s"));
    }

    [Test]
    public void Resolve_ForPositionalPrefixAndNamedSuffix_BindsBoth()
    {
        var root = ResolveTransport("alpha(beta(x), fast, timeout=5s)");

        Assert.That(
            root.Provided.Select(b => b.Param.Name),
            Is.EqualTo(new[] { "input", "mode", "timeout" }));
    }

    [Test]
    public void Resolve_ForAliasSelectedByExpectedType_ReturnsAliasConstructor()
    {
        var formula = SemanticsAssertions.ResolveSucceeds(
            SemanticsTestData.StandardResolver(),
            "outer(alpha(x))");
        var root = SemanticsAssertions.AsTransport(formula.Root);

        Assert.That(root.Constructor.Name, Is.EqualTo("outer"));

        var input = SemanticsAssertions.ProvidedTransport(root, "input");
        Assert.That(input.Constructor.Name, Is.EqualTo("beta"));
        Assert.That(input.Type, Is.EqualTo(SemanticsTestData.TypeB));
        Assert.That(
            SemanticsAssertions.ProvidedScalar(input, "value").Text,
            Is.EqualTo("x"));
    }

    [Test]
    public void Resolve_ForAliasOnlySchemeWithExpectedType_SelectsMatchingAlias()
    {
        var formula = SemanticsAssertions.ResolveSucceeds(
            SemanticsTestData.StandardResolver(),
            "needsC(shared([alpha(beta(x)), alpha(beta(x))]))");
        var root = SemanticsAssertions.AsTransport(formula.Root);

        var input = SemanticsAssertions.ProvidedTransport(root, "input");
        Assert.That(input.Constructor.Name, Is.EqualTo("gamma"));
        Assert.That(input.Type, Is.EqualTo(SemanticsTestData.TypeC));
        Assert.That(SemanticsAssertions.ProvidedList(input, "items"), Has.Count.EqualTo(2));
    }

    [Test]
    public void Resolve_ForUnionExpectedSet_PrefersCanonicalAfterFiltering()
    {
        var formula = SemanticsAssertions.ResolveSucceeds(
            SemanticsTestData.StandardResolver(),
            "dual(alpha(beta(x)))");
        var root = SemanticsAssertions.AsTransport(formula.Root);

        var input = SemanticsAssertions.ProvidedTransport(root, "input");
        Assert.That(input.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(input.Type, Is.EqualTo(SemanticsTestData.TypeA));
    }

    [Test]
    public void Resolve_ForCastThatChangesType_ReturnsResolvedCast()
    {
        var formula = Resolve("alpha(beta(x)) as TypeB");
        var cast = SemanticsAssertions.AsCast(formula.Root);

        Assert.That(cast.Target, Is.EqualTo(SemanticsTestData.TypeB));
        Assert.That(cast.Type, Is.EqualTo(SemanticsTestData.TypeB));
        Assert.That(cast.Inner.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(cast.Inner.Type, Is.EqualTo(SemanticsTestData.TypeA));
        Assert.That(cast.Inner.Type, Is.Not.EqualTo(cast.Target));
    }

    [Test]
    public void Resolve_ForIdentityCast_CollapsesToInnerTransportWithFullCastSpan()
    {
        const string source = "alpha(beta(x)) as TypeA";
        var formula = Resolve(source);
        var root = SemanticsAssertions.AsTransport(formula.Root);

        Assert.That(root, Is.Not.InstanceOf<ResolvedCast>());
        Assert.That(root.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(root.Type, Is.EqualTo(SemanticsTestData.TypeA));
        Assert.That(root.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
    }

    [Test]
    public void Resolve_ForCastInsideTransportParameter_ResolvesInnerWithAny()
    {
        // outer expects Exactly(TypeB); the cast target is TypeB. The inner
        // transport must be resolved with Any, which selects canonical alpha
        // (TypeA), not the TypeB alias.
        var formula = Resolve("outer(alpha(beta(x)) as TypeB)");
        var root = SemanticsAssertions.AsTransport(formula.Root);
        var binding = SemanticsAssertions.Provided(root, "input");

        Assert.That(binding.Value, Is.InstanceOf<ResolvedExpr>());
        var cast = SemanticsAssertions.AsCast((ResolvedExpr)binding.Value);
        Assert.That(cast.Inner.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(cast.Inner.Type, Is.EqualTo(SemanticsTestData.TypeA));
        Assert.That(cast.Target, Is.EqualTo(SemanticsTestData.TypeB));
    }

    [Test]
    public void Resolve_ForNonIdentityCast_RetainsInnerCallAndFullCastSpans()
    {
        const string source = "alpha(beta(x)) as TypeB";
        var cast = SemanticsAssertions.AsCast(Resolve(source).Root);

        Assert.That(cast.Inner.Span, Is.EqualTo(new SourceSpan(0, "alpha(beta(x))".Length)));
        Assert.That(cast.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
    }

    [Test]
    public void Resolve_ForNestedTransport_RetainsEachSpan()
    {
        const string source = "alpha(beta(x))";
        var root = ResolveTransport(source);
        var input = SemanticsAssertions.ProvidedTransport(root, "input");

        Assert.That(root.Span, Is.EqualTo(new SourceSpan(0, source.Length)));
        Assert.That(input.Span, Is.EqualTo(new SourceSpan(6, "beta(x)".Length)));
    }

    [Test]
    public void Resolve_ForHomogeneousList_ResolvesElementsInOrder()
    {
        var root = ResolveTransport("gamma([alpha(beta(x)), alpha(beta(y))])");
        var elements = SemanticsAssertions.ProvidedList(root, "items");

        Assert.That(elements, Has.Count.EqualTo(2));
        Assert.That(
            elements.Select(e => SemanticsAssertions.AsTransport(e).Constructor.Name),
            Is.EqualTo(new[] { "alpha", "alpha" }));
        Assert.That(elements.All(e => e.Type == SemanticsTestData.TypeA), Is.True);
        Assert.That(elements[0].Span, Is.EqualTo(new SourceSpan(7, 14)));
        Assert.That(elements[1].Span, Is.EqualTo(new SourceSpan(23, 14)));
        Assert.That(
            SemanticsAssertions.ProvidedScalar(
                SemanticsAssertions.ProvidedTransport(
                    SemanticsAssertions.AsTransport(elements[0]),
                    "input"),
                "value").Text,
            Is.EqualTo("x"));
        Assert.That(
            SemanticsAssertions.ProvidedScalar(
                SemanticsAssertions.ProvidedTransport(
                    SemanticsAssertions.AsTransport(elements[1]),
                    "input"),
                "value").Text,
            Is.EqualTo("y"));
    }

    [Test]
    public void Resolve_ForNonHomogeneousList_PreservesElementOrder()
    {
        var root = ResolveTransport("bag([beta(x), alpha(beta(x)), beta(y)])");
        var elements = SemanticsAssertions.ProvidedList(root, "items");

        Assert.That(
            elements.Select(e => SemanticsAssertions.AsTransport(e).Constructor.Name),
            Is.EqualTo(new[] { "beta", "alpha", "beta" }));
        Assert.That(
            elements.Select(e => e.Type),
            Is.EqualTo(new[]
            {
                SemanticsTestData.TypeB,
                SemanticsTestData.TypeA,
                SemanticsTestData.TypeB
            }));
    }

    [Test]
    public void Resolve_ForListWithExpectedSet_ResolvesElementsWithParameterExpectedType()
    {
        var root = ResolveTransport("listB([alpha(x)])");
        var element = SemanticsAssertions.AsTransport(
            SemanticsAssertions.ProvidedList(root, "items").Single());

        Assert.That(element.Constructor.Name, Is.EqualTo("beta"));
        Assert.That(element.Type, Is.EqualTo(SemanticsTestData.TypeB));
    }

    [Test]
    public void Resolve_ForNestedStruct_ResolvesFieldsInSchemaOrder()
    {
        var root = ResolveTransport("structy({name=x, nested={a=1}})");
        var config = SemanticsAssertions.ProvidedStruct(root, "config");

        Assert.That(config.Fields.Select(f => f.Field.Name), Is.EqualTo(new[] { "name", "nested" }));
        Assert.That(
            ((StringValue)config.Fields[0].Value).Text,
            Is.EqualTo("x"));

        var nested = (StructValue)config.Fields[1].Value;
        Assert.That(nested.Fields, Has.Count.EqualTo(1));
        Assert.That(nested.Fields[0].Field.Name, Is.EqualTo("a"));
        Assert.That(((StringValue)nested.Fields[0].Value).Text, Is.EqualTo("1"));
    }

    [Test]
    public void Resolve_ForStructSchemaOrderDifferentFromSourceOrder_OrdersFieldsBySchema()
    {
        var root = ResolveTransport("structy({nested={a=1}, name=x})");
        var config = SemanticsAssertions.ProvidedStruct(root, "config");

        Assert.That(config.Fields.Select(f => f.Field.Name), Is.EqualTo(new[] { "name", "nested" }));
    }

    [Test]
    public void Resolve_ForDeepNestedStruct_ProducesNestedFieldValues()
    {
        var root = ResolveTransport("deep({outer={inner={value=v}}})");
        var config = SemanticsAssertions.ProvidedStruct(root, "config");
        var outer = (StructValue)config.Fields[0].Value;
        var inner = (StructValue)outer.Fields[0].Value;

        Assert.That(config.Fields[0].Field.Name, Is.EqualTo("outer"));
        Assert.That(outer.Fields[0].Field.Name, Is.EqualTo("inner"));
        Assert.That(inner.Fields[0].Field.Name, Is.EqualTo("value"));
        Assert.That(((StringValue)inner.Fields[0].Value).Text, Is.EqualTo("v"));
    }

    [Test]
    public void Resolve_ForEmptyList_ProducesEmptyElementList()
    {
        var root = ResolveTransport("bag([])");

        Assert.That(SemanticsAssertions.ProvidedList(root, "items"), Is.Empty);
    }

    [Test]
    public void Resolve_ForEmptyStruct_ProducesEmptyFieldList()
    {
        var root = ResolveTransport("emptyStruct({})");

        Assert.That(SemanticsAssertions.ProvidedStruct(root, "config").Fields, Is.Empty);
    }

    [Test]
    public void Resolve_ForSingleElementList_ResolvesTheElement()
    {
        var root = ResolveTransport("single([beta(x)])");

        Assert.That(SemanticsAssertions.ProvidedList(root, "item"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Resolve_ForNamedArgumentsInReverseOrder_StoresProvidedInDeclarationOrder()
    {
        var root = ResolveTransport("alpha(timeout=5s, input=beta(x))");

        Assert.That(
            root.Provided.Select(b => b.Param.Name),
            Is.EqualTo(new[] { "input", "timeout" }));
    }

    [Test]
    public void Resolve_ForNamedOnlyBeforePositionalParameter_StoresProvidedInDeclarationOrder()
    {
        var root = ResolveTransport("mixed(flag=fast, input=beta(x))");

        Assert.That(root.Provided.Select(b => b.Param.Name), Is.EqualTo(new[] { "flag", "input" }));
        Assert.That(
            SemanticsAssertions.ProvidedScalar(root, "flag").Text,
            Is.EqualTo("fast"));
    }

    [Test]
    public void Resolve_ForNamedPositionalCapableParameter_BindsItWithoutSynthesizingEarlierOnes()
    {
        var root = ResolveTransport("opt(b=two)");

        Assert.That(root.Provided.Select(b => b.Param.Name), Is.EqualTo(new[] { "b" }));
        Assert.That(root.Provided[0].Param.Required, Is.False);
    }

    [Test]
    public void Resolve_WhenOptionalParametersAreAbsent_DoesNotSynthesizeDefaults()
    {
        var root = ResolveTransport("alpha(beta(x))");

        Assert.That(root.Provided.Select(b => b.Param.Name), Is.EqualTo(new[] { "input" }));
        Assert.That(root.Provided, Has.Count.EqualTo(1));
    }

    [Test]
    public void Resolve_ForUppercaseSchemeAndArgumentNames_ProducesCanonicalMetadata()
    {
        var root = ResolveTransport("ALPHA(BETA(x), MODE=fast, TIMEOUT=5s)");

        Assert.That(root.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(
            root.Provided.Select(b => b.Param.Name),
            Is.EqualTo(new[] { "input", "mode", "timeout" }));
        Assert.That(
            SemanticsAssertions.AsTransport(SemanticsAssertions.ProvidedTransport(root, "input"))
                .Constructor.Name,
            Is.EqualTo("beta"));
    }

    [Test]
    public void Resolve_ForQuotedScalarWithComma_PreservesDecodedText()
    {
        var root = ResolveTransport("beta(\"a, b\")");

        Assert.That(
            SemanticsAssertions.ProvidedScalar(root, "value").Text,
            Is.EqualTo("a, b"));
    }

    [Test]
    public void Resolve_ForEmptyQuotedScalar_PreservesEmptyText()
    {
        var root = ResolveTransport("beta(\"\")");

        Assert.That(SemanticsAssertions.ProvidedScalar(root, "value").Text, Is.Empty);
    }

    [Test]
    public void Resolve_ForBareNumericScalar_AcceptsItAsString()
    {
        var root = ResolveTransport("beta(1)");

        Assert.That(SemanticsAssertions.ProvidedScalar(root, "value").Text, Is.EqualTo("1"));
    }

    [Test]
    public void Resolve_BindsProvidedParametersToConstructorParameterInstances()
    {
        var root = ResolveTransport("alpha(beta(x), fast, timeout=5s)");

        foreach (var binding in root.Provided)
        {
            Assert.That(
                root.Constructor.Params.Any(p => ReferenceEquals(p, binding.Param)),
                Is.True,
                "provided parameter '" + binding.Param.Name + "' is not a constructor parameter instance");
        }
    }

    [Test]
    public void Resolve_BindsStructFieldsToSchemaFieldInstances()
    {
        var root = ResolveTransport("structy({name=x, nested={a=1}})");
        var config = SemanticsAssertions.ProvidedStruct(root, "config");
        var schema = ((StructParam)SemanticsTestData.FindParam(root.Constructor, "config")).Schema;

        Assert.That(config.Fields, Has.Count.EqualTo(schema.Fields.Count));
        for (var i = 0; i < config.Fields.Count; i++)
        {
            Assert.That(ReferenceEquals(config.Fields[i].Field, schema.Fields[i]), Is.True);
        }
    }

    [Test]
    public void Resolve_ForRequiredParameters_ReportsEveryProvidedTypeIsInParameterExpectedSet()
    {
        var root = ResolveTransport("dual(alpha(beta(x)))");
        var parameter = (TransportParam)SemanticsTestData.FindParam(root.Constructor, "input");
        var value = (ResolvedExpr)SemanticsAssertions.Provided(root, "input").Value;

        Assert.That(parameter.Expected.Contains(value.Type), Is.True);
    }

    [Test]
    public void Resolve_ForCastInsideTransportList_ResolvesInnerWithAny()
    {
        var root = ResolveTransport("listB([alpha(beta(x)) as TypeB])");
        var element = SemanticsAssertions.AsCast(
            SemanticsAssertions.ProvidedList(root, "items").Single());

        Assert.That(element.Target, Is.EqualTo(SemanticsTestData.TypeB));
        Assert.That(element.Inner.Constructor.Name, Is.EqualTo("alpha"));
        Assert.That(element.Inner.Type, Is.EqualTo(SemanticsTestData.TypeA));
    }

    [Test]
    public void Resolve_ForCyclicStructSchema_ResolvesFiniteNestedStruct()
    {
        var fields = new StructField[1];
        var schema = new StructSchema(fields);
        fields[0] = new NestedStructField("self", false, schema);

        var builder = SemanticsTestData.NewBuilder();
        SemanticsTestData.Register(
            builder,
            "cyclic",
            SemanticsTestData.TypeA,
            System.Array.Empty<string>(),
            SemanticsTestData.CreateStructParam("config", schema, required: true, positional: true));

        var formula = SemanticsAssertions.ResolveSucceeds(
            SemanticsTestData.Resolver(builder.Build()),
            "cyclic({self={}})");
        var root = SemanticsAssertions.AsTransport(formula.Root);
        var config = SemanticsAssertions.ProvidedStruct(root, "config");

        Assert.That(config.Fields, Has.Count.EqualTo(1));
        Assert.That(config.Fields[0].Field.Name, Is.EqualTo("self"));
        Assert.That(((StructValue)config.Fields[0].Value).Fields, Is.Empty);
    }

    [Test]
    public void Resolve_ForIntegerAndDecimalParameters_ProducesTypedValues()
    {
        var root = ResolveTransport("numbers(5, 1.50)");

        Assert.That(
            SemanticsAssertions.ProvidedInteger(root, "count").Value,
            Is.EqualTo(5L));
        Assert.That(
            SemanticsAssertions.ProvidedDecimal(root, "ratio").Value,
            Is.EqualTo(1.5m));
    }

    [Test]
    public void Resolve_ForIntegerAndDecimalStructFields_ProducesTypedValues()
    {
        var root = ResolveTransport("numberStruct({count=5, ratio=1.50})");
        var config = SemanticsAssertions.ProvidedStruct(root, "config");

        Assert.That(
            ((IntegerValue)config.Fields[0].Value).Value,
            Is.EqualTo(5L));
        Assert.That(
            ((DecimalValue)config.Fields[1].Value).Value,
            Is.EqualTo(1.5m));
    }

    private static ResolvedFormula Resolve(string source)
    {
        return SemanticsAssertions.ResolveSucceeds(
            SemanticsTestData.StandardResolver(),
            source);
    }

    private static ResolvedTransport ResolveTransport(string source)
    {
        return SemanticsAssertions.AsTransport(Resolve(source).Root);
    }
}
