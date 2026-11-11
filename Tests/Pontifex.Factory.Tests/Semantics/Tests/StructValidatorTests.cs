using Pontifex.Factory.Registry;
using Pontifex.Factory.Semantics;
using Pontifex.Factory.Tests.Semantics.Harness;

namespace Pontifex.Factory.Tests.Semantics;

[TestFixture]
[Category("Factory.Semantics")]
public sealed class StructValidatorTests
{
    [Test]
    public void Validate_ForProvidedFields_ReturnsBindingsInSchemaOrder()
    {
        var fields = SemanticsAssertions.ValidateStruct(
            "{nested={a=1}, name=x}",
            StructySchema());

        Assert.That(fields.Select(f => f.Field.Name), Is.EqualTo(new[] { "name", "nested" }));
        Assert.That(((StringFieldValue)fields[0].Value).Text, Is.EqualTo("x"));
    }

    [Test]
    public void Validate_ForUnknownField_ReportsRawLeafPathAndSchemaExpectation()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, extra=1}",
            StructySchema(),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'extra' in struct for parameter 'config'; one of: 'name', 'nested'");
    }

    [Test]
    public void Validate_ForUnknownNestedField_ReportsFullPathAndNestedExpectation()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, nested={unknown=1}}",
            StructySchema(),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'nested.unknown' in struct for parameter 'config'; one of: 'a'");
    }

    [Test]
    public void Validate_ForUnknownField_PreservesRawLeafSpelling()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, nested={UNKNOWN=1}}",
            StructySchema(),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'nested.UNKNOWN' in struct for parameter 'config'; one of: 'a'");
    }

    [Test]
    public void Validate_ForDuplicateField_ReportsCanonicalPath()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, NAME=y}",
            StructySchema(),
            "E_SEM_DUPLICATE_STRUCT_FIELD",
            "field 'name' is specified more than once");
    }

    [Test]
    public void Validate_ForDuplicateNestedField_ReportsFullPath()
    {
        SemanticsAssertions.ValidateStructError(
            "{outer={inner={value=a, VALUE=b}}}",
            DeepSchema(),
            "E_SEM_DUPLICATE_STRUCT_FIELD",
            "field 'outer.inner.value' is specified more than once");
    }

    [Test]
    public void Validate_ForMissingRequiredField_ReportsPathAndParameter()
    {
        SemanticsAssertions.ValidateStructError(
            "{}",
            StructySchema(),
            "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
            "missing required field 'name' in struct for parameter 'config'");
    }

    [Test]
    public void Validate_ForMissingNestedRequiredField_ReportsFullPath()
    {
        SemanticsAssertions.ValidateStructError(
            "{outer={inner={}}}",
            DeepSchema(),
            "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
            "missing required field 'outer.inner.value' in struct for parameter 'config'");
    }

    [Test]
    public void Validate_ChecksMissingRequiredFieldsBeforeProvidedFieldKinds()
    {
        SemanticsAssertions.ValidateStructError(
            "{a=alpha(beta(x))}",
            PairSchema(),
            "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
            "missing required field 'b' in struct for parameter 'config'");
    }

    [Test]
    public void Validate_ReportsUnknownFieldBeforeMissingRequiredField()
    {
        SemanticsAssertions.ValidateStructError(
            "{x=1}",
            PairSchema(),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'x' in struct for parameter 'config'; one of: 'a', 'b'");
    }

    [Test]
    public void Validate_ForStringFieldReceivingTransport_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=alpha(beta(x))}",
            StructySchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'name' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForStringFieldReceivingStruct_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateStructError(
            "{name={}}",
            StructySchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'name' expects a scalar value, got struct");
    }

    [Test]
    public void Validate_ForNestedStructFieldReceivingScalar_ReportsExpectedStruct()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, nested=1}",
            StructySchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_STRUCT",
            "field 'nested' expects a struct, got scalar");
    }

    [Test]
    public void Validate_ForNestedStringFieldReceivingTransport_ReportsFullPath()
    {
        SemanticsAssertions.ValidateStructError(
            "{outer={inner={value=beta(x)}}}",
            DeepSchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'outer.inner.value' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForFieldValidatorFailure_ReportsFieldPathAndParameter()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=bad}",
            FieldValidatedSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'name' of parameter 'config' is invalid: field must not be 'bad'");
    }

    [Test]
    public void Validate_ForThrowingFieldValidator_ReportsValidationFailed()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=ok, strict=x}",
            FieldValidatedSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'strict' of parameter 'config' is invalid: validation failed");
    }

    [Test]
    public void Validate_ForEmptyFieldValidatorReason_ReportsValidationFailed()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=ok, nested={a=x}}",
            FieldValidatedSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'nested.a' of parameter 'config' is invalid: validation failed");
    }

    [Test]
    public void Validate_ForEmptySchemaWithNoFields_AcceptsEmptyStruct()
    {
        var fields = SemanticsAssertions.ValidateStruct("{}", EmptySchema());

        Assert.That(fields, Is.Empty);
    }

    [Test]
    public void Validate_ForEmptySchemaWithProvidedField_ReportsNoFieldsExpectation()
    {
        SemanticsAssertions.ValidateStructError(
            "{x=1}",
            EmptySchema(),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'x' in struct for parameter 'config'; the struct has no fields");
    }

    [Test]
    public void Validate_ForNestedStruct_ProducesNestedStructFieldValue()
    {
        var fields = SemanticsAssertions.ValidateStruct(
            "{outer={inner={value=v}}}",
            DeepSchema());
        var outer = (NestedStructFieldValue)fields[0].Value;
        var inner = (NestedStructFieldValue)outer.Fields[0].Value;
        var value = (StringFieldValue)inner.Fields[0].Value;

        Assert.That(fields[0].Field.Name, Is.EqualTo("outer"));
        Assert.That(outer.Fields[0].Field.Name, Is.EqualTo("inner"));
        Assert.That(inner.Fields[0].Field.Name, Is.EqualTo("value"));
        Assert.That(value.Text, Is.EqualTo("v"));
    }

    [Test]
    public void Validate_BindsFieldsToSchemaFieldInstances()
    {
        var schema = StructySchema();
        var fields = SemanticsAssertions.ValidateStruct("{name=x, nested={a=1}}", schema);

        Assert.That(fields, Has.Count.EqualTo(schema.Fields.Count));
        for (var i = 0; i < fields.Count; i++)
        {
            Assert.That(ReferenceEquals(fields[i].Field, schema.Fields[i]), Is.True);
        }
    }

    [Test]
    public void Validate_ForCaseInsensitiveFieldName_UsesCanonicalSchemaField()
    {
        var schema = StructySchema();
        var fields = SemanticsAssertions.ValidateStruct("{NAME=x}", schema);

        Assert.That(fields, Has.Count.EqualTo(1));
        Assert.That(fields[0].Field.Name, Is.EqualTo("name"));
        Assert.That(ReferenceEquals(fields[0].Field, schema.Fields[0]), Is.True);
    }

    private static StructSchema StructySchema()
    {
        return SchemaOf("structy");
    }

    private static StructSchema DeepSchema()
    {
        return SchemaOf("deep");
    }

    private static StructSchema PairSchema()
    {
        return SchemaOf("pair");
    }

    private static StructSchema EmptySchema()
    {
        return SchemaOf("emptyStruct");
    }

    private static StructSchema FieldValidatedSchema()
    {
        return SchemaOf("fieldValidated");
    }

    private static StructSchema SchemaOf(string scheme)
    {
        var constructor = SemanticsTestData.Canonical(scheme);
        return ((StructParam)SemanticsTestData.FindParam(constructor, "config")).Schema;
    }
}
