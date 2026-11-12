using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Pontifex.TransportFormula.Internal
{
    // Exact decimal text parsing and canonical formatting following parsing.md
    // §6.2 and §10.4. Parsing accepts an optional sign, a decimal point, and an
    // exponent, and succeeds only when the literal is exactly representable as a
    // System.Decimal: overflow, underflow, and precision loss are parse failures.
    // Formatting never uses exponent notation, trims trailing zeros, and
    // normalizes negative zero to "0".
    internal static class DecimalText
    {
        // 2^96 - 1, the largest coefficient a System.Decimal can hold.
        private const string MaxCoefficientText = "79228162514264337593543950335";

        private static readonly BigInteger MaxCoefficient =
            BigInteger.Parse(MaxCoefficientText, CultureInfo.InvariantCulture);

        private static readonly BigInteger Ten = new BigInteger(10);

        public static bool TryParse(string? text, out decimal value)
        {
            value = 0m;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var source = text!;
            var index = 0;
            var negative = false;
            if (source[0] == '+' || source[0] == '-')
            {
                negative = source[0] == '-';
                index++;
            }

            if (index >= source.Length)
            {
                return false;
            }

            var digits = new StringBuilder(source.Length);
            var sawDigit = false;
            var fractionalDigits = 0;

            while (index < source.Length && IsDigit(source[index]))
            {
                digits.Append(source[index]);
                sawDigit = true;
                index++;
            }

            if (index < source.Length && source[index] == '.')
            {
                index++;
                while (index < source.Length && IsDigit(source[index]))
                {
                    digits.Append(source[index]);
                    fractionalDigits++;
                    sawDigit = true;
                    index++;
                }
            }

            if (!sawDigit)
            {
                return false;
            }

            long exponent = 0;
            var exponentTooLarge = false;
            if (index < source.Length && (source[index] == 'e' || source[index] == 'E'))
            {
                index++;
                var exponentNegative = false;
                if (index < source.Length && (source[index] == '+' || source[index] == '-'))
                {
                    exponentNegative = source[index] == '-';
                    index++;
                }

                if (index >= source.Length || !IsDigit(source[index]))
                {
                    return false;
                }

                while (index < source.Length && IsDigit(source[index]))
                {
                    if (!exponentTooLarge)
                    {
                        exponent = exponent * 10 + (source[index] - '0');
                        if (exponent > MaxExponent)
                        {
                            exponentTooLarge = true;
                        }
                    }

                    index++;
                }

                if (exponentNegative)
                {
                    exponent = -exponent;
                }
            }

            if (index != source.Length)
            {
                return false;
            }

            // Scale is the number of digits after the decimal point once the
            // explicit exponent is folded in. It may be negative, meaning the
            // coefficient must be scaled up.
            var scale = (long)fractionalDigits - exponent;

            var coefficient = BigInteger.Parse(digits.ToString(), CultureInfo.InvariantCulture);
            if (coefficient.IsZero)
            {
                value = 0m;
                return true;
            }

            if (exponentTooLarge)
            {
                return false;
            }

            if (scale < 0)
            {
                // 10^29 already exceeds the maximum coefficient.
                if (scale < -28)
                {
                    return false;
                }

                coefficient *= BigInteger.Pow(10, (int)-scale);
                scale = 0;
            }

            // Removing trailing zeros is value-preserving and may bring the
            // coefficient and scale into the representable range.
            while (scale > 0)
            {
                if (!BigInteger.Remainder(coefficient, Ten).IsZero)
                {
                    break;
                }

                coefficient /= Ten;
                scale--;
            }

            if (scale > 28 || coefficient > MaxCoefficient)
            {
                return false;
            }

            value = CreateDecimal(coefficient, (int)scale, negative);
            return true;
        }

        public static string Format(decimal value)
        {
            var bits = decimal.GetBits(value);
            var negative = (bits[3] & int.MinValue) != 0;
            var scale = (bits[3] >> 16) & 0xFF;

            var coefficient =
                new BigInteger((uint)bits[0])
                + (new BigInteger((uint)bits[1]) << 32)
                + (new BigInteger((uint)bits[2]) << 64);

            if (coefficient.IsZero)
            {
                return "0";
            }

            var digits = coefficient.ToString(CultureInfo.InvariantCulture);
            while (scale > 0 && digits[digits.Length - 1] == '0')
            {
                digits = digits.Substring(0, digits.Length - 1);
                scale--;
            }

            string text;
            if (scale == 0)
            {
                text = digits;
            }
            else if (digits.Length > scale)
            {
                var split = digits.Length - scale;
                text = digits.Substring(0, split) + "." + digits.Substring(split);
            }
            else
            {
                text = "0." + new string('0', scale - digits.Length) + digits;
            }

            return negative ? "-" + text : text;
        }

        private const long MaxExponent = 1000000;

        private static decimal CreateDecimal(BigInteger coefficient, int scale, bool negative)
        {
            var remaining = coefficient;
            var lo = (int)(uint)(remaining & uint.MaxValue);
            remaining >>= 32;
            var mid = (int)(uint)(remaining & uint.MaxValue);
            remaining >>= 32;
            var hi = (int)(uint)(remaining & uint.MaxValue);

            return new decimal(lo, mid, hi, negative, (byte)scale);
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
