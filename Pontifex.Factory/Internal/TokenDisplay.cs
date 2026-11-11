using System.Collections.Generic;
using System.Text;

namespace Pontifex.Factory.Internal;

internal static class TokenDisplay
{
    public const int MaxEscapedLength = 40;
    public const string EndOfInput = "<end of input>";

    public static string Word(string text)
    {
        return Quote(EscapeAndTruncate(text));
    }

    public static string QuotedString(string text)
    {
        return Quote(EscapeAndTruncate(text));
    }

    public static string Punctuation(char punctuation)
    {
        return "'" + punctuation + "'";
    }

    public static string Escape(string text)
    {
        if (text is null)
        {
            return string.Empty;
        }

        var units = EscapeUnits(text);
        var builder = new StringBuilder();
        foreach (var unit in units)
        {
            builder.Append(unit.Text);
        }

        return builder.ToString();
    }

    public static string EscapeAndTruncate(string text)
    {
        var units = EscapeUnits(text);

        var totalLength = 0;
        foreach (var unit in units)
        {
            totalLength += unit.Text.Length;
        }

        if (totalLength <= MaxEscapedLength)
        {
            var full = new StringBuilder(totalLength);
            foreach (var unit in units)
            {
                full.Append(unit.Text);
            }

            return full.ToString();
        }

        var boundary = 0;
        var length = 0;
        for (var i = 0; i < units.Count; i++)
        {
            if (length + units[i].Text.Length > MaxEscapedLength)
            {
                break;
            }

            length += units[i].Text.Length;
            boundary = i + 1;
        }

        if (boundary > 0
            && boundary < units.Count
            && units[boundary - 1].IsHighSurrogate)
        {
            boundary--;
            length -= units[boundary].Text.Length;
        }

        var truncated = new StringBuilder(length + 3);
        for (var i = 0; i < boundary; i++)
        {
            truncated.Append(units[i].Text);
        }

        truncated.Append("...");
        return truncated.ToString();
    }

    private static string Quote(string escaped)
    {
        return "'" + escaped + "'";
    }

    private static List<EscapeUnit> EscapeUnits(string? text)
    {
        var units = new List<EscapeUnit>();
        if (string.IsNullOrEmpty(text))
        {
            return units;
        }

        for (var i = 0; i < text!.Length; i++)
        {
            var c = text[i];

            if (char.IsHighSurrogate(c)
                && i + 1 < text.Length
                && char.IsLowSurrogate(text[i + 1]))
            {
                units.Add(new EscapeUnit(UnicodeEscape(c), isHighSurrogate: true));
                units.Add(new EscapeUnit(UnicodeEscape(text[i + 1]), isHighSurrogate: false));
                i++;
                continue;
            }

            units.Add(new EscapeUnit(EscapeCodeUnit(c), isHighSurrogate: false));
        }

        return units;
    }

    private static string EscapeCodeUnit(char c)
    {
        switch (c)
        {
            case '\\':
                return "\\\\";
            case '\'':
                return "\\'";
            case '"':
                return "\\\"";
            case '\r':
                return "\\r";
            case '\n':
                return "\\n";
            case '\t':
                return "\\t";
        }

        if (char.IsControl(c) || char.IsSurrogate(c))
        {
            return UnicodeEscape(c);
        }

        return c.ToString();
    }

    private static string UnicodeEscape(char c)
    {
        return "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
    }

    private readonly struct EscapeUnit
    {
        public EscapeUnit(string text, bool isHighSurrogate)
        {
            Text = text;
            IsHighSurrogate = isHighSurrogate;
        }

        public string Text { get; }

        public bool IsHighSurrogate { get; }
    }
}
