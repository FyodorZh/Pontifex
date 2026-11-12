using NUnit.Framework;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.Factory.Tests.Canonicalization.Harness;

namespace Pontifex.Factory.Tests.Canonicalization;

[TestFixture]
[Category("Factory.Canonicalization")]
public sealed class CanonicalizationErrorFactoryTests
{
    [TestCase("identity cast node")]
    [TestCase("expected-type membership violation")]
    [TestCase("constructor type mismatch")]
    [TestCase("non-canonical name")]
    [TestCase("struct field order violation")]
    [TestCase("provided parameter order violation")]
    [TestCase("transport type not in catalog")]
    [TestCase("unspecified invariant violation")]
    public void InconsistentTree_ForKnownDetail_FormatsTemplate(string detail)
    {
        var exception = CanonicalizationErrorFactory.InconsistentTree(detail);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_INCONSISTENT_TREE",
            "resolved tree is inconsistent: " + detail);
    }

    [Test]
    public void MissingRequiredParam_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.MissingRequiredParam("alpha", "input");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_MISSING_REQUIRED_PARAM",
            "resolved transport 'alpha' is missing required parameter 'input'");
    }

    [Test]
    public void UnknownParam_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.UnknownParam("alpha", "x");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_UNKNOWN_PARAM",
            "resolved transport 'alpha' contains unknown parameter 'x'");
    }

    [Test]
    public void DuplicateParam_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.DuplicateParam("alpha", "input");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_DUPLICATE_PARAM",
            "resolved transport 'alpha' contains parameter 'input' more than once");
    }

    [Test]
    public void ParamKindMismatch_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.ParamKindMismatch(
            "timeout",
            "alpha",
            "transport",
            "string");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'timeout' of scheme 'alpha' has value kind transport, expected string");
    }

    [Test]
    public void ValueOutOfRange_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.ValueOutOfRange("config.values[2]", "1.6");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'config.values[2]' is out of range: 1.6");
    }

    [Test]
    public void ArrayArity_WithMaximum_FormatsClosedRange()
    {
        var exception = CanonicalizationErrorFactory.ArrayArity("values", "config", 4, 1, 2);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_ARRAY_ARITY",
            "resolved array field 'values' of parameter 'config' has 4 element(s), expected 1..2");
    }

    [Test]
    public void ArrayArity_WithoutMaximum_FormatsAtLeastRange()
    {
        var exception = CanonicalizationErrorFactory.ArrayArity("values", "config", 0, 1, null);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_ARRAY_ARITY",
            "resolved array field 'values' of parameter 'config' has 0 element(s), expected at least 1");
    }

    [Test]
    public void ArrayElementType_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.ArrayElementType(
            "values[2]",
            "config",
            "integer",
            "string");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_ARRAY_ELEMENT_TYPE",
            "resolved array element 'values[2]' of parameter 'config' expects integer, got string");
    }

    [Test]
    public void ListArity_WithMaximum_FormatsClosedRange()
    {
        var exception = CanonicalizationErrorFactory.ListArity("items", "gamma", 4, 2, 3);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_LIST_ARITY",
            "transport list parameter 'items' of scheme 'gamma' has 4 element(s), expected 2..3");
    }

    [Test]
    public void ListArity_WithoutMaximum_FormatsAtLeastRange()
    {
        var exception = CanonicalizationErrorFactory.ListArity("items", "bag", 0, 1, null);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_LIST_ARITY",
            "transport list parameter 'items' of scheme 'bag' has 0 element(s), expected at least 1");
    }

    [Test]
    public void ListHomogeneity_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.ListHomogeneity("items", "gamma");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_LIST_HOMOGENEITY",
            "transport list parameter 'items' of scheme 'gamma' mixes transport types");
    }

    [Test]
    public void StructFieldUnknown_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.StructFieldUnknown(
            "config",
            "structy",
            "nested.extra");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_STRUCT_FIELD_UNKNOWN",
            "struct parameter 'config' of scheme 'structy' contains unknown field 'nested.extra'");
    }

    [Test]
    public void StructFieldMissing_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.StructFieldMissing(
            "config",
            "structy",
            "name");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_STRUCT_FIELD_MISSING",
            "struct parameter 'config' of scheme 'structy' is missing required field 'name'");
    }

    [Test]
    public void StructFieldDuplicate_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.StructFieldDuplicate(
            "config",
            "structy",
            "name");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_STRUCT_FIELD_DUPLICATE",
            "struct parameter 'config' of scheme 'structy' contains field 'name' more than once");
    }

    [Test]
    public void StructFieldKindMismatch_FormatsTemplate()
    {
        var exception = CanonicalizationErrorFactory.StructFieldKindMismatch(
            "nested",
            "config",
            "string",
            "struct");

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'nested' of struct parameter 'config' has value kind string, expected struct");
    }

    [Test]
    public void InvalidCastTarget_UsesTransportTypeToString()
    {
        var exception = CanonicalizationErrorFactory.InvalidCastTarget(
            CanonicalizationTestData.TypeNotInCatalog);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_INVALID_CAST_TARGET",
            "cast target 'RawUnreliableNoAck' is invalid");
    }

    [TestCase('\u0000', "U+0000")]
    [TestCase('\n', "U+000A")]
    [TestCase('\r', "U+000D")]
    [TestCase('\uD800', "U+D800")]
    [TestCase('\uDC00', "U+DC00")]
    public void InvalidScalar_FormatsFirstOffendingCodeUnitAsFourUppercaseHexDigits(
        char codeUnit,
        string formatted)
    {
        var exception = CanonicalizationErrorFactory.InvalidScalar(codeUnit);

        CanonicalizationAssertions.AssertError(
            exception.Error,
            "E_PRN_INVALID_SCALAR",
            "scalar value contains a character that cannot be printed: " + formatted);
    }
}
