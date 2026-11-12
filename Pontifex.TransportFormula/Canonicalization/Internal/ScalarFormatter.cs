using System;
using System.Text;

namespace Pontifex.TransportFormula.Canonicalization.Internal
{
    // Minimal scalar quoting following parsing.md §10.4. Empty text prints as "".
    // Text with no whitespace or delimiter prints bare; otherwise it is quoted and
    // only backslash and double quote are escaped. A raw CR, LF, or unpaired
    // surrogate is rejected before the quoting decision.
    internal static class ScalarFormatter
    {
        public static string Format(string text)
        {
            EnsurePrintable(text);

            if (text.Length == 0)
            {
                return "\"\"";
            }

            if (!NeedsQuotes(text))
            {
                return text;
            }

            var builder = new StringBuilder(text.Length + 2);
            builder.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\')
                {
                    builder.Append("\\\\");
                }
                else if (c == '"')
                {
                    builder.Append("\\\"");
                }
                else
                {
                    builder.Append(c);
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        // Reports the first offending UTF-16 code unit in text order.
        public static void EnsurePrintable(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == '\r' || c == '\n')
                {
                    throw CanonicalizationErrorFactory.InvalidScalar(c);
                }

                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        i++;
                        continue;
                    }

                    throw CanonicalizationErrorFactory.InvalidScalar(c);
                }

                if (char.IsLowSurrogate(c))
                {
                    throw CanonicalizationErrorFactory.InvalidScalar(c);
                }
            }
        }

        private static bool NeedsQuotes(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (IsWhitespace(text[i]) || IsDelimiter(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }

        private static bool IsDelimiter(char c)
        {
            return c == '(' || c == ')' || c == '[' || c == ']'
                || c == '{' || c == '}' || c == ',' || c == '=' || c == '"';
        }
    }
}
