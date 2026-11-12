using System;
using System.Globalization;
using Pontifex;

namespace Pontifex.TransportFormula.Canonicalization.Internal
{
    // Owns the exact E_PRN_* message templates from parsing.md §13.5. Every error is
    // a PrinterException with phase Printing and no source span.
    internal static class CanonicalizationErrorFactory
    {
        public const string IdentityCastNode = "identity cast node";
        public const string ExpectedTypeMembershipViolation = "expected-type membership violation";
        public const string ConstructorTypeMismatch = "constructor type mismatch";
        public const string NonCanonicalName = "non-canonical name";
        public const string StructFieldOrderViolation = "struct field order violation";
        public const string ProvidedParameterOrderViolation = "provided parameter order violation";
        public const string TransportTypeNotInCatalog = "transport type not in catalog";
        public const string UnspecifiedInvariantViolation = "unspecified invariant violation";

        public static PrinterException InconsistentTree(string detail)
        {
            return new PrinterException(
                "E_PRN_INCONSISTENT_TREE",
                "resolved tree is inconsistent: " + detail);
        }

        public static PrinterException MissingRequiredParam(string scheme, string name)
        {
            return new PrinterException(
                "E_PRN_MISSING_REQUIRED_PARAM",
                "resolved transport '" + scheme + "' is missing required parameter '" + name + "'");
        }

        public static PrinterException UnknownParam(string scheme, string name)
        {
            return new PrinterException(
                "E_PRN_UNKNOWN_PARAM",
                "resolved transport '" + scheme + "' contains unknown parameter '" + name + "'");
        }

        public static PrinterException DuplicateParam(string scheme, string name)
        {
            return new PrinterException(
                "E_PRN_DUPLICATE_PARAM",
                "resolved transport '" + scheme + "' contains parameter '" + name + "' more than once");
        }

        public static PrinterException ParamKindMismatch(
            string name,
            string scheme,
            string actual,
            string expected)
        {
            return new PrinterException(
                "E_PRN_PARAM_KIND_MISMATCH",
                "parameter '" + name + "' of scheme '" + scheme + "' has value kind " + actual
                + ", expected " + expected);
        }

        public static PrinterException ValueOutOfRange(string path, string value)
        {
            return new PrinterException(
                "E_PRN_VALUE_OUT_OF_RANGE",
                "resolved value for '" + path + "' is out of range: " + value);
        }

        public static PrinterException ListArity(
            string name,
            string scheme,
            int count,
            int min,
            int? max)
        {
            var range = max.HasValue
                ? FormatNumber(min) + ".." + FormatNumber(max.Value)
                : "at least " + FormatNumber(min);

            return new PrinterException(
                "E_PRN_LIST_ARITY",
                "transport list parameter '" + name + "' of scheme '" + scheme + "' has "
                + FormatNumber(count) + " element(s), expected " + range);
        }

        public static PrinterException ListHomogeneity(string name, string scheme)
        {
            return new PrinterException(
                "E_PRN_LIST_HOMOGENEITY",
                "transport list parameter '" + name + "' of scheme '" + scheme
                + "' mixes transport types");
        }

        public static PrinterException StructFieldUnknown(string name, string scheme, string path)
        {
            return new PrinterException(
                "E_PRN_STRUCT_FIELD_UNKNOWN",
                "struct parameter '" + name + "' of scheme '" + scheme
                + "' contains unknown field '" + path + "'");
        }

        public static PrinterException StructFieldMissing(string name, string scheme, string path)
        {
            return new PrinterException(
                "E_PRN_STRUCT_FIELD_MISSING",
                "struct parameter '" + name + "' of scheme '" + scheme
                + "' is missing required field '" + path + "'");
        }

        public static PrinterException StructFieldDuplicate(string name, string scheme, string path)
        {
            return new PrinterException(
                "E_PRN_STRUCT_FIELD_DUPLICATE",
                "struct parameter '" + name + "' of scheme '" + scheme
                + "' contains field '" + path + "' more than once");
        }

        public static PrinterException StructFieldKindMismatch(
            string path,
            string name,
            string actual,
            string expected)
        {
            return new PrinterException(
                "E_PRN_STRUCT_FIELD_KIND_MISMATCH",
                "field '" + path + "' of struct parameter '" + name + "' has value kind " + actual
                + ", expected " + expected);
        }

        public static PrinterException InvalidCastTarget(TransportType type)
        {
            return new PrinterException(
                "E_PRN_INVALID_CAST_TARGET",
                "cast target '" + type + "' is invalid");
        }

        public static PrinterException InvalidScalar(char codeUnit)
        {
            return new PrinterException(
                "E_PRN_INVALID_SCALAR",
                "scalar value contains a character that cannot be printed: U+"
                + ((int)codeUnit).ToString("X4", CultureInfo.InvariantCulture));
        }

        private static string FormatNumber(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
