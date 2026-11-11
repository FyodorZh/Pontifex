using System;
using System.Collections.Generic;
using Pontifex.Factory.Internal;

namespace Pontifex.Factory.Parsing.Internal
{
    // Recursive descent. The grammar imposes no language-level limits; nesting depth is
    // bounded only by the CLR stack, as documented in the parsing suite's Coverage.md.
    internal sealed class FormulaParser : IParser
    {
        private TokenCursor _cursor = null!;

        public FormulaParseResult Parse(string formula)
        {
            try
            {
                if (!FormulaLexer.TryLex(formula, out var tokens, out var lexError))
                {
                    return new FormulaParseResult(null, lexError);
                }

                _cursor = new TokenCursor(tokens);

                if (_cursor.Current.Kind == FormulaTokenKind.EndOfInput)
                {
                    throw new SyntaxAbort(ParsingErrorFactory.SyntaxEmptyInput());
                }

                if (_cursor.Current.Kind != FormulaTokenKind.Word
                    || !NameNormalizer.IsIdentifier(_cursor.Current.Text))
                {
                    throw Unexpected("a transport expression");
                }

                var root = ParseExpr();

                if (_cursor.Current.Kind != FormulaTokenKind.EndOfInput)
                {
                    throw new SyntaxAbort(
                        ParsingErrorFactory.SyntaxTrailingInput(_cursor.Current.Span.Start));
                }

                return new FormulaParseResult(new AstFormula(root, root.Span), null);
            }
            catch (SyntaxAbort abort)
            {
                return new FormulaParseResult(null, abort.Error);
            }
        }

        private AstExpr ParseExpr()
        {
            var call = ParseTransportCall();

            if (_cursor.IsCurrentAsKeyword())
            {
                _cursor.Consume();
                var typeToken = ParseTypeName();

                if (_cursor.IsCurrentAsKeyword())
                {
                    throw new SyntaxAbort(
                        ParsingErrorFactory.SyntaxChainedCast(_cursor.Current.Span.Start));
                }

                var span = SpanFrom(call.Span.Start, typeToken.Span.End);
                var cast = new AstCast(typeToken.Text, typeToken.Span, span);
                return new AstExpr(call, cast, span);
            }

            return new AstExpr(call, null, call.Span);
        }

        private AstTransportCall ParseTransportCall()
        {
            if (_cursor.Current.Kind != FormulaTokenKind.Word
                || !NameNormalizer.IsIdentifier(_cursor.Current.Text))
            {
                throw Unexpected("a transport scheme identifier");
            }

            var schemeToken = _cursor.Consume();

            if (_cursor.Current.Kind != FormulaTokenKind.LParen)
            {
                throw Unexpected("'('");
            }

            _cursor.Consume();
            var arguments = ParseArguments(out var closeParen);

            return new AstTransportCall(
                schemeToken.Text,
                schemeToken.Span,
                arguments,
                closeParen.Span.Start,
                SpanFrom(schemeToken.Span.Start, closeParen.Span.End));
        }

        private IReadOnlyList<AstArgument> ParseArguments(out FormulaToken closeParen)
        {
            var arguments = new List<AstArgument>();
            var seenNamed = false;

            if (_cursor.Current.Kind == FormulaTokenKind.RParen)
            {
                closeParen = _cursor.Consume();
                return arguments;
            }

            if (!CanStartValue(_cursor.Current))
            {
                throw Unexpected("a value or ')'");
            }

            while (true)
            {
                if (!CanStartValue(_cursor.Current))
                {
                    throw Unexpected("a value");
                }

                AstArgument argument;
                if (_cursor.Current.Kind == FormulaTokenKind.Word
                    && _cursor.Peek(1).Kind == FormulaTokenKind.Equals)
                {
                    if (!NameNormalizer.IsIdentifier(_cursor.Current.Text))
                    {
                        throw Unexpected("an identifier");
                    }

                    var nameToken = _cursor.Consume();
                    _cursor.Consume();
                    var value = ParseValue();
                    argument = new AstArgument(
                        nameToken.Text,
                        nameToken.Span,
                        value,
                        SpanFrom(nameToken.Span.Start, value.Span.End));
                    seenNamed = true;
                }
                else
                {
                    if (seenNamed)
                    {
                        throw new SyntaxAbort(
                            ParsingErrorFactory.SyntaxPositionalAfterNamed(_cursor.Current.Span.Start));
                    }

                    var value = ParseValue();
                    argument = new AstArgument(null, null, value, value.Span);
                }

                arguments.Add(argument);

                if (_cursor.Current.Kind == FormulaTokenKind.Comma)
                {
                    var comma = _cursor.Consume();
                    if (_cursor.Current.Kind == FormulaTokenKind.RParen)
                    {
                        throw new SyntaxAbort(
                            ParsingErrorFactory.SyntaxTrailingComma("argument list", comma.Span.Start));
                    }

                    continue;
                }

                if (_cursor.Current.Kind == FormulaTokenKind.RParen)
                {
                    closeParen = _cursor.Consume();
                    return arguments;
                }

                throw Unexpected("',' or ')'");
            }
        }

        private AstValue ParseValue()
        {
            switch (_cursor.Current.Kind)
            {
                case FormulaTokenKind.QuotedString:
                {
                    var token = _cursor.Consume();
                    return new AstScalar(token.Text, WasQuoted: true, token.Span);
                }

                case FormulaTokenKind.LBracket:
                    return ParseList();

                case FormulaTokenKind.LBrace:
                    return ParseStruct();

                case FormulaTokenKind.Word:
                {
                    if (_cursor.Peek(1).Kind == FormulaTokenKind.LParen)
                    {
                        return ParseExpr();
                    }

                    var token = _cursor.Consume();
                    return new AstScalar(token.Text, WasQuoted: false, token.Span);
                }

                default:
                    throw Unexpected("a value");
            }
        }

        private AstList ParseList()
        {
            var open = _cursor.Consume();

            if (_cursor.Current.Kind == FormulaTokenKind.RBracket)
            {
                var emptyClose = _cursor.Consume();
                return new AstList(
                    Array.Empty<AstExpr>(),
                    open.Span.Start,
                    emptyClose.Span.Start,
                    SpanFrom(open.Span.Start, emptyClose.Span.End));
            }

            var elements = new List<AstExpr>();

            while (true)
            {
                if (_cursor.Current.Kind == FormulaTokenKind.LBracket)
                {
                    throw new SyntaxAbort(
                        ParsingErrorFactory.SyntaxNestedList(_cursor.Current.Span.Start));
                }

                if (_cursor.Current.Kind != FormulaTokenKind.Word
                    || !NameNormalizer.IsIdentifier(_cursor.Current.Text))
                {
                    throw Unexpected("a transport expression");
                }

                elements.Add(ParseExpr());

                if (_cursor.Current.Kind == FormulaTokenKind.Comma)
                {
                    var comma = _cursor.Consume();
                    if (_cursor.Current.Kind == FormulaTokenKind.RBracket)
                    {
                        throw new SyntaxAbort(
                            ParsingErrorFactory.SyntaxTrailingComma("transport list", comma.Span.Start));
                    }

                    continue;
                }

                if (_cursor.Current.Kind == FormulaTokenKind.RBracket)
                {
                    var close = _cursor.Consume();
                    return new AstList(
                        elements,
                        open.Span.Start,
                        close.Span.Start,
                        SpanFrom(open.Span.Start, close.Span.End));
                }

                throw Unexpected("',' or ']'");
            }
        }

        private AstStruct ParseStruct()
        {
            var open = _cursor.Consume();

            if (_cursor.Current.Kind == FormulaTokenKind.RBrace)
            {
                var emptyClose = _cursor.Consume();
                return new AstStruct(
                    Array.Empty<AstStructField>(),
                    open.Span.Start,
                    emptyClose.Span.Start,
                    SpanFrom(open.Span.Start, emptyClose.Span.End));
            }

            var fields = new List<AstStructField>();

            while (true)
            {
                if (_cursor.Current.Kind != FormulaTokenKind.Word
                    || !NameNormalizer.IsIdentifier(_cursor.Current.Text))
                {
                    throw Unexpected("a struct field name");
                }

                var nameToken = _cursor.Consume();

                if (_cursor.Current.Kind != FormulaTokenKind.Equals)
                {
                    throw Unexpected("'='");
                }

                _cursor.Consume();
                var value = ParseValue();
                fields.Add(new AstStructField(
                    nameToken.Text,
                    nameToken.Span,
                    value,
                    SpanFrom(nameToken.Span.Start, value.Span.End)));

                if (_cursor.Current.Kind == FormulaTokenKind.Comma)
                {
                    var comma = _cursor.Consume();
                    if (_cursor.Current.Kind == FormulaTokenKind.RBrace)
                    {
                        throw new SyntaxAbort(
                            ParsingErrorFactory.SyntaxTrailingComma("struct", comma.Span.Start));
                    }

                    continue;
                }

                if (_cursor.Current.Kind == FormulaTokenKind.RBrace)
                {
                    var close = _cursor.Consume();
                    return new AstStruct(
                        fields,
                        open.Span.Start,
                        close.Span.Start,
                        SpanFrom(open.Span.Start, close.Span.End));
                }

                throw Unexpected("',' or '}'");
            }
        }

        private FormulaToken ParseTypeName()
        {
            if (_cursor.Current.Kind != FormulaTokenKind.Word
                || !NameNormalizer.IsIdentifier(_cursor.Current.Text))
            {
                throw Unexpected("a transport type name");
            }

            return _cursor.Consume();
        }

        private SyntaxAbort Unexpected(string expected)
        {
            return new SyntaxAbort(
                ParsingErrorFactory.SyntaxUnexpectedToken(expected, _cursor.Current));
        }

        private static bool CanStartValue(FormulaToken token)
        {
            switch (token.Kind)
            {
                case FormulaTokenKind.Word:
                case FormulaTokenKind.QuotedString:
                case FormulaTokenKind.LBracket:
                case FormulaTokenKind.LBrace:
                    return true;
                default:
                    return false;
            }
        }

        private static SourceSpan SpanFrom(int start, int end)
        {
            return new SourceSpan(start, end - start);
        }

        private sealed class SyntaxAbort : Exception
        {
            public SyntaxAbort(FactoryError error)
                : base(error.Message)
            {
                Error = error;
            }

            public FactoryError Error { get; }
        }
    }
}
