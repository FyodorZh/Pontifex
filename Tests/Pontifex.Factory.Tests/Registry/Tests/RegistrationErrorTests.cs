using System;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Registry.Internal;
using Pontifex.Factory.Tests.Registry.Harness;

namespace Pontifex.Factory.Tests.Registry;

// One test per E_REG_* code in parsing.md §13.4, asserting the exact message,
// phase, and absence of an offset.
[TestFixture]
[Category("Factory.Registry")]
public sealed class RegistrationErrorTests
{
    [Test]
    public void RegisterTransport_WithNonIdentifierCanonicalName_ReturnsInvalidCanonicalName()
    {
        var builder = RegistryTestData.NewBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "1Alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>())),
            "E_REG_INVALID_CANONICAL_NAME",
            "invalid canonical scheme name '1Alpha'");
    }

    [Test]
    public void RegisterTransport_WithAlreadyRegisteredCanonicalName_ReturnsDuplicateCanonicalName()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        builder.RegisterTransport(RegistryTestData.Registration(
            "alpha",
            RegistryTestData.TypeA,
            Array.Empty<string>()));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "Alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>())),
            "E_REG_DUPLICATE_CANONICAL_NAME",
            "canonical scheme 'alpha' is already registered");
    }

    [Test]
    public void RegisterTransport_WithNonIdentifierParameterName_ReturnsInvalidParamName()
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
    public void RegisterTransport_WithDuplicateParameterNames_ReturnsDuplicateParamName()
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
    public void RegisterTransport_WithNonIdentifierAlias_ReturnsInvalidAliasName()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                new[] { "1alias" })),
            "E_REG_INVALID_ALIAS_NAME",
            "invalid alias name '1alias'");
    }

    [Test]
    public void RegisterTransport_WithAliasRegisteredForSameType_ReturnsDuplicateAliasForType()
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
                new[] { "shared" })),
            "E_REG_DUPLICATE_ALIAS_FOR_TYPE",
            "alias 'shared' for transport type TypeA is already registered");
    }

    [Test]
    public void RegisterTransport_WithEmptyOneOfSet_ReturnsEmptyTypeSet()
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
    public void RegisterTransport_WithNegativeListMinimum_ReturnsInvalidListRangeWithUnboundedMaximum()
    {
        var builder = RegistryTestData.NewStandardBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateListParam("items", RegistryTestData.AnySet(), min: -1))),
            "E_REG_INVALID_LIST_RANGE",
            "invalid transport list range: min -1, max unbounded");
    }

    [Test]
    public void RegisterTransport_WithMaximumBelowMinimum_ReturnsInvalidListRangeWithNumbers()
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
                    min: 3,
                    max: 2))),
            "E_REG_INVALID_LIST_RANGE",
            "invalid transport list range: min 3, max 2");
    }

    [Test]
    public void RegisterTransport_WithDuplicateStructFieldNames_ReturnsDuplicateStructField()
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
    public void RegisterTransport_WithNonIdentifierStructFieldName_ReturnsInvalidStructFieldName()
    {
        var builder = RegistryTestData.NewStandardBuilder();
        var schema = RegistryTestData.Schema(RegistryTestData.CreateStringField("1name"));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateStructParam("config", schema))),
            "E_REG_INVALID_STRUCT_FIELD_NAME",
            "invalid struct field name '1name'");
    }

    [Test]
    public void RegisterType_WithNonIdentifierName_ReturnsInvalidTypeName()
    {
        var builder = RegistryTestData.NewBuilder();

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterType(RegistryTestData.TypeInfo(RegistryTestData.TypeA, "1Type")),
            "E_REG_INVALID_TYPE_NAME",
            "invalid transport type name '1Type'");
    }

    [Test]
    public void RegisterType_WithDuplicateName_ReturnsDuplicateTypeName()
    {
        var builder = RegistryTestData.NewBuilder(
            RegistryTestData.TypeInfo(RegistryTestData.TypeA, "TypeA"));

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterType(RegistryTestData.TypeInfo(RegistryTestData.TypeB, "typea")),
            "E_REG_DUPLICATE_TYPE_NAME",
            "transport type name 'typea' is already registered");
    }

    [Test]
    public void RegisterTransport_WithConstructorTypeAbsentFromCatalog_ReturnsTypeNotInCatalog()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeB,
                Array.Empty<string>())),
            "E_REG_TYPE_NOT_IN_CATALOG",
            "transport type " + RegistryTestData.TypeB + " is not present in the type catalog");
    }

    [Test]
    public void RegisterTransport_WithExpectedTypeAbsentFromCatalog_ReturnsTypeNotInCatalog()
    {
        var builder = RegistryTestData.NewBuilder(RegistryTestData.TypeAInfo());

        RegistryAssertions.ThrowsRegistration(
            () => builder.RegisterTransport(RegistryTestData.Registration(
                "alpha",
                RegistryTestData.TypeA,
                Array.Empty<string>(),
                RegistryTestData.CreateTransportParam(
                    "input",
                    RegistryTestData.Exactly(RegistryTestData.TypeC)))),
            "E_REG_TYPE_NOT_IN_CATALOG",
            "transport type " + RegistryTestData.TypeC + " is not present in the type catalog");
    }
}
