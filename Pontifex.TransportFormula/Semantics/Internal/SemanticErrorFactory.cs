using System;
using System.Collections.Generic;
using Pontifex;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Registry;

namespace Pontifex.TransportFormula.Semantics.Internal
{
    // Owns the exact E_SEM_* message templates from parsing.md §13.3. Every error is
    // phase Resolution and carries the offset defined by §12.2 as a zero-length
    // span at that offset. Placeholder spelling and list ordering follow §13.3.
    internal static class SemanticErrorFactory
    {
        public const string ValidationFailedReason = "validation failed";

        public static FactoryError UnknownScheme(string rawScheme, int offset, IRegistry registry)
        {
            return Create(
                "E_SEM_UNKNOWN_SCHEME",
                "unknown transport scheme '" + rawScheme + "'; " + FormatKnownSchemes(registry),
                offset);
        }

        public static FactoryError NoVariantForExpectedType(
            string rawScheme,
            int offset,
            TransportTypeSet expected,
            IReadOnlyList<TransportConstructorInfo> allCandidates,
            ITransportTypeCatalog catalog)
        {
            return Create(
                "E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE",
                "scheme '" + rawScheme + "' has no variant for expected transport type "
                + FormatTypeSet(expected, catalog)
                + "; available: " + FormatVariants(allCandidates, catalog),
                offset);
        }

        public static FactoryError AmbiguousScheme(
            string rawScheme,
            int offset,
            IReadOnlyList<TransportConstructorInfo> candidates,
            ITransportTypeCatalog catalog)
        {
            return Create(
                "E_SEM_AMBIGUOUS_SCHEME",
                "scheme '" + rawScheme + "' is ambiguous; candidates: "
                + FormatVariants(candidates, catalog),
                offset);
        }

        public static FactoryError UnknownParam(
            string rawName,
            string rawScheme,
            int offset,
            IReadOnlyList<Param> parameters)
        {
            return Create(
                "E_SEM_UNKNOWN_PARAM",
                "unknown parameter '" + rawName + "' for scheme '" + rawScheme + "'; "
                + FormatParamExpectation(parameters),
                offset);
        }

        public static FactoryError DuplicateParam(string rawName, int offset)
        {
            return Create(
                "E_SEM_DUPLICATE_PARAM",
                "parameter '" + rawName + "' is specified more than once",
                offset);
        }

        public static FactoryError MissingRequiredParam(
            string paramName,
            string rawScheme,
            int offset)
        {
            return Create(
                "E_SEM_MISSING_REQUIRED_PARAM",
                "missing required parameter '" + paramName + "' for scheme '" + rawScheme + "'",
                offset);
        }

        public static FactoryError TooManyPositional(
            string rawScheme,
            int maximum,
            int offset)
        {
            return Create(
                "E_SEM_TOO_MANY_POSITIONAL",
                "too many positional arguments for scheme '" + rawScheme
                + "'; expected at most " + FormatNumber(maximum),
                offset);
        }

        public static FactoryError ExpectedScalar(string paramName, string shape, int offset)
        {
            return Create(
                "E_SEM_EXPECTED_SCALAR",
                "parameter '" + paramName + "' expects a scalar value, got " + shape,
                offset);
        }

        public static FactoryError ExpectedTransport(string paramName, string shape, int offset)
        {
            return Create(
                "E_SEM_EXPECTED_TRANSPORT",
                "parameter '" + paramName + "' expects a transport, got " + shape,
                offset);
        }

        public static FactoryError ExpectedTransportList(string paramName, string shape, int offset)
        {
            return Create(
                "E_SEM_EXPECTED_TRANSPORT_LIST",
                "parameter '" + paramName + "' expects a transport list, got " + shape,
                offset);
        }

        public static FactoryError ExpectedStruct(string paramName, string shape, int offset)
        {
            return Create(
                "E_SEM_EXPECTED_STRUCT",
                "parameter '" + paramName + "' expects a struct, got " + shape,
                offset);
        }

        public static FactoryError ListElementType(
            string paramName,
            string shape,
            int offset)
        {
            return Create(
                "E_SEM_LIST_ELEMENT_TYPE",
                "transport list for '" + paramName + "' expects transport elements, got " + shape,
                offset);
        }

        public static FactoryError ListMin(string paramName, int min, int count, int offset)
        {
            return Create(
                "E_SEM_LIST_MIN",
                "transport list for '" + paramName + "' requires at least " + FormatNumber(min)
                + " element(s), got " + FormatNumber(count),
                offset);
        }

        public static FactoryError ListMax(string paramName, int max, int count, int offset)
        {
            return Create(
                "E_SEM_LIST_MAX",
                "transport list for '" + paramName + "' requires at most " + FormatNumber(max)
                + " element(s), got " + FormatNumber(count),
                offset);
        }

        public static FactoryError ListHomogeneity(
            string paramName,
            IReadOnlyList<TransportType> elementTypes,
            int offset,
            ITransportTypeCatalog catalog)
        {
            return Create(
                "E_SEM_LIST_HOMOGENEITY",
                "transport list for '" + paramName + "' must contain one transport type; found "
                + FormatTypes(elementTypes, catalog),
                offset);
        }

        public static FactoryError UnknownStructField(
            string path,
            string paramName,
            int offset,
            StructSchema schema)
        {
            return Create(
                "E_SEM_UNKNOWN_STRUCT_FIELD",
                "unknown field '" + path + "' in struct for parameter '" + paramName + "'; "
                + FormatFieldExpectation(schema),
                offset);
        }

        public static FactoryError DuplicateStructField(string path, int offset)
        {
            return Create(
                "E_SEM_DUPLICATE_STRUCT_FIELD",
                "field '" + path + "' is specified more than once",
                offset);
        }

        public static FactoryError MissingRequiredStructField(
            string path,
            string paramName,
            int offset)
        {
            return Create(
                "E_SEM_MISSING_REQUIRED_STRUCT_FIELD",
                "missing required field '" + path + "' in struct for parameter '" + paramName + "'",
                offset);
        }

        public static FactoryError StructFieldExpectedScalar(string path, string shape, int offset)
        {
            return Create(
                "E_SEM_STRUCT_FIELD_EXPECTED_SCALAR",
                "field '" + path + "' expects a scalar value, got " + shape,
                offset);
        }

        public static FactoryError StructFieldExpectedStruct(string path, string shape, int offset)
        {
            return Create(
                "E_SEM_STRUCT_FIELD_EXPECTED_STRUCT",
                "field '" + path + "' expects a struct, got " + shape,
                offset);
        }

        public static FactoryError StructFieldExpectedArray(string path, string shape, int offset)
        {
            return Create(
                "E_SEM_STRUCT_FIELD_EXPECTED_ARRAY",
                "field '" + path + "' expects an array, got " + shape,
                offset);
        }

        public static FactoryError ArrayElementType(
            string path,
            string paramName,
            string expected,
            string actual,
            int offset)
        {
            return Create(
                "E_SEM_ARRAY_ELEMENT_TYPE",
                "array element '" + path + "' of parameter '" + paramName
                + "' expects " + expected + ", got " + actual,
                offset);
        }

        public static FactoryError ArrayMin(
            string path,
            string paramName,
            int min,
            int count,
            int offset)
        {
            return Create(
                "E_SEM_ARRAY_MIN",
                "array field '" + path + "' of parameter '" + paramName
                + "' requires at least " + FormatNumber(min)
                + " element(s), got " + FormatNumber(count),
                offset);
        }

        public static FactoryError ArrayMax(
            string path,
            string paramName,
            int max,
            int count,
            int offset)
        {
            return Create(
                "E_SEM_ARRAY_MAX",
                "array field '" + path + "' of parameter '" + paramName
                + "' requires at most " + FormatNumber(max)
                + " element(s), got " + FormatNumber(count),
                offset);
        }

        public static FactoryError UnknownTransportType(
            string rawName,
            int offset,
            ITransportTypeCatalog catalog)
        {
            return Create(
                "E_SEM_UNKNOWN_TRANSPORT_TYPE",
                "unknown transport type '" + rawName + "'; " + FormatKnownTypes(catalog),
                offset);
        }

        public static FactoryError CastTypeMismatch(
            TransportType target,
            TransportTypeSet expected,
            int offset,
            ITransportTypeCatalog catalog)
        {
            return Create(
                "E_SEM_CAST_TYPE_MISMATCH",
                "cast produces transport type " + FormatTypeName(target, catalog)
                + ", but expected " + FormatTypeSet(expected, catalog),
                offset);
        }

        public static FactoryError ValidatorFailedParam(
            string paramName,
            string reason,
            int offset)
        {
            return Create(
                "E_SEM_VALIDATOR_FAILED",
                "parameter '" + paramName + "' is invalid: " + reason,
                offset);
        }

        public static FactoryError ValidatorFailedField(
            string path,
            string paramName,
            string reason,
            int offset)
        {
            return Create(
                "E_SEM_VALIDATOR_FAILED",
                "field '" + path + "' of parameter '" + paramName + "' is invalid: " + reason,
                offset);
        }

        public static string FormatTypeSet(TransportTypeSet set, ITransportTypeCatalog catalog)
        {
            if (set.IsAny)
            {
                return "any transport type";
            }

            var names = new List<string>();
            if (set.Types is not null)
            {
                for (var i = 0; i < set.Types.Count; i++)
                {
                    var name = FormatTypeName(set.Types[i], catalog);
                    if (!names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }

            names.Sort(StringComparer.Ordinal);
            return string.Join(" or ", names);
        }

        // Entries are `name (Type)`, sorted by canonical scheme name and then by
        // canonical type name.
        public static string FormatVariants(
            IReadOnlyList<TransportConstructorInfo> candidates,
            ITransportTypeCatalog catalog)
        {
            var entries = new List<VariantEntry>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                entries.Add(new VariantEntry(
                    candidate.Name,
                    FormatTypeName(candidate.Type, catalog)));
            }

            entries.Sort(CompareVariants);

            var formatted = new List<string>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                formatted.Add(entries[i].Name + " (" + entries[i].TypeName + ")");
            }

            return string.Join(", ", formatted);
        }

        public static string FormatParamExpectation(IReadOnlyList<Param> parameters)
        {
            if (parameters.Count == 0)
            {
                return "the scheme has no parameters";
            }

            var names = new List<string>(parameters.Count);
            for (var i = 0; i < parameters.Count; i++)
            {
                names.Add("'" + parameters[i].Name + "'");
            }

            return "one of: " + string.Join(", ", names);
        }

        public static string FormatFieldExpectation(StructSchema schema)
        {
            if (schema.Fields.Count == 0)
            {
                return "the struct has no fields";
            }

            var names = new List<string>(schema.Fields.Count);
            for (var i = 0; i < schema.Fields.Count; i++)
            {
                names.Add("'" + schema.Fields[i].Name + "'");
            }

            return "one of: " + string.Join(", ", names);
        }

        public static string FormatKnownSchemes(IRegistry registry)
        {
            var names = new List<string>(registry.AllSchemeNames);
            if (names.Count == 0)
            {
                return "no schemes are registered";
            }

            names.Sort(StringComparer.Ordinal);
            return "known schemes: " + string.Join(", ", names);
        }

        public static string FormatKnownTypes(ITransportTypeCatalog catalog)
        {
            var names = new List<string>();
            for (var i = 0; i < catalog.AllTypes.Count; i++)
            {
                var name = FormatTypeName(catalog.AllTypes[i], catalog);
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count == 0)
            {
                return "no transport types are registered";
            }

            names.Sort(StringComparer.Ordinal);
            return "known types: " + string.Join(", ", names);
        }

        private static string FormatTypes(
            IReadOnlyList<TransportType> types,
            ITransportTypeCatalog catalog)
        {
            var names = new List<string>();
            for (var i = 0; i < types.Count; i++)
            {
                var name = FormatTypeName(types[i], catalog);
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }

            return string.Join(", ", names);
        }

        private static string FormatTypeName(TransportType type, ITransportTypeCatalog catalog)
        {
            return catalog.TryGetCanonicalName(type, out var canonicalName)
                ? canonicalName!
                : type.ToString();
        }

        private static string FormatNumber(int value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int CompareVariants(VariantEntry x, VariantEntry y)
        {
            var byName = string.CompareOrdinal(x.Name, y.Name);
            return byName != 0 ? byName : string.CompareOrdinal(x.TypeName, y.TypeName);
        }

        private static FactoryError Create(string code, string message, int offset)
        {
            return FormulaErrorFactory.Create(FactoryPhase.Resolution, code, message, offset);
        }

        private readonly struct VariantEntry
        {
            public VariantEntry(string name, string typeName)
            {
                Name = name;
                TypeName = typeName;
            }

            public string Name { get; }

            public string TypeName { get; }
        }
    }
}
