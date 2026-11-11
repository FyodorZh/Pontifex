using System.Collections.Generic;
using Pontifex.Factory.Semantics;
using Pontifex.Factory.Tests.Semantics.Harness;

namespace Pontifex.Factory.Tests.Semantics;

[TestFixture]
[Category("Factory.Semantics")]
public sealed class SemanticErrorTests
{
    [TestCaseSource(nameof(SemanticErrorCases))]
    public void Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage(
        string source,
        string code,
        int offset,
        string message)
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.StandardResolver(),
            source,
            code,
            offset,
            message);
    }

    [Test]
    public void Resolve_ForUnknownScheme_ReportsSortedKnownSchemesAndPreservesRawSpelling()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.Resolver(SemanticsTestData.SortedNamesRegistry()),
            "NoSuch()",
            "E_SEM_UNKNOWN_SCHEME",
            0,
            "unknown transport scheme 'NoSuch'; known schemes: alpha, mu, zeta");
    }

    [Test]
    public void Resolve_ForUnknownSchemeWhenNoSchemesAreRegistered_ReportsEmptyRegistry()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.Resolver(SemanticsTestData.EmptyRegistry()),
            "nosuch()",
            "E_SEM_UNKNOWN_SCHEME",
            0,
            "unknown transport scheme 'nosuch'; no schemes are registered");
    }

    [Test]
    public void Resolve_ForUnknownTransportType_ReportsSortedKnownTypes()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.Resolver(SemanticsTestData.SortedTypeCatalogRegistry()),
            "alpha(beta(x)) as TypeZ",
            "E_SEM_UNKNOWN_TRANSPORT_TYPE",
            18,
            "unknown transport type 'TypeZ'; known types: TypeA, TypeB, TypeC");
    }

    [Test]
    public void Resolve_ForUnknownTransportTypeWhenNoTypesAreRegistered_ReportsEmptyCatalog()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.Resolver(SemanticsTestData.EmptyRegistry()),
            "alpha() as TypeZ",
            "E_SEM_UNKNOWN_TRANSPORT_TYPE",
            11,
            "unknown transport type 'TypeZ'; no transport types are registered");
    }

    [Test]
    public void Resolve_ChecksUnknownCastTargetBeforeExpectedMismatch()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.StandardResolver(),
            "outer(alpha(beta(x)) as TypeZ)",
            "E_SEM_UNKNOWN_TRANSPORT_TYPE",
            24,
            "unknown transport type 'TypeZ'; known types: TypeA, TypeB, TypeC");
    }

    [Test]
    public void Resolve_ChecksCastExpectedMismatchBeforeResolvingInnerTransport()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.StandardResolver(),
            "outer(nosuch() as TypeC)",
            "E_SEM_CAST_TYPE_MISMATCH",
            18,
            "cast produces transport type TypeC, but expected TypeB");
    }

    [Test]
    public void Resolve_ChecksBindingErrorsBeforeParameterValidation()
    {
        var result = SemanticsTestData.StandardResolver().Resolve(
            SemanticsTestData.Parse("alpha(1, q=1)"),
            SemanticsTestData.AnySet());

        Assert.That(result.Formula, Is.Null);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo("E_SEM_UNKNOWN_PARAM"));
        Assert.That(result.Error.Message, Is.EqualTo(
            "unknown parameter 'q' for scheme 'alpha'; one of: 'input', 'mode', 'timeout'"));
    }

    [Test]
    public void Resolve_ChecksStructBindingErrorsBeforeFieldValueValidation()
    {
        SemanticsAssertions.ResolveError(
            SemanticsTestData.StandardResolver(),
            "structy({name=alpha(beta(x)), extra=1})",
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            30,
            "unknown field 'extra' in struct for parameter 'config'; one of: 'name', 'nested'");
    }

    [Test]
    public void Resolve_RunsTwiceOnTheSameInput_ProducesTheSameError()
    {
        var resolver = SemanticsTestData.StandardResolver();

        var first = resolver.Resolve(SemanticsTestData.Parse("shared(x)"), SemanticsTestData.AnySet());
        var second = resolver.Resolve(SemanticsTestData.Parse("shared(x)"), SemanticsTestData.AnySet());

        Assert.That(first.Formula, Is.Null);
        Assert.That(second.Formula, Is.Null);
        Assert.That(second.Error, Is.EqualTo(first.Error));
    }

    private static IEnumerable<TestCaseData> SemanticErrorCases()
    {
        yield return Case(
            "MissingRequiredParameter",
            "alpha()",
            "E_SEM_MISSING_REQUIRED_PARAM",
            6,
            "missing required parameter 'input' for scheme 'alpha'");

        yield return Case(
            "UnknownParameter",
            "alpha(beta(x), q=1)",
            "E_SEM_UNKNOWN_PARAM",
            15,
            "unknown parameter 'q' for scheme 'alpha'; one of: 'input', 'mode', 'timeout'");

        yield return Case(
            "DuplicateParameter",
            "alpha(beta(x), input=beta(y))",
            "E_SEM_DUPLICATE_PARAM",
            15,
            "parameter 'input' is specified more than once");

        yield return Case(
            "TooManyPositionalArguments",
            "alpha(beta(x), fast, extra)",
            "E_SEM_TOO_MANY_POSITIONAL",
            21,
            "too many positional arguments for scheme 'alpha'; expected at most 2");

        yield return Case(
            "ExpectedTransport",
            "alpha(1)",
            "E_SEM_EXPECTED_TRANSPORT",
            6,
            "parameter 'input' expects a transport, got scalar");

        yield return Case(
            "ExpectedTransportList",
            "gamma(beta(x))",
            "E_SEM_EXPECTED_TRANSPORT_LIST",
            6,
            "parameter 'items' expects a transport list, got transport");

        yield return Case(
            "ExpectedStruct",
            "structy(1)",
            "E_SEM_EXPECTED_STRUCT",
            8,
            "parameter 'config' expects a struct, got scalar");

        yield return Case(
            "ExpectedScalar",
            "beta([alpha(beta(x))])",
            "E_SEM_EXPECTED_SCALAR",
            5,
            "parameter 'value' expects a scalar value, got transport list");

        yield return Case(
            "ListMinimum",
            "gamma([alpha(beta(x))])",
            "E_SEM_LIST_MIN",
            6,
            "transport list for 'items' requires at least 2 element(s), got 1");

        yield return Case(
            "ListMaximum",
            "gamma([alpha(beta(x)), alpha(beta(x)), alpha(beta(x)), alpha(beta(x))])",
            "E_SEM_LIST_MAX",
            6,
            "transport list for 'items' requires at most 3 element(s), got 4");

        yield return Case(
            "ListHomogeneity",
            "gamma([alpha(beta(x)), beta(y)])",
            "E_SEM_LIST_HOMOGENEITY",
            23,
            "transport list for 'items' must contain one transport type; found TypeA, TypeB");

        yield return Case(
            "ListHomogeneityPreservesElementOrder",
            "gamma([beta(y), alpha(beta(x))])",
            "E_SEM_LIST_HOMOGENEITY",
            16,
            "transport list for 'items' must contain one transport type; found TypeB, TypeA");

        yield return Case(
            "NoVariantForExpectedType",
            "outer(gamma([alpha(beta(x)), alpha(beta(x))]))",
            "E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE",
            6,
            "scheme 'gamma' has no variant for expected transport type TypeB; "
            + "available: gamma (TypeC)");

        yield return Case(
            "AmbiguousAliasOnlyScheme",
            "shared(x)",
            "E_SEM_AMBIGUOUS_SCHEME",
            0,
            "scheme 'shared' is ambiguous; candidates: beta (TypeB), gamma (TypeC)");

        yield return Case(
            "AmbiguousAliasGroup",
            "multi(x)",
            "E_SEM_AMBIGUOUS_SCHEME",
            0,
            "scheme 'multi' is ambiguous; candidates: alpha (TypeA), beta (TypeB), gamma (TypeC)");

        yield return Case(
            "ParameterValidatorFailed",
            "validated(bad)",
            "E_SEM_VALIDATOR_FAILED",
            10,
            "parameter 'value' is invalid: value must not be 'bad'");

        yield return Case(
            "ParameterValidatorThrew",
            "validatedThrow(x)",
            "E_SEM_VALIDATOR_FAILED",
            15,
            "parameter 'value' is invalid: validation failed");

        yield return Case(
            "ParameterValidatorReturnedEmptyReason",
            "validatedEmpty(x)",
            "E_SEM_VALIDATOR_FAILED",
            15,
            "parameter 'value' is invalid: validation failed");

        yield return Case(
            "UnknownStructField",
            "structy({name=x, extra=1})",
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            17,
            "unknown field 'extra' in struct for parameter 'config'; one of: 'name', 'nested'");

        yield return Case(
            "UnknownNestedStructField",
            "structy({name=x, nested={unknown=1}})",
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            25,
            "unknown field 'nested.unknown' in struct for parameter 'config'; one of: 'a'");

        yield return Case(
            "DuplicateStructField",
            "structy({name=x, name=y})",
            "E_SEM_DUPLICATE_STRUCT_FIELD",
            17,
            "field 'name' is specified more than once");

        yield return Case(
            "MissingRequiredStructField",
            "structy({})",
            "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
            9,
            "missing required field 'name' in struct for parameter 'config'");

        yield return Case(
            "MissingRequiredNestedStructField",
            "deep({outer={inner={}}})",
            "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
            20,
            "missing required field 'outer.inner.value' in struct for parameter 'config'");

        yield return Case(
            "StructFieldExpectedScalarFromTransport",
            "structy({name=alpha(beta(x))})",
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            14,
            "field 'name' expects a scalar value, got transport");

        yield return Case(
            "StructFieldExpectedScalarFromStruct",
            "structy({name={}})",
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            14,
            "field 'name' expects a scalar value, got struct");

        yield return Case(
            "StructFieldExpectedStruct",
            "structy({name=x, nested=1})",
            "E_SEM_STRUCT_FIELD_EXPECTED_STRUCT",
            24,
            "field 'nested' expects a struct, got scalar");

        yield return Case(
            "NestedStructFieldExpectedScalar",
            "deep({outer={inner={value=beta(x)}}})",
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            26,
            "field 'outer.inner.value' expects a scalar value, got transport");

        yield return Case(
            "StructFieldValidatorFailed",
            "fieldValidated({name=bad})",
            "E_SEM_VALIDATOR_FAILED",
            21,
            "field 'name' of parameter 'config' is invalid: field must not be 'bad'");

        yield return Case(
            "StructFieldValidatorThrew",
            "fieldValidated({name=ok, strict=x})",
            "E_SEM_VALIDATOR_FAILED",
            32,
            "field 'strict' of parameter 'config' is invalid: validation failed");

        yield return Case(
            "NestedStructFieldValidatorReturnedEmptyReason",
            "fieldValidated({name=ok, nested={a=x}})",
            "E_SEM_VALIDATOR_FAILED",
            35,
            "field 'nested.a' of parameter 'config' is invalid: validation failed");

        yield return Case(
            "UnknownTransportType",
            "alpha(beta(x)) as TypeZ",
            "E_SEM_UNKNOWN_TRANSPORT_TYPE",
            18,
            "unknown transport type 'TypeZ'; known types: TypeA, TypeB, TypeC");

        yield return Case(
            "UnknownTransportTypePreservesRawSpelling",
            "alpha(beta(x)) as typez",
            "E_SEM_UNKNOWN_TRANSPORT_TYPE",
            18,
            "unknown transport type 'typez'; known types: TypeA, TypeB, TypeC");

        yield return Case(
            "CastTypeMismatch",
            "outer(alpha(beta(x)) as TypeC)",
            "E_SEM_CAST_TYPE_MISMATCH",
            24,
            "cast produces transport type TypeC, but expected TypeB");

        yield return Case(
            "CastTypeMismatchUnderExactly",
            "outer(beta(x) as TypeA)",
            "E_SEM_CAST_TYPE_MISMATCH",
            17,
            "cast produces transport type TypeA, but expected TypeB");

        yield return Case(
            "CastTypeMismatchUnderOneOf",
            "dual(beta(x) as TypeC)",
            "E_SEM_CAST_TYPE_MISMATCH",
            16,
            "cast produces transport type TypeC, but expected TypeA or TypeB");

        yield return Case(
            "UnknownParameterWhenSchemeHasNoParameters",
            "empty(x=1)",
            "E_SEM_UNKNOWN_PARAM",
            6,
            "unknown parameter 'x' for scheme 'empty'; the scheme has no parameters");

        yield return Case(
            "UnknownStructFieldWhenSchemaHasNoFields",
            "emptyStruct({x=1})",
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            13,
            "unknown field 'x' in struct for parameter 'config'; the struct has no fields");

        yield return Case(
            "MissingRequiredParameterPreservesRawSchemeSpelling",
            "ALPHA()",
            "E_SEM_MISSING_REQUIRED_PARAM",
            6,
            "missing required parameter 'input' for scheme 'ALPHA'");

        yield return Case(
            "UnknownParameterPreservesRawSpelling",
            "alpha(beta(x), Q=1)",
            "E_SEM_UNKNOWN_PARAM",
            15,
            "unknown parameter 'Q' for scheme 'alpha'; one of: 'input', 'mode', 'timeout'");

        yield return Case(
            "MissingRequiredFieldCheckedBeforeFieldKinds",
            "pair({a=alpha(beta(x))})",
            "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
            22,
            "missing required field 'b' in struct for parameter 'config'");
    }

    private static TestCaseData Case(
        string name,
        string source,
        string code,
        int offset,
        string message)
    {
        return new TestCaseData(source, code, offset, message).SetName(name);
    }
}
