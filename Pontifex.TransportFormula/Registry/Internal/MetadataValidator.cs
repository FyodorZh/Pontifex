using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Pontifex;
using Pontifex.TransportFormula.Internal;

namespace Pontifex.TransportFormula.Registry.Internal
{
    // Name, parameter, struct-schema, alias, and catalog-membership validation.
    // The registry builder owns the ordering from parsing.md §6.7; this type owns
    // the per-entity rules and the normalized metadata view.
    internal static class MetadataValidator
    {
        public static void ValidateCanonicalName(string? name)
        {
            if (!NameNormalizer.IsIdentifier(name))
            {
                throw RegistryErrorFactory.InvalidCanonicalName(name ?? string.Empty);
            }
        }

        public static void ValidateParamName(string? name)
        {
            if (!NameNormalizer.IsIdentifier(name))
            {
                throw RegistryErrorFactory.InvalidParamName(name ?? string.Empty);
            }
        }

        public static void ValidateAliasName(string? name)
        {
            if (!NameNormalizer.IsIdentifier(name))
            {
                throw RegistryErrorFactory.InvalidAliasName(name ?? string.Empty);
            }
        }

        public static void ValidateStructFieldName(string? name)
        {
            if (!NameNormalizer.IsIdentifier(name))
            {
                throw RegistryErrorFactory.InvalidStructFieldName(name ?? string.Empty);
            }
        }

        public static void ValidateTypeName(string? name)
        {
            if (!NameNormalizer.IsIdentifier(name))
            {
                throw RegistryErrorFactory.InvalidTypeName(name ?? string.Empty);
            }
        }

        // Rule 5: normalize type sets; reject an empty set. Any always has an empty
        // Types list; Exactly/OneOf keep their distinct types in declaration order.
        public static TransportTypeSet NormalizeTypeSet(TransportTypeSet? set)
        {
            if (set is null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            if (set.IsAny)
            {
                return new TransportTypeSet(TransportTypeSetKind.Any, Array.Empty<TransportType>());
            }

            var types = new List<TransportType>();
            if (set.Types is not null)
            {
                for (var i = 0; i < set.Types.Count; i++)
                {
                    var type = set.Types[i];
                    if (!types.Contains(type))
                    {
                        types.Add(type);
                    }
                }
            }

            if (types.Count == 0)
            {
                throw RegistryErrorFactory.EmptyTypeSet();
            }

            return new TransportTypeSet(set.Kind, types.AsReadOnly());
        }

        // Rule 6: list ranges.
        public static void ValidateListRange(TransportListParam param)
        {
            if (param.Min < 0 || (param.Max.HasValue && param.Max.Value < param.Min))
            {
                throw RegistryErrorFactory.InvalidListRange(param.Min, param.Max);
            }
        }

        // Rules 4-7, in order: duplicate parameter names, type-set normalization,
        // scalar parameter ranges, list ranges, then recursive struct-schema
        // validation. Returns the normalized parameter metadata snapshot.
        public static IReadOnlyList<Param> NormalizeParameters(
            string canonicalSchemeName,
            IReadOnlyList<Param>? parameters)
        {
            var source = parameters ?? Array.Empty<Param>();
            var count = source.Count;

            // Rule 4: duplicate parameter names within one constructor.
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var normalizedName = NameNormalizer.Normalize(source[i].Name);
                if (!names.Add(normalizedName))
                {
                    throw RegistryErrorFactory.DuplicateParamName(normalizedName, canonicalSchemeName);
                }
            }

            // Rule 5: normalize every type set in declaration order.
            var typeSets = new TransportTypeSet?[count];
            for (var i = 0; i < count; i++)
            {
                switch (source[i])
                {
                    case TransportParam transportParam:
                        typeSets[i] = NormalizeTypeSet(transportParam.Expected);
                        break;
                    case TransportListParam listParam:
                        typeSets[i] = NormalizeTypeSet(listParam.Expected);
                        break;
                }
            }

            // Rule 5 (continued): scalar parameter ranges, declaration order. A
            // full range pass follows the full type-set pass so a later scalar
            // range error precedes an earlier transport-list range error.
            for (var i = 0; i < count; i++)
            {
                switch (source[i])
                {
                    case IntegerParam integerParam:
                        ValidateIntegerRange(integerParam.Type);
                        break;
                    case DecimalParam decimalParam:
                        ValidateDecimalRange(decimalParam.Type);
                        break;
                }
            }

            // Rule 6: validate every transport-list range.
            for (var i = 0; i < count; i++)
            {
                if (source[i] is TransportListParam listParam)
                {
                    ValidateListRange(listParam);
                }
            }

            // Rule 7: validate struct schemas recursively, once per distinct
            // instance. Shared references are normalized to the same new instance;
            // cycles terminate through the memo before a field list is populated.
            var schemas = new StructSchema?[count];
            var normalizedSchemas = new Dictionary<StructSchema, StructSchema>(ReferenceComparer.Instance);
            for (var i = 0; i < count; i++)
            {
                if (source[i] is StructParam structParam)
                {
                    schemas[i] = NormalizeStructSchema(structParam.Schema, normalizedSchemas);
                }
            }

            var result = new List<Param>(count);
            for (var i = 0; i < count; i++)
            {
                var name = NameNormalizer.Normalize(source[i].Name);
                switch (source[i])
                {
                    case StringParam stringParam:
                        result.Add(new StringParam(
                            name,
                            stringParam.Required,
                            stringParam.Positional,
                            stringParam.Validate));
                        break;
                    case IntegerParam integerParam:
                        result.Add(new IntegerParam(
                            name,
                            integerParam.Required,
                            integerParam.Positional,
                            integerParam.Type));
                        break;
                    case DecimalParam decimalParam:
                        result.Add(new DecimalParam(
                            name,
                            decimalParam.Required,
                            decimalParam.Positional,
                            decimalParam.Type));
                        break;
                    case TransportParam transportParam:
                        result.Add(new TransportParam(
                            name,
                            transportParam.Required,
                            transportParam.Positional,
                            typeSets[i]!));
                        break;
                    case TransportListParam listParam:
                        result.Add(new TransportListParam(
                            name,
                            listParam.Required,
                            listParam.Positional,
                            typeSets[i]!,
                            listParam.Homogeneous,
                            listParam.Min,
                            listParam.Max));
                        break;
                    case StructParam structParam:
                        result.Add(new StructParam(
                            name,
                            structParam.Required,
                            structParam.Positional,
                            schemas[i]!));
                        break;
                    default:
                        throw new ArgumentException(
                            "Unsupported parameter kind: " + source[i].GetType().FullName,
                            nameof(parameters));
                }
            }

            return result.AsReadOnly();
        }

        // Rule 8: normalize aliases, ignore duplicates within the registration and
        // an alias equal to the constructor's own canonical name.
        public static IReadOnlyList<string> NormalizeAliases(
            string canonicalName,
            IReadOnlyList<string>? aliases)
        {
            var result = new List<string>();
            if (aliases is null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < aliases.Count; i++)
            {
                var normalized = NameNormalizer.Normalize(aliases[i]);
                if (string.Equals(normalized, canonicalName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!seen.Add(normalized))
                {
                    continue;
                }

                result.Add(normalized);
            }

            return result;
        }

        // Rule 11: the constructor type and every Expected member must be present
        // in the catalog.
        public static void ValidateCatalogMembership(
            TransportType constructorType,
            IReadOnlyList<Param> parameters,
            ITransportTypeCatalog catalog)
        {
            ValidateTypeInCatalog(constructorType, catalog);

            for (var i = 0; i < parameters.Count; i++)
            {
                switch (parameters[i])
                {
                    case TransportParam transportParam:
                        ValidateTypeSetInCatalog(transportParam.Expected, catalog);
                        break;
                    case TransportListParam listParam:
                        ValidateTypeSetInCatalog(listParam.Expected, catalog);
                        break;
                }
            }
        }

        public static void ValidateTypeInCatalog(TransportType type, ITransportTypeCatalog catalog)
        {
            if (!catalog.TryGetCanonicalName(type, out _))
            {
                throw RegistryErrorFactory.TypeNotInCatalog(type.ToString());
            }
        }

        public static string FormatTypeName(TransportType type, ITransportTypeCatalog catalog)
        {
            return catalog.TryGetCanonicalName(type, out var canonicalName)
                ? canonicalName!
                : type.ToString();
        }

        private static void ValidateTypeSetInCatalog(TransportTypeSet set, ITransportTypeCatalog catalog)
        {
            if (set.IsAny)
            {
                return;
            }

            for (var i = 0; i < set.Types.Count; i++)
            {
                ValidateTypeInCatalog(set.Types[i], catalog);
            }
        }

        private static void ValidateIntegerRange(IntegerType type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.Min > type.Max)
            {
                throw RegistryErrorFactory.InvalidIntegerRange(type.Min, type.Max);
            }
        }

        private static void ValidateDecimalRange(DecimalType type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.Min > type.Max)
            {
                throw RegistryErrorFactory.InvalidDecimalRange(type.Min, type.Max);
            }
        }

        private static void ValidateArrayRange(int min, int? max)
        {
            if (min < 0 || (max.HasValue && max.Value < min))
            {
                throw RegistryErrorFactory.InvalidArrayRange(min, max);
            }
        }

        private static void ValidateSingleTypeRange(SingleType type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            switch (type)
            {
                case StringType:
                    return;
                case IntegerType integerType:
                    ValidateIntegerRange(integerType);
                    return;
                case DecimalType decimalType:
                    ValidateDecimalRange(decimalType);
                    return;
                default:
                    throw new ArgumentException(
                        "Unsupported single type: " + type.GetType().FullName,
                        nameof(type));
            }
        }

        private static StructSchema NormalizeStructSchema(
            StructSchema schema,
            Dictionary<StructSchema, StructSchema> normalizedSchemas)
        {
            if (schema is null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (normalizedSchemas.TryGetValue(schema, out var existing))
            {
                return existing;
            }

            var fields = new StructFieldList();
            var result = new StructSchema(fields);
            normalizedSchemas.Add(schema, result);

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in schema.Fields)
            {
                // Validate the name before duplicates and kind.
                ValidateStructFieldName(field.Name);
                var name = NameNormalizer.Normalize(field.Name);
                if (!names.Add(name))
                {
                    throw RegistryErrorFactory.DuplicateStructField(name);
                }

                switch (field)
                {
                    case StringField stringField:
                        fields.Add(new StringField(name, stringField.Required, stringField.Validate));
                        break;
                    case IntegerField integerField:
                        ValidateIntegerRange(integerField.Type);
                        fields.Add(new IntegerField(name, integerField.Required, integerField.Type));
                        break;
                    case DecimalField decimalField:
                        ValidateDecimalRange(decimalField.Type);
                        fields.Add(new DecimalField(name, decimalField.Required, decimalField.Type));
                        break;
                    case ArrayField arrayField:
                        ValidateArrayRange(arrayField.Min, arrayField.Max);
                        ValidateSingleTypeRange(arrayField.ElementType);
                        fields.Add(new ArrayField(
                            name,
                            arrayField.Required,
                            arrayField.ElementType,
                            arrayField.Min,
                            arrayField.Max));
                        break;
                    case NestedStructField nestedField:
                        fields.Add(new NestedStructField(
                            name,
                            nestedField.Required,
                            NormalizeStructSchema(nestedField.Schema, normalizedSchemas)));
                        break;
                    default:
                        throw new ArgumentException(
                            "Unsupported struct field kind: " + field.GetType().FullName,
                            nameof(schema));
                }
            }

            return result;
        }

        // Record equality is structural and would collapse distinct schema
        // instances; registration semantics require reference identity for the
        // visited set.
        private sealed class ReferenceComparer : IEqualityComparer<StructSchema>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(StructSchema? x, StructSchema? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(StructSchema obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        // Read-only view over the normalized schema fields. A cyclic schema needs
        // the instance to exist before its fields are added, so the backing list is
        // populated after construction but is not exposed as a mutable list.
        private sealed class StructFieldList : IReadOnlyList<StructField>
        {
            private readonly List<StructField> _fields = new();

            public StructField this[int index] => _fields[index];

            public int Count => _fields.Count;

            public void Add(StructField field)
            {
                _fields.Add(field);
            }

            public IEnumerator<StructField> GetEnumerator()
            {
                return _fields.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
