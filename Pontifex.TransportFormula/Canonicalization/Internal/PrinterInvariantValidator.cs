using System;
using System.Collections.Generic;
using System.Globalization;
using Pontifex;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Registry;
using Pontifex.TransportFormula.Semantics;

namespace Pontifex.TransportFormula.Canonicalization.Internal
{
    // Validates every resolved-tree invariant that the printer relies on, following
    // parsing.md §10 and §8. Checks are depth-first over values. Within a transport,
    // required parameters are checked first, then provided parameters are checked
    // for declaration membership, duplicates, and declaration order; the transport
    // type is checked next, and finally every provided parameter in declaration
    // order. Cast identity and target checks precede validation of the inner
    // transport. Every failure is a PrinterException with phase Printing and no
    // span.
    internal sealed class PrinterInvariantValidator
    {
        private readonly ITransportTypeCatalog _catalog;

        public PrinterInvariantValidator(ITransportTypeCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public void Validate(ResolvedFormula formula)
        {
            if (formula is null)
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }

            ValidateExpression(formula.Root);
        }

        private void ValidateExpression(ResolvedExpr? expression)
        {
            switch (expression)
            {
                case ResolvedCast cast:
                    ValidateCast(cast);
                    return;
                case ResolvedTransport transport:
                    ValidateTransport(transport);
                    return;
                default:
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }
        }

        private void ValidateCast(ResolvedCast cast)
        {
            if (cast.Inner is null)
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }

            if (cast.Inner.Type == cast.Target)
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.IdentityCastNode);
            }

            if (!_catalog.TryGetCanonicalName(cast.Target, out _))
            {
                throw CanonicalizationErrorFactory.InvalidCastTarget(cast.Target);
            }

            ValidateTransport(cast.Inner);
        }

        private void ValidateTransport(ResolvedTransport transport)
        {
            var constructor = transport.Constructor;
            if (constructor is null || transport.Provided is null)
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }

            EnsureCanonicalName(constructor.Name);

            var parameters = constructor.Params ?? Array.Empty<Param>();
            var provided = transport.Provided;

            // Required parameters, declaration order.
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter.Required && !ContainsParam(provided, parameter))
                {
                    throw CanonicalizationErrorFactory.MissingRequiredParam(
                        constructor.Name,
                        parameter.Name);
                }
            }

            // Unknown provided parameter, by reference identity.
            for (var i = 0; i < provided.Count; i++)
            {
                var parameter = provided[i].Param;
                if (parameter is null || !ContainsParameter(parameters, parameter))
                {
                    throw CanonicalizationErrorFactory.UnknownParam(
                        constructor.Name,
                        parameter?.Name ?? string.Empty);
                }
            }

            // Duplicate provided parameter.
            for (var i = 0; i < provided.Count; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    if (ReferenceEquals(provided[j].Param, provided[i].Param))
                    {
                        throw CanonicalizationErrorFactory.DuplicateParam(
                            constructor.Name,
                            provided[i].Param.Name);
                    }
                }
            }

            // Provided parameters must follow declaration order.
            var lastIndex = -1;
            for (var i = 0; i < provided.Count; i++)
            {
                var index = IndexOfParameter(parameters, provided[i].Param);
                if (index <= lastIndex)
                {
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.ProvidedParameterOrderViolation);
                }

                lastIndex = index;
            }

            // ResolvedTransport.Type is Constructor.Type by construction; the check
            // keeps the documented invariant explicit and guards future DTO changes.
            if (transport.Type != constructor.Type)
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.ConstructorTypeMismatch);
            }

            if (!_catalog.TryGetCanonicalName(transport.Type, out _))
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.TransportTypeNotInCatalog);
            }

            // Provided values, declaration order, independent of print order.
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var binding = FindBinding(provided, parameter);
                if (binding is null)
                {
                    continue;
                }

                ValidateParameter(constructor, parameter, binding.Value);
            }
        }

        private void ValidateParameter(
            TransportConstructorInfo constructor,
            Param parameter,
            ResolvedValue? value)
        {
            EnsureCanonicalName(parameter.Name);

            switch (parameter)
            {
                case StringParam:
                {
                    if (value is not StringValue stringValue)
                    {
                        throw CanonicalizationErrorFactory.ParamKindMismatch(
                            parameter.Name,
                            constructor.Name,
                            ValueKind(value),
                            "string");
                    }

                    ScalarFormatter.EnsurePrintable(stringValue.Text);
                    return;
                }

                case IntegerParam integerParam:
                {
                    if (value is not IntegerValue integerValue)
                    {
                        throw CanonicalizationErrorFactory.ParamKindMismatch(
                            parameter.Name,
                            constructor.Name,
                            ValueKind(value),
                            "integer");
                    }

                    if (integerValue.Value < integerParam.Type.Min
                        || integerValue.Value > integerParam.Type.Max)
                    {
                        throw CanonicalizationErrorFactory.ValueOutOfRange(
                            parameter.Name,
                            integerValue.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    return;
                }

                case DecimalParam decimalParam:
                {
                    if (value is not DecimalValue decimalValue)
                    {
                        throw CanonicalizationErrorFactory.ParamKindMismatch(
                            parameter.Name,
                            constructor.Name,
                            ValueKind(value),
                            "decimal");
                    }

                    if (decimalValue.Value < decimalParam.Type.Min
                        || decimalValue.Value > decimalParam.Type.Max)
                    {
                        throw CanonicalizationErrorFactory.ValueOutOfRange(
                            parameter.Name,
                            DecimalText.Format(decimalValue.Value));
                    }

                    return;
                }

                case TransportParam transportParam:
                {
                    if (value is not ResolvedExpr expression)
                    {
                        throw CanonicalizationErrorFactory.ParamKindMismatch(
                            parameter.Name,
                            constructor.Name,
                            ValueKind(value),
                            "transport");
                    }

                    if (!transportParam.Expected.Contains(expression.Type))
                    {
                        throw CanonicalizationErrorFactory.InconsistentTree(
                            CanonicalizationErrorFactory.ExpectedTypeMembershipViolation);
                    }

                    ValidateExpression(expression);
                    return;
                }

                case TransportListParam listParam:
                {
                    if (value is not TransportListValue listValue)
                    {
                        throw CanonicalizationErrorFactory.ParamKindMismatch(
                            parameter.Name,
                            constructor.Name,
                            ValueKind(value),
                            "transport list");
                    }

                    var elements = listValue.Elements;
                    if (elements is null)
                    {
                        throw CanonicalizationErrorFactory.InconsistentTree(
                            CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
                    }

                    for (var i = 0; i < elements.Count; i++)
                    {
                        var element = elements[i];
                        if (element is null || !listParam.Expected.Contains(element.Type))
                        {
                            throw CanonicalizationErrorFactory.InconsistentTree(
                                CanonicalizationErrorFactory.ExpectedTypeMembershipViolation);
                        }
                    }

                    if (elements.Count < listParam.Min
                        || (listParam.Max.HasValue && elements.Count > listParam.Max.Value))
                    {
                        throw CanonicalizationErrorFactory.ListArity(
                            parameter.Name,
                            constructor.Name,
                            elements.Count,
                            listParam.Min,
                            listParam.Max);
                    }

                    if (listParam.Homogeneous && elements.Count > 1)
                    {
                        var firstType = elements[0].Type;
                        for (var i = 1; i < elements.Count; i++)
                        {
                            if (elements[i].Type != firstType)
                            {
                                throw CanonicalizationErrorFactory.ListHomogeneity(
                                    parameter.Name,
                                    constructor.Name);
                            }
                        }
                    }

                    for (var i = 0; i < elements.Count; i++)
                    {
                        ValidateExpression(elements[i]);
                    }

                    return;
                }

                case StructParam structParam:
                {
                    if (value is not StructValue structValue)
                    {
                        throw CanonicalizationErrorFactory.ParamKindMismatch(
                            parameter.Name,
                            constructor.Name,
                            ValueKind(value),
                            "struct");
                    }

                    if (structValue.Fields is null || structParam.Schema is null
                        || structParam.Schema.Fields is null)
                    {
                        throw CanonicalizationErrorFactory.InconsistentTree(
                            CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
                    }

                    ValidateStruct(
                        constructor.Name,
                        parameter.Name,
                        structParam.Schema,
                        structValue.Fields,
                        string.Empty);
                    return;
                }

                default:
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }
        }

        private void ValidateStruct(
            string scheme,
            string parameterName,
            StructSchema schema,
            IReadOnlyList<StructFieldBinding> fields,
            string parentPath)
        {
            // Unknown fields, source order.
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i].Field;
                if (field is null || !ContainsField(schema.Fields, field))
                {
                    throw CanonicalizationErrorFactory.StructFieldUnknown(
                        parameterName,
                        scheme,
                        JoinPath(parentPath, field?.Name ?? string.Empty));
                }
            }

            // Missing required fields, schema order.
            for (var i = 0; i < schema.Fields.Count; i++)
            {
                var schemaField = schema.Fields[i];
                if (schemaField.Required && !ContainsFieldBinding(fields, schemaField))
                {
                    throw CanonicalizationErrorFactory.StructFieldMissing(
                        parameterName,
                        scheme,
                        JoinPath(parentPath, schemaField.Name));
                }
            }

            // Duplicate fields.
            for (var i = 0; i < fields.Count; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    if (ReferenceEquals(fields[j].Field, fields[i].Field))
                    {
                        throw CanonicalizationErrorFactory.StructFieldDuplicate(
                            parameterName,
                            scheme,
                            JoinPath(parentPath, fields[i].Field.Name));
                    }
                }
            }

            // Provided fields must follow schema order.
            var lastIndex = -1;
            for (var i = 0; i < fields.Count; i++)
            {
                var index = IndexOfField(schema.Fields, fields[i].Field);
                if (index <= lastIndex)
                {
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.StructFieldOrderViolation);
                }

                lastIndex = index;
            }

            // Provided fields, schema order.
            for (var i = 0; i < schema.Fields.Count; i++)
            {
                var schemaField = schema.Fields[i];
                var binding = FindFieldBinding(fields, schemaField);
                if (binding is null)
                {
                    continue;
                }

                EnsureCanonicalName(schemaField.Name);
                var path = JoinPath(parentPath, schemaField.Name);

                switch (schemaField)
                {
                    case StringField:
                    {
                        if (binding.Value is not StringValue stringValue)
                        {
                            throw CanonicalizationErrorFactory.StructFieldKindMismatch(
                                path,
                                parameterName,
                                ValueKind(binding.Value),
                                "string");
                        }

                        ScalarFormatter.EnsurePrintable(stringValue.Text);
                        break;
                    }

                    case IntegerField integerField:
                    {
                        if (binding.Value is not IntegerValue integerValue)
                        {
                            throw CanonicalizationErrorFactory.StructFieldKindMismatch(
                                path,
                                parameterName,
                                ValueKind(binding.Value),
                                "integer");
                        }

                        if (integerValue.Value < integerField.Type.Min
                            || integerValue.Value > integerField.Type.Max)
                        {
                            throw CanonicalizationErrorFactory.ValueOutOfRange(
                                JoinPath(parameterName, path),
                                integerValue.Value.ToString(CultureInfo.InvariantCulture));
                        }

                        break;
                    }

                    case DecimalField decimalField:
                    {
                        if (binding.Value is not DecimalValue decimalValue)
                        {
                            throw CanonicalizationErrorFactory.StructFieldKindMismatch(
                                path,
                                parameterName,
                                ValueKind(binding.Value),
                                "decimal");
                        }

                        if (decimalValue.Value < decimalField.Type.Min
                            || decimalValue.Value > decimalField.Type.Max)
                        {
                            throw CanonicalizationErrorFactory.ValueOutOfRange(
                                JoinPath(parameterName, path),
                                DecimalText.Format(decimalValue.Value));
                        }

                        break;
                    }

                    case ArrayField arrayField:
                    {
                        if (binding.Value is not ArrayValue arrayValue)
                        {
                            throw CanonicalizationErrorFactory.StructFieldKindMismatch(
                                path,
                                parameterName,
                                ValueKind(binding.Value),
                                "array");
                        }

                        var elements = arrayValue.Elements;
                        if (elements is null)
                        {
                            throw CanonicalizationErrorFactory.InconsistentTree(
                                CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
                        }

                        if (elements.Count < arrayField.Min
                            || (arrayField.Max.HasValue && elements.Count > arrayField.Max.Value))
                        {
                            throw CanonicalizationErrorFactory.ArrayArity(
                                path,
                                parameterName,
                                elements.Count,
                                arrayField.Min,
                                arrayField.Max);
                        }

                        for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++)
                        {
                            ValidateArrayElement(
                                path,
                                parameterName,
                                elementIndex,
                                arrayField.ElementType,
                                elements[elementIndex]);
                        }

                        break;
                    }

                    case NestedStructField nestedField:
                    {
                        if (binding.Value is not StructValue nestedValue)
                        {
                            throw CanonicalizationErrorFactory.StructFieldKindMismatch(
                                path,
                                parameterName,
                                ValueKind(binding.Value),
                                "struct");
                        }

                        if (nestedValue.Fields is null || nestedField.Schema is null
                            || nestedField.Schema.Fields is null)
                        {
                            throw CanonicalizationErrorFactory.InconsistentTree(
                                CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
                        }

                        ValidateStruct(
                            scheme,
                            parameterName,
                            nestedField.Schema,
                            nestedValue.Fields,
                            path);
                        break;
                    }

                    default:
                        throw CanonicalizationErrorFactory.InconsistentTree(
                            CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
                }
            }
        }

        private static void ValidateArrayElement(
            string path,
            string parameterName,
            int index,
            SingleType elementType,
            DataValue element)
        {
            var elementPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

            switch (elementType)
            {
                case StringType:
                    if (element is not StringValue stringValue)
                    {
                        throw CanonicalizationErrorFactory.ArrayElementType(
                            elementPath,
                            parameterName,
                            "string",
                            ValueKind(element));
                    }

                    ScalarFormatter.EnsurePrintable(stringValue.Text);
                    return;

                case IntegerType integerType:
                    if (element is not IntegerValue integerValue)
                    {
                        throw CanonicalizationErrorFactory.ArrayElementType(
                            elementPath,
                            parameterName,
                            "integer",
                            ValueKind(element));
                    }

                    if (integerValue.Value < integerType.Min
                        || integerValue.Value > integerType.Max)
                    {
                        throw CanonicalizationErrorFactory.ValueOutOfRange(
                            JoinPath(parameterName, elementPath),
                            integerValue.Value.ToString(CultureInfo.InvariantCulture));
                    }

                    return;

                case DecimalType decimalType:
                    if (element is not DecimalValue decimalValue)
                    {
                        throw CanonicalizationErrorFactory.ArrayElementType(
                            elementPath,
                            parameterName,
                            "decimal",
                            ValueKind(element));
                    }

                    if (decimalValue.Value < decimalType.Min
                        || decimalValue.Value > decimalType.Max)
                    {
                        throw CanonicalizationErrorFactory.ValueOutOfRange(
                            JoinPath(parameterName, elementPath),
                            DecimalText.Format(decimalValue.Value));
                    }

                    return;

                default:
                    throw CanonicalizationErrorFactory.InconsistentTree(
                        CanonicalizationErrorFactory.UnspecifiedInvariantViolation);
            }
        }

        private static void EnsureCanonicalName(string? name)
        {
            if (name is null
                || !NameNormalizer.IsIdentifier(name)
                || !string.Equals(NameNormalizer.Normalize(name), name, StringComparison.Ordinal))
            {
                throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.NonCanonicalName);
            }
        }

        private static string ValueKind(ResolvedValue? value)
        {
            return value switch
            {
                StringValue => "string",
                IntegerValue => "integer",
                DecimalValue => "decimal",
                StructValue => "struct",
                ArrayValue => "array",
                TransportListValue => "transport list",
                ResolvedExpr => "transport",
                _ => throw CanonicalizationErrorFactory.InconsistentTree(
                    CanonicalizationErrorFactory.UnspecifiedInvariantViolation)
            };
        }

        private static bool ContainsParam(
            IReadOnlyList<ParamBinding> provided,
            Param parameter)
        {
            return FindBinding(provided, parameter) is not null;
        }

        private static ParamBinding? FindBinding(
            IReadOnlyList<ParamBinding> provided,
            Param parameter)
        {
            for (var i = 0; i < provided.Count; i++)
            {
                if (ReferenceEquals(provided[i].Param, parameter))
                {
                    return provided[i];
                }
            }

            return null;
        }

        private static bool ContainsParameter(IReadOnlyList<Param> parameters, Param parameter)
        {
            return IndexOfParameter(parameters, parameter) >= 0;
        }

        private static int IndexOfParameter(IReadOnlyList<Param> parameters, Param parameter)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (ReferenceEquals(parameters[i], parameter))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ContainsField(IReadOnlyList<StructField> fields, StructField field)
        {
            return IndexOfField(fields, field) >= 0;
        }

        private static bool ContainsFieldBinding(
            IReadOnlyList<StructFieldBinding> fields,
            StructField field)
        {
            return FindFieldBinding(fields, field) is not null;
        }

        private static StructFieldBinding? FindFieldBinding(
            IReadOnlyList<StructFieldBinding> fields,
            StructField field)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (ReferenceEquals(fields[i].Field, field))
                {
                    return fields[i];
                }
            }

            return null;
        }

        private static int IndexOfField(IReadOnlyList<StructField> fields, StructField field)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (ReferenceEquals(fields[i], field))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string JoinPath(string parentPath, string name)
        {
            return parentPath.Length == 0 ? name : parentPath + "." + name;
        }
    }
}
