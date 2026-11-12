using System;
using System.Collections.Generic;
using Pontifex.Converters;
using Pontifex.Factory.Compilation;
using Pontifex.Factory.Registration;
using Pontifex.Factory.Tests.Compilation.Harness;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Parsing.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Registry.Internal;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Semantics.Internal;
using Core = Pontifex.Factory.Registration;
using Formula = Pontifex.TransportFormula.Registry;

namespace Pontifex.Factory.Tests.Compilation;

[TestFixture]
[Category("Factory.Compilation")]
public sealed class TransportCompilerTests
{
    private const string InconsistentTreeMessage =
        "compiler received an inconsistent resolved tree: unspecified invariant violation";

    // --- Compiler input and lookup ---------------------------------------

    [Test]
    public void Compile_WithNullFormula_ThrowsArgumentNullException()
    {
        var compiler = Compiler(CompilationTestData.NewRegistry());

        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!));
    }

    [Test]
    public void Compile_WithNullRoot_ThrowsCompilerExceptionInconsistentTree()
    {
        var compiler = Compiler(CompilationTestData.NewRegistry());

        var exception = Assert.Throws<CompilerException>(
            () => compiler.Compile(new ResolvedFormula(null!)))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Compilation));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    [Test]
    public void Compile_WithUnknownNodeKind_ThrowsCompilerExceptionInconsistentTree()
    {
        var compiler = Compiler(CompilationTestData.NewRegistry());
        var formula = new ResolvedFormula(new UnknownExpr(CompilationTestData.ZeroSpan));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(formula))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Compilation));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    [Test]
    public void Compile_WithUnknownConstructor_ThrowsBuildExceptionConstructorNotFound()
    {
        var compiler = Compiler(CompilationTestData.NewRegistry());
        var formula = CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                CompilationTestData.Constructor("missing", CompilationTestData.TypeA)));

        var exception = Assert.Throws<BuildException>(() => compiler.Compile(formula))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_BLD_CONSTRUCTOR_NOT_FOUND"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Building));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo("transport constructor 'missing' is not registered"));
    }

    [Test]
    public void Compile_WithUnknownConstructorInMixedCase_ReportsNormalizedScheme()
    {
        var compiler = Compiler(CompilationTestData.NewRegistry());
        var formula = CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                CompilationTestData.Constructor("Missing", CompilationTestData.TypeA)));

        var exception = Assert.Throws<BuildException>(() => compiler.Compile(formula))!;

        Assert.That(
            exception.Error.Message,
            Is.EqualTo("transport constructor 'missing' is not registered"));
    }

    [Test]
    public void Compile_WithUppercaseResolvedScheme_FindsNormalizedRegistration()
    {
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));
        var formula = CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                CompilationTestData.Constructor("ALPHA", CompilationTestData.TypeA)));

        var transport = compiler.Compile(formula);

        Assert.That(transport, Is.SameAs(registration.Created[0]));
        Assert.That(registration.InvocationCount, Is.EqualTo(1));
    }

    [Test]
    public void Compile_WithRegisteredConstructor_CallsCreateExactlyOnce()
    {
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(AlphaTransport()));

        Assert.That(registration.InvocationCount, Is.EqualTo(1));
        Assert.That(registration.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public void Compile_WithRegisteredConstructor_ReturnsCreatedTransport()
    {
        var created = new FakeTransport(CompilationTestData.TypeA, "alpha");
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            created);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var transport = compiler.Compile(
            CompilationTestData.FormulaFor(AlphaTransport()));

        Assert.That(transport, Is.SameAs(created));
    }

    // --- Build context ---------------------------------------------------

    [Test]
    public void Compile_PassesRoleLoggerMemoryToCreate()
    {
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(registration),
            role: TransportRole.Client);

        compiler.Compile(CompilationTestData.FormulaFor(AlphaTransport()));

        var context = registration.LastContext;
        Assert.That(context, Is.Not.Null);
        Assert.That(context!.Role, Is.EqualTo(TransportRole.Client));
        Assert.That(context.Logger, Is.SameAs(CompilationTestData.Logger));
        Assert.That(context.Memory, Is.SameAs(CompilationTestData.Memory));
    }

    [Test]
    public void Compile_WithStringParameter_PassesDecodedValue()
    {
        var decoded = "a \"quoted\" value";
        var param = CompilationTestData.StringParam(
            "path",
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreStringParameter("path", required: true, positional: true) },
            Array.Empty<string>(),
            _ => new FakeTransport(CompilationTestData.TypeA, "alpha"));
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Scalar(param, decoded))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetString("path", out var value), Is.True);
        Assert.That(value, Is.EqualTo(decoded));
    }

    [Test]
    public void Compile_WithAbsentOptionalString_ReturnsFalse()
    {
        var param = CompilationTestData.StringParam("path", positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(constructor)));

        var context = registration.LastContext!;
        Assert.That(context.TryGetString("path", out _), Is.False);
    }

    [Test]
    public void Compile_WithIntegerAndDecimalParameters_PassesTypedValues()
    {
        var count = CompilationTestData.IntegerParam(
            "count",
            -10,
            10,
            required: true,
            positional: true);
        var ratio = CompilationTestData.DecimalParam(
            "ratio",
            -1.5m,
            1.5m,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            count,
            ratio);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Integer(count, -7),
                CompilationTestData.Decimal(ratio, 1.25m))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetInt64("count", out var integer), Is.True);
        Assert.That(integer, Is.EqualTo(-7L));
        Assert.That(context.TryGetDecimal("ratio", out var decimalValue), Is.True);
        Assert.That(decimalValue, Is.EqualTo(1.25m));
    }

    [Test]
    public void Compile_WithAbsentOptionalIntegerAndDecimal_ReturnsFalse()
    {
        var count = CompilationTestData.IntegerParam("count", 0, 10, positional: true);
        var ratio = CompilationTestData.DecimalParam("ratio", -1.5m, 1.5m, positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            count,
            ratio);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(constructor)));

        var context = registration.LastContext!;
        Assert.That(context.TryGetInt64("count", out _), Is.False);
        Assert.That(context.TryGetDecimal("ratio", out _), Is.False);
    }

    [Test]
    public void Compile_WithNestedTransport_ReturnsLazyFactory()
    {
        var (compiler, outer, inner) = OuterWithNestedTransport();

        compiler.Compile(OuterWithInnerFormula());

        Assert.That(outer.InvocationCount, Is.EqualTo(1));
        Assert.That(inner.InvocationCount, Is.Zero);

        var context = outer.LastContext!;
        Assert.That(context.TryGetTransport("input", out var factory), Is.True);
        Assert.That(factory, Is.Not.Null);
        Assert.That(inner.InvocationCount, Is.Zero);
    }

    [Test]
    public void Compile_WithNestedTransportFactory_InvokedTwice_CreatesTwoTransports()
    {
        var (compiler, outer, inner) = OuterWithNestedTransport();
        compiler.Compile(OuterWithInnerFormula());
        var context = outer.LastContext!;
        Assert.That(context.TryGetTransport("input", out var factory), Is.True);

        var first = factory!();
        var second = factory!();

        Assert.That(inner.InvocationCount, Is.EqualTo(2));
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(first.Type, Is.EqualTo(CompilationTestData.TypeB));
        Assert.That(second.Type, Is.EqualTo(CompilationTestData.TypeB));
    }

    [Test]
    public void Compile_WithNestedTransportContainingCast_CompilesTheCast()
    {
        var inputParam = CompilationTestData.TransportParam(
            "input",
            CompilationTestData.AnySet(),
            required: true,
            positional: true);
        var outerConstructor = CompilationTestData.Constructor(
            "outer",
            CompilationTestData.TypeA,
            inputParam);
        var innerConstructor = CompilationTestData.Constructor(
            "inner",
            CompilationTestData.TypeA);
        var cast = CompilationTestData.Cast(
            CompilationTestData.Transport(innerConstructor),
            CompilationTestData.TypeB);
        var converted = new FakeTransport(CompilationTestData.TypeB, "converted");
        var converters = new RecordingConvertersGraph(() => converted);
        var innerRegistration = new RecordingRegistration(
            "inner",
            CompilationTestData.TypeA);
        var outerRegistration = new RecordingRegistration(
            "outer",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreNestedTransportParameter("input", CompilationTestData.CoreAnySet(), required: true, positional: true) },
            Array.Empty<string>(),
            _ => new FakeTransport(CompilationTestData.TypeA, "outer"));
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(outerRegistration, innerRegistration),
            converters);

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                outerConstructor,
                CompilationTestData.Nested(inputParam, cast))));

        var context = outerRegistration.LastContext!;
        Assert.That(context.TryGetTransport("input", out var factory), Is.True);
        Assert.That(converters.Calls, Is.Empty);

        var nested = factory!();

        Assert.That(nested, Is.SameAs(converted));
        Assert.That(innerRegistration.InvocationCount, Is.EqualTo(1));
        Assert.That(converters.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public void Compile_WithTransportList_PreservesOrder()
    {
        var listParam = CompilationTestData.TransportListParam(
            "items",
            CompilationTestData.AnySet(),
            required: true,
            positional: true);
        var outerConstructor = CompilationTestData.Constructor(
            "outer",
            CompilationTestData.TypeC,
            listParam);
        var first = CompilationTestData.Constructor("alpha", CompilationTestData.TypeA);
        var second = CompilationTestData.Constructor("beta", CompilationTestData.TypeB);
        var third = CompilationTestData.Constructor("gamma", CompilationTestData.TypeA);
        var registration = new RecordingRegistration("outer", CompilationTestData.TypeC);
        var compiler = Compiler(CompilationTestData.NewRegistry(
            registration,
            new RecordingRegistration("alpha", CompilationTestData.TypeA),
            new RecordingRegistration("beta", CompilationTestData.TypeB),
            new RecordingRegistration("gamma", CompilationTestData.TypeA)));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                outerConstructor,
                CompilationTestData.List(
                    listParam,
                    CompilationTestData.Transport(first),
                    CompilationTestData.Transport(second),
                    CompilationTestData.Transport(third)))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetTransportList("items", out var factories), Is.True);
        Assert.That(factories, Has.Count.EqualTo(3));

        var created = new List<ITransport>();
        for (var i = 0; i < factories.Count; i++)
        {
            created.Add(factories[i]());
        }

        Assert.That(
            created.ConvertAll(transport => transport.Name),
            Is.EqualTo(new[] { "alpha", "beta", "gamma" }));
    }

    [Test]
    public void Compile_WithEmptyTransportList_ReturnsEmptyList()
    {
        var listParam = CompilationTestData.TransportListParam(
            "items",
            CompilationTestData.AnySet(),
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "outer",
            CompilationTestData.TypeC,
            listParam);
        var registration = new RecordingRegistration("outer", CompilationTestData.TypeC);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.List(listParam))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetTransportList("items", out var factories), Is.True);
        Assert.That(factories, Is.Empty);
    }

    [Test]
    public void Compile_WithAbsentOptionalTransportList_ReturnsFalse()
    {
        var listParam = CompilationTestData.TransportListParam(
            "items",
            CompilationTestData.AnySet(),
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "outer",
            CompilationTestData.TypeC,
            listParam);
        var registration = new RecordingRegistration("outer", CompilationTestData.TypeC);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(constructor)));

        var context = registration.LastContext!;
        Assert.That(context.TryGetTransportList("items", out _), Is.False);
    }

    [Test]
    public void Compile_WithStruct_ExposesStringAndNestedFields()
    {
        var nameField = CompilationTestData.StringField("name", required: true);
        var valueField = CompilationTestData.StringField("value", required: true);
        var nestedField = CompilationTestData.NestedStructField(
            "nested",
            CompilationTestData.Schema(valueField),
            required: true);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(nameField, nestedField),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(
                    configParam,
                    CompilationTestData.Text(nameField, "n"),
                    CompilationTestData.NestedStruct(
                        nestedField,
                        CompilationTestData.Text(valueField, "v"))))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetStruct("config", out var value), Is.True);
        Assert.That(value, Is.Not.Null);
        Assert.That(value!.TryGetString("name", out var name), Is.True);
        Assert.That(name, Is.EqualTo("n"));
        Assert.That(value.TryGetStruct("nested", out var nested), Is.True);
        Assert.That(nested, Is.Not.Null);
        Assert.That(nested!.TryGetString("value", out var nestedValue), Is.True);
        Assert.That(nestedValue, Is.EqualTo("v"));
    }

    [Test]
    public void Compile_WithStruct_ExposesIntegerAndDecimalFields()
    {
        var countField = CompilationTestData.IntegerField("count", 0, 10, required: true);
        var ratioField = CompilationTestData.DecimalField("ratio", -1.5m, 1.5m);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(countField, ratioField),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(
                    configParam,
                    CompilationTestData.IntegerText(countField, 5),
                    CompilationTestData.DecimalText(ratioField, 1.25m)))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetStruct("config", out var value), Is.True);
        Assert.That(value!.TryGetInt64("count", out var count), Is.True);
        Assert.That(count, Is.EqualTo(5L));
        Assert.That(value.TryGetDecimal("ratio", out var ratio), Is.True);
        Assert.That(ratio, Is.EqualTo(1.25m));
    }

    [Test]
    public void Compile_WithStructArrayFields_ExposesTypedElements()
    {
        var names = CompilationTestData.ArrayField(
            "names",
            new Formula.StringType(null),
            0,
            2);
        var counts = CompilationTestData.ArrayField(
            "counts",
            new Formula.IntegerType(-1, 1),
            1,
            3,
            required: true);
        var ratios = CompilationTestData.ArrayField(
            "ratios",
            new Formula.DecimalType(-1.5m, 1.5m),
            0);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(names, counts, ratios),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(
                    configParam,
                    CompilationTestData.ArrayField(
                        names,
                        new StringValue("a"),
                        new StringValue("b")),
                    CompilationTestData.ArrayField(
                        counts,
                        new IntegerValue(-1),
                        new IntegerValue(1)),
                    CompilationTestData.ArrayField(
                        ratios,
                        new DecimalValue(1.5m))))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetStruct("config", out var value), Is.True);

        Assert.That(value!.TryGetArray("names", out var namesValue), Is.True);
        Assert.That(namesValue!.Count, Is.EqualTo(2));
        Assert.That(namesValue.TryGetString(0, out var name), Is.True);
        Assert.That(name, Is.EqualTo("a"));

        Assert.That(value.TryGetArray("counts", out var countsValue), Is.True);
        Assert.That(countsValue!.TryGetInt64(0, out var count), Is.True);
        Assert.That(count, Is.EqualTo(-1L));

        Assert.That(value.TryGetArray("ratios", out var ratiosValue), Is.True);
        Assert.That(ratiosValue!.TryGetDecimal(0, out var ratio), Is.True);
        Assert.That(ratio, Is.EqualTo(1.5m));
    }

    [Test]
    public void Compile_WithAbsentOptionalArray_ReturnsFalse()
    {
        var values = CompilationTestData.ArrayField(
            "values",
            new Formula.IntegerType(0, 1),
            0);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(configParam))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetStruct("config", out var value), Is.True);
        Assert.That(value!.TryGetArray("values", out _), Is.False);
    }

    [Test]
    public void Compile_WithEmptyArray_ReturnsZeroCount()
    {
        var values = CompilationTestData.ArrayField(
            "values",
            new Formula.IntegerType(0, 1),
            0);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(
                    configParam,
                    CompilationTestData.ArrayField(values)))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetStruct("config", out var value), Is.True);
        Assert.That(value!.TryGetArray("values", out var array), Is.True);
        Assert.That(array!.Count, Is.Zero);
    }

    [Test]
    public void Compile_WhenArrayElementHasWrongKind_ReturnsFalse()
    {
        var values = CompilationTestData.ArrayField(
            "values",
            new Formula.IntegerType(0, 1),
            1);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(
                    configParam,
                    CompilationTestData.ArrayField(
                        values,
                        new DecimalValue(1.5m))))));

        var context = registration.LastContext!;
        context.TryGetStruct("config", out var value);
        Assert.That(value!.TryGetArray("values", out var array), Is.True);
        Assert.That(array!.TryGetInt64(0, out _), Is.False);
    }

    [Test]
    public void Compile_WhenArrayIndexIsOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var values = CompilationTestData.ArrayField(
            "values",
            new Formula.IntegerType(0, 1),
            1);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(
                    configParam,
                    CompilationTestData.ArrayField(
                        values,
                        new IntegerValue(1))))));

        var context = registration.LastContext!;
        context.TryGetStruct("config", out var value);
        value!.TryGetArray("values", out var array);

        Assert.Throws<ArgumentOutOfRangeException>(() => array!.TryGetInt64(1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => array!.TryGetString(-1, out _));
    }

    [Test]
    public void Compile_WhenArrayFieldHasNonArrayValue_ThrowsInconsistentTree()
    {
        var values = CompilationTestData.ArrayField(
            "values",
            new Formula.IntegerType(0, 1),
            0);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(values),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            Array.Empty<Core.TransportParameter>(),
            Array.Empty<string>(),
            context =>
            {
                context.TryGetStruct("config", out var value);
                value!.TryGetArray("values", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Struct(
                        configParam,
                        CompilationTestData.Text(values, "x"))))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    [Test]
    public void Compile_WhenNonArrayFieldHasArrayValue_ThrowsInconsistentTree()
    {
        var valueField = CompilationTestData.StringField("value", required: true);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(valueField),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            Array.Empty<Core.TransportParameter>(),
            Array.Empty<string>(),
            context =>
            {
                context.TryGetStruct("config", out var value);
                value!.TryGetArray("value", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Struct(
                        configParam,
                        CompilationTestData.ArrayField(
                            valueField,
                            new StringValue("x")))))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    [Test]
    public void Compile_WithAbsentOptionalStructField_ReturnsFalse()
    {
        var nameField = CompilationTestData.StringField("name");
        var nestedField = CompilationTestData.NestedStructField(
            "nested",
            CompilationTestData.Schema());
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(nameField, nestedField),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        compiler.Compile(CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                constructor,
                CompilationTestData.Struct(configParam))));

        var context = registration.LastContext!;
        Assert.That(context.TryGetStruct("config", out var value), Is.True);
        Assert.That(value!.TryGetString("name", out _), Is.False);
        Assert.That(value.TryGetStruct("nested", out _), Is.False);
    }

    [Test]
    public void Compile_WithParameterKindMismatch_ThrowsCompilerExceptionParamKindMismatch()
    {
        var param = CompilationTestData.StringParam(
            "path",
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreStringParameter("path", required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetString("path", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));
        var nested = CompilationTestData.Transport(
            CompilationTestData.Constructor("beta", CompilationTestData.TypeB));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Nested(param, nested)))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_PARAM_KIND_MISMATCH"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Compilation));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo(
                "compiler expected value kind string for parameter 'path', got transport"));
    }

    [Test]
    public void Compile_WhenStringParameterReceivesInteger_ReportsStringExpected()
    {
        var param = CompilationTestData.StringParam(
            "path",
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreStringParameter("path", required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetString("path", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Integer(param, 5)))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_PARAM_KIND_MISMATCH"));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo(
                "compiler expected value kind string for parameter 'path', got integer"));
    }

    [Test]
    public void Compile_WhenIntegerParameterReceivesString_ReportsIntegerExpected()
    {
        var param = CompilationTestData.IntegerParam(
            "count",
            0,
            10,
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreIntegerParameter("count", 0, 10, required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetInt64("count", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Scalar(param, "x")))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_PARAM_KIND_MISMATCH"));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo(
                "compiler expected value kind integer for parameter 'count', got string"));
    }

    [Test]
    public void Compile_WhenDecimalParameterReceivesInteger_ReportsDecimalExpected()
    {
        var param = CompilationTestData.DecimalParam(
            "ratio",
            -1.5m,
            1.5m,
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreDecimalParameter("ratio", -1.5m, 1.5m, required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetDecimal("ratio", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Integer(param, 1)))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_PARAM_KIND_MISMATCH"));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo(
                "compiler expected value kind decimal for parameter 'ratio', got integer"));
    }

    [Test]
    public void Compile_WhenDecimalFieldReceivesString_ThrowsInconsistentTree()
    {
        var field = CompilationTestData.DecimalField("ratio", -1.5m, 1.5m, required: true);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(field),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            Array.Empty<Core.TransportParameter>(),
            Array.Empty<string>(),
            context =>
            {
                context.TryGetStruct("config", out var value);
                value!.TryGetDecimal("ratio", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Struct(
                        configParam,
                        CompilationTestData.Text(field, "x"))))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    [Test]
    public void Compile_WhenStringParameterReceivesArray_ReportsArrayExpected()
    {
        var param = CompilationTestData.StringParam(
            "path",
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreStringParameter("path", required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetString("path", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    new ParamBinding(
                        param,
                        new ArrayValue(Array.Empty<DataValue>()))))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_PARAM_KIND_MISMATCH"));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo(
                "compiler expected value kind string for parameter 'path', got array"));
    }

    [Test]
    public void Compile_WhenIntegerFieldReceivesString_ThrowsInconsistentTree()
    {
        var field = CompilationTestData.IntegerField("count", 0, 10, required: true);
        var configParam = CompilationTestData.StructParam(
            "config",
            CompilationTestData.Schema(field),
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            configParam);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            Array.Empty<Core.TransportParameter>(),
            Array.Empty<string>(),
            context =>
            {
                context.TryGetStruct("config", out var value);
                value!.TryGetInt64("count", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    CompilationTestData.Struct(
                        configParam,
                        CompilationTestData.Text(field, "x"))))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    [Test]
    public void Compile_WithUnknownValueKind_ThrowsCompilerExceptionInconsistentTree()
    {
        var param = CompilationTestData.StringParam(
            "path",
            required: true,
            positional: true);
        var constructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA,
            param);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreStringParameter("path", required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetString("path", out _);
                return new FakeTransport(CompilationTestData.TypeA, "alpha");
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<CompilerException>(() => compiler.Compile(
            CompilationTestData.FormulaFor(
                CompilationTestData.Transport(
                    constructor,
                    new ParamBinding(param, new UnknownValue())))))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_CMP_INCONSISTENT_TREE"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Compilation));
        Assert.That(exception.Error.Message, Is.EqualTo(InconsistentTreeMessage));
    }

    // --- Failure paths ---------------------------------------------------

    [Test]
    public void Compile_WhenConstructorThrows_WrapsAsBuildExceptionConstructorFailed()
    {
        var failure = new InvalidOperationException("constructor boom");
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            Array.Empty<Core.TransportParameter>(),
            Array.Empty<string>(),
            _ => throw failure);
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var exception = Assert.Throws<BuildException>(
            () => compiler.Compile(
                CompilationTestData.FormulaFor(AlphaTransport())))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_BLD_CONSTRUCTOR_FAILED"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Building));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo("transport constructor 'alpha' failed"));
        Assert.That(exception.InnerException, Is.SameAs(failure));
    }

    [Test]
    public void Compile_WhenNestedConstructorThrows_WrapsParentConstructorFailedWithNestedInner()
    {
        var marker = new InvalidOperationException("inner boom");
        var inputParam = CompilationTestData.TransportParam(
            "input",
            CompilationTestData.AnySet(),
            required: true,
            positional: true);
        var outerConstructor = CompilationTestData.Constructor(
            "outer",
            CompilationTestData.TypeA,
            inputParam);
        var innerConstructor = CompilationTestData.Constructor(
            "inner",
            CompilationTestData.TypeB);
        var innerRegistration = new RecordingRegistration(
            "inner",
            CompilationTestData.TypeB,
            Array.Empty<Core.TransportParameter>(),
            Array.Empty<string>(),
            _ => throw marker);
        var outerRegistration = new RecordingRegistration(
            "outer",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreNestedTransportParameter("input", CompilationTestData.CoreAnySet(), required: true, positional: true) },
            Array.Empty<string>(),
            context =>
            {
                context.TryGetTransport("input", out var factory);
                return factory!();
            });
        var compiler = Compiler(CompilationTestData.NewRegistry(
            outerRegistration,
            innerRegistration));
        var formula = CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                outerConstructor,
                CompilationTestData.Nested(
                    inputParam,
                    CompilationTestData.Transport(innerConstructor))));

        var exception = Assert.Throws<BuildException>(
            () => compiler.Compile(formula))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_BLD_CONSTRUCTOR_FAILED"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Building));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo("transport constructor 'outer' failed"));

        var nested = exception.InnerException as BuildException;
        Assert.That(nested, Is.Not.Null);
        Assert.That(nested!.Error.Code, Is.EqualTo("E_BLD_CONSTRUCTOR_FAILED"));
        Assert.That(
            nested.Error.Message,
            Is.EqualTo("transport constructor 'inner' failed"));
        Assert.That(nested.InnerException, Is.SameAs(marker));
        Assert.That(outerRegistration.InvocationCount, Is.EqualTo(1));
        Assert.That(innerRegistration.InvocationCount, Is.EqualTo(1));
    }

    [Test]
    public void Compile_WhenConverterThrows_WrapsAsBuildExceptionConstructorFailedForInnerScheme()
    {
        var failure = new InvalidOperationException("converter boom");
        var converters = new RecordingConvertersGraph(() => throw failure);
        var innerConstructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA);
        var cast = CompilationTestData.Cast(
            CompilationTestData.Transport(innerConstructor),
            CompilationTestData.TypeB);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(registration),
            converters);

        var exception = Assert.Throws<BuildException>(
            () => compiler.Compile(CompilationTestData.FormulaFor(cast)))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_BLD_CONSTRUCTOR_FAILED"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Building));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo("transport constructor 'alpha' failed"));
        Assert.That(exception.InnerException, Is.SameAs(failure));
        Assert.That(registration.InvocationCount, Is.EqualTo(1));
    }

    [Test]
    public void Compile_WithNoConversionPath_ThrowsBuildExceptionNoConversionPath()
    {
        var converters = new RecordingConvertersGraph(null);
        var innerConstructor = CompilationTestData.Constructor(
            "alpha",
            CompilationTestData.TypeA);
        var cast = CompilationTestData.Cast(
            CompilationTestData.Transport(innerConstructor),
            CompilationTestData.TypeC);
        var registration = new RecordingRegistration("alpha", CompilationTestData.TypeA);
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(registration),
            converters);

        var exception = Assert.Throws<BuildException>(
            () => compiler.Compile(CompilationTestData.FormulaFor(cast)))!;

        Assert.That(exception.Error.Code, Is.EqualTo("E_BLD_NO_CONVERSION_PATH"));
        Assert.That(exception.Error.Phase, Is.EqualTo(FactoryPhase.Building));
        Assert.That(
            exception.Error.Message,
            Is.EqualTo("no conversion path from RawReliableAck to RawUnreliableAck"));
        Assert.That(registration.InvocationCount, Is.EqualTo(1));
        Assert.That(converters.Calls, Has.Count.EqualTo(1));
    }

    // --- Casts -----------------------------------------------------------

    [Test]
    public void Compile_WithIdentityCast_ReturnsInnerWithoutCallingConverter()
    {
        var inner = new FakeTransport(CompilationTestData.TypeA, "alpha");
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            inner);
        var converters = new RecordingConvertersGraph(
            () => new FakeTransport(CompilationTestData.TypeA, "unexpected"));
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(registration),
            converters);
        var cast = CompilationTestData.Cast(
            CompilationTestData.Transport(
                CompilationTestData.Constructor("alpha", CompilationTestData.TypeA)),
            CompilationTestData.TypeA);

        var transport = compiler.Compile(CompilationTestData.FormulaFor(cast));

        Assert.That(transport, Is.SameAs(inner));
        Assert.That(converters.Calls, Is.Empty);
    }

    [Test]
    public void Compile_WithChangingCast_CallsConverterWithExpectedArguments()
    {
        var inner = new FakeTransport(CompilationTestData.TypeA, "alpha");
        var converted = new FakeTransport(CompilationTestData.TypeB, "converted");
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA,
            inner);
        var converters = new RecordingConvertersGraph(() => converted);
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(registration),
            converters);
        var cast = CompilationTestData.Cast(
            CompilationTestData.Transport(
                CompilationTestData.Constructor("alpha", CompilationTestData.TypeA)),
            CompilationTestData.TypeB);

        compiler.Compile(CompilationTestData.FormulaFor(cast));

        var call = converters.LastCall;
        Assert.That(call, Is.Not.Null);
        Assert.That(call!.FromType, Is.EqualTo(CompilationTestData.TypeA));
        Assert.That(call.ToType, Is.EqualTo(CompilationTestData.TypeB));
        Assert.That(call.InnerTransportCtor(), Is.SameAs(inner));
        Assert.That(call.MemoryRental, Is.SameAs(CompilationTestData.Memory));
        Assert.That(call.Logger, Is.SameAs(CompilationTestData.Logger));
    }

    [Test]
    public void Compile_WithChangingCast_ReturnsConvertedTransport()
    {
        var converted = new FakeTransport(CompilationTestData.TypeB, "converted");
        var converters = new RecordingConvertersGraph(() => converted);
        var registration = new RecordingRegistration(
            "alpha",
            CompilationTestData.TypeA);
        var compiler = CompilationTestData.NewCompiler(
            CompilationTestData.NewRegistry(registration),
            converters);
        var cast = CompilationTestData.Cast(
            CompilationTestData.Transport(
                CompilationTestData.Constructor("alpha", CompilationTestData.TypeA)),
            CompilationTestData.TypeB);

        var transport = compiler.Compile(CompilationTestData.FormulaFor(cast));

        Assert.That(transport, Is.SameAs(converted));
        Assert.That(transport.Type, Is.EqualTo(CompilationTestData.TypeB));
    }

    // --- Integration -----------------------------------------------------

    [Test]
    public void Compile_ThroughFormulaPipeline_ProducesTransportFromRegisteredConstructor()
    {
        var formulaRegistry = FormulaRegistryWithGreeting();
        var parse = new FormulaParser().Parse("GREETING(\"hello\")");
        Assert.That(parse.Error, Is.Null, parse.Error?.Message);
        Assert.That(parse.Formula, Is.Not.Null);

        var resolve = new FormulaResolver(formulaRegistry).Resolve(
            parse.Formula!,
            CompilationTestData.AnySet());
        Assert.That(resolve.Error, Is.Null, resolve.Error?.Message);
        Assert.That(resolve.Formula, Is.Not.Null);

        var resolved = resolve.Formula!.Root as ResolvedTransport;
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Constructor.Name, Is.EqualTo("greeting"));

        var registration = new RecordingRegistration(
            "Greeting",
            CompilationTestData.TypeA,
            new[] { CompilationTestData.CoreStringParameter("text", required: true, positional: true) },
            new[] { "g" },
            _ => new FakeTransport(CompilationTestData.TypeA, "greeting"));
        var compiler = Compiler(CompilationTestData.NewRegistry(registration));

        var transport = compiler.Compile(resolve.Formula!);

        Assert.That(transport.Type, Is.EqualTo(CompilationTestData.TypeA));
        Assert.That(registration.InvocationCount, Is.EqualTo(1));
        var context = registration.LastContext!;
        Assert.That(context.TryGetString("text", out var text), Is.True);
        Assert.That(text, Is.EqualTo("hello"));
    }

    // --- Helpers ---------------------------------------------------------

    private static TransportCompiler Compiler(
        ITransportConstructorRegistry constructors,
        IConvertersGraph? converters = null)
    {
        return CompilationTestData.NewCompiler(constructors, converters);
    }

    private static ResolvedTransport AlphaTransport()
    {
        return CompilationTestData.Transport(
            CompilationTestData.Constructor("alpha", CompilationTestData.TypeA));
    }

    private static ResolvedFormula OuterWithInnerFormula()
    {
        var inputParam = CompilationTestData.TransportParam(
            "input",
            CompilationTestData.AnySet(),
            required: true,
            positional: true);
        var outerConstructor = CompilationTestData.Constructor(
            "outer",
            CompilationTestData.TypeA,
            inputParam);
        var innerConstructor = CompilationTestData.Constructor(
            "inner",
            CompilationTestData.TypeB);
        return CompilationTestData.FormulaFor(
            CompilationTestData.Transport(
                outerConstructor,
                CompilationTestData.Nested(
                    inputParam,
                    CompilationTestData.Transport(innerConstructor))));
    }

    private static (TransportCompiler Compiler, RecordingRegistration Outer, RecordingRegistration Inner)
        OuterWithNestedTransport()
    {
        var outer = new RecordingRegistration("outer", CompilationTestData.TypeA);
        var inner = new RecordingRegistration("inner", CompilationTestData.TypeB);
        return (Compiler(CompilationTestData.NewRegistry(outer, inner)), outer, inner);
    }

    private static IRegistry FormulaRegistryWithGreeting()
    {
        var builder = new TransportRegistryBuilder();
        builder.RegisterType(new TransportTypeInfo(
            CompilationTestData.TypeA,
            "TypeA"));
        builder.RegisterTransport(new Formula.TransportRegistration(
            new TransportConstructorInfo(
                "Greeting",
                CompilationTestData.TypeA,
                new Formula.Param[]
                {
                    new Formula.StringParam("text", true, true, null)
                },
                Array.Empty<string>()),
            new[] { "g" }));
        return builder.Build();
    }

    // A resolved expression node the compiler does not know. It models a tree
    // produced by a future resolver version or by a corrupted caller.
    private sealed record UnknownExpr(SourceSpan Span) : ResolvedExpr(Span)
    {
        public override TransportType Type => CompilationTestData.TypeA;
    }

    // A resolved value node the build context does not know. It models an
    // unknown value kind reaching the defensive accessor branch.
    private sealed record UnknownValue : ResolvedValue;
}
