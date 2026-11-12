using System;
using System.Collections.Generic;
using System.Globalization;
using Pontifex;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Registry;

namespace Pontifex.TransportFormula.Semantics.Internal
{
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

                        provided.Add(new ParamBinding(stringParam, new StringValue(scalar.Text)));
                        break;
                    }

                    case IntegerParam integerParam:
                    {
                        if (value is not AstScalar scalar)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.ExpectedScalar(
                                integerParam.Name,
                                Shape(value),
                                value.Span.Start));
                        }

                        var reason = ParseInteger(scalar.Text, integerParam.Type, out var parsed);
                        if (reason is not null)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.ValidatorFailedParam(
                                integerParam.Name,
                                reason,
                                value.Span.Start));
                        }

                        provided.Add(new ParamBinding(integerParam, new IntegerValue(parsed)));
                        break;
                    }

                    case DecimalParam decimalParam:
                    {
                        if (value is not AstScalar scalar)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.ExpectedScalar(
                                decimalParam.Name,
                                Shape(value),
                                value.Span.Start));
                        }

                        var reason = ParseDecimal(scalar.Text, decimalParam.Type, out var parsed);
                        if (reason is not null)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.ValidatorFailedParam(
                                decimalParam.Name,
                                reason,
                                value.Span.Start));
                        }

                        provided.Add(new ParamBinding(decimalParam, new DecimalValue(parsed)));
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
                        provided.Add(new ParamBinding(transportParam, resolved));
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

        // Parses a signed 64-bit integer with no whitespace, decimal point, or
        // exponent, then checks the inclusive range. A non-parse is a parse
        // failure; only a parsed value outside the range is an out-of-range
        // failure.
        public static string? ParseInteger(string text, IntegerType type, out long value)
        {
            if (!long.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return "expected an integer";
            }

            if (value < type.Min || value > type.Max)
            {
                return "out of range ["
                    + type.Min.ToString(CultureInfo.InvariantCulture) + ", "
                    + type.Max.ToString(CultureInfo.InvariantCulture) + "]";
            }

            return null;
        }

        // Parses an exactly representable decimal, then checks the inclusive
        // range. Overflow, underflow, and precision loss are parse failures.
        public static string? ParseDecimal(string text, DecimalType type, out decimal value)
        {
            if (!DecimalText.TryParse(text, out value))
            {
                return "expected a decimal";
            }

            if (value < type.Min || value > type.Max)
            {
                return "out of range ["
                    + DecimalText.Format(type.Min) + ", "
                    + DecimalText.Format(type.Max) + "]";
            }

            return null;
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
                    return "list";
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
                var element = list.Elements[i];
                if (element is not AstExpr expression)
                {
                    throw new SemanticAbort(SemanticErrorFactory.ListElementType(
                        parameter.Name,
                        Shape(element),
                        element.Span.Start));
                }

                elements.Add(resolveExpression(expression, parameter.Expected));
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
}
