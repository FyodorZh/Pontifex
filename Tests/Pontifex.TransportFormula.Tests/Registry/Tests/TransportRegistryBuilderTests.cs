using System;
using System.Linq;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;
using Pontifex.TransportFormula.Tests.Registry.Harness;

namespace Pontifex.TransportFormula.Tests.Registry;

[TestFixture]
[Category("TransportFormula.Registry")]
public sealed class TransportRegistryBuilderTests
{
    [Test]
    public void RegisterTransport_NormalizesCanonicalNameAndParameterNames()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            RegistryTestData.Constructor(
                "ALPHA",
                RegistryTestData.TypeA,
                RegistryTestData.CreateStringParam("INPUT"),
                RegistryTestData.CreateStringParam("Mode")),
            Array.Empty<string>()));

        var constructor = builder.Build().FindCanonical("alpha");

        Assert.That(constructor, Is.Not.Null);
        Assert.That(constructor!.Name, Is.EqualTo("alpha"));
        Assert.That(constructor.Params.Select(p => p.Name), Is.EqualTo(new[] { "input", "mode" }));
    }

    [Test]
    public void RegisterTransport_NormalizesAliasNames()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "ALPHA",
            RegistryTestData.TypeA,
            new[] { "SHARED" }));

        var registry = builder.Build();

        Assert.That(registry.FindCandidates("shared").Select(c => c.Name), Is.EqualTo(new[] { "alpha" }));
        Assert.That(registry.FindCandidates("SHARED"), Is.Empty);
    }

    [Test]
    public void RegisterTransport_NormalizesStructFieldNamesRecursively()
    {
        var nested = RegistryTestData.Schema(
            RegistryTestData.CreateStringField("A"));
        var schema = RegistryTestData.Schema(
            RegistryTestData.CreateStringField("Name"),
            RegistryTestData.CreateNestedField("Nested", nested));

        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "ALPHA",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateStructParam("CONFIG", schema)));

        var parameter = (StructParam)builder.Build().FindCanonical("alpha")!.Params[0];

        Assert.That(parameter.Name, Is.EqualTo("config"));
        Assert.That(parameter.Schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "name", "nested" }));
        var nestedField = (NestedStructField)parameter.Schema.Fields[1];
        Assert.That(nestedField.Schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void RegisterTransport_NormalizesTypeSetAndRemovesDuplicates()
    {
        var expected = RegistryTestData.OneOf(
            RegistryTestData.TypeA,
            RegistryTestData.TypeB,
            RegistryTestData.TypeA);

        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "ALPHA",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateTransportParam("INPUT", expected)));

        var parameter = (TransportParam)builder.Build().FindCanonical("alpha")!.Params[0];

        Assert.That(parameter.Expected.Kind, Is.EqualTo(TransportTypeSetKind.OneOf));
        Assert.That(parameter.Expected.Types, Is.EqualTo(new[]
        {
            RegistryTestData.TypeA,
            RegistryTestData.TypeB
        }));
    }

    [Test]
    public void RegisterTransport_NormalizesSuggestedAliasesButDoesNotRegisterThem()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            RegistryTestData.ConstructorWithSuggestedAliases(
                "alpha",
                RegistryTestData.TypeA,
                new[] { "Fast" }),
            Array.Empty<string>()));

        var registry = builder.Build();

        Assert.That(
            registry.FindCanonical("alpha")!.SuggestedAliases,
            Is.EqualTo(new[] { "fast" }));
        Assert.That(registry.FindCandidates("fast"), Is.Empty);
    }

    [TestCase("1alpha")]
    [TestCase("alpha beta")]
    [TestCase("")]
    public void RegisterTransport_WithInvalidCanonicalName_ThrowsInvalidCanonicalName(string name)
    {
        var builder = RegistryTestData.NewBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                name,
                RegistryTestData.TypeA,
                Array.Empty<string>())),
            "E_REG_INVALID_CANONICAL_NAME",
            "invalid canonical scheme name '" + name + "'");
    }

    [Test]
    public void RegisterTransport_WithDuplicateCanonicalNameDifferentCase_ThrowsDuplicateCanonicalName()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "Alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>())),
            "E_REG_DUPLICATE_CANONICAL_NAME",
            "canonical scheme 'alpha' is already registered");
    }

    [Test]
    public void RegisterTransport_WithInvalidParameterName_ThrowsInvalidParameterName()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStringParam("1input"))),
            "E_REG_INVALID_PARAM_NAME",
            "invalid parameter name '1input'");
    }

    [Test]
    public void RegisterTransport_WithDuplicateParameterNamesDifferentCase_ThrowsDuplicateParameterName()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStringParam("input"),
                RegistryTestData.CreateStringParam("INPUT"))),
            "E_REG_DUPLICATE_PARAM_NAME",
            "parameter 'input' is declared more than once in scheme 'alpha'");
    }

    [Test]
    public void RegisterTransport_WithEmptyOneOfTypeSet_ThrowsEmptyTypeSet()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateTransportParam("input", RegistryTestData.OneOf()))),
            "E_REG_EMPTY_TYPE_SET",
            "transport type set must contain at least one type");
    }

    [Test]
    public void RegisterTransport_WithEmptyExactlyTypeSet_ThrowsEmptyTypeSet()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        var empty = new TransportTypeSet(TransportTypeSetKind.Exactly, Array.Empty<Pontifex.TransportType>());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateTransportParam("input", empty))),
            "E_REG_EMPTY_TYPE_SET",
            "transport type set must contain at least one type");
    }

    [Test]
    public void RegisterTransport_WithNegativeMinimum_ThrowsInvalidListRangeWithUnboundedMaximum()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam(
                    "items",
                    RegistryTestData.AnySet(),
                    min: -1))),
            "E_REG_INVALID_LIST_RANGE",
            "invalid transport list range: min -1, max unbounded");
    }

    [Test]
    public void RegisterTransport_WithMaximumBelowMinimum_ThrowsInvalidListRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam(
                    "items",
                    RegistryTestData.AnySet(),
                    min: 2,
                    max: 1))),
            "E_REG_INVALID_LIST_RANGE",
            "invalid transport list range: min 2, max 1");
    }

    [Test]
    public void RegisterTransport_NormalizesIntegerAndDecimalMetadataInDeclarationOrder()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "ALPHA",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateIntegerParam("COUNT", -5, 5, positional: true),
            RegistryTestData.CreateDecimalParam("Ratio", -1.5m, 1.5m),
            RegistryTestData.CreateStructParam(
                "Config",
                RegistryTestData.Schema(
                    RegistryTestData.CreateIntegerField("Count", 0, 10, required: true),
                    RegistryTestData.CreateDecimalField("Ratio", -2.5m, 2.5m)))));

        var parameters = builder.Build().FindCanonical("alpha")!.Params;

        Assert.That(
            parameters.Select(p => p.Name),
            Is.EqualTo(new[] { "count", "ratio", "config" }));

        var integer = (IntegerParam)parameters[0];
        Assert.That(integer.Type.Min, Is.EqualTo(-5L));
        Assert.That(integer.Type.Max, Is.EqualTo(5L));

        var decimalParameter = (DecimalParam)parameters[1];
        Assert.That(decimalParameter.Type.Min, Is.EqualTo(-1.5m));
        Assert.That(decimalParameter.Type.Max, Is.EqualTo(1.5m));

        var schema = ((StructParam)parameters[2]).Schema;
        Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "count", "ratio" }));
        Assert.That(((IntegerField)schema.Fields[0]).Type.Min, Is.EqualTo(0L));
        Assert.That(((IntegerField)schema.Fields[0]).Type.Max, Is.EqualTo(10L));
        Assert.That(((DecimalField)schema.Fields[1]).Type.Min, Is.EqualTo(-2.5m));
        Assert.That(((DecimalField)schema.Fields[1]).Type.Max, Is.EqualTo(2.5m));
    }

    [Test]
    public void RegisterTransport_WithInvertedIntegerParameterRange_ThrowsInvalidIntegerRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateIntegerParam("count", 5, 1))),
            "E_REG_INVALID_INTEGER_RANGE",
            "invalid integer range: min 5, max 1");
    }

    [Test]
    public void RegisterTransport_WithInvertedDecimalParameterRange_FormatsCanonicalBounds()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateDecimalParam("ratio", 2.50m, 1.50m))),
            "E_REG_INVALID_DECIMAL_RANGE",
            "invalid decimal range: min 2.5, max 1.5");
    }

    [Test]
    public void RegisterTransport_WithInvertedIntegerFieldRange_ThrowsInvalidIntegerRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateIntegerField("count", 2, 1))))),
            "E_REG_INVALID_INTEGER_RANGE",
            "invalid integer range: min 2, max 1");
    }

    [Test]
    public void RegisterTransport_WithInvertedDecimalFieldRange_ThrowsInvalidDecimalRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateDecimalField("ratio", 2, 1))))),
            "E_REG_INVALID_DECIMAL_RANGE",
            "invalid decimal range: min 2, max 1");
    }

    [Test]
    public void RegisterTransport_NormalizesArrayFieldMetadata()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateStructParam(
                "Config",
                RegistryTestData.Schema(
                    RegistryTestData.CreateStringArrayField("Names", 0, 2, validate: value => null),
                    RegistryTestData.CreateArrayField(
                        "Counts",
                        new IntegerType(-1, 1),
                        1,
                        3,
                        required: true),
                    RegistryTestData.CreateArrayField(
                        "Ratios",
                        new DecimalType(-1.5m, 1.5m),
                        0,
                        null)))));

        var schema = ((StructParam)builder.Build().FindCanonical("alpha")!.Params[0]).Schema;

        Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "names", "counts", "ratios" }));
        var names = (ArrayField)schema.Fields[0];
        Assert.That(names.ElementType, Is.InstanceOf<StringType>());
        Assert.That(names.Min, Is.EqualTo(0));
        Assert.That(names.Max, Is.EqualTo(2));
        Assert.That(((StringType)names.ElementType).Validate, Is.Not.Null);

        var counts = (ArrayField)schema.Fields[1];
        Assert.That(counts.ElementType, Is.InstanceOf<IntegerType>());
        Assert.That(((IntegerType)counts.ElementType).Min, Is.EqualTo(-1L));
        Assert.That(((IntegerType)counts.ElementType).Max, Is.EqualTo(1L));
        Assert.That(counts.Min, Is.EqualTo(1));
        Assert.That(counts.Max, Is.EqualTo(3));

        var ratios = (ArrayField)schema.Fields[2];
        Assert.That(ratios.ElementType, Is.InstanceOf<DecimalType>());
        Assert.That(((DecimalType)ratios.ElementType).Min, Is.EqualTo(-1.5m));
        Assert.That(((DecimalType)ratios.ElementType).Max, Is.EqualTo(1.5m));
        Assert.That(ratios.Max, Is.Null);
    }

    [Test]
    public void RegisterTransport_WithNegativeArrayMinimum_ThrowsInvalidArrayRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateArrayField(
                            "values",
                            new IntegerType(0, 1),
                            min: -1))))),
            "E_REG_INVALID_ARRAY_RANGE",
            "invalid array range: min -1, max unbounded");
    }

    [Test]
    public void RegisterTransport_WithArrayMaximumBelowMinimum_ThrowsInvalidArrayRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateArrayField(
                            "values",
                            new IntegerType(0, 1),
                            min: 2,
                            max: 1))))),
            "E_REG_INVALID_ARRAY_RANGE",
            "invalid array range: min 2, max 1");
    }

    [Test]
    public void RegisterTransport_WithUnsortedIntegerArrayElementRange_ThrowsInvalidIntegerRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateArrayField(
                            "values",
                            new IntegerType(5, 1),
                            min: 0))))),
            "E_REG_INVALID_INTEGER_RANGE",
            "invalid integer range: min 5, max 1");
    }

    [Test]
    public void RegisterTransport_WithUnsortedDecimalArrayElementRange_ThrowsInvalidDecimalRange()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateArrayField(
                            "values",
                            new DecimalType(2.5m, 1.5m),
                            min: 0))))),
            "E_REG_INVALID_DECIMAL_RANGE",
            "invalid decimal range: min 2.5, max 1.5");
    }

    [Test]
    public void RegisterTransport_WithInvalidArrayRangeAndElementRange_ReportsArrayRangeFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(
                        RegistryTestData.CreateArrayField(
                            "values",
                            new IntegerType(5, 1),
                            min: -1))))),
            "E_REG_INVALID_ARRAY_RANGE",
            "invalid array range: min -1, max unbounded");
    }

    [Test]
    public void RegisterTransport_WithInvalidIntegerRangeAndInvalidStructField_ReportsIntegerRangeFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateIntegerParam("count", 5, 1),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(RegistryTestData.CreateStringField("1field"))))),
            "E_REG_INVALID_INTEGER_RANGE",
            "invalid integer range: min 5, max 1");
    }

    [Test]
    public void RegisterTransport_WithLaterScalarRangeAndEarlierListRange_ReportsScalarRangeFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam(
                    "items",
                    RegistryTestData.AnySet(),
                    min: -1),
                RegistryTestData.CreateIntegerParam("count", 5, 1))),
            "E_REG_INVALID_INTEGER_RANGE",
            "invalid integer range: min 5, max 1");
    }

    [Test]
    public void RegisterTransport_WithZeroMinimumAndUnboundedMaximum_Registers()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateListParam("items", RegistryTestData.AnySet(), min: 0)));

        var parameter = (TransportListParam)builder.Build().FindCanonical("alpha")!.Params[0];

        Assert.That(parameter.Min, Is.EqualTo(0));
        Assert.That(parameter.Max, Is.Null);
    }

    [TestCase("1name")]
    [TestCase("na me")]
    [TestCase("")]
    public void RegisterTransport_WithInvalidStructFieldName_ThrowsInvalidStructFieldName(string name)
    {
        var builder = RegistryTestData.NewStandardBuilder();
        var schema = RegistryTestData.Schema(RegistryTestData.CreateStringField(name));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam("config", schema))),
            "E_REG_INVALID_STRUCT_FIELD_NAME",
            "invalid struct field name '" + name + "'");
    }

    [Test]
    public void RegisterTransport_WithInvalidNestedStructFieldName_ThrowsInvalidStructFieldName()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        var nested = RegistryTestData.Schema(RegistryTestData.CreateStringField("1nested"));
        var schema = RegistryTestData.Schema(RegistryTestData.CreateNestedField("outer", nested));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam("config", schema))),
            "E_REG_INVALID_STRUCT_FIELD_NAME",
            "invalid struct field name '1nested'");
    }

    [Test]
    public void RegisterTransport_WithDuplicateStructFieldsDifferentCase_ThrowsDuplicateStructField()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        var schema = RegistryTestData.Schema(
            RegistryTestData.CreateStringField("name"),
            RegistryTestData.CreateStringField("NAME"));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam("config", schema))),
            "E_REG_DUPLICATE_STRUCT_FIELD",
            "struct field 'name' is declared more than once");
    }

    [Test]
    public void RegisterTransport_WithSharedStructSchema_NormalizesItOnceAndSharesTheInstance()
    {
        var schema = RegistryTestData.Schema(RegistryTestData.CreateStringField("Name"));
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateStructParam("first", schema),
            RegistryTestData.CreateStructParam("second", schema)));

        var parameters = builder.Build().FindCanonical("alpha")!.Params;
        var first = ((StructParam)parameters[0]).Schema;
        var second = ((StructParam)parameters[1]).Schema;

        Assert.That(first, Is.SameAs(second));
        Assert.That(first.Fields[0].Name, Is.EqualTo("name"));
    }

    [Test]
    public void RegisterTransport_WithCyclicStructSchema_CompletesAndNormalizesTheCycle()
    {
        var fields = new StructField[1];
        var cyclicSchema = new StructSchema(fields);
        fields[0] = new NestedStructField("Self", false, cyclicSchema);

        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateStructParam("config", cyclicSchema)));

        var schema = ((StructParam)builder.Build().FindCanonical("alpha")!.Params[0]).Schema;
        var field = (NestedStructField)schema.Fields[0];

        Assert.That(field.Name, Is.EqualTo("self"));
        Assert.That(field.Schema, Is.SameAs(schema));
    }

    [Test]
    public void RegisterTransport_WithAliasEqualToOwnCanonicalName_IgnoresTheAlias()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "ALPHA", "alpha" }));

        Assert.That(builder.Build().FindCandidates("alpha"), Has.Count.EqualTo(1));
    }

    [Test]
    public void RegisterTransport_WithDuplicateAliasesInOneRegistration_RegistersThemOnce()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared", "SHARED", "Shared" }));

        Assert.That(builder.Build().FindCandidates("shared"), Has.Count.EqualTo(1));
    }

    [Test]
    public void RegisterTransport_WithSameAliasForDifferentTypes_AllowsBothAliases()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            new[] { "shared" }));

        var candidates = builder.Build().FindCandidates("shared");

        Assert.That(candidates.Select(c => c.Name), Is.EqualTo(new[] { "alpha", "beta" }));
        Assert.That(candidates.Select(c => c.Type), Is.EqualTo(new[]
        {
            RegistryTestData.TypeA,
            RegistryTestData.TypeB
        }));
    }

    [Test]
    public void RegisterTransport_WithSameAliasForSameType_ThrowsDuplicateAliasForType()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "beta",
                RegistryTestData.TypeA,
                new[] { "SHARED" })),
            "E_REG_DUPLICATE_ALIAS_FOR_TYPE",
            "alias 'shared' for transport type TypeA is already registered");
    }

    [Test]
    public void RegisterTransport_WithAliasEqualToAnotherCanonicalScheme_AllowsIt()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "beta" }));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeB,
            Array.Empty<string>()));

        Assert.That(
            builder.Build().FindCandidates("beta").Select(c => c.Name),
            Is.EqualTo(new[] { "beta", "alpha" }));
    }

    [Test]
    public void RegisterTransport_WithAliasTypeEqualToCanonicalType_AllowsCanonicalOverlap()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));
        builder.RegisterTransport(RegistryTestData.Registration(
            "beta",
            RegistryTestData.TypeA,
            new[] { "alpha" }));

        var candidates = builder.Build().FindCandidates("alpha");

        Assert.That(candidates.Select(c => c.Name), Is.EqualTo(new[] { "alpha", "beta" }));
        Assert.That(candidates.Select(c => c.Type), Is.EqualTo(new[]
        {
            RegistryTestData.TypeA,
            RegistryTestData.TypeA
        }));
    }

    [Test]
    public void RegisterTransport_WithConstructorTypeAbsentFromCatalog_ThrowsTypeNotInCatalog()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.UnregisteredType,
                Array.Empty<string>())),
            "E_REG_TYPE_NOT_IN_CATALOG",
            "transport type " + RegistryTestData.UnregisteredType + " is not present in the type catalog");
    }

    [Test]
    public void RegisterTransport_WithTransportExpectedTypeAbsentFromCatalog_ThrowsTypeNotInCatalog()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateTransportParam(
                    "input",
                    RegistryTestData.Exactly(RegistryTestData.UnregisteredType)))),
            "E_REG_TYPE_NOT_IN_CATALOG",
            "transport type " + RegistryTestData.UnregisteredType + " is not present in the type catalog");
    }

    [Test]
    public void RegisterTransport_WithListExpectedTypeAbsentFromCatalog_ThrowsTypeNotInCatalog()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam(
                    "items",
                    RegistryTestData.OneOf(RegistryTestData.TypeA, RegistryTestData.UnregisteredType)))),
            "E_REG_TYPE_NOT_IN_CATALOG",
            "transport type " + RegistryTestData.UnregisteredType + " is not present in the type catalog");
    }

    [Test]
    public void RegisterTransport_WithAnyExpectedSet_DoesNotRequireCatalogMembership()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>(),
            RegistryTestData.CreateTransportParam("input", RegistryTestData.AnySet())));

        Assert.That(builder.Build().FindCanonical("alpha"), Is.Not.Null);
    }

    [Test]
    public void RegisterTransport_WithInvalidCanonicalNameAmongOtherErrors_ReportsCanonicalNameFirst()
    {
        var builder = RegistryTestData.NewBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                RegistryTestData.Constructor(
                    "1alpha",
                    RegistryTestData.TypeA,
                    RegistryTestData.CreateStringParam("1input")),
                "1alias")),
            "E_REG_INVALID_CANONICAL_NAME",
            "invalid canonical scheme name '1alpha'");
    }

    [Test]
    public void RegisterTransport_WithInvalidParameterAndInvalidAlias_ReportsParameterFirst()
    {
        var builder = RegistryTestData.NewBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                RegistryTestData.Constructor(
                    "alpha",
                    RegistryTestData.TypeA,
                    RegistryTestData.CreateStringParam("1input")),
                "1alias")),
            "E_REG_INVALID_PARAM_NAME",
            "invalid parameter name '1input'");
    }

    [Test]
    public void RegisterTransport_WithInvalidAliasAndDuplicateCanonical_ReportsAliasFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "ALPHA",
                RegistryTestData.TypeA,
                new[] { "1alias" })),
            "E_REG_INVALID_ALIAS_NAME",
            "invalid alias name '1alias'");
    }

    [Test]
    public void RegisterTransport_WithDuplicateCanonicalAndDuplicateParameters_ReportsDuplicateCanonicalFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "ALPHA",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStringParam("input"),
                RegistryTestData.CreateStringParam("INPUT"))),
            "E_REG_DUPLICATE_CANONICAL_NAME",
            "canonical scheme 'alpha' is already registered");
    }

    [Test]
    public void RegisterTransport_WithDuplicateParameterAndEmptyTypeSet_ReportsDuplicateParameterFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateTransportParam("INPUT", RegistryTestData.OneOf()),
                RegistryTestData.CreateStringParam("input"))),
            "E_REG_DUPLICATE_PARAM_NAME",
            "parameter 'input' is declared more than once in scheme 'alpha'");
    }

    [Test]
    public void RegisterTransport_WithEmptyTypeSetAndInvalidListRange_ReportsEmptyTypeSetFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam(
                    "items",
                    RegistryTestData.OneOf(),
                    min: -1))),
            "E_REG_EMPTY_TYPE_SET",
            "transport type set must contain at least one type");
    }

    [Test]
    public void RegisterTransport_WithInvalidListRangeAndInvalidStructField_ReportsListRangeFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam(
                    "items",
                    RegistryTestData.AnySet(),
                    min: -1),
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(RegistryTestData.CreateStringField("1field"))))),
            "E_REG_INVALID_LIST_RANGE",
            "invalid transport list range: min -1, max unbounded");
    }

    [Test]
    public void RegisterTransport_WithInvalidStructFieldAndDuplicateAliasForType_ReportsStructFieldFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "beta",
                RegistryTestData.TypeA,
                new[] { "shared" },
                RegistryTestData.CreateStructParam(
                    "config",
                    RegistryTestData.Schema(RegistryTestData.CreateStringField("1field"))))),
            "E_REG_INVALID_STRUCT_FIELD_NAME",
            "invalid struct field name '1field'");
    }

    [Test]
    public void RegisterTransport_WithDuplicateAliasAndExpectedTypeAbsent_ReportsDuplicateAliasFirst()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            new[] { "shared" }));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "beta",
                RegistryTestData.TypeA,
                new[] { "shared" },
                RegistryTestData.CreateTransportParam(
                    "input",
                    RegistryTestData.Exactly(RegistryTestData.UnregisteredType)))),
            "E_REG_DUPLICATE_ALIAS_FOR_TYPE",
            "alias 'shared' for transport type TypeA is already registered");
    }

    [Test]
    public void RegisterTransport_WithConstructorAndExpectedTypesAbsent_ReportsConstructorTypeFirst()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.UnregisteredType,
                Array.Empty<string>(),
                RegistryTestData.CreateTransportParam(
                    "input",
                    RegistryTestData.Exactly(RegistryTestData.TypeB)))),
            "E_REG_TYPE_NOT_IN_CATALOG",
            "transport type " + RegistryTestData.UnregisteredType + " is not present in the type catalog");
    }

    [Test]
    public void RegisterTransport_WithTwoInvalidParameterNames_ReportsFirstInDeclarationOrder()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStringParam("1first"),
                RegistryTestData.CreateStringParam("2second"))),
            "E_REG_INVALID_PARAM_NAME",
            "invalid parameter name '1first'");
    }

    [Test]
    public void RegisterTransport_WithTwoInvalidAliases_ReportsFirstInInputOrder()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                new[] { "1first", "2second" })),
            "E_REG_INVALID_ALIAS_NAME",
            "invalid alias name '1first'");
    }

    [Test]
    public void RegisterTransport_WithTwoInvalidStructFieldNames_ReportsFirstInSchemaOrder()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        var schema = RegistryTestData.Schema(
            RegistryTestData.CreateStringField("1first"),
            RegistryTestData.CreateStringField("2second"));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam("config", schema))),
            "E_REG_INVALID_STRUCT_FIELD_NAME",
            "invalid struct field name '1first'");
    }

    [Test]
    public void RegisterTransport_AfterBuild_ThrowsBuilderSealed()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.Build();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>())),
            "E_REG_BUILDER_SEALED",
            "registry builder has already been built");
    }
}
