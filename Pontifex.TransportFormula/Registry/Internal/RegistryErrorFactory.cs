using System.Globalization;
using Pontifex.TransportFormula.Internal;

namespace Pontifex.TransportFormula.Registry.Internal
{
    // Owns the exact E_REG_* message templates from parsing.md §13.4.
    // Registration errors carry no source offset and are phase Registration.
    internal static class RegistryErrorFactory
    {
        public static RegistrationException InvalidCanonicalName(string name)
        {
            return Create(
                "E_REG_INVALID_CANONICAL_NAME",
                "invalid canonical scheme name '" + name + "'");
        }

        public static RegistrationException DuplicateCanonicalName(string name)
        {
            return Create(
                "E_REG_DUPLICATE_CANONICAL_NAME",
                "canonical scheme '" + name + "' is already registered");
        }

        public static RegistrationException InvalidParamName(string name)
        {
            return Create(
                "E_REG_INVALID_PARAM_NAME",
                "invalid parameter name '" + name + "'");
        }

        public static RegistrationException DuplicateParamName(string name, string scheme)
        {
            return Create(
                "E_REG_DUPLICATE_PARAM_NAME",
                "parameter '" + name + "' is declared more than once in scheme '" + scheme + "'");
        }

        public static RegistrationException InvalidAliasName(string name)
        {
            return Create(
                "E_REG_INVALID_ALIAS_NAME",
                "invalid alias name '" + name + "'");
        }

        public static RegistrationException DuplicateAliasForType(string name, string type)
        {
            return Create(
                "E_REG_DUPLICATE_ALIAS_FOR_TYPE",
                "alias '" + name + "' for transport type " + type + " is already registered");
        }

        public static RegistrationException EmptyTypeSet()
        {
            return Create(
                "E_REG_EMPTY_TYPE_SET",
                "transport type set must contain at least one type");
        }

        public static RegistrationException InvalidListRange(int min, int? max)
        {
            return Create(
                "E_REG_INVALID_LIST_RANGE",
                "invalid transport list range: min " + min + ", max " + FormatMax(max));
        }

        public static RegistrationException InvalidIntegerRange(long min, long max)
        {
            return Create(
                "E_REG_INVALID_INTEGER_RANGE",
                "invalid integer range: min " + FormatLong(min) + ", max " + FormatLong(max));
        }

        public static RegistrationException InvalidDecimalRange(decimal min, decimal max)
        {
            return Create(
                "E_REG_INVALID_DECIMAL_RANGE",
                "invalid decimal range: min " + DecimalText.Format(min)
                + ", max " + DecimalText.Format(max));
        }

        public static RegistrationException InvalidArrayRange(int min, int? max)
        {
            return Create(
                "E_REG_INVALID_ARRAY_RANGE",
                "invalid array range: min " + min + ", max " + FormatMax(max));
        }

        public static RegistrationException DuplicateStructField(string name)
        {
            return Create(
                "E_REG_DUPLICATE_STRUCT_FIELD",
                "struct field '" + name + "' is declared more than once");
        }

        public static RegistrationException InvalidStructFieldName(string name)
        {
            return Create(
                "E_REG_INVALID_STRUCT_FIELD_NAME",
                "invalid struct field name '" + name + "'");
        }

        public static RegistrationException InvalidTypeName(string name)
        {
            return Create(
                "E_REG_INVALID_TYPE_NAME",
                "invalid transport type name '" + name + "'");
        }

        public static RegistrationException DuplicateTypeName(string name)
        {
            return Create(
                "E_REG_DUPLICATE_TYPE_NAME",
                "transport type name '" + name + "' is already registered");
        }

        public static RegistrationException TypeNotInCatalog(string type)
        {
            return Create(
                "E_REG_TYPE_NOT_IN_CATALOG",
                "transport type " + type + " is not present in the type catalog");
        }

        // Builder lifecycle error, defined by parsing.md §6.7 rule 12 and
        // catalogued in §13.4.
        public static RegistrationException BuilderSealed()
        {
            return Create(
                "E_REG_BUILDER_SEALED",
                "registry builder has already been built");
        }

        private static string FormatMax(int? max)
        {
            return max.HasValue ? max.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unbounded";
        }

        private static string FormatLong(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static RegistrationException Create(string code, string message)
        {
            return new RegistrationException(code, message);
        }
    }
}
