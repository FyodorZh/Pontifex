using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Pontifex.Factory.Internal;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics.Internal
{
    // Resolves struct values following parsing.md §7.8: bind source fields in source
    // order, check missing required fields in schema order, then validate provided
    // fields in schema order. Field paths are dot-separated; known schema segments
    // are canonical lowercase and an unknown leaf keeps its raw input spelling. The
    // top-level parameter name is preserved for nested diagnostics.
    internal static class StructValidator
    {
        public static IReadOnlyList<StructFieldBinding> Validate(
            AstStruct astStruct,
            StructSchema schema,
            string paramName)
        {
            return Validate(astStruct, schema, paramName, string.Empty);
        }

        private static IReadOnlyList<StructFieldBinding> Validate(
            AstStruct astStruct,
            StructSchema schema,
            string paramName,
            string parentPath)
        {
            var bound = new Dictionary<StructField, AstValue>(FieldReferenceComparer.Instance);

            // 1. Bind source fields in source order.
            for (var i = 0; i < astStruct.Fields.Count; i++)
            {
                var field = astStruct.Fields[i];
                var name = NameNormalizer.Normalize(field.Name);
                var schemaField = FindByName(schema.Fields, name);

                if (schemaField is null)
                {
                    throw new SemanticAbort(SemanticErrorFactory.UnknownStructField(
                        JoinPath(parentPath, field.Name),
                        paramName,
                        field.NameSpan.Start,
                        schema));
                }

                var path = JoinPath(parentPath, schemaField.Name);
                if (bound.ContainsKey(schemaField))
                {
                    throw new SemanticAbort(SemanticErrorFactory.DuplicateStructField(
                        path,
                        field.NameSpan.Start));
                }

                bound.Add(schemaField, field.Value);
            }

            // 2. Missing required fields in schema order.
            for (var i = 0; i < schema.Fields.Count; i++)
            {
                var schemaField = schema.Fields[i];
                if (schemaField.Required && !bound.ContainsKey(schemaField))
                {
                    throw new SemanticAbort(SemanticErrorFactory.MissingRequiredStructField(
                        JoinPath(parentPath, schemaField.Name),
                        paramName,
                        astStruct.CloseOffset));
                }
            }

            // 3. Validate provided fields in schema order.
            var result = new List<StructFieldBinding>();
            for (var i = 0; i < schema.Fields.Count; i++)
            {
                var schemaField = schema.Fields[i];
                if (!bound.TryGetValue(schemaField, out var value))
                {
                    continue;
                }

                var path = JoinPath(parentPath, schemaField.Name);

                switch (schemaField)
                {
                    case StringField stringField:
                    {
                        if (value is not AstScalar scalar)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.StructFieldExpectedScalar(
                                path,
                                ParameterValidator.Shape(value),
                                value.Span.Start));
                        }

                        var reason = ParameterValidator.InvokeValidator(
                            stringField.Validate,
                            scalar.Text);
                        if (reason is not null)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.ValidatorFailedField(
                                path,
                                paramName,
                                reason,
                                value.Span.Start));
                        }

                        result.Add(new StructFieldBinding(
                            stringField,
                            new StringFieldValue(scalar.Text)));
                        break;
                    }

                    case NestedStructField nestedField:
                    {
                        if (value is not AstStruct nestedStruct)
                        {
                            throw new SemanticAbort(SemanticErrorFactory.StructFieldExpectedStruct(
                                path,
                                ParameterValidator.Shape(value),
                                value.Span.Start));
                        }

                        result.Add(new StructFieldBinding(
                            nestedField,
                            new NestedStructFieldValue(Validate(
                                nestedStruct,
                                nestedField.Schema,
                                paramName,
                                path))));
                        break;
                    }

                    default:
                        throw new ArgumentException(
                            "Unsupported struct field kind: " + schemaField.GetType().FullName,
                            nameof(schema));
                }
            }

            return result.AsReadOnly();
        }

        private static StructField? FindByName(IReadOnlyList<StructField> fields, string normalizedName)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].Name, normalizedName, StringComparison.Ordinal))
                {
                    return fields[i];
                }
            }

            return null;
        }

        private static string JoinPath(string parentPath, string name)
        {
            return parentPath.Length == 0 ? name : parentPath + "." + name;
        }

        // Record equality is structural; binding requires reference identity to the
        // schema's field instances.
        private sealed class FieldReferenceComparer : IEqualityComparer<StructField>
        {
            public static readonly FieldReferenceComparer Instance = new();

            public bool Equals(StructField? x, StructField? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(StructField obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
