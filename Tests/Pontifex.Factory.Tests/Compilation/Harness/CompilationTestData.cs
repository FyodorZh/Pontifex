using System;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Converters;
using Pontifex.Factory.Compilation;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Scriba;
using Core = Pontifex.Factory.Registration;
using Formula = Pontifex.TransportFormula.Registry;

namespace Pontifex.Factory.Tests.Compilation.Harness;

// Hand-written construction helpers for the compilation suite. The formula DTO
// builders cover only the shapes the compiler consumes; the core registration
// builders cover only the metadata the runtime registry keys on. No mocking
// library is used and no production state is inspected.
internal static class CompilationTestData
{
    public static readonly TransportType TypeA = TransportType.RawReliableAck;
    public static readonly TransportType TypeB = TransportType.RawReliableNoAck;
    public static readonly TransportType TypeC = TransportType.RawUnreliableAck;

    public static SourceSpan ZeroSpan => new SourceSpan(0, 0);

    public static ILogger Logger => VoidLogger.Instance;

    public static IMemoryRental Memory => MemoryRental.Shared;

    public static TransportConstructorRegistry NewRegistry(
        params Core.ITransportRegistration[] registrations)
    {
        var registry = new TransportConstructorRegistry();
        for (var i = 0; i < registrations.Length; i++)
        {
            registry.Register(registrations[i]);
        }

        return registry;
    }

    public static TransportCompiler NewCompiler(
        ITransportConstructorRegistry constructors,
        IConvertersGraph? converters = null,
        Core.TransportRole role = Core.TransportRole.Server,
        ILogger? logger = null,
        IMemoryRental? memory = null)
    {
        return new TransportCompiler(
            constructors,
            converters ?? new RecordingConvertersGraph(null),
            logger ?? Logger,
            memory ?? Memory,
            role);
    }

    // --- Formula metadata -------------------------------------------------

    public static Formula.Param StringParam(
        string name,
        bool required = false,
        bool positional = false,
        Formula.ScalarValidator? validate = null)
    {
        return new Formula.StringParam(name, required, positional, validate);
    }

    public static Formula.Param IntegerParam(
        string name,
        long min,
        long max,
        bool required = false,
        bool positional = false)
    {
        return new Formula.IntegerParam(
            name,
            required,
            positional,
            new Formula.IntegerType(min, max));
    }

    public static Formula.Param DecimalParam(
        string name,
        decimal min,
        decimal max,
        bool required = false,
        bool positional = false)
    {
        return new Formula.DecimalParam(
            name,
            required,
            positional,
            new Formula.DecimalType(min, max));
    }

    public static Formula.Param TransportParam(
        string name,
        Formula.TransportTypeSet expected,
        bool required = false,
        bool positional = false)
    {
        return new Formula.TransportParam(name, required, positional, expected);
    }

    public static Formula.Param TransportListParam(
        string name,
        Formula.TransportTypeSet expected,
        int min = 0,
        int? max = null,
        bool homogeneous = false,
        bool required = false,
        bool positional = false)
    {
        return new Formula.TransportListParam(
            name,
            required,
            positional,
            expected,
            homogeneous,
            min,
            max);
    }

    public static Formula.Param StructParam(
        string name,
        Formula.StructSchema schema,
        bool required = false,
        bool positional = false)
    {
        return new Formula.StructParam(name, required, positional, schema);
    }

    public static Formula.StructSchema Schema(params Formula.StructField[] fields)
    {
        return new Formula.StructSchema(fields);
    }

    public static Formula.StructField StringField(
        string name,
        bool required = false,
        Formula.ScalarValidator? validate = null)
    {
        return new Formula.StringField(name, required, validate);
    }

    public static Formula.StructField IntegerField(
        string name,
        long min,
        long max,
        bool required = false)
    {
        return new Formula.IntegerField(name, required, new Formula.IntegerType(min, max));
    }

    public static Formula.StructField DecimalField(
        string name,
        decimal min,
        decimal max,
        bool required = false)
    {
        return new Formula.DecimalField(name, required, new Formula.DecimalType(min, max));
    }

    public static Formula.StructField NestedStructField(
        string name,
        Formula.StructSchema schema,
        bool required = false)
    {
        return new Formula.NestedStructField(name, required, schema);
    }

    public static Formula.TransportTypeSet AnySet()
    {
        return new Formula.TransportTypeSet(
            Formula.TransportTypeSetKind.Any,
            Array.Empty<TransportType>());
    }

    // --- Core registration metadata --------------------------------------

    public static Core.TransportParameter CoreStringParameter(
        string name,
        bool required = false,
        bool positional = false,
        Core.ScalarValidator? validate = null)
    {
        return new Core.StringParameter(name, required, positional, validate);
    }

    public static Core.TransportParameter CoreIntegerParameter(
        string name,
        long min,
        long max,
        bool required = false,
        bool positional = false)
    {
        return new Core.IntegerParameter(
            name,
            required,
            positional,
            new Core.IntegerType(min, max));
    }

    public static Core.TransportParameter CoreDecimalParameter(
        string name,
        decimal min,
        decimal max,
        bool required = false,
        bool positional = false)
    {
        return new Core.DecimalParameter(
            name,
            required,
            positional,
            new Core.DecimalType(min, max));
    }

    public static Core.TransportParameter CoreNestedTransportParameter(
        string name,
        Core.TransportTypeSet expected,
        bool required = false,
        bool positional = false)
    {
        return new Core.NestedTransportParameter(name, required, positional, expected);
    }

    public static Core.TransportTypeSet CoreAnySet()
    {
        return Core.TransportTypeSet.Any;
    }

    // --- Resolved tree ----------------------------------------------------

    public static TransportConstructorInfo Constructor(
        string name,
        TransportType type,
        params Formula.Param[] parameters)
    {
        return new TransportConstructorInfo(
            name,
            type,
            parameters,
            Array.Empty<string>());
    }

    public static ResolvedTransport Transport(
        TransportConstructorInfo constructor,
        params ParamBinding[] provided)
    {
        return new ResolvedTransport(constructor, provided, ZeroSpan);
    }

    public static ResolvedFormula FormulaFor(ResolvedExpr root)
    {
        return new ResolvedFormula(root);
    }

    public static ResolvedFormula FormulaFor(ResolvedTransport transport)
    {
        return new ResolvedFormula(transport);
    }

    public static ResolvedCast Cast(ResolvedTransport inner, TransportType target)
    {
        return new ResolvedCast(inner, target, ZeroSpan);
    }

    public static ParamBinding Scalar(Formula.Param param, string text)
    {
        return new ParamBinding(param, new StringValue(text));
    }

    public static ParamBinding Integer(Formula.Param param, long value)
    {
        return new ParamBinding(param, new IntegerValue(value));
    }

    public static ParamBinding Decimal(Formula.Param param, decimal value)
    {
        return new ParamBinding(param, new DecimalValue(value));
    }

    public static ParamBinding Nested(Formula.Param param, ResolvedExpr expr)
    {
        // A transport parameter stores the resolved expression directly.
        return new ParamBinding(param, expr);
    }

    public static ParamBinding List(Formula.Param param, params ResolvedExpr[] elements)
    {
        return new ParamBinding(param, new TransportListValue(elements));
    }

    public static ParamBinding Struct(Formula.Param param, params StructFieldBinding[] fields)
    {
        return new ParamBinding(param, new StructValue(fields));
    }

    public static StructFieldBinding Text(Formula.StructField field, string text)
    {
        return new StructFieldBinding(field, new StringValue(text));
    }

    public static StructFieldBinding IntegerText(Formula.StructField field, long value)
    {
        return new StructFieldBinding(field, new IntegerValue(value));
    }

    public static StructFieldBinding DecimalText(Formula.StructField field, decimal value)
    {
        return new StructFieldBinding(field, new DecimalValue(value));
    }

    public static StructFieldBinding NestedStruct(
        Formula.StructField field,
        params StructFieldBinding[] fields)
    {
        return new StructFieldBinding(field, new StructValue(fields));
    }
}
