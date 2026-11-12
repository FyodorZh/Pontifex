using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
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
        Assert.That(((StringValue)fields[0].Value).Text, Is.EqualTo("x"));
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
    public void Validate_ForDuplicateNestedField_ReportsFullPath()
    {
        SemanticsAssertions.ValidateStructError(
            "{outer={inner={value=a, VALUE=b}}}",
            DeepSchema(),
            "E_SEM_DUPLICATE_STRUCT_FIELD",
            "field 'outer.inner.value' is specified more than once");
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
    public void Validate_ReportsBindingErrorsInSourceOrder()
    {
        SemanticsAssertions.ValidateStructError(
            "{extra=1, name=x, NAME=y}",
            StructySchema(),
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'extra' in struct for parameter 'config'; one of: 'name', 'nested'");
    }

    [Test]
    public void Validate_ReportsFieldValueErrorsInSchemaOrder()
    {
        var schema = SemanticsTestData.Schema(
            SemanticsTestData.CreateStringField("zeta"),
            SemanticsTestData.CreateStringField("alpha"));

        SemanticsAssertions.ValidateStructError(
            "{alpha=beta(x), zeta=beta(y)}",
            schema,
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'zeta' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForUnknownField_ReportsSchemaFieldOrder()
    {
        var schema = SemanticsTestData.Schema(
            SemanticsTestData.CreateStringField("zeta"),
            SemanticsTestData.CreateStringField("alpha"));

        SemanticsAssertions.ValidateStructError(
            "{unknown=1}",
            schema,
            "E_SEM_UNKNOWN_STRUCT_FIELD",
            "unknown field 'unknown' in struct for parameter 'config'; one of: 'zeta', 'alpha'");
    }

    [Test]
    public void Validate_ForStringFieldReceivingList_ReportsExpectedScalarWithListShape()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=[beta()]}",
            StructySchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'name' expects a scalar value, got list");
    }

    [Test]
    public void Validate_ForNestedStructFieldReceivingTransport_ReportsExpectedStruct()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, nested=beta(x)}",
            StructySchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_STRUCT",
            "field 'nested' expects a struct, got transport");
    }

    [Test]
    public void Validate_ForNestedStructFieldReceivingList_ReportsExpectedStruct()
    {
        SemanticsAssertions.ValidateStructError(
            "{name=x, nested=[beta()]}",
            StructySchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_STRUCT",
            "field 'nested' expects a struct, got list");
    }

    [Test]
    public void Validate_ForEmptySchemaWithNoFields_AcceptsEmptyStruct()
    {
        var fields = SemanticsAssertions.ValidateStruct("{}", EmptySchema());

        Assert.That(fields, Is.Empty);
    }

    [Test]
    public void Validate_ForNestedStruct_ProducesStructValue()
    {
        var fields = SemanticsAssertions.ValidateStruct(
            "{outer={inner={value=v}}}",
            DeepSchema());
        var outer = (StructValue)fields[0].Value;
        var inner = (StructValue)outer.Fields[0].Value;
        var value = (StringValue)inner.Fields[0].Value;

        Assert.That(fields[0].Field.Name, Is.EqualTo("outer"));
        Assert.That(outer.Fields[0].Field.Name, Is.EqualTo("inner"));
        Assert.That(inner.Fields[0].Field.Name, Is.EqualTo("value"));
        Assert.That(value.Text, Is.EqualTo("v"));
    }

    [Test]
    public void Validate_ForStringField_ProducesValueAssignableToDataValue()
    {
        var fields = SemanticsAssertions.ValidateStruct("{name=x}", StructySchema());

        DataValue value = fields[0].Value;
        Assert.That(value, Is.InstanceOf<StringValue>());
        Assert.That(((StringValue)value).Text, Is.EqualTo("x"));
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

    [Test]
    public void Validate_ForIntegerAndDecimalFields_ProducesTypedValues()
    {
        var fields = SemanticsAssertions.ValidateStruct(
            "{count=5, ratio=1.50}",
            NumberStructSchema());

        Assert.That(fields[0].Value, Is.InstanceOf<IntegerValue>());
        Assert.That(((IntegerValue)fields[0].Value).Value, Is.EqualTo(5L));
        Assert.That(fields[1].Value, Is.InstanceOf<DecimalValue>());
        Assert.That(((DecimalValue)fields[1].Value).Value, Is.EqualTo(1.5m));
    }

    [Test]
    public void Validate_ForOutOfRangeNestedIntegerField_ReportsFullPath()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=1, nested={value=2}}",
            NumberStructSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'nested.value' of parameter 'config' is invalid: out of range [-1, 1]");
    }

    [Test]
    public void Validate_ForNonIntegerField_ReportsExpectedInteger()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=x}",
            NumberStructSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'count' of parameter 'config' is invalid: expected an integer");
    }

    [Test]
    public void Validate_ForOutOfRangeDecimalField_ReportsOutOfRange()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=1, ratio=2}",
            NumberStructSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'ratio' of parameter 'config' is invalid: out of range [-1.5, 1.5]");
    }

    [Test]
    public void Validate_ForNonDecimalField_ReportsExpectedDecimal()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=1, ratio=x}",
            NumberStructSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'ratio' of parameter 'config' is invalid: expected a decimal");
    }

    [Test]
    public void Validate_ForInexactDecimalField_ReportsExpectedDecimal()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=1, ratio=1e-29}",
            NumberStructSchema(),
            "E_SEM_VALIDATOR_FAILED",
            "field 'ratio' of parameter 'config' is invalid: expected a decimal");
    }

    [Test]
    public void Validate_ForExactSmallestScaleDecimalField_AcceptsIt()
    {
        var fields = SemanticsAssertions.ValidateStruct(
            "{count=1, ratio=1e-28}",
            NumberStructSchema());

        Assert.That(((DecimalValue)fields[1].Value).Value, Is.EqualTo(0.0000000000000000000000000001m));
    }

    [Test]
    public void Validate_ForIntegerFieldReceivingTransport_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=beta()}",
            NumberStructSchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'count' expects a scalar value, got transport");
    }

    [Test]
    public void Validate_ForDecimalFieldReceivingTransport_ReportsExpectedScalar()
    {
        SemanticsAssertions.ValidateStructError(
            "{count=1, ratio=beta()}",
            NumberStructSchema(),
            "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
            "field 'ratio' expects a scalar value, got transport");
    }

    private static StructSchema NumberStructSchema()
    {
        return SchemaOf("numberStruct");
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

    private static StructSchema SchemaOf(string scheme)
    {
        var constructor = SemanticsTestData.Canonical(scheme);
        return ((StructParam)SemanticsTestData.FindParam(constructor, "config")).Schema;
    }
}
