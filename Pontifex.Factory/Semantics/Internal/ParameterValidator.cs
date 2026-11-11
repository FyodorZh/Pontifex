using System;
using System.Collections.Generic;
using Pontifex;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics.Internal;

// Validates bound parameters in declaration order, visiting only bound
// parameters, following parsing.md §7.5. List arity is checked before elements,
// elements are resolved in order, and homogeneity is checked after all elements.
// Struct parameters delegate to StructValidator.
internal sealed class ParameterValidator
{
    private readonly ITransportTypeCatalog _catalog;

    public ParameterValidator(IRegistry registry)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        _catalog = registry.Types;
    }

    public IReadOnlyList<ParamBinding> Validate(
        TransportConstructorInfo constructor,
        IReadOnlyList<BoundParameter> bound,
        Func<AstExpr, TransportTypeSet, ResolvedExpr> resolveExpression)
    {
        var provided = new List<ParamBinding>();

        for (var i = 0; i < constructor.Params.Count; i++)
        {
            var parameter = constructor.Params[i];
            var value = FindBoundValue(bound, parameter);
            if (value is null)
            {
                continue;
            }

            switch (parameter)
            {
                case StringParam stringParam:
                {
                    if (value is not AstScalar scalar)
                    {
                        throw new SemanticAbort(SemanticErrorFactory.ExpectedScalar(
                            stringParam.Name,
                            Shape(value),
                            value.Span.Start));
                    }

                    var reason = InvokeValidator(stringParam.Validate, scalar.Text);
                    if (reason is not null)
                    {
                        throw new SemanticAbort(SemanticErrorFactory.ValidatorFailedParam(
                            stringParam.Name,
                            reason,
                            value.Span.Start));
                    }

                    provided.Add(new ParamBinding(stringParam, new ScalarValue(scalar.Text)));
                    break;
                }

                case TransportParam transportParam:
                {
                    if (value is not AstExpr expression)
                    {
                        throw new SemanticAbort(SemanticErrorFactory.ExpectedTransport(
                            transportParam.Name,
                            Shape(value),
                            value.Span.Start));
                    }

                    // Expected-type membership is guaranteed by ResolveExpression;
                    // there is no user-visible post-resolution membership error.
                    var resolved = resolveExpression(expression, transportParam.Expected);
                    provided.Add(new ParamBinding(transportParam, new TransportValue(resolved)));
                    break;
                }

                case TransportListParam listParam:
                {
                    if (value is not AstList list)
                    {
                        throw new SemanticAbort(SemanticErrorFactory.ExpectedTransportList(
                            listParam.Name,
                            Shape(value),
                            value.Span.Start));
                    }

                    provided.Add(new ParamBinding(
                        listParam,
                        ResolveList(listParam, list, resolveExpression)));
                    break;
                }

                case StructParam structParam:
                {
                    if (value is not AstStruct astStruct)
                    {
                        throw new SemanticAbort(SemanticErrorFactory.ExpectedStruct(
                            structParam.Name,
                            Shape(value),
                            value.Span.Start));
                    }

                    var fields = StructValidator.Validate(
                        astStruct,
                        structParam.Schema,
                        structParam.Name);
                    provided.Add(new ParamBinding(structParam, new StructValue(fields)));
                    break;
                }

                default:
                    throw new ArgumentException(
                        "Unsupported parameter kind: " + parameter.GetType().FullName,
                        nameof(constructor));
            }
        }

        return provided.AsReadOnly();
    }

    // Input is the decoded scalar text. A null or empty validator result means
    // valid; a thrown or empty-reason failure becomes "validation failed".
    public static string? InvokeValidator(ScalarValidator? validator, string text)
    {
        if (validator is null)
        {
            return null;
        }

        try
        {
            var result = validator(text);
            if (result is null)
            {
                return null;
            }

            return result.Length == 0
                ? SemanticErrorFactory.ValidationFailedReason
                : result;
        }
        catch (Exception)
        {
            return SemanticErrorFactory.ValidationFailedReason;
        }
    }

    public static string Shape(AstValue value)
    {
        switch (value)
        {
            case AstScalar:
                return "scalar";
            case AstExpr:
                return "transport";
            case AstList:
                return "transport list";
            case AstStruct:
                return "struct";
            default:
                throw new ArgumentException(
                    "Unsupported AST value kind: " + value.GetType().FullName,
                    nameof(value));
        }
    }

    private TransportListValue ResolveList(
        TransportListParam parameter,
        AstList list,
        Func<AstExpr, TransportTypeSet, ResolvedExpr> resolveExpression)
    {
        var count = list.Elements.Count;
        if (count < parameter.Min)
        {
            throw new SemanticAbort(SemanticErrorFactory.ListMin(
                parameter.Name,
                parameter.Min,
                count,
                list.OpenOffset));
        }

        if (parameter.Max.HasValue && count > parameter.Max.Value)
        {
            throw new SemanticAbort(SemanticErrorFactory.ListMax(
                parameter.Name,
                parameter.Max.Value,
                count,
                list.OpenOffset));
        }

        var elements = new List<ResolvedExpr>(count);
        for (var i = 0; i < count; i++)
        {
            elements.Add(resolveExpression(list.Elements[i], parameter.Expected));
        }

        if (parameter.Homogeneous && elements.Count > 1)
        {
            var firstType = elements[0].Type;
            for (var i = 1; i < elements.Count; i++)
            {
                if (elements[i].Type != firstType)
                {
                    throw new SemanticAbort(SemanticErrorFactory.ListHomogeneity(
                        parameter.Name,
                        DistinctTypes(elements),
                        elements[i].Span.Start,
                        _catalog));
                }
            }
        }

        return new TransportListValue(elements.AsReadOnly());
    }

    private static IReadOnlyList<TransportType> DistinctTypes(IReadOnlyList<ResolvedExpr> elements)
    {
        var types = new List<TransportType>();
        for (var i = 0; i < elements.Count; i++)
        {
            if (!types.Contains(elements[i].Type))
            {
                types.Add(elements[i].Type);
            }
        }

        return types;
    }

    private static AstValue? FindBoundValue(
        IReadOnlyList<BoundParameter> bound,
        Param parameter)
    {
        for (var i = 0; i < bound.Count; i++)
        {
            if (ReferenceEquals(bound[i].Param, parameter))
            {
                return bound[i].Value;
            }
        }

        return null;
    }
}
