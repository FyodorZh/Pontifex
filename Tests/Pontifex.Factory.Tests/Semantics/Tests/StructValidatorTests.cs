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
            "field 'name' expects a scalar value, got transport list");
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
            "field 'nested' expects a struct, got transport list");
    }

    [Test]
    public void Validate_ForEmptySchemaWithNoFields_AcceptsEmptyStruct()
    {
        var fields = SemanticsAssertions.ValidateStruct("{}", EmptySchema());

        Assert.That(fields, Is.Empty);
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

    private static StructSchema SchemaOf(string scheme)
    {
        var constructor = SemanticsTestData.Canonical(scheme);
        return ((StructParam)SemanticsTestData.FindParam(constructor, "config")).Schema;
    }
}
