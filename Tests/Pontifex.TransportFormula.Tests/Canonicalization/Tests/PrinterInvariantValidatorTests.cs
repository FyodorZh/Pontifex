using NUnit.Framework;
using Pontifex;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Canonicalization.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Tests.Canonicalization.Harness;

namespace Pontifex.TransportFormula.Tests.Canonicalization;

[TestFixture]
[Category("TransportFormula.Canonicalization")]
public sealed class PrinterInvariantValidatorTests
{
    [Test]
    public void Validate_WhenRequiredParameterIsAbsent_ReportsMissingRequiredParam()
    {
        var constructor = AlphaConstructor();
        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(constructor));

        AssertPrinterError(
            formula,
            "E_PRN_MISSING_REQUIRED_PARAM",
            "resolved transport 'alpha' is missing required parameter 'input'");
    }

    [Test]
    public void Validate_WhenProvidedParameterIsNotDeclared_ReportsUnknownParam()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var unknown = CanonicalizationTestData.TransportParam(
            "ghost",
            CanonicalizationTestData.AnySet());

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x")),
                CanonicalizationTestData.Bind(unknown, CanonicalizationTestData.Scalar("y"))));

        AssertPrinterError(
            formula,
            "E_PRN_UNKNOWN_PARAM",
            "resolved transport 'alpha' contains unknown parameter 'ghost'");
    }

    [Test]
    public void Validate_WhenDeclaredParameterNameIsRecreated_ReportsUnknownParamByReference()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var mode = Param(constructor, "mode");
        var clone = CanonicalizationTestData.StringParam("mode", positional: true);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x")),
                CanonicalizationTestData.Bind(clone, CanonicalizationTestData.Scalar("y"))));

        AssertPrinterError(
            formula,
            "E_PRN_UNKNOWN_PARAM",
            "resolved transport 'alpha' contains unknown parameter 'mode'");
    }

    [Test]
    public void Validate_WhenParameterIsProvidedTwice_ReportsDuplicateParam()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x")),
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("y"))));

        AssertPrinterError(
            formula,
            "E_PRN_DUPLICATE_PARAM",
            "resolved transport 'alpha' contains parameter 'input' more than once");
    }

    [Test]
    public void Validate_WhenProvidedParametersAreOutOfDeclarationOrder_ReportsInconsistentTree()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");
        var inner = CanonicalizationTestData.Transport(
            CanonicalizationTestData.Constructor("beta", CanonicalizationTestData.TypeB));

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(timeout, CanonicalizationTestData.Scalar("t")),
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Expression(inner))));

        AssertInconsistent(formula, "provided parameter order violation");
    }

    [Test]
    public void Validate_WhenSchemeNameIsNotCanonical_ReportsInconsistentTree()
    {
        var input = CanonicalizationTestData.TransportParam(
            "input",
            CanonicalizationTestData.AnySet(),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "Alpha",
            CanonicalizationTestData.TypeA,
            input);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x"))));

        AssertInconsistent(formula, "non-canonical name");
    }

    [Test]
    public void Validate_WhenParameterNameIsNotCanonical_ReportsInconsistentTree()
    {
        var input = CanonicalizationTestData.TransportParam(
            "Input",
            CanonicalizationTestData.AnySet(),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "alpha",
            CanonicalizationTestData.TypeA,
            input);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x"))));

        AssertInconsistent(formula, "non-canonical name");
    }

    [Test]
    public void Validate_WhenStructFieldNameIsNotCanonical_ReportsInconsistentTree()
    {
        var name = CanonicalizationTestData.StringField("Name", required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(name),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "structy",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x"))))));

        AssertInconsistent(formula, "non-canonical name");
    }

    [Test]
    public void Validate_WhenStringParameterReceivesTransport_ReportsKindMismatch()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(
                        EmptyTransport("beta", CanonicalizationTestData.TypeB))),
                CanonicalizationTestData.Bind(
                    timeout,
                    CanonicalizationTestData.Expression(EmptyTransport("beta", CanonicalizationTestData.TypeB)))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'timeout' of scheme 'alpha' has value kind transport, expected string");
    }

    [Test]
    public void Validate_WhenStringParameterReceivesList_ReportsKindMismatch()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(
                        EmptyTransport("beta", CanonicalizationTestData.TypeB))),
                CanonicalizationTestData.Bind(
                    timeout,
                    CanonicalizationTestData.List())));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'timeout' of scheme 'alpha' has value kind transport list, expected string");
    }

    [Test]
    public void Validate_WhenStringParameterReceivesStruct_ReportsKindMismatch()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(
                        EmptyTransport("beta", CanonicalizationTestData.TypeB))),
                CanonicalizationTestData.Bind(
                    timeout,
                    CanonicalizationTestData.Struct())));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'timeout' of scheme 'alpha' has value kind struct, expected string");
    }

    [Test]
    public void Validate_WhenTransportParameterReceivesString_ReportsKindMismatch()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x"))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'input' of scheme 'alpha' has value kind string, expected transport");
    }

    [Test]
    public void Validate_WhenListParameterReceivesString_ReportsKindMismatch()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.AnySet(),
            min: 0,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "bag",
            CanonicalizationTestData.TypeA,
            items);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(items, CanonicalizationTestData.Scalar("x"))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'items' of scheme 'bag' has value kind string, expected transport list");
    }

    [Test]
    public void Validate_WhenStructParameterReceivesString_ReportsKindMismatch()
    {
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "emptystruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(config, CanonicalizationTestData.Scalar("x"))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'config' of scheme 'emptystruct' has value kind string, expected struct");
    }

    [Test]
    public void Validate_WhenListIsBelowMinimum_ReportsListArityRange()
    {
        var constructor = GammaConstructor();
        var items = Param(constructor, "items");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(items, CanonicalizationTestData.List())));

        AssertPrinterError(
            formula,
            "E_PRN_LIST_ARITY",
            "transport list parameter 'items' of scheme 'gamma' has 0 element(s), expected 2..3");
    }

    [Test]
    public void Validate_WhenUnboundedListIsBelowMinimum_ReportsAtLeastRange()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.AnySet(),
            min: 1,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "bag",
            CanonicalizationTestData.TypeA,
            items);
        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(items, CanonicalizationTestData.List())));

        AssertPrinterError(
            formula,
            "E_PRN_LIST_ARITY",
            "transport list parameter 'items' of scheme 'bag' has 0 element(s), expected at least 1");
    }

    [Test]
    public void Validate_WhenListIsAboveMaximum_ReportsListArityRange()
    {
        var constructor = GammaConstructor();
        var items = Param(constructor, "items");
        var first = EmptyTransport("alpha", CanonicalizationTestData.TypeA);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.List(first, first, first, first))));

        AssertPrinterError(
            formula,
            "E_PRN_LIST_ARITY",
            "transport list parameter 'items' of scheme 'gamma' has 4 element(s), expected 2..3");
    }

    [Test]
    public void Validate_WhenUnboundedListExceedsMinimum_AcceptsLargeList()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.AnySet(),
            min: 0,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "bag",
            CanonicalizationTestData.TypeA,
            items);
        var element = EmptyTransport("beta", CanonicalizationTestData.TypeB);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.List(element, element, element, element, element))));

        var output = CanonicalizationAssertions.CanonicalizeSucceeds(
            CanonicalizationTestData.Canonicalizer(),
            formula);

        Assert.That(
            output,
            Is.EqualTo("bag([beta(), beta(), beta(), beta(), beta()])"));
    }

    [Test]
    public void Validate_WhenHomogeneousListMixesTypes_ReportsListHomogeneity()
    {
        var constructor = GammaConstructor();
        var items = Param(constructor, "items");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.List(
                        EmptyTransport("alpha", CanonicalizationTestData.TypeA),
                        EmptyTransport("beta", CanonicalizationTestData.TypeB)))));

        AssertPrinterError(
            formula,
            "E_PRN_LIST_HOMOGENEITY",
            "transport list parameter 'items' of scheme 'gamma' mixes transport types");
    }

    [Test]
    public void Validate_WhenTransportExpressionIsOutsideExpectedSet_ReportsMembershipViolation()
    {
        var input = CanonicalizationTestData.TransportParam(
            "input",
            CanonicalizationTestData.Exactly(CanonicalizationTestData.TypeB),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "outer",
            CanonicalizationTestData.TypeC,
            input);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(
                        EmptyTransport("alpha", CanonicalizationTestData.TypeA)))));

        AssertInconsistent(formula, "expected-type membership violation");
    }

    [Test]
    public void Validate_WhenListElementIsOutsideExpectedSet_ReportsMembershipViolation()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.Exactly(CanonicalizationTestData.TypeB),
            min: 0,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "listb",
            CanonicalizationTestData.TypeC,
            items);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.List(
                        EmptyTransport("alpha", CanonicalizationTestData.TypeA)))));

        AssertInconsistent(formula, "expected-type membership violation");
    }

    [Test]
    public void Validate_WhenStructContainsUndeclaredField_ReportsUnknownField()
    {
        var constructor = StructyConstructor(out var config, out var name);
        var unknown = CanonicalizationTestData.StringField("extra");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            unknown,
                            CanonicalizationTestData.StringValue("y"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_UNKNOWN",
            "struct parameter 'config' of scheme 'structy' contains unknown field 'extra'");
    }

    [Test]
    public void Validate_WhenNestedStructContainsUndeclaredField_ReportsFullPath()
    {
        var constructor = StructyConstructor(out var config, out var name, out var nested);
        var unknown = CanonicalizationTestData.StringField("unknown");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue(
                                CanonicalizationTestData.Field(
                                    unknown,
                                    CanonicalizationTestData.StringValue("y"))))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_UNKNOWN",
            "struct parameter 'config' of scheme 'structy' contains unknown field 'nested.unknown'");
    }

    [Test]
    public void Validate_WhenStructBindingUsesARecreatedField_ReportsUnknownFieldByReference()
    {
        var constructor = StructyConstructor(out var config, out _);
        var clone = CanonicalizationTestData.StringField("name", required: true);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            clone,
                            CanonicalizationTestData.StringValue("x"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_UNKNOWN",
            "struct parameter 'config' of scheme 'structy' contains unknown field 'name'");
    }

    [Test]
    public void Validate_WhenRequiredStructFieldIsAbsent_ReportsMissingField()
    {
        var constructor = StructyConstructor(out var config, out _);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct())));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_MISSING",
            "struct parameter 'config' of scheme 'structy' is missing required field 'name'");
    }

    [Test]
    public void Validate_WhenNestedRequiredStructFieldIsAbsent_ReportsFullPath()
    {
        var innerValue = CanonicalizationTestData.StringField("value", required: true);
        var innerSchema = CanonicalizationTestData.Schema(innerValue);
        var outer = CanonicalizationTestData.NestedField("inner", innerSchema, required: true);
        var outerSchema = CanonicalizationTestData.Schema(outer);
        var root = CanonicalizationTestData.NestedField("outer", outerSchema, required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(root),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "deep",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            root,
                            CanonicalizationTestData.NestedValue(
                                CanonicalizationTestData.Field(
                                    outer,
                                    CanonicalizationTestData.NestedValue())))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_MISSING",
            "struct parameter 'config' of scheme 'deep' is missing required field 'outer.inner.value'");
    }

    [Test]
    public void Validate_WhenStructFieldAppearsTwice_ReportsDuplicateField()
    {
        var constructor = StructyConstructor(out var config, out var name);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("y"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_DUPLICATE",
            "struct parameter 'config' of scheme 'structy' contains field 'name' more than once");
    }

    [Test]
    public void Validate_WhenStructFieldHasWrongKind_ReportsKindMismatch()
    {
        var constructor = StructyConstructor(out var config, out var name);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.NestedValue())))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'name' of struct parameter 'config' has value kind struct, expected string");
    }

    [Test]
    public void Validate_WhenNestedStructFieldHasWrongKind_ReportsKindMismatch()
    {
        var constructor = StructyConstructor(out var config, out var name, out var nested);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.StringValue("y"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'nested' of struct parameter 'config' has value kind string, expected struct");
    }

    [Test]
    public void Validate_WhenStructFieldsAreOutOfSchemaOrder_ReportsStructOrderViolation()
    {
        var constructor = StructyConstructor(out var config, out var name, out var nested);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue()),
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x"))))));

        AssertInconsistent(formula, "struct field order violation");
    }

    [Test]
    public void Validate_WhenStringFieldContainsCarriageReturn_ReportsInvalidScalar()
    {
        var constructor = StructyConstructor(out var config, out var name);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("a\rb"))))));

        AssertPrinterError(
            formula,
            "E_PRN_INVALID_SCALAR",
            "scalar value contains a character that cannot be printed: U+000D");
    }

    [Test]
    public void Validate_WhenCastTargetIsAbsentFromCatalog_ReportsInvalidCastTarget()
    {
        var inner = EmptyTransport("alpha", CanonicalizationTestData.TypeA);
        var formula = CanonicalizationTestData.Formula(
            new ResolvedCast(
                inner,
                CanonicalizationTestData.TypeNotInCatalog,
                new SourceSpan(0, 0)));

        AssertPrinterError(
            formula,
            "E_PRN_INVALID_CAST_TARGET",
            "cast target 'RawUnreliableNoAck' is invalid");
    }

    [Test]
    public void Validate_WhenCastIsAnIdentity_ReportsInconsistentTreeBeforeCatalogLookup()
    {
        var inner = EmptyTransport("orphan", CanonicalizationTestData.TypeNotInCatalog);
        var formula = CanonicalizationTestData.Formula(
            new ResolvedCast(
                inner,
                CanonicalizationTestData.TypeNotInCatalog,
                new SourceSpan(0, 0)));

        AssertInconsistent(formula, "identity cast node");
    }

    [Test]
    public void Validate_WhenCastTargetAndInnerAreInvalid_ReportsCastTargetFirst()
    {
        var input = CanonicalizationTestData.TransportParam(
            "input",
            CanonicalizationTestData.AnySet(),
            required: true,
            positional: true);
        var alpha = CanonicalizationTestData.Constructor(
            "alpha",
            CanonicalizationTestData.TypeA,
            input);
        var inner = CanonicalizationTestData.Transport(alpha);
        var formula = CanonicalizationTestData.Formula(
            new ResolvedCast(inner, CanonicalizationTestData.TypeNotInCatalog, new SourceSpan(0, 0)));

        AssertPrinterError(
            formula,
            "E_PRN_INVALID_CAST_TARGET",
            "cast target '" + CanonicalizationTestData.TypeNotInCatalog + "' is invalid");
    }

    [Test]
    public void Validate_WhenTransportTypeIsAbsentFromCatalog_ReportsInconsistentTree()
    {
        var constructor = CanonicalizationTestData.Constructor(
            "orphan",
            CanonicalizationTestData.TypeNotInCatalog);
        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(constructor));

        AssertInconsistent(formula, "transport type not in catalog");
    }

    [Test]
    public void Validate_ChecksMissingRequiredBeforeUnknownProvidedParameters()
    {
        var constructor = AlphaConstructor();
        var unknown = CanonicalizationTestData.TransportParam(
            "ghost",
            CanonicalizationTestData.AnySet());

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(unknown, CanonicalizationTestData.Scalar("y"))));

        AssertPrinterError(
            formula,
            "E_PRN_MISSING_REQUIRED_PARAM",
            "resolved transport 'alpha' is missing required parameter 'input'");
    }

    [Test]
    public void Validate_ChecksDuplicateBeforeProvidedParameterOrder()
    {
        var constructor = CanonicalizationTestData.Canonical("opt");
        var a = Param(constructor, "a");
        var b = Param(constructor, "b");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(b, CanonicalizationTestData.Scalar("b")),
                CanonicalizationTestData.Bind(a, CanonicalizationTestData.Scalar("x")),
                CanonicalizationTestData.Bind(a, CanonicalizationTestData.Scalar("y"))));

        AssertPrinterError(
            formula,
            "E_PRN_DUPLICATE_PARAM",
            "resolved transport 'opt' contains parameter 'a' more than once");
    }

    [Test]
    public void Validate_ChecksUnknownStructFieldBeforeMissingRequiredField()
    {
        var constructor = StructyConstructor(out var config, out _);
        var unknown = CanonicalizationTestData.StringField("extra");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            unknown,
                            CanonicalizationTestData.StringValue("y"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_UNKNOWN",
            "struct parameter 'config' of scheme 'structy' contains unknown field 'extra'");
    }

    [Test]
    public void Validate_ChecksMissingStructFieldBeforeDuplicateField()
    {
        var a = CanonicalizationTestData.StringField("a", required: true);
        var b = CanonicalizationTestData.StringField("b", required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(a, b),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "pair",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            a,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            a,
                            CanonicalizationTestData.StringValue("y"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_MISSING",
            "struct parameter 'config' of scheme 'pair' is missing required field 'b'");
    }

    [Test]
    public void Validate_ChecksDuplicateStructFieldBeforeSchemaOrder()
    {
        var constructor = StructyConstructor(out var config, out var name, out var nested);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue()),
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("x")),
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.StringValue("y"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_DUPLICATE",
            "struct parameter 'config' of scheme 'structy' contains field 'name' more than once");
    }

    [Test]
    public void Validate_WhenMultipleRequiredParametersAreAbsent_ReportsFirstInDeclarationOrder()
    {
        var constructor = CanonicalizationTestData.Constructor(
            "requiredpair",
            CanonicalizationTestData.TypeA,
            CanonicalizationTestData.TransportParam(
                "zeta",
                CanonicalizationTestData.AnySet(),
                required: true,
                positional: true),
            CanonicalizationTestData.TransportParam(
                "alpha",
                CanonicalizationTestData.AnySet(),
                required: true,
                positional: true));

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(constructor));

        AssertPrinterError(
            formula,
            "E_PRN_MISSING_REQUIRED_PARAM",
            "resolved transport 'requiredpair' is missing required parameter 'zeta'");
    }

    [Test]
    public void Validate_WhenMultipleRequiredStructFieldsAreAbsent_ReportsFirstInSchemaOrder()
    {
        var a = CanonicalizationTestData.StringField("a", required: true);
        var b = CanonicalizationTestData.StringField("b", required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(a, b),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "pair",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(config, CanonicalizationTestData.Struct())));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_MISSING",
            "struct parameter 'config' of scheme 'pair' is missing required field 'a'");
    }

    [Test]
    public void Validate_ChecksUnknownBeforeDuplicate()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var unknown = CanonicalizationTestData.TransportParam(
            "ghost",
            CanonicalizationTestData.AnySet());

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x")),
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("y")),
                CanonicalizationTestData.Bind(unknown, CanonicalizationTestData.Scalar("z"))));

        AssertPrinterError(
            formula,
            "E_PRN_UNKNOWN_PARAM",
            "resolved transport 'alpha' contains unknown parameter 'ghost'");
    }

    [Test]
    public void Validate_ChecksProvidedOrderBeforeParameterKinds()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(timeout, CanonicalizationTestData.Scalar("t")),
                CanonicalizationTestData.Bind(input, CanonicalizationTestData.Scalar("x"))));

        AssertInconsistent(formula, "provided parameter order violation");
    }

    [Test]
    public void Validate_WhenListParameterReceivesTransport_ReportsKindMismatch()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.AnySet(),
            min: 0,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "bag",
            CanonicalizationTestData.TypeA,
            items);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.Expression(
                        EmptyTransport("beta", CanonicalizationTestData.TypeB)))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'items' of scheme 'bag' has value kind transport, expected transport list");
    }

    [Test]
    public void Validate_WhenListParameterReceivesStruct_ReportsKindMismatch()
    {
        var items = CanonicalizationTestData.ListParam(
            "items",
            CanonicalizationTestData.AnySet(),
            min: 0,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "bag",
            CanonicalizationTestData.TypeA,
            items);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    items,
                    CanonicalizationTestData.Struct())));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'items' of scheme 'bag' has value kind struct, expected transport list");
    }

    [Test]
    public void Validate_WhenStructParameterReceivesTransport_ReportsKindMismatch()
    {
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "emptystruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Expression(
                        EmptyTransport("beta", CanonicalizationTestData.TypeB)))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'config' of scheme 'emptystruct' has value kind transport, expected struct");
    }

    [Test]
    public void Validate_WhenStructParameterReceivesList_ReportsKindMismatch()
    {
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "emptystruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.List())));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'config' of scheme 'emptystruct' has value kind transport list, expected struct");
    }

    [Test]
    public void Validate_ChecksStructOrderBeforeFieldKinds()
    {
        var constructor = StructyConstructor(out var config, out var name, out var nested);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue()),
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.NestedValue())))));

        AssertInconsistent(formula, "struct field order violation");
    }

    [Test]
    public void Validate_WhenStringParameterReceivesInteger_ReportsKindMismatch()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(EmptyTransport("beta", CanonicalizationTestData.TypeB))),
                CanonicalizationTestData.Bind(timeout, CanonicalizationTestData.Integer(5))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'timeout' of scheme 'alpha' has value kind integer, expected string");
    }

    [Test]
    public void Validate_WhenStringParameterReceivesDecimal_ReportsKindMismatch()
    {
        var constructor = AlphaConstructor();
        var input = Param(constructor, "input");
        var timeout = Param(constructor, "timeout");

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    input,
                    CanonicalizationTestData.Expression(EmptyTransport("beta", CanonicalizationTestData.TypeB))),
                CanonicalizationTestData.Bind(timeout, CanonicalizationTestData.Decimal(1.5m))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'timeout' of scheme 'alpha' has value kind decimal, expected string");
    }

    [Test]
    public void Validate_WhenIntegerParameterReceivesString_ReportsKindMismatch()
    {
        var count = CanonicalizationTestData.IntegerParam(
            "count",
            0,
            10,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, count);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(count, CanonicalizationTestData.Scalar("x"))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'count' of scheme 'numeric' has value kind string, expected integer");
    }

    [Test]
    public void Validate_WhenIntegerParameterReceivesDecimal_ReportsKindMismatch()
    {
        var count = CanonicalizationTestData.IntegerParam(
            "count",
            0,
            10,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, count);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(count, CanonicalizationTestData.Decimal(1.5m))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'count' of scheme 'numeric' has value kind decimal, expected integer");
    }

    [Test]
    public void Validate_WhenDecimalParameterReceivesString_ReportsKindMismatch()
    {
        var ratio = CanonicalizationTestData.DecimalParam(
            "ratio",
            -1.5m,
            1.5m,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, ratio);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(ratio, CanonicalizationTestData.Scalar("x"))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'ratio' of scheme 'numeric' has value kind string, expected decimal");
    }

    [Test]
    public void Validate_WhenDecimalParameterReceivesInteger_ReportsKindMismatch()
    {
        var ratio = CanonicalizationTestData.DecimalParam(
            "ratio",
            -1.5m,
            1.5m,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, ratio);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(ratio, CanonicalizationTestData.Integer(1))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'ratio' of scheme 'numeric' has value kind integer, expected decimal");
    }

    [Test]
    public void Validate_WhenIntegerParameterValueIsOutOfRange_ReportsValueOutOfRange()
    {
        var count = CanonicalizationTestData.IntegerParam(
            "count",
            0,
            10,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, count);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(count, CanonicalizationTestData.Integer(11))));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'count' is out of range: 11");
    }

    [Test]
    public void Validate_WhenDecimalParameterValueIsOutOfRange_ReportsCanonicalValue()
    {
        var ratio = CanonicalizationTestData.DecimalParam(
            "ratio",
            -1.5m,
            1.5m,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, ratio);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(ratio, CanonicalizationTestData.Decimal(1.60m))));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'ratio' is out of range: 1.6");
    }

    [Test]
    public void Validate_WhenIntegerFieldHasWrongKind_ReportsStructFieldKindMismatch()
    {
        var count = CanonicalizationTestData.IntegerField("count", 0, 10, required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(count),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "numericstruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(count, CanonicalizationTestData.Decimal(1.5m))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'count' of struct parameter 'config' has value kind decimal, expected integer");
    }

    [Test]
    public void Validate_WhenStringFieldReceivesInteger_ReportsStructFieldKindMismatch()
    {
        var name = CanonicalizationTestData.StringField("name", required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(name),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "typedstruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            name,
                            CanonicalizationTestData.Integer(5))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'name' of struct parameter 'config' has value kind integer, expected string");
    }

    [Test]
    public void Validate_WhenDecimalFieldReceivesString_ReportsStructFieldKindMismatch()
    {
        var ratio = CanonicalizationTestData.DecimalField("ratio", -1.5m, 1.5m, required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(ratio),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "typedstruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            ratio,
                            CanonicalizationTestData.Scalar("x"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'ratio' of struct parameter 'config' has value kind string, expected decimal");
    }

    [Test]
    public void Validate_WhenDecimalFieldValueIsOutOfRange_ReportsFullPath()
    {
        var ratio = CanonicalizationTestData.DecimalField("ratio", -1.5m, 1.5m, required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(ratio),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "numericstruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            ratio,
                            CanonicalizationTestData.Decimal(1.60m))))));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'config.ratio' is out of range: 1.6");
    }

    [Test]
    public void Validate_WhenIntegerParameterHasWrongKindAndOutOfRange_ReportsKindMismatchFirst()
    {
        var count = CanonicalizationTestData.IntegerParam(
            "count",
            0,
            10,
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor("numeric", CanonicalizationTestData.TypeA, count);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(count, CanonicalizationTestData.Decimal(11m))));

        AssertPrinterError(
            formula,
            "E_PRN_PARAM_KIND_MISMATCH",
            "parameter 'count' of scheme 'numeric' has value kind decimal, expected integer");
    }

    [Test]
    public void Validate_WhenNestedIntegerFieldValueIsOutOfRange_ReportsFullPath()
    {
        var value = CanonicalizationTestData.IntegerField("value", -1, 1, required: true);
        var nested = CanonicalizationTestData.NestedField(
            "nested",
            CanonicalizationTestData.Schema(value),
            required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(nested),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "numericstruct",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue(
                                CanonicalizationTestData.Field(
                                    value,
                                    CanonicalizationTestData.Integer(2))))))));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'config.nested.value' is out of range: 2");
    }

    [Test]
    public void Validate_WhenArrayFieldHasNonArrayValue_ReportsKindMismatchExpectedArray()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "arrays",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(values, CanonicalizationTestData.Scalar("x"))))));

        AssertPrinterError(
            formula,
            "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
            "field 'values' of struct parameter 'config' has value kind string, expected array");
    }

    [Test]
    public void Validate_WhenArrayHasWrongArity_ReportsArrayArity()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var formula = ArrayFormula(values, CanonicalizationTestData.ArrayValue());

        AssertPrinterError(
            formula,
            "E_PRN_ARRAY_ARITY",
            "resolved array field 'values' of parameter 'config' has 0 element(s), expected 1..2");
    }

    [Test]
    public void Validate_WhenArrayElementHasWrongKind_ReportsArrayElementType()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(CanonicalizationTestData.Scalar("x")));

        AssertPrinterError(
            formula,
            "E_PRN_ARRAY_ELEMENT_TYPE",
            "resolved array element 'values[0]' of parameter 'config' expects integer, got string");
    }

    [Test]
    public void Validate_WhenIntegerArrayElementIsOutOfRange_ReportsIndexedPath()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(CanonicalizationTestData.Integer(2)));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'config.values[0]' is out of range: 2");
    }

    [Test]
    public void Validate_WhenDecimalArrayElementIsOutOfRange_ReportsCanonicalValue()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new DecimalType(-1.5m, 1.5m),
            1,
            2,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(CanonicalizationTestData.Decimal(1.60m)));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'config.values[0]' is out of range: 1.6");
    }

    [Test]
    public void Validate_WhenStringArrayElementContainsCr_ReportsInvalidScalar()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new StringType(null),
            1,
            2,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(CanonicalizationTestData.Scalar("a\rb")));

        AssertPrinterError(
            formula,
            "E_PRN_INVALID_SCALAR",
            "scalar value contains a character that cannot be printed: U+000D");
    }

    [Test]
    public void Validate_ChecksArrayArityBeforeElementKinds()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            3,
            4,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(
                CanonicalizationTestData.Scalar("x"),
                CanonicalizationTestData.Scalar("y")));

        AssertPrinterError(
            formula,
            "E_PRN_ARRAY_ARITY",
            "resolved array field 'values' of parameter 'config' has 2 element(s), expected 3..4");
    }

    [Test]
    public void Validate_ChecksArrayElementKindBeforeRange()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(
                CanonicalizationTestData.Decimal(1.5m),
                CanonicalizationTestData.Integer(2)));

        AssertPrinterError(
            formula,
            "E_PRN_ARRAY_ELEMENT_TYPE",
            "resolved array element 'values[0]' of parameter 'config' expects integer, got decimal");
    }

    [Test]
    public void Validate_WhenArrayElementIsAnArray_ReportsArrayElementType()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var formula = ArrayFormula(
            values,
            CanonicalizationTestData.ArrayValue(
                CanonicalizationTestData.ArrayValue()));

        AssertPrinterError(
            formula,
            "E_PRN_ARRAY_ELEMENT_TYPE",
            "resolved array element 'values[0]' of parameter 'config' expects integer, got array");
    }

    [Test]
    public void Validate_WhenNestedArrayElementIsOutOfRange_ReportsFullPath()
    {
        var values = CanonicalizationTestData.ArrayField(
            "values",
            new IntegerType(0, 1),
            1,
            2,
            required: true);
        var nested = CanonicalizationTestData.NestedField(
            "nested",
            CanonicalizationTestData.Schema(values),
            required: true);
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(nested),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "nestedarrays",
            CanonicalizationTestData.TypeA,
            config);

        var formula = CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(
                            nested,
                            CanonicalizationTestData.NestedValue(
                                CanonicalizationTestData.Field(
                                    values,
                                    CanonicalizationTestData.ArrayValue(
                                        CanonicalizationTestData.Integer(0),
                                        CanonicalizationTestData.Integer(2)))))))));

        AssertPrinterError(
            formula,
            "E_PRN_VALUE_OUT_OF_RANGE",
            "resolved value for 'config.nested.values[1]' is out of range: 2");
    }

    private static ResolvedFormula ArrayFormula(StructField values, ArrayValue array)
    {
        var config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CanonicalizationTestData.Constructor(
            "arrays",
            CanonicalizationTestData.TypeA,
            config);

        return CanonicalizationTestData.Formula(
            CanonicalizationTestData.Transport(
                constructor,
                CanonicalizationTestData.Bind(
                    config,
                    CanonicalizationTestData.Struct(
                        CanonicalizationTestData.Field(values, array)))));
    }

    private static void AssertInconsistent(ResolvedFormula formula, string detail)
    {
        AssertPrinterError(
            formula,
            "E_PRN_INCONSISTENT_TREE",
            "resolved tree is inconsistent: " + detail);
    }

    private static void AssertPrinterError(
        ResolvedFormula formula,
        string code,
        string message)
    {
        CanonicalizationAssertions.CanonicalizeError(
            CanonicalizationTestData.Canonicalizer(),
            formula,
            code,
            message);
    }

    private static TransportConstructorInfo AlphaConstructor()
    {
        return CanonicalizationTestData.Constructor(
            "alpha",
            CanonicalizationTestData.TypeA,
            CanonicalizationTestData.TransportParam(
                "input",
                CanonicalizationTestData.AnySet(),
                required: true,
                positional: true),
            CanonicalizationTestData.StringParam("mode", positional: true),
            CanonicalizationTestData.StringParam("timeout"));
    }

    private static TransportConstructorInfo GammaConstructor()
    {
        return CanonicalizationTestData.Constructor(
            "gamma",
            CanonicalizationTestData.TypeC,
            CanonicalizationTestData.ListParam(
                "items",
                CanonicalizationTestData.AnySet(),
                min: 2,
                max: 3,
                homogeneous: true,
                required: true,
                positional: true));
    }

    private static TransportConstructorInfo StructyConstructor(
        out Param config,
        out StructField name)
    {
        return StructyConstructor(out config, out name, out _);
    }

    private static TransportConstructorInfo StructyConstructor(
        out Param config,
        out StructField name,
        out StructField nested)
    {
        name = CanonicalizationTestData.StringField("name", required: true);
        nested = CanonicalizationTestData.NestedField(
            "nested",
            CanonicalizationTestData.Schema(CanonicalizationTestData.StringField("a")));
        config = CanonicalizationTestData.StructParam(
            "config",
            CanonicalizationTestData.Schema(name, nested),
            required: true,
            positional: true);
        return CanonicalizationTestData.Constructor(
            "structy",
            CanonicalizationTestData.TypeA,
            config);
    }

    private static Param Param(TransportConstructorInfo constructor, string name)
    {
        return CanonicalizationTestData.FindParam(constructor, name);
    }

    private static ResolvedTransport EmptyTransport(string name, Pontifex.TransportType type)
    {
        return CanonicalizationTestData.Transport(
            CanonicalizationTestData.Constructor(name, type));
    }
}
