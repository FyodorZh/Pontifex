using System;
using System.Collections.Generic;
using System.Linq;
using Pontifex.TransportFormula;
using Pontifex.TransportFormula.Internal;
using Pontifex.TransportFormula.Parsing;
using Pontifex.TransportFormula.Parsing.Internal;

namespace Pontifex.Factory.Tests.Parsing;

[TestFixture]
[Category("Factory.Parsing")]
public sealed class ParserTests
{
    #region Valid grammar forms

    [Test]
    public void Parse_ForEmptyCall_ProducesEmptyArgumentList()
    {
        var formula = Parse("alpha()");

        Assert.That(formula.Root.Call.Scheme, Is.EqualTo("alpha"));
        Assert.That(formula.Root.Call.Arguments, Is.Empty);
        Assert.That(formula.Root.Cast, Is.Null);
    }

    [Test]
    public void Parse_ForBareScalarWithAddressLikeText_ProducesUnquotedScalar()
    {
        var formula = Parse("alpha(127.0.0.1:9000)");
        var scalar = SingleScalar(formula);

        Assert.That(scalar.Text, Is.EqualTo("127.0.0.1:9000"));
        Assert.That(scalar.WasQuoted, Is.False);
    }

    [Test]
    public void Parse_ForMultipleQuotedScalars_UsesDecodedTextAndQuotedFlag()
    {
        var formula = Parse("alpha(\"a, b\", \"x=y\", \"\")");
        var scalars = formula.Root.Call.Arguments.Select(a => (AstScalar)a.Value).ToList();

        Assert.That(scalars.Select(s => s.Text), Is.EqualTo(new[] { "a, b", "x=y", "" }));
        Assert.That(scalars.All(s => s.WasQuoted), Is.True);
    }

    [Test]
    public void Parse_ForNamedArguments_RecordsNamesAndSpans()
    {
        var formula = Parse("alpha(p=1, q=two)");
        var arguments = formula.Root.Call.Arguments;

        Assert.That(arguments.Select(a => a.Name), Is.EqualTo(new[] { "p", "q" }));
        Assert.That(arguments.All(a => a.NameSpan is not null), Is.True);
        Assert.That(((AstScalar)arguments[0].Value).Text, Is.EqualTo("1"));
        Assert.That(((AstScalar)arguments[1].Value).Text, Is.EqualTo("two"));
    }

    [Test]
    public void Parse_ForUppercaseNames_PreservesRawSpelling()
    {
        var formula = Parse("ALPHA(BETA(x), MODE=Fast, CONFIG={Name=one})");
        var arguments = formula.Root.Call.Arguments;

        Assert.That(formula.Root.Call.Scheme, Is.EqualTo("ALPHA"));
        Assert.That(arguments.Select(a => a.Name), Is.EqualTo(new string?[] { null, "MODE", "CONFIG" }));
        Assert.That(((AstExpr)arguments[0].Value).Call.Scheme, Is.EqualTo("BETA"));
        Assert.That(((AstStruct)arguments[2].Value).Fields.Single().Name, Is.EqualTo("Name"));
    }

    [Test]
    public void Parse_ForPositionalPrefixThenNamedSuffix_PreservesOrderAndForms()
    {
        var formula = Parse("alpha(1, 2, p=3, q=4)");
        var arguments = formula.Root.Call.Arguments;

        Assert.That(arguments.Select(a => a.Name), Is.EqualTo(new string?[] { null, null, "p", "q" }));
        Assert.That(arguments[0].NameSpan, Is.Null);
        Assert.That(arguments[1].NameSpan, Is.Null);
        Assert.That(arguments[2].NameSpan, Is.Not.Null);
        Assert.That(arguments[3].NameSpan, Is.Not.Null);
    }

    [Test]
    public void Parse_ForNestedCalls_ProducesNestedTransportExpressions()
    {
        var formula = Parse("alpha(beta(), gamma(x=1))");
        var arguments = formula.Root.Call.Arguments;

        var beta = (AstExpr)arguments[0].Value;
        var gamma = (AstExpr)arguments[1].Value;

        Assert.That(beta.Call.Scheme, Is.EqualTo("beta"));
        Assert.That(beta.Call.Arguments, Is.Empty);
        Assert.That(gamma.Call.Scheme, Is.EqualTo("gamma"));
        Assert.That(gamma.Call.Arguments.Single().Name, Is.EqualTo("x"));
    }

    [Test]
    public void Parse_ForTransportList_ProducesExpressionElements()
    {
        var formula = Parse("alpha([beta(), gamma(), delta()])");
        var list = (AstList)formula.Root.Call.Arguments.Single().Value;

        Assert.That(
            list.Elements.Cast<AstExpr>().Select(e => e.Call.Scheme),
            Is.EqualTo(new[] { "beta", "gamma", "delta" }));
    }

    [Test]
    public void Parse_ForListWithScalarsAndStructs_ProducesGenericValueElements()
    {
        var formula = Parse("alpha([beta(), 1, {p=2}])");
        var list = (AstList)formula.Root.Call.Arguments.Single().Value;

        Assert.That(list.Elements, Has.Count.EqualTo(3));
        Assert.That(list.Elements[0], Is.InstanceOf<AstExpr>());
        Assert.That(((AstScalar)list.Elements[1]).Text, Is.EqualTo("1"));
        Assert.That(list.Elements[2], Is.InstanceOf<AstStruct>());
    }

    [Test]
    public void Parse_ForStructWithNestedStruct_ProducesFieldTree()
    {
        var formula = Parse("alpha({p=1, q={r=2}})");
        var structValue = (AstStruct)formula.Root.Call.Arguments.Single().Value;

        Assert.That(structValue.Fields.Select(f => f.Name), Is.EqualTo(new[] { "p", "q" }));
        Assert.That(((AstScalar)structValue.Fields[0].Value).Text, Is.EqualTo("1"));

        var nested = (AstStruct)structValue.Fields[1].Value;
        Assert.That(nested.Fields.Single().Name, Is.EqualTo("r"));
        Assert.That(((AstScalar)nested.Fields.Single().Value).Text, Is.EqualTo("2"));
    }

    [Test]
    public void Parse_ForTopLevelCast_ProducesSingleCast()
    {
        var formula = Parse("alpha() as TypeA");

        Assert.That(formula.Root.Cast, Is.Not.Null);
        Assert.That(formula.Root.Cast!.TypeName, Is.EqualTo("TypeA"));
    }

    [Test]
    public void Parse_ForCastInsideArgument_ProducesCastOnNestedExpression()
    {
        var formula = Parse("alpha(beta() as TypeA)");
        var argumentExpression = (AstExpr)formula.Root.Call.Arguments.Single().Value;

        Assert.That(argumentExpression.Call.Scheme, Is.EqualTo("beta"));
        Assert.That(argumentExpression.Cast, Is.Not.Null);
        Assert.That(argumentExpression.Cast!.TypeName, Is.EqualTo("TypeA"));
    }

    [Test]
    public void Parse_ForCastInsideList_ProducesCastsOnElements()
    {
        var formula = Parse("alpha([beta() as TypeA, gamma()])");
        var list = (AstList)formula.Root.Call.Arguments.Single().Value;

        Assert.That(((AstExpr)list.Elements[0]).Cast, Is.Not.Null);
        Assert.That(((AstExpr)list.Elements[0]).Cast!.TypeName, Is.EqualTo("TypeA"));
        Assert.That(((AstExpr)list.Elements[1]).Cast, Is.Null);
    }

    [Test]
    public void Parse_ForEmptyListAndStruct_ProducesZeroElementsAndFields()
    {
        var formula = Parse("alpha([], {})");
        var arguments = formula.Root.Call.Arguments;

        Assert.That(((AstList)arguments[0].Value).Elements, Is.Empty);
        Assert.That(((AstStruct)arguments[1].Value).Fields, Is.Empty);
    }

    [Test]
    public void Parse_ForBareScalarsWithBackslashAndPipe_PreservesText()
    {
        var formula = Parse("alpha(a\\b, a|b)");
        var arguments = formula.Root.Call.Arguments;

        Assert.That(((AstScalar)arguments[0].Value).Text, Is.EqualTo("a\\b"));
        Assert.That(((AstScalar)arguments[1].Value).Text, Is.EqualTo("a|b"));
    }

    [Test]
    public void Parse_WithWhitespaceBetweenEveryToken_ParsesEquivalentShape()
    {
        var formula = Parse("  alpha ( beta ( ) as TypeA , [ gamma ( ) ] )  ");

        Assert.That(formula.Root.Call.Scheme, Is.EqualTo("alpha"));
        Assert.That(formula.Root.Call.Arguments, Has.Count.EqualTo(2));
        Assert.That(((AstExpr)formula.Root.Call.Arguments[0].Value).Cast, Is.Not.Null);
        Assert.That(
            ((AstExpr)((AstList)formula.Root.Call.Arguments[1].Value).Elements.Single()).Call.Scheme,
            Is.EqualTo("gamma"));
    }

    [Test]
    public void Parse_ForAsInValuePosition_TreatsItAsBareScalar()
    {
        var formula = Parse("alpha(as)");
        var scalar = SingleScalar(formula);

        Assert.That(scalar.Text, Is.EqualTo("as"));
        Assert.That(scalar.WasQuoted, Is.False);
        Assert.That(formula.Root.Cast, Is.Null);
    }

    [Test]
    public void Parse_ForUpperCaseAs_RecognizesCast()
    {
        var formula = Parse("alpha() AS TypeA");

        Assert.That(formula.Root.Cast, Is.Not.Null);
        Assert.That(formula.Root.Cast!.TypeName, Is.EqualTo("TypeA"));
    }

    [Test]
    public void Parse_ForAsAsSchemeName_ProducesTransportCall()
    {
        var formula = Parse("as()");

        Assert.That(formula.Root.Call.Scheme, Is.EqualTo("as"));
        Assert.That(formula.Root.Cast, Is.Null);
    }

    #endregion

    #region AST spans and offsets

    [Test]
    public void SourceSpan_End_IsStartPlusLength()
    {
        var span = new SourceSpan(7, 5);

        Assert.That(span.End, Is.EqualTo(12));
    }

    [Test]
    public void Parse_ThroughIParser_ReturnsTheSameOutcome()
    {
        IParser parser = new FormulaParser();

        var result = parser.Parse("alpha()");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Formula, Is.Not.Null);
        Assert.That(result.Formula!.Root.Call.Scheme, Is.EqualTo("alpha"));
    }

    [Test]
    public void Parse_ForTransportCall_RecordsSchemeSpanCloseParenOffsetAndFullSpan()
    {
        var formula = Parse("alpha(beta())");
        var call = formula.Root.Call;

        Assert.That(call.SchemeSpan, Is.EqualTo(new SourceSpan(0, 5)));
        Assert.That(call.CloseParenOffset, Is.EqualTo(12));
        Assert.That(call.Span, Is.EqualTo(new SourceSpan(0, 13)));
        Assert.That(formula.Root.Span, Is.EqualTo(call.Span));
        Assert.That(formula.Span, Is.EqualTo(call.Span));
    }

    [Test]
    public void Parse_ForCast_RecordsTypeSpanAndFullCastSpan()
    {
        var formula = Parse("alpha() as TypeA");
        var cast = formula.Root.Cast!;

        Assert.That(cast.TypeSpan, Is.EqualTo(new SourceSpan(11, 5)));
        Assert.That(cast.Span, Is.EqualTo(new SourceSpan(0, 16)));
        Assert.That(formula.Root.Span, Is.EqualTo(new SourceSpan(0, 16)));
    }

    [Test]
    public void Parse_ForList_RecordsBracketOffsetsAndFullSpan()
    {
        var formula = Parse("alpha([beta(), gamma()])");
        var list = (AstList)formula.Root.Call.Arguments.Single().Value;

        Assert.That(list.OpenOffset, Is.EqualTo(6));
        Assert.That(list.CloseOffset, Is.EqualTo(22));
        Assert.That(list.Span, Is.EqualTo(new SourceSpan(6, 17)));
        Assert.That(formula.Root.Call.CloseParenOffset, Is.EqualTo(23));
    }

    [Test]
    public void Parse_ForStruct_RecordsBraceOffsetsAndFullSpan()
    {
        var formula = Parse("alpha({p=1, q={r=2}})");
        var structValue = (AstStruct)formula.Root.Call.Arguments.Single().Value;

        Assert.That(structValue.OpenOffset, Is.EqualTo(6));
        Assert.That(structValue.CloseOffset, Is.EqualTo(19));
        Assert.That(structValue.Span, Is.EqualTo(new SourceSpan(6, 14)));

        var firstField = structValue.Fields[0];
        Assert.That(firstField.NameSpan, Is.EqualTo(new SourceSpan(7, 1)));
        Assert.That(firstField.Span, Is.EqualTo(new SourceSpan(7, 3)));

        var nested = (AstStruct)structValue.Fields[1].Value;
        Assert.That(nested.OpenOffset, Is.EqualTo(14));
        Assert.That(nested.CloseOffset, Is.EqualTo(18));
        Assert.That(nested.Span, Is.EqualTo(new SourceSpan(14, 5)));

        var nestedField = nested.Fields.Single();
        Assert.That(nestedField.NameSpan, Is.EqualTo(new SourceSpan(15, 1)));
        Assert.That(nestedField.Span, Is.EqualTo(new SourceSpan(15, 3)));
    }

    [Test]
    public void Parse_ForNamedArgument_RecordsNameSpanAndArgumentSpan()
    {
        var formula = Parse("alpha(p=1)");
        var argument = formula.Root.Call.Arguments.Single();

        Assert.That(argument.NameSpan, Is.EqualTo(new SourceSpan(6, 1)));
        Assert.That(argument.Span, Is.EqualTo(new SourceSpan(6, 3)));
        Assert.That(formula.Root.Call.CloseParenOffset, Is.EqualTo(9));
    }

    [Test]
    public void Parse_ForPositionalArgument_ArgumentSpanEqualsValueSpan()
    {
        var formula = Parse("alpha(1)");
        var argument = formula.Root.Call.Arguments.Single();

        Assert.That(argument.Name, Is.Null);
        Assert.That(argument.NameSpan, Is.Null);
        Assert.That(argument.Span, Is.EqualTo(argument.Value.Span));
        Assert.That(argument.Span, Is.EqualTo(new SourceSpan(6, 1)));
    }

    [Test]
    public void Parse_ForQuotedScalar_SpanIncludesSurroundingQuotes()
    {
        var formula = Parse("alpha(\"\")");
        var scalar = SingleScalar(formula);

        Assert.That(scalar.Text, Is.Empty);
        Assert.That(scalar.Span, Is.EqualTo(new SourceSpan(6, 2)));
    }

    [Test]
    public void Parse_ForBareScalarWithSurrogatePair_SpanCountsBothCodeUnits()
    {
        const string emoji = "\uD83D\uDE00";
        var formula = Parse("alpha(" + emoji + ")");
        var scalar = SingleScalar(formula);

        Assert.That(scalar.Text, Is.EqualTo(emoji));
        Assert.That(scalar.Span, Is.EqualTo(new SourceSpan(6, 2)));
    }

    [TestCase("alpha([])", 6, 7, 2)]
    [TestCase("alpha({})", 6, 7, 2)]
    public void Parse_ForEmptyCollection_RecordsOpenAndCloseOffsets(
        string source,
        int expectedOpen,
        int expectedClose,
        int expectedLength)
    {
        var formula = Parse(source);
        var value = formula.Root.Call.Arguments.Single().Value;

        if (value is AstList list)
        {
            Assert.That(list.OpenOffset, Is.EqualTo(expectedOpen));
            Assert.That(list.CloseOffset, Is.EqualTo(expectedClose));
            Assert.That(list.Span, Is.EqualTo(new SourceSpan(expectedOpen, expectedLength)));
        }
        else
        {
            var structValue = (AstStruct)value;
            Assert.That(structValue.OpenOffset, Is.EqualTo(expectedOpen));
            Assert.That(structValue.CloseOffset, Is.EqualTo(expectedClose));
            Assert.That(structValue.Span, Is.EqualTo(new SourceSpan(expectedOpen, expectedLength)));
        }
    }

    [Test]
    public void Parse_ForQuotedAndBareScalars_SetsWasQuotedFlagAccordingly()
    {
        var formula = Parse("alpha(plain, \"plain\", \"\")");
        var scalars = formula.Root.Call.Arguments.Select(a => (AstScalar)a.Value).ToList();

        Assert.That(scalars.Select(s => s.WasQuoted), Is.EqualTo(new[] { false, true, true }));
        Assert.That(scalars.Select(s => s.Text), Is.EqualTo(new[] { "plain", "plain", "" }));
    }

    #endregion

    #region Syntax errors

    [Test]
    public void Parse_WhenLexicalErrorFollowsSyntaxError_ReportsLexicalErrorFirst()
    {
        var result = new FormulaParser().Parse("alpha(] \"unterminated");

        Assert.That(result.Formula, Is.Null);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo("E_LEX_UNTERMINATED_STRING"));
        Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Lexing));
        Assert.That(result.Error.Span!.Value.Start, Is.EqualTo(8));
    }

    [TestCaseSource(nameof(SyntaxErrorCases))]
    public void Parse_ForInvalidInput_ReturnsExactSyntaxError(
        string source,
        string expectedCode,
        int expectedOffset,
        string expectedMessage)
    {
        var result = new FormulaParser().Parse(source);

        Assert.That(result.Formula, Is.Null);
        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo(expectedCode));
        Assert.That(result.Error.Phase, Is.EqualTo(FactoryPhase.Parsing));
        Assert.That(result.Error.Span, Is.Not.Null);
        Assert.That(result.Error.Span!.Value.Start, Is.EqualTo(expectedOffset));
        Assert.That(result.Error.Message, Is.EqualTo(expectedMessage));
    }

    private static IEnumerable<TestCaseData> SyntaxErrorCases()
    {
        yield return SyntaxError("", "E_SYN_EMPTY_INPUT", 0,
            "expected a transport expression").SetName("EmptyInput");

        yield return SyntaxError(" \t\r\n", "E_SYN_EMPTY_INPUT", 0,
            "expected a transport expression").SetName("WhitespaceOnlyInput");

        yield return SyntaxError("alpha", "E_SYN_UNEXPECTED_TOKEN", 5,
            "expected '(', got <end of input>").SetName("BareSchemeWithoutCall");

        yield return SyntaxError("alpha(", "E_SYN_UNEXPECTED_TOKEN", 6,
            "expected a value or ')', got <end of input>").SetName("DanglingOpenParenthesis");

        yield return SyntaxError("alpha(a,)", "E_SYN_TRAILING_COMMA", 7,
            "trailing comma is not allowed in argument list").SetName("ArgumentTrailingComma");

        yield return SyntaxError("alpha(,)", "E_SYN_UNEXPECTED_TOKEN", 6,
            "expected a value or ')', got ','").SetName("ArgumentStartsWithComma");

        yield return SyntaxError("alpha(,", "E_SYN_UNEXPECTED_TOKEN", 6,
            "expected a value or ')', got ','").SetName("UnterminatedArgumentStartsWithComma");

        yield return SyntaxError("alpha(a,,b)", "E_SYN_UNEXPECTED_TOKEN", 8,
            "expected a value, got ','").SetName("MissingArgumentAfterComma");

        yield return SyntaxError("alpha([,])", "E_SYN_UNEXPECTED_TOKEN", 7,
            "expected a value, got ','").SetName("ListStartsWithComma");

        yield return SyntaxError("alpha(a b)", "E_SYN_UNEXPECTED_TOKEN", 8,
            "expected ',' or ')', got 'b'").SetName("MissingCommaBetweenArguments");

        yield return SyntaxError("alpha(p=1, a)", "E_SYN_POSITIONAL_AFTER_NAMED", 11,
            "positional argument is not allowed after a named argument").SetName("PositionalAfterNamed");

        yield return SyntaxError("alpha([[alpha()]])", "E_SYN_NESTED_LIST", 7,
            "nested lists are not allowed").SetName("NestedList");

        yield return SyntaxError("alpha({p})", "E_SYN_UNEXPECTED_TOKEN", 8,
            "expected '=', got '}'").SetName("StructFieldMissingEquals");

        yield return SyntaxError("alpha({p=1 q=2})", "E_SYN_UNEXPECTED_TOKEN", 11,
            "expected ',' or '}', got 'q'").SetName("MissingCommaBetweenStructFields");

        yield return SyntaxError("alpha({p=1,})", "E_SYN_TRAILING_COMMA", 10,
            "trailing comma is not allowed in struct").SetName("StructTrailingComma");

        yield return SyntaxError("alpha({1=2})", "E_SYN_UNEXPECTED_TOKEN", 7,
            "expected a struct field name, got '1'").SetName("NonIdentifierStructFieldName");

        yield return SyntaxError("alpha(1=2)", "E_SYN_UNEXPECTED_TOKEN", 6,
            "expected an identifier, got '1'").SetName("NonIdentifierNamedArgument");

        yield return SyntaxError("alpha(1())", "E_SYN_UNEXPECTED_TOKEN", 6,
            "expected a transport scheme identifier, got '1'").SetName("NonIdentifierScheme");

        yield return SyntaxError("alpha() as", "E_SYN_UNEXPECTED_TOKEN", 10,
            "expected a transport type name, got <end of input>").SetName("CastMissingTypeName");

        yield return SyntaxError("alpha() as 1Type", "E_SYN_UNEXPECTED_TOKEN", 11,
            "expected a transport type name, got '1Type'").SetName("CastTypeIsNotIdentifier");

        yield return SyntaxError("alpha() as TypeA as TypeB", "E_SYN_CHAINED_CAST", 17,
            "chained casts are not allowed").SetName("ChainedCast");

        yield return SyntaxError("alpha([beta() as TypeA,])", "E_SYN_TRAILING_COMMA", 22,
            "trailing comma is not allowed in list").SetName("ListTrailingComma");

        yield return SyntaxError("alpha([beta(),gamma()", "E_SYN_UNEXPECTED_TOKEN", 21,
            "expected ',' or ']', got <end of input>").SetName("ListMissingCloseBracket");

        yield return SyntaxError("alpha({p=1", "E_SYN_UNEXPECTED_TOKEN", 10,
            "expected ',' or '}', got <end of input>").SetName("StructMissingCloseBrace");

        yield return SyntaxError("[alpha()]", "E_SYN_UNEXPECTED_TOKEN", 0,
            "expected a transport expression, got '['").SetName("TopLevelList");

        yield return SyntaxError("{alpha()}", "E_SYN_UNEXPECTED_TOKEN", 0,
            "expected a transport expression, got '{'").SetName("TopLevelStruct");

        yield return SyntaxError("\"alpha\"", "E_SYN_UNEXPECTED_TOKEN", 0,
            "expected a transport expression, got 'alpha'").SetName("TopLevelQuotedString");

        yield return SyntaxError("123", "E_SYN_UNEXPECTED_TOKEN", 0,
            "expected a transport expression, got '123'").SetName("TopLevelNonIdentifierScalar");

        yield return SyntaxError("alpha() extra", "E_SYN_TRAILING_INPUT", 8,
            "unexpected trailing input").SetName("TrailingInput");

        yield return SyntaxError("alpha() as TypeA extra", "E_SYN_TRAILING_INPUT", 17,
            "unexpected trailing input").SetName("TrailingInputAfterCast");
    }

    private static TestCaseData SyntaxError(
        string source,
        string code,
        int offset,
        string message)
    {
        return new TestCaseData(source, code, offset, message);
    }

    #endregion

    #region Robustness

    [Test]
    public void Parse_ForDeterministicCorpus_NeverThrowsAndReturnsExactlyOneOutcome()
    {
        var parser = new FormulaParser();

        foreach (var source in DeterministicCorpus())
        {
            FormulaParseResult? result = null;
            try
            {
                result = parser.Parse(source);
            }
            catch (Exception exception)
            {
                Assert.Fail(
                    "Parser threw for " + TokenDisplay.QuotedString(source) + ": " + exception);
            }

            Assert.That(result, Is.Not.Null);
            var parsed = result!;
            Assert.That(
                parsed.Formula is null ^ parsed.Error is null,
                Is.True,
                "Exactly one of Formula and Error must be present for "
                + TokenDisplay.QuotedString(source));

            if (parsed.Error is not null)
            {
                Assert.That(parsed.Error.Span, Is.Not.Null);
                Assert.That(parsed.Error.Span!.Value.Start, Is.InRange(0, source.Length));
            }

            if (parsed.Formula is not null)
            {
                AssertAstWithin(parsed.Formula, source.Length);
            }
        }
    }

    private static IEnumerable<string> DeterministicCorpus()
    {
        yield return "";
        yield return " ";
        yield return "\t\r\n";
        yield return "alpha";
        yield return "alpha(";
        yield return "alpha()";
        yield return "alpha(,)";
        yield return "alpha([";
        yield return "alpha([[]])";
        yield return "alpha({";
        yield return "alpha({}";
        yield return "alpha(\"";
        yield return "alpha(\"\\";
        yield return "alpha(\"\\q\")";
        yield return "alpha(\"x\ny\")";
        yield return "alpha() as";
        yield return "alpha() as TypeA as";
        yield return "\uD800";
        yield return "\uDC00";
        yield return "\uD83D\uDE00";
        yield return "alpha(" + '\uD800' + ")";
        yield return "alpha(" + '\uDC00' + ")";
        yield return "alpha(\"" + '\uD800' + "\")";
        yield return "alpha(\"" + '\uDC00' + "\")";
        yield return "()[]{},=";
        yield return "\"\"";
        yield return "\"\\\"\\\\\"";
        yield return "alpha(a\\b, a|b)";
        yield return "alpha(p=1, q=two)";
        yield return "alpha([beta() as TypeA, gamma()])";
        yield return "alpha({p=1, q={r=2}})";

        const string alphabet = "aA0_-()[]{},=\"\\ \t\r\n";
        foreach (var first in alphabet)
        {
            foreach (var second in alphabet)
            {
                yield return new string(new[] { first, second });
            }
        }

        foreach (var first in alphabet)
        {
            foreach (var second in alphabet)
            {
                yield return "alpha(" + first + second + ")";
            }
        }
    }

    private static void AssertAstWithin(AstFormula formula, int inputLength)
    {
        AssertSpanWithin(formula.Span, inputLength);
        AssertExprWithin(formula.Root, inputLength);
    }

    private static void AssertExprWithin(AstExpr expr, int inputLength)
    {
        AssertSpanWithin(expr.Span, inputLength);
        AssertSpanWithin(expr.Call.Span, inputLength);
        AssertSpanWithin(expr.Call.SchemeSpan, inputLength);
        Assert.That(expr.Call.CloseParenOffset, Is.InRange(0, inputLength));

        foreach (var argument in expr.Call.Arguments)
        {
            AssertSpanWithin(argument.Span, inputLength);
            if (argument.NameSpan is not null)
            {
                AssertSpanWithin(argument.NameSpan.Value, inputLength);
            }

            AssertValueWithin(argument.Value, inputLength);
        }

        if (expr.Cast is not null)
        {
            AssertSpanWithin(expr.Cast.Span, inputLength);
            AssertSpanWithin(expr.Cast.TypeSpan, inputLength);
        }
    }

    private static void AssertValueWithin(AstValue value, int inputLength)
    {
        AssertSpanWithin(value.Span, inputLength);

        switch (value)
        {
            case AstExpr expr:
                AssertExprWithin(expr, inputLength);
                break;

            case AstList list:
                Assert.That(list.OpenOffset, Is.InRange(0, inputLength));
                Assert.That(list.CloseOffset, Is.InRange(0, inputLength));
                foreach (var element in list.Elements)
                {
                    AssertValueWithin(element, inputLength);
                }

                break;

            case AstStruct structValue:
                Assert.That(structValue.OpenOffset, Is.InRange(0, inputLength));
                Assert.That(structValue.CloseOffset, Is.InRange(0, inputLength));
                foreach (var field in structValue.Fields)
                {
                    AssertSpanWithin(field.Span, inputLength);
                    AssertSpanWithin(field.NameSpan, inputLength);
                    AssertValueWithin(field.Value, inputLength);
                }

                break;
        }
    }

    private static void AssertSpanWithin(SourceSpan span, int inputLength)
    {
        Assert.That(span.Start, Is.GreaterThanOrEqualTo(0));
        Assert.That(span.Length, Is.GreaterThanOrEqualTo(0));
        Assert.That(span.End, Is.LessThanOrEqualTo(inputLength));
    }

    #endregion

    #region Helpers

    private static AstFormula Parse(string source)
    {
        var result = new FormulaParser().Parse(source);

        Assert.That(result.Error, Is.Null, result.Error?.Message);
        Assert.That(result.Formula, Is.Not.Null);
        return result.Formula!;
    }

    private static AstScalar SingleScalar(AstFormula formula)
    {
        return (AstScalar)formula.Root.Call.Arguments.Single().Value;
    }

    #endregion
}
