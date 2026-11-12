using System;
using System.Collections.Generic;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;
using Pontifex.TransportFormula.Semantics.Internal;

namespace Pontifex.Factory.Tests.Semantics.Harness;

// Centralised assertions for the semantics suite. Error helpers always assert
// phase Resolution, a zero-length span, the exact code, the exact message, and
// the documented offset.
internal static class SemanticsAssertions
{
    public static ResolvedFormula ResolveSucceeds(
        FormulaResolver resolver,
        string source,
        TransportTypeSet expected)
    {
        var result = resolver.Resolve(SemanticsTestData.Parse(source), expected);

        Assert.That(result.Formula, Is.Not.Null, Describe(result.Error));
        Assert.That(result.Error, Is.Null);
        return result.Formula!;
    }

    public static ResolvedFormula ResolveSucceeds(FormulaResolver resolver, string source)
    {
        return ResolveSucceeds(resolver, source, SemanticsTestData.AnySet());
    }

    public static FactoryError ResolveError(
        FormulaResolver resolver,
        string source,
        TransportTypeSet expected,
        string code,
        int offset,
        string message)
    {
        var result = resolver.Resolve(SemanticsTestData.Parse(source), expected);

        Assert.That(result.Formula, Is.Null);
        AssertError(result.Error, code, offset, message);
        return result.Error!;
    }

    public static FactoryError ResolveError(
        FormulaResolver resolver,
        string source,
        string code,
        int offset,
        string message)
    {
        return ResolveError(
            resolver,
            source,
            SemanticsTestData.AnySet(),
            code,
            offset,
            message);
    }

    public static TransportConstructorInfo Select(
        ConstructorSelector selector,
        string rawScheme,
        TransportTypeSet expected)
    {
        return selector.Select(
            rawScheme,
            new SourceSpan(0, rawScheme.Length),
            expected);
    }

    public static FactoryError SelectError(
        ConstructorSelector selector,
        string rawScheme,
        TransportTypeSet expected,
        string code,
        string message)
    {
        var error = CatchSemantic(() => Select(selector, rawScheme, expected));
        AssertError(error, code, 0, message);
        return error;
    }

    public static IReadOnlyList<BoundParameter> Bind(
        string source,
        TransportConstructorInfo constructor)
    {
        return new ArgumentBinder().Bind(SemanticsTestData.Call(source), constructor);
    }

    public static FactoryError BindError(
        string source,
        TransportConstructorInfo constructor,
        string code,
        int offset,
        string message)
    {
        var error = CatchSemantic(() => Bind(source, constructor));
        AssertError(error, code, offset, message);
        return error;
    }

    public static IReadOnlyList<ParamBinding> ValidateParams(
        IRegistry registry,
        string source,
        TransportConstructorInfo constructor)
    {
        var call = SemanticsTestData.Call(source);
        var bound = new ArgumentBinder().Bind(call, constructor);
        var resolver = new FormulaResolver(registry);
        return new ParameterValidator(registry).Validate(
            constructor,
            bound,
            resolver.ResolveExpression);
    }

    public static FactoryError ValidateParamsError(
        IRegistry registry,
        string source,
        TransportConstructorInfo constructor,
        string code,
        string message)
    {
        var error = CatchSemantic(() => ValidateParams(registry, source, constructor));
        Assert.That(error.Code, Is.EqualTo(code), Describe(error));
        Assert.That(error.Message, Is.EqualTo(message));
        return error;
    }

    public static IReadOnlyList<StructFieldBinding> ValidateStruct(
        string structSource,
        StructSchema schema,
        string paramName = "config")
    {
        return StructValidator.Validate(
            SemanticsTestData.Struct(structSource),
            schema,
            paramName);
    }

    public static FactoryError ValidateStructError(
        string structSource,
        StructSchema schema,
        string code,
        string message)
    {
        var error = CatchSemantic(() => ValidateStruct(structSource, schema));
        Assert.That(error.Code, Is.EqualTo(code), Describe(error));
        Assert.That(error.Message, Is.EqualTo(message));
        return error;
    }

    public static ResolvedTransport AsTransport(ResolvedExpr expression)
    {
        Assert.That(expression, Is.InstanceOf<ResolvedTransport>());
        return (ResolvedTransport)expression;
    }

    public static ResolvedCast AsCast(ResolvedExpr expression)
    {
        Assert.That(expression, Is.InstanceOf<ResolvedCast>());
        return (ResolvedCast)expression;
    }

    public static ParamBinding Provided(ResolvedTransport transport, string parameterName)
    {
        for (var i = 0; i < transport.Provided.Count; i++)
        {
            if (transport.Provided[i].Param.Name == parameterName)
            {
                return transport.Provided[i];
            }
        }

        throw new AssertionException(
            "transport '" + transport.Constructor.Name
            + "' has no provided parameter '" + parameterName + "'");
    }

    public static ResolvedTransport ProvidedTransport(
        ResolvedTransport transport,
        string parameterName)
    {
        var binding = Provided(transport, parameterName);
        Assert.That(binding.Value, Is.InstanceOf<ResolvedExpr>());
        return AsTransport((ResolvedExpr)binding.Value);
    }

    public static IReadOnlyList<ResolvedExpr> ProvidedList(
        ResolvedTransport transport,
        string parameterName)
    {
        var binding = Provided(transport, parameterName);
        Assert.That(binding.Value, Is.InstanceOf<TransportListValue>());
        return ((TransportListValue)binding.Value).Elements;
    }

    public static StructValue ProvidedStruct(
        ResolvedTransport transport,
        string parameterName)
    {
        var binding = Provided(transport, parameterName);
        Assert.That(binding.Value, Is.InstanceOf<StructValue>());
        return (StructValue)binding.Value;
    }

    public static StringValue ProvidedScalar(
        ResolvedTransport transport,
        string parameterName)
    {
        var binding = Provided(transport, parameterName);
        Assert.That(binding.Value, Is.InstanceOf<StringValue>());
        return (StringValue)binding.Value;
    }

    public static IntegerValue ProvidedInteger(
        ResolvedTransport transport,
        string parameterName)
    {
        var binding = Provided(transport, parameterName);
        Assert.That(binding.Value, Is.InstanceOf<IntegerValue>());
        return (IntegerValue)binding.Value;
    }

    public static DecimalValue ProvidedDecimal(
        ResolvedTransport transport,
        string parameterName)
    {
        var binding = Provided(transport, parameterName);
        Assert.That(binding.Value, Is.InstanceOf<DecimalValue>());
        return (DecimalValue)binding.Value;
    }

    public static void AssertError(
        FactoryError? error,
        string code,
        int offset,
        string message)
    {
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Code, Is.EqualTo(code), Describe(error));
        Assert.That(error.Message, Is.EqualTo(message));
        Assert.That(error.Phase, Is.EqualTo(FactoryPhase.Resolution));
        Assert.That(error.Span, Is.Not.Null);
        Assert.That(error.Span!.Value.Start, Is.EqualTo(offset));
        Assert.That(error.Span!.Value.Length, Is.EqualTo(0));
    }

    private static FactoryError CatchSemantic(TestDelegate action)
    {
        var abort = Assert.Throws<SemanticAbort>(action);
        Assert.That(abort, Is.Not.Null);
        return abort!.Error;
    }

    private static string Describe(FactoryError? error)
    {
        return error is null ? "expected an error, but resolution succeeded" : error.ToString();
    }
}
