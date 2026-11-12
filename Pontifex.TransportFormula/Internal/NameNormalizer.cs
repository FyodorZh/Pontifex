using System;

namespace Pontifex.TransportFormula.Internal
{
    internal static class NameNormalizer
    {
        public static bool IsIdentifier(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (!IsAsciiLetter(value![0]))
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                var c = value[i];
                if (!IsAsciiLetter(c) && !IsAsciiDigit(c) && c != '_' && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        public static string Normalize(string value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c >= 'A' && c <= 'Z')
                {
                    chars[i] = (char)(c + ('a' - 'A'));
                }
            }

            return new string(chars);
        }

        public static bool IsAsKeyword(string? value)
        {
            return value is not null
                   && value.Length == 2
                   && AsciiLower(value[0]) == 'a'
                   && AsciiLower(value[1]) == 's';
        }

        private static char AsciiLower(char c)
        {
            return c >= 'A' && c <= 'Z' ? (char)(c + ('a' - 'A')) : c;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static bool IsAsciiDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
