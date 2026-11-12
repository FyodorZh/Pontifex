using System;
using Pontifex;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Parsing.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Semantics.Internal;

namespace Pontifex.Factory.Tests.Canonicalization.Harness;

// Hand-written metadata, resolved-tree, parsing, and canonicalizer construction
// helpers for the canonicalization suite. The standard registry mirrors the
// parsing.md §14 registry plus the fixture schemes needed for quoting, empty
// containers, positional-prefix gaps, named-only interleaving, and nested
// structs. No mocking library is used.
internal static class CanonicalizationTestData
{
    public static readonly TransportType TypeA = TransportType.RawReliableAck;
    public static readonly TransportType TypeB = TransportType.RawReliableNoAck;
    public static readonly TransportType TypeC = TransportType.RawUnreliableAck;

    // Deliberately absent from the standard type catalog.
    public static readonly TransportType TypeNotInCatalog = TransportType.RawUnreliableNoAck;

    public static readonly TransportTypeSet AnySetValue =
        new(TransportTypeSetKind.Any, Array.Empty<TransportType>());

    private static readonly IRegistry Standard = NewStandardBuilder().Build();

    public static TransportConstructorInfo Canonical(string name)
    {
        return Standard.FindCanonical(NameNormalizer.Normalize(name))
            ?? throw new ArgumentException("no canonical constructor '" + name + "'", nameof(name));
    }

    public static FormulaCanonicalizer Canonicalizer()
    {
        return new FormulaCanonicalizer(Standard.Types);
    }

    public static TransportTypeSet AnySet()
    {
        return AnySetValue;
    }

    public static TransportTypeSet Exactly(TransportType type)
    {
        return new TransportTypeSet(TransportTypeSetKind.Exactly, new[] { type });
    }

    public static TransportConstructorInfo Constructor(
        string name,
        TransportType type,
        params Param[] parameters)
    {
        return new TransportConstructorInfo(name, type, parameters, Array.Empty<string>());
    }

    public static Param StringParam(
        string name,
        bool required = false,
        bool positional = false,
        ScalarValidator? validate = null)
    {
        return new StringParam(name, required, positional, validate);
    }

    public static Param TransportParam(
        string name,
        TransportTypeSet expected,
        bool required = false,
        bool positional = false)
    {
        return new TransportParam(name, required, positional, expected);
    }

    public static Param ListParam(
        string name,
        TransportTypeSet expected,
        int min = 0,
        int? max = null,
        bool homogeneous = false,
        bool required = false,
        bool positional = false)
    {
        return new TransportListParam(
            name,
            required,
            positional,
            expected,
            homogeneous,
            min,
            max);
    }

    public static Param StructParam(
        string name,
        StructSchema schema,
        bool required = false,
        bool positional = false)
    {
        return new StructParam(name, required, positional, schema);
    }

    public static StructSchema Schema(params StructField[] fields)
    {
        return new StructSchema(fields);
    }

    public static StructField StringField(
        string name,
        bool required = false,
        ScalarValidator? validate = null)
    {
        return new StringField(name, required, validate);
    }

    public static StructField NestedField(
        string name,
        StructSchema schema,
        bool required = false)
    {
        return new NestedStructField(name, required, schema);
    }

    public static ParamBinding Bind(Param parameter, ResolvedValue value)
    {
        return new ParamBinding(parameter, value);
    }

    public static ResolvedTransport Transport(
        TransportConstructorInfo constructor,
        params ParamBinding[] provided)
    {
        return new ResolvedTransport(constructor, provided, new SourceSpan(0, 0));
    }

    public static ResolvedFormula Formula(ResolvedExpr root)
    {
        return new ResolvedFormula(root);
    }

    public static StringValue Scalar(string text)
    {
        return new StringValue(text);
    }

    // A transport parameter value stores the resolved expression directly.
    public static ResolvedExpr Expression(ResolvedExpr expression)
    {
        return expression;
    }

    public static TransportListValue List(params ResolvedExpr[] elements)
    {
        return new TransportListValue(elements);
    }

    public static StructValue Struct(params StructFieldBinding[] fields)
    {
        return new StructValue(fields);
    }

    public static StructFieldBinding Field(StructField field, DataValue value)
    {
        return new StructFieldBinding(field, value);
    }

    public static StringValue StringValue(string text)
    {
        return new StringValue(text);
    }

    public static StructValue NestedValue(params StructFieldBinding[] fields)
    {
        return new StructValue(fields);
    }

    public static AstFormula Parse(string source)
    {
        var result = new FormulaParser().Parse(source);
        Assert.That(
            result.Error,
            Is.Null,
            "the canonicalization harness expects a syntactically valid formula: " + source);
        Assert.That(result.Formula, Is.Not.Null);
        return result.Formula!;
    }

    public static ResolvedFormula Resolve(string source)
    {
        return Resolve(source, AnySetValue);
    }

    public static ResolvedFormula Resolve(string source, TransportTypeSet expected)
    {
        var result = new FormulaResolver(Standard).Resolve(Parse(source), expected);
        Assert.That(
            result.Error,
            Is.Null,
            "the canonicalization harness expects a semantically valid formula: " + source);
        Assert.That(result.Formula, Is.Not.Null);
        return result.Formula!;
    }

    public static string Canonicalize(string source)
    {
        return Canonicalizer().Canonicalize(Resolve(source));
    }

    public static TransportRegistryBuilder NewBuilder()
    {
        var builder = new TransportRegistryBuilder();
        builder.RegisterType(new TransportTypeInfo(TypeA, "TypeA"));
        builder.RegisterType(new TransportTypeInfo(TypeB, "TypeB"));
        builder.RegisterType(new TransportTypeInfo(TypeC, "TypeC"));
        return builder;
    }

    // parsing.md §14 registry shape plus extensions for canonicalization
    // branches the §14 table does not reach.
    public static TransportRegistryBuilder NewStandardBuilder()
    {
        var builder = NewBuilder();

        Register(
            builder,
            "alpha",
            TypeA,
            Array.Empty<string>(),
            TransportParam("input", AnySetValue, required: true, positional: true),
            StringParam("mode", positional: true),
            StringParam("timeout"));

        Register(
            builder,
            "beta",
            TypeB,
            new[] { "alpha" },
            StringParam("value", required: true, positional: true));

        Register(
            builder,
            "gamma",
            TypeC,
            Array.Empty<string>(),
            ListParam(
                "items",
                AnySetValue,
                min: 2,
                max: 3,
                homogeneous: true,
                required: true,
                positional: true));

        Register(
            builder,
            "outer",
            TypeC,
            Array.Empty<string>(),
            TransportParam("input", Exactly(TypeB), required: true, positional: true));

        Register(
            builder,
            "structy",
            TypeA,
            Array.Empty<string>(),
            StructParam(
                "config",
                Schema(
                    StringField("name", required: true),
                    NestedField("nested", Schema(StringField("a")))),
                required: true,
                positional: true));

        Register(
            builder,
            "mixed",
            TypeA,
            Array.Empty<string>(),
            StringParam("flag"),
            TransportParam("input", AnySetValue, required: true, positional: true));

        Register(
            builder,
            "opt",
            TypeA,
            Array.Empty<string>(),
            StringParam("a", positional: true),
            StringParam("b", positional: true));

        Register(
            builder,
            "bag",
            TypeA,
            Array.Empty<string>(),
            ListParam("items", AnySetValue, min: 0, positional: true));

        Register(
            builder,
            "emptyStruct",
            TypeA,
            Array.Empty<string>(),
            StructParam("config", Schema(), required: true, positional: true));

        Register(
            builder,
            "single",
            TypeA,
            Array.Empty<string>(),
            ListParam("item", AnySetValue, min: 1, max: 1, required: true, positional: true));

        Register(
            builder,
            "deep",
            TypeA,
            Array.Empty<string>(),
            StructParam(
                "config",
                Schema(NestedField(
                    "outer",
                    Schema(NestedField(
                        "inner",
                        Schema(StringField("value", required: true)),
                        required: true)),
                    required: true)),
                required: true,
                positional: true));

        return builder;
    }

    private static void Register(
        TransportRegistryBuilder builder,
        string name,
        TransportType type,
        string[] aliases,
        params Param[] parameters)
    {
        builder.RegisterTransport(new TransportRegistration(
            Constructor(name, type, parameters),
            aliases));
    }

    public static Param FindParam(TransportConstructorInfo constructor, string name)
    {
        for (var i = 0; i < constructor.Params.Count; i++)
        {
            if (constructor.Params[i].Name == name)
            {
                return constructor.Params[i];
            }
        }

        throw new ArgumentException("no parameter '" + name + "'", nameof(name));
    }
}
