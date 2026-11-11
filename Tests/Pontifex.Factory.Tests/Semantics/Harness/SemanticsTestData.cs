using System;
using Pontifex;
using Pontifex.Factory.Internal;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Parsing.Internal;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Registry.Internal;
using Pontifex.Factory.Semantics;
using Pontifex.Factory.Semantics.Internal;

namespace Pontifex.Factory.Tests.Semantics.Harness;

// Hand-written metadata, parsing, and resolver construction helpers for the
// semantics suite. The standard registry mirrors parsing.md §14 plus a few
// explicitly named harness extensions needed for contract branches that the
// §14 table does not reach (empty parameter/field expectations, required-field
// ordering, parameter-level empty and throwing validators, and an alias group
// with a single candidate). No mocking library is used.
internal static class SemanticsTestData
{
    public static readonly TransportType TypeA = TransportType.RawReliableAck;
    public static readonly TransportType TypeB = TransportType.RawReliableNoAck;
    public static readonly TransportType TypeC = TransportType.RawUnreliableAck;

    public const string ParameterValidatorReason = "value must not be 'bad'";
    public const string FieldValidatorReason = "field must not be 'bad'";

    public static readonly ScalarValidator NotBadValidator =
        value => value == "bad" ? ParameterValidatorReason : null;

    public static readonly ScalarValidator NotBadFieldValidator =
        value => value == "bad" ? FieldValidatorReason : null;

    public static readonly ScalarValidator ThrowingValidator =
        _ => throw new InvalidOperationException("validator must not throw");

    public static readonly ScalarValidator EmptyReasonValidator =
        _ => string.Empty;

    private static readonly IRegistry Standard = NewStandardBuilder().Build();
    private static readonly IRegistry AlphaOnly = NewAlphaOnlyBuilder().Build();

    public static IRegistry StandardRegistry()
    {
        return Standard;
    }

    public static IRegistry AlphaOnlyRegistry()
    {
        return AlphaOnly;
    }

    public static FormulaResolver StandardResolver()
    {
        return new FormulaResolver(Standard);
    }

    public static FormulaResolver AlphaOnlyResolver()
    {
        return new FormulaResolver(AlphaOnly);
    }

    // Registry lookup takes an already-normalized lowercase scheme; tests may
    // spell canonical metadata names naturally.
    public static TransportConstructorInfo Canonical(string name)
    {
        return Standard.FindCanonical(NameNormalizer.Normalize(name))!;
    }

    public static TransportTypeSet AnySet()
    {
        return new TransportTypeSet(TransportTypeSetKind.Any, Array.Empty<TransportType>());
    }

    public static TransportTypeSet Exactly(TransportType type)
    {
        return new TransportTypeSet(TransportTypeSetKind.Exactly, new[] { type });
    }

    public static TransportTypeSet OneOf(params TransportType[] types)
    {
        return new TransportTypeSet(TransportTypeSetKind.OneOf, types);
    }

    public static TransportConstructorInfo Constructor(
        string name,
        TransportType type,
        params Param[] parameters)
    {
        return new TransportConstructorInfo(name, type, parameters, Array.Empty<string>());
    }

    public static void Register(
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

    public static Param CreateStringParam(
        string name,
        bool required = false,
        bool positional = false,
        ScalarValidator? validate = null)
    {
        return new StringParam(name, required, positional, validate);
    }

    public static Param CreateTransportParam(
        string name,
        TransportTypeSet expected,
        bool required = false,
        bool positional = false)
    {
        return new TransportParam(name, required, positional, expected);
    }

    public static Param CreateListParam(
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

    public static Param CreateStructParam(
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

    public static StructField CreateStringField(
        string name,
        bool required = false,
        ScalarValidator? validate = null)
    {
        return new StringField(name, required, validate);
    }

    public static StructField CreateNestedField(
        string name,
        StructSchema schema,
        bool required = false)
    {
        return new NestedStructField(name, required, schema);
    }

    public static TransportRegistryBuilder NewBuilder()
    {
        var builder = new TransportRegistryBuilder();
        builder.RegisterType(new TransportTypeInfo(TypeA, "TypeA"));
        builder.RegisterType(new TransportTypeInfo(TypeB, "TypeB"));
        builder.RegisterType(new TransportTypeInfo(TypeC, "TypeC"));
        return builder;
    }

    public static TransportRegistryBuilder NewAlphaOnlyBuilder()
    {
        var builder = NewBuilder();
        Register(
            builder,
            "alpha",
            TypeA,
            Array.Empty<string>(),
            CreateTransportParam("input", AnySet(), required: true, positional: true));
        return builder;
    }

    // parsing.md §14 registry shape with the alias contract:
    // - `alpha` is a canonical scheme for TypeA and also an alias for `beta`.
    // - `shared` is an alias for both `beta` (TypeB) and `gamma` (TypeC).
    // Harness extensions are marked below.
    public static TransportRegistryBuilder NewStandardBuilder()
    {
        var builder = NewBuilder();

        Register(
            builder,
            "alpha",
            TypeA,
            new[] { "multi" },
            CreateTransportParam("input", AnySet(), required: true, positional: true),
            CreateStringParam("mode", positional: true),
            CreateStringParam("timeout"));

        Register(
            builder,
            "beta",
            TypeB,
            new[] { "alpha", "shared", "multi", "onlybeta" },
            CreateStringParam("value", required: true, positional: true));

        Register(
            builder,
            "gamma",
            TypeC,
            new[] { "shared", "multi" },
            CreateListParam(
                "items",
                AnySet(),
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
            CreateTransportParam(
                "input",
                Exactly(TypeB),
                required: true,
                positional: true));

        Register(
            builder,
            "structy",
            TypeA,
            Array.Empty<string>(),
            CreateStructParam(
                "config",
                Schema(
                    CreateStringField("name", required: true),
                    CreateNestedField("nested", Schema(CreateStringField("a")))),
                required: true,
                positional: true));

        Register(
            builder,
            "validated",
            TypeB,
            Array.Empty<string>(),
            CreateStringParam("value", required: true, positional: true, validate: NotBadValidator));

        Register(
            builder,
            "mixed",
            TypeA,
            Array.Empty<string>(),
            CreateStringParam("flag"),
            CreateTransportParam("input", AnySet(), required: true, positional: true));

        Register(
            builder,
            "opt",
            TypeA,
            Array.Empty<string>(),
            CreateStringParam("a", positional: true),
            CreateStringParam("b", positional: true));

        Register(
            builder,
            "bag",
            TypeA,
            Array.Empty<string>(),
            CreateListParam("items", AnySet(), min: 0, positional: true));

        Register(
            builder,
            "emptyStruct",
            TypeA,
            Array.Empty<string>(),
            CreateStructParam("config", Schema(), required: true, positional: true));

        Register(
            builder,
            "single",
            TypeA,
            Array.Empty<string>(),
            CreateListParam(
                "item",
                AnySet(),
                min: 1,
                max: 1,
                required: true,
                positional: true));

        Register(
            builder,
            "dual",
            TypeA,
            Array.Empty<string>(),
            CreateTransportParam(
                "input",
                OneOf(TypeA, TypeB),
                required: true,
                positional: true));

        Register(
            builder,
            "deep",
            TypeA,
            Array.Empty<string>(),
            CreateStructParam(
                "config",
                Schema(CreateNestedField(
                    "outer",
                    Schema(CreateNestedField(
                        "inner",
                        Schema(CreateStringField("value", required: true)),
                        required: true)),
                    required: true)),
                required: true,
                positional: true));

        // Harness extension: alias-only `shared` group resolved by an expected
        // TypeC.
        Register(
            builder,
            "needsC",
            TypeC,
            Array.Empty<string>(),
            CreateTransportParam("input", Exactly(TypeC), required: true, positional: true));

        // Harness extension: a scheme with no parameters and an empty struct
        // schema, used for empty expectation messages.
        Register(builder, "empty", TypeA, Array.Empty<string>());

        // Harness extension: required-field ordering independent of field value
        // validation.
        Register(
            builder,
            "pair",
            TypeA,
            Array.Empty<string>(),
            CreateStructParam(
                "config",
                Schema(
                    CreateStringField("a", required: true),
                    CreateStringField("b", required: true)),
                required: true,
                positional: true));

        // Harness extension: list elements resolved with a non-Any expected set.
        Register(
            builder,
            "listB",
            TypeC,
            Array.Empty<string>(),
            CreateListParam(
                "items",
                Exactly(TypeB),
                min: 0,
                required: true,
                positional: true));

        // Harness extension: valid, empty-reason, and throwing parameter-field
        // validators in one schema.
        Register(
            builder,
            "fieldValidated",
            TypeA,
            Array.Empty<string>(),
            CreateStructParam(
                "config",
                Schema(
                    CreateStringField("name", required: true, validate: NotBadFieldValidator),
                    CreateStringField("strict", validate: ThrowingValidator),
                    CreateNestedField(
                        "nested",
                        Schema(CreateStringField("a", validate: EmptyReasonValidator)))),
                required: true,
                positional: true));

        // Harness extension: parameter-level empty-reason and throwing
        // validators.
        Register(
            builder,
            "validatedEmpty",
            TypeB,
            Array.Empty<string>(),
            CreateStringParam(
                "value",
                required: true,
                positional: true,
                validate: EmptyReasonValidator));

        Register(
            builder,
            "validatedThrow",
            TypeB,
            Array.Empty<string>(),
            CreateStringParam(
                "value",
                required: true,
                positional: true,
                validate: ThrowingValidator));

        return builder;
    }

    public static AstFormula Parse(string source)
    {
        var result = new FormulaParser().Parse(source);
        Assert.That(
            result.Error,
            Is.Null,
            "the semantics harness expects a syntactically valid formula: " + source);
        Assert.That(result.Formula, Is.Not.Null);
        return result.Formula!;
    }

    public static AstTransportCall Call(string source)
    {
        return Parse(source).Root.Call;
    }

    // Parses a single value through a syntactically valid probe call. Offsets
    // are relative to `probe(...)`; error-catalogue tests use whole formulas so
    // their offsets are natural.
    public static AstValue Value(string valueSource)
    {
        var formula = Parse("probe(" + valueSource + ")");
        Assert.That(formula.Root.Call.Arguments, Has.Count.EqualTo(1));
        return formula.Root.Call.Arguments[0].Value;
    }

    public static AstStruct Struct(string structSource)
    {
        return (AstStruct)Value(structSource);
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
