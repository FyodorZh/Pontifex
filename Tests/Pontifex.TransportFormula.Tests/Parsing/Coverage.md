# Parsing Layer Coverage

- **Layer:** Parsing (root layer)
- **Subject:** Transport Formula lexical and syntax subsystem group:
  `FormulaLexer`, `TokenCursor` (indirectly), `FormulaParser`,
  `ParsingErrorFactory`, and `TokenDisplay`.
- **Directory:** `Tests/Pontifex.TransportFormula.Tests/Parsing`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [grammar.md](../../../Pontifex.TransportFormula/grammar.md),
  [parsing.md](../../../Pontifex.TransportFormula/parsing.md),
  [IParser.cs](../../../Pontifex.TransportFormula/Parsing/IParser.cs),
  [FormulaToken.cs](../../../Pontifex.TransportFormula/Parsing/FormulaToken.cs),
  [AstDtos.cs](../../../Pontifex.TransportFormula/Parsing/AstDtos.cs),
  [SourceSpan.cs](../../../Pontifex.TransportFormula/SourceSpan.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `TryLex_WithAllWhitespaceKinds_SkipsThemAndReportsEndOfInput` | `grammar.md` §3 WS; `parsing.md` §3.1 | SP, HTAB, CR, and LF are insignificant and the input ends with one EOF token at input length. | EOF has zero length and empty text. |
| `TryLex_WithSurroundingWhitespace_ProducesTokensAtUtf16Offsets` | `parsing.md` §3.1–3.2; offset model | Whitespace is skipped between tokens and token offsets are 0-based UTF-16 offsets. | Exact offsets are asserted. |
| `TryLex_ForBareScalarExamples_ProducesOneWordToken` | `grammar.md` §4.1; `parsing.md` §3.2 | Maximal bare runs become one `Word`; identifier shape is not imposed by the lexer. | Parameterized cases: `alpha`, `alpha-beta`, `alpha_beta`, `127.0.0.1:9000`, `a\b`, `a|b`, `a+b`, `as`, `123`. |
| `TryLex_ForPunctuation_ProducesExpectedKindsAtConsecutiveOffsets` | `parsing.md` §3.1 | Every punctuation character maps one-to-one to its token kind at a one-code-unit span. | — |
| `TryLex_ForWordTerminatedByQuote_ProducesWordThenQuotedString` | `parsing.md` §3.2 | `"` terminates a word and begins a quoted-string token. | `RawText` for quoted strings includes the quotes. |
| `TryLex_ForEmptyQuotedString_ProducesEmptyDecodedText` | `grammar.md` §4.2 | `""` is valid and decodes to the empty string. | Raw slice is retained for diagnostics. |
| `TryLex_ForQuotedStringContainingDelimiters_PreservesTheirCharacters` | `grammar.md` §4.2 | Commas, braces, spaces, and `=` are ordinary content inside quotes. | — |
| `TryLex_ForQuotedStringContainingWhitespace_PreservesWhitespaceAndTab` | `grammar.md` §4.2 | Whitespace other than CR/LF is preserved exactly. | — |
| `TryLex_ForQuotedStringWithEscapes_DecodesQuoteAndBackslash` | `grammar.md` §4.2; `parsing.md` §3.3 | Only `\"` and `\\` decode; surrounding quotes are removed from `Text` but kept in `RawText`. | — |
| `TryLex_ForQuotedStringWithSurrogatePair_PreservesBothCodeUnits` | `grammar.md` §2 | A valid surrogate pair is a scalar value and spans two code units. | — |
| `TryLex_ForWordWithSurrogatePair_CountsBothCodeUnitsInSpan` | `grammar.md` §2 | A valid surrogate pair in a bare word is preserved and counted in the span. | — |
| `TryLex_ForInputEndingInWhitespace_ReportsEndOfInputAtInputLength` | `parsing.md` §3.1 | EOF offset equals the input length even with trailing whitespace. | EOF token text is not asserted. |
| `TryLex_WithRawLineFeedInsideQuotedString_ReturnsRawNewlineError` | `parsing.md` §3.3, §13.1 | A raw LF inside quotes is `E_LEX_RAW_NEWLINE_IN_STRING` at the newline. | Exact message asserted. |
| `TryLex_WithRawCarriageReturnInsideQuotedString_ReturnsRawNewlineError` | `parsing.md` §3.3, §13.1 | A raw CR inside quotes is `E_LEX_RAW_NEWLINE_IN_STRING` at the carriage return. | — |
| `TryLex_WithInvalidEscape_ReturnsInvalidEscapeAtBackslash` | `parsing.md` §3.3, §13.1 | An unsupported backslash sequence is rejected at the backslash with the escaped sequence rendered in the message. | Exact message asserted, including the escaped `\\q` sequence. |
| `TryLex_WithInvalidEscapeOfSurrogatePair_RendersPairInMessage` | `parsing.md` §3.3, §13.1 | A backslash followed by a valid surrogate pair is one invalid escape and renders both code units in the message. | Exact message asserted. |
| `TryLex_WithUnpairedSurrogateBeforeRawLineFeed_ReturnsInvalidUnicode` | `parsing.md` §13.1 precedence | An unpaired surrogate is reported before a following raw newline. | — |
| `TryLex_WithBackslashBeforeLineFeed_ReturnsInvalidEscapeNotRawNewline` | `parsing.md` §13.1 precedence | A backslash consumes the following code unit even when it is CR/LF, so invalid-escape wins over raw-newline. | — |
| `TryLex_WithBackslashBeforeUnpairedSurrogate_ReturnsInvalidEscapeNotInvalidUnicode` | `parsing.md` §13.1 precedence | A backslash consumes an unpaired surrogate, so invalid-escape wins over invalid-Unicode. | — |
| `TryLex_WithBackslashAtEndOfInput_ReturnsUnterminatedAtOpeningQuote` | `parsing.md` §3.3, §13.1 | A trailing backslash in a quoted string is unterminated, reported at the opening quote. | — |
| `TryLex_WithUnterminatedQuotedString_ReturnsErrorAtOpeningQuote` | `parsing.md` §3.3, §13.1 | EOF before the closing quote reports at the opening quote, wherever it occurs. | Parameterized cases: `alpha("x` at 6 and `alpha(1) "unterminated` at 9. |
| `TryLex_WithUnpairedHighSurrogateInQuotedString_ReturnsInvalidUnicode` | `parsing.md` §3.3, §13.1 | An unpaired high surrogate in a quoted string is `E_LEX_INVALID_UNICODE` at that code unit. | Exact message asserted. |
| `TryLex_WithUnpairedLowSurrogateInQuotedString_ReturnsInvalidUnicode` | `parsing.md` §3.3, §13.1 | An unpaired low surrogate in a quoted string is rejected at that code unit. | — |
| `TryLex_WithUnpairedHighSurrogateInWord_ReturnsInvalidUnicode` | `parsing.md` §3.2, §13.1 | An unpaired high surrogate in a bare word is rejected at that code unit. | — |
| `TryLex_WithUnpairedLowSurrogateInWord_ReturnsInvalidUnicode` | `parsing.md` §3.2, §13.1 | An unpaired low surrogate in a bare word is rejected at that code unit. | — |
| `Word_WithBackslash_EscapesBackslash` | `parsing.md` §3.4 | `displayToken` escapes backslash as `\\` inside single quotes. | `TokenDisplay` is a shared internal helper; access is via `InternalsVisibleTo`. |
| `QuotedString_WithTab_RendersTabEscape` | `parsing.md` §3.4 | HTAB renders as `\t`. | — |
| `QuotedString_WithSurrogatePair_RendersTwoUnicodeEscapes` | `parsing.md` §3.4 | Surrogate code units render independently as `\uXXXX`, including valid pairs. | — |
| `Word_WithQuoteAndApostrophe_EscapesBoth` | `parsing.md` §3.4 | `'` and `"` are escaped. | — |
| `Word_WithControlCode_RendersUnicodeEscape` | `parsing.md` §3.4 | Other control code units render as `\uXXXX`. | — |
| `Word_WithCarriageReturnAndLineFeed_RendersNamedEscapes` | `parsing.md` §3.4 | CR and LF render as `\r` and `\n`. | — |
| `Word_WithFiftyCharacters_TruncatesToFortyAndEllipsis` | `parsing.md` §3.4 | Escaped text longer than 40 units truncates to 40 units plus `...`. | The limit applies to escaped text, not to the surrounding quotes. |
| `Word_WithEscapedTextExactlyFortyCharacters_DoesNotTruncate` | `parsing.md` §3.4 | Escaped text of exactly 40 units is kept without an ellipsis. | — |
| `QuotedString_WithFiftyCharacters_TruncatesToFortyAndEllipsis` | `parsing.md` §3.4 | Truncation also applies to quoted-string display, not only words. | — |
| `Word_WithEscapeThatDoesNotFitAfterThirtyNineCharacters_DropsWholeEscape` | `parsing.md` §3.4 | Truncation never cuts an escape sequence. | — |
| `Word_WithEscapeEndingExactlyAtFortyCharacters_KeepsEscape` | `parsing.md` §3.4 | A two-unit escape ending exactly at offset 40 is kept. | — |
| `Word_WithSurrogatePairAtTruncationBoundary_DropsWholePair` | `parsing.md` §3.4 | Truncation never separates the two `\uXXXX` escapes of a surrogate pair. | — |
| `Punctuation_FormatsCharacterInSingleQuotes` | `parsing.md` §3.4 | Punctuation displays as `'<char>'`. | — |
| `Escape_WithUnpairedHighSurrogate_ReturnsUnicodeEscape` | `parsing.md` §3.4, §13.1 | Escape-sequence rendering handles an unpaired surrogate as one code unit. | Used by `E_LEX_INVALID_ESCAPE` message formatting. |
| `EndOfInput_IsTheDocumentedMarker` | `parsing.md` §3.4 | EOF displays as `<end of input>`. | — |
| `Parse_ForEmptyCall_ProducesEmptyArgumentList` | `grammar.md` §5 | `name()` is a valid zero-argument transport declaration. | — |
| `Parse_ForBareScalarWithAddressLikeText_ProducesUnquotedScalar` | `grammar.md` §3, §4.1 | Bare words with `.` and `:` remain one scalar. | — |
| `Parse_ForMultipleQuotedScalars_UsesDecodedTextAndQuotedFlag` | `grammar.md` §4.2; AST §4 | Comma, equals, and empty quoted scalars decode correctly and set `WasQuoted`. | — |
| `Parse_ForNamedArguments_RecordsNamesAndSpans` | `grammar.md` §5; `parsing.md` §5.4 | Named arguments record the raw name spelling and a name span. | Names are not normalized by the parser. |
| `Parse_ForUppercaseNames_PreservesRawSpelling` | `parsing.md` §4 | Scheme, argument-name, and struct-field-name spelling is preserved exactly by the parser. | Normalization is deferred to resolution. |
| `Parse_ForPositionalPrefixThenNamedSuffix_PreservesOrderAndForms` | `grammar.md` §5; `parsing.md` §5.4 | A positional prefix may be followed by named arguments; shapes are recorded. | — |
| `Parse_ForNestedCalls_ProducesNestedTransportExpressions` | `grammar.md` §5–6 | A word followed by `(` is a transport expression, including nested arguments. | — |
| `Parse_ForTransportList_ProducesExpressionElements` | `grammar.md` §5–6 | Lists of transport expressions contain `AstExpr` elements and preserve order. | — |
| `Parse_ForListWithScalarsAndStructs_ProducesGenericValueElements` | `grammar.md` §5–6; `parsing.md` §4 | A list may contain an expression, a scalar, and a struct; elements are `AstValue`. | Non-expression elements are valid syntax and fail later only for a transport-list parameter. |
| `Parse_ForStructWithNestedStruct_ProducesFieldTree` | `grammar.md` §5–6 | Struct fields are named and may contain nested structs and scalar values. | — |
| `Parse_ForTopLevelCast_ProducesSingleCast` | `grammar.md` §5; `parsing.md` §5.2 | `expr as Type` records one cast. | Type validity is semantic and not checked here. |
| `Parse_ForCastInsideArgument_ProducesCastOnNestedExpression` | `grammar.md` §5 | A cast may appear inside an argument value. | — |
| `Parse_ForCastTypeNamedAs_ParsesItAsATypeName` | `grammar.md` §4.3, §5 | `as` is accepted as a cast type name because a type name is any identifier. | — |
| `Parse_ForChainedCastAfterCastTypeNamedAs_ReportsChainedCast` | `grammar.md` §4.3, §5 | A further `as` keyword after a cast type named `as` is a chained cast at the second keyword. | — |
| `Parse_ForCastInsideList_ProducesCastsOnElements` | `grammar.md` §5 | A cast may appear on a transport-list element. | — |
| `Parse_ForEmptyListAndStruct_ProducesZeroElementsAndFields` | `grammar.md` §5 | `[]` and `{}` are syntactically valid. | — |
| `Parse_ForBareScalarsWithBackslashAndPipe_PreservesText` | `grammar.md` §3, §4.1 | Backslash and pipe are ordinary bare-scalar characters outside quotes. | — |
| `Parse_WithWhitespaceBetweenEveryToken_ParsesEquivalentShape` | `grammar.md` §5 | Whitespace is allowed between any two tokens, including before `(`. | — |
| `Parse_ForAsInValuePosition_TreatsItAsBareScalar` | `grammar.md` §4.3, §6 | `as` in value position is an ordinary bare scalar, not a cast. | — |
| `Parse_ForUpperCaseAs_RecognizesCast` | `grammar.md` §4.3; `parsing.md` §5.2 | The cast keyword comparison is ASCII case-insensitive. | Type spelling is preserved in the AST. |
| `Parse_ForAsAsSchemeName_ProducesTransportCall` | `grammar.md` §4.3 | `as` may be a scheme name because it is contextual. | — |
| `Parse_ThroughIParser_ProducesTheSameResultAsTheConcreteParser` | `IParser` contract; `parsing.md` §5.1 | The parser is usable through its public interface and produces the same success and error outcomes as the concrete parser. | Compares both a valid and an invalid input. |
| `Parse_ForTransportCall_RecordsSchemeSpanCloseParenOffsetAndFullSpan` | `parsing.md` §4, §5.3 | Call span, scheme span, and closing-parenthesis offset are exact. | `CloseParenOffset` supports missing-required-parameter diagnostics in later phases. |
| `Parse_ForCast_RecordsTypeSpanAndFullCastSpan` | `parsing.md` §4, §5.2 | Cast span covers call start through type end; type token span is retained. | — |
| `Parse_ForList_RecordsBracketOffsetsAndFullSpan` | `parsing.md` §4, §5.6 | List open/close offsets and full span are exact. | Open/close offsets are required by list-arity diagnostics. |
| `Parse_ForStruct_RecordsBraceOffsetsAndFullSpan` | `parsing.md` §4, §5.7 | Struct and nested-struct brace offsets, spans, field name spans, and field spans are exact. | Close offset is required by missing-required-field diagnostics. |
| `Parse_ForNamedArgument_RecordsNameSpanAndArgumentSpan` | `parsing.md` §4, §5.4 | Named argument span starts at the name and ends after the value. | — |
| `Parse_ForPositionalArgument_ArgumentSpanEqualsValueSpan` | `parsing.md` §4, §5.4 | Positional arguments have no name span and inherit the value span. | — |
| `Parse_ForQuotedScalar_SpanIncludesSurroundingQuotes` | `parsing.md` §3.3, §4 | Quoted scalar spans include both quotes while `Text` is decoded. | — |
| `Parse_ForBareScalarWithSurrogatePair_SpanCountsBothCodeUnits` | `grammar.md` §2; `parsing.md` §4 | Scalar spans are measured in UTF-16 code units. | — |
| `Parse_ForEmptyCollection_RecordsOpenAndCloseOffsets` | `parsing.md` §4, §5.6–5.7 | Empty lists and structs record their single opening/closing token offsets and two-unit span. | Parameterized cases: `alpha([])` and `alpha({})`. |
| `Parse_ForQuotedAndBareScalars_SetsWasQuotedFlagAccordingly` | `parsing.md` §4, §5.5 | `AstScalar.WasQuoted` distinguishes quoted from bare scalars even when text is equal. | — |
| `Parse_WhenLexicalErrorFollowsSyntaxError_ReportsLexicalErrorFirst` | `parsing.md` §2, §12.3 | The whole input is lexed before parsing, so a later lexical error wins over an earlier syntax error. | Proves strict phase order. |
| `Parse_ForInvalidInput_ReturnsExactSyntaxError` | `parsing.md` §5, §12, §13.2, §14.2 | Every syntax error returns one `FactoryError` with exact code, phase, message, and offset, and no formula. | Parameterized matrix below; each case is a separate NUnit test. |
| `Parse_ForDeterministicCorpus_NeverThrowsAndReturnsExactlyOneOutcome` | `parsing.md` §2, §5; acceptance criteria | Parser never throws for arbitrary strings, always returns exactly one of formula/error, error offsets are in range, and AST spans are within the input. | Fixed corpus plus every two-character combination of a fixed alphabet; no randomness, time, or I/O. |

## Syntax error matrix

Each row below is one case of `Parse_ForInvalidInput_ReturnsExactSyntaxError`.
The message column is the exact expected `FactoryError.Message`.

| Case | Source | Code | Offset | Message |
|---|---|---|---:|---|
| `EmptyInput` | `` | `E_SYN_EMPTY_INPUT` | 0 | `expected a transport expression` |
| `WhitespaceOnlyInput` | SP HTAB CR LF | `E_SYN_EMPTY_INPUT` | 0 | `expected a transport expression` |
| `BareSchemeWithoutCall` | `alpha` | `E_SYN_UNEXPECTED_TOKEN` | 5 | `expected '(', got <end of input>` |
| `DanglingOpenParenthesis` | `alpha(` | `E_SYN_UNEXPECTED_TOKEN` | 6 | `expected a value or ')', got <end of input>` |
| `ArgumentTrailingComma` | `alpha(a,)` | `E_SYN_TRAILING_COMMA` | 7 | `trailing comma is not allowed in argument list` |
| `ArgumentStartsWithComma` | `alpha(,)` | `E_SYN_UNEXPECTED_TOKEN` | 6 | `expected a value or ')', got ','` |
| `UnterminatedArgumentStartsWithComma` | `alpha(,` | `E_SYN_UNEXPECTED_TOKEN` | 6 | `expected a value or ')', got ','` |
| `MissingArgumentAfterComma` | `alpha(a,,b)` | `E_SYN_UNEXPECTED_TOKEN` | 8 | `expected a value, got ','` |
| `ListStartsWithComma` | `alpha([,])` | `E_SYN_UNEXPECTED_TOKEN` | 7 | `expected a value, got ','` |
| `MissingCommaBetweenArguments` | `alpha(a b)` | `E_SYN_UNEXPECTED_TOKEN` | 8 | `expected ',' or ')', got 'b'` |
| `PositionalAfterNamed` | `alpha(p=1, a)` | `E_SYN_POSITIONAL_AFTER_NAMED` | 11 | `positional argument is not allowed after a named argument` |
| `NestedList` | `alpha([[alpha()]])` | `E_SYN_NESTED_LIST` | 7 | `nested lists are not allowed` |
| `StructFieldMissingEquals` | `alpha({p})` | `E_SYN_UNEXPECTED_TOKEN` | 8 | `expected '=', got '}'` |
| `MissingCommaBetweenStructFields` | `alpha({p=1 q=2})` | `E_SYN_UNEXPECTED_TOKEN` | 11 | `expected ',' or '}', got 'q'` |
| `StructTrailingComma` | `alpha({p=1,})` | `E_SYN_TRAILING_COMMA` | 10 | `trailing comma is not allowed in struct` |
| `NonIdentifierStructFieldName` | `alpha({1=2})` | `E_SYN_UNEXPECTED_TOKEN` | 7 | `expected a struct field name, got '1'` |
| `NonIdentifierNamedArgument` | `alpha(1=2)` | `E_SYN_UNEXPECTED_TOKEN` | 6 | `expected an identifier, got '1'` |
| `NonIdentifierScheme` | `alpha(1())` | `E_SYN_UNEXPECTED_TOKEN` | 6 | `expected a transport scheme identifier, got '1'` |
| `CastMissingTypeName` | `alpha() as` | `E_SYN_UNEXPECTED_TOKEN` | 10 | `expected a transport type name, got <end of input>` |
| `CastTypeIsNotIdentifier` | `alpha() as 1Type` | `E_SYN_UNEXPECTED_TOKEN` | 11 | `expected a transport type name, got '1Type'` |
| `ChainedCast` | `alpha() as TypeA as TypeB` | `E_SYN_CHAINED_CAST` | 17 | `chained casts are not allowed` |
| `ListTrailingComma` | `alpha([beta() as TypeA,])` | `E_SYN_TRAILING_COMMA` | 22 | `trailing comma is not allowed in list` |
| `ListMissingCloseBracket` | `alpha([beta(),gamma()` | `E_SYN_UNEXPECTED_TOKEN` | 21 | `expected ',' or ']', got <end of input>` |
| `StructMissingCloseBrace` | `alpha({p=1` | `E_SYN_UNEXPECTED_TOKEN` | 10 | `expected ',' or '}', got <end of input>` |
| `TopLevelList` | `[alpha()]` | `E_SYN_UNEXPECTED_TOKEN` | 0 | `expected a transport expression, got '['` |
| `TopLevelStruct` | `{alpha()}` | `E_SYN_UNEXPECTED_TOKEN` | 0 | `expected a transport expression, got '{'` |
| `TopLevelQuotedString` | `"alpha"` | `E_SYN_UNEXPECTED_TOKEN` | 0 | `expected a transport expression, got 'alpha'` |
| `TopLevelNonIdentifierScalar` | `123` | `E_SYN_UNEXPECTED_TOKEN` | 0 | `expected a transport expression, got '123'` |
| `TrailingInput` | `alpha() extra` | `E_SYN_TRAILING_INPUT` | 8 | `unexpected trailing input` |
| `TrailingInputAfterCast` | `alpha() as TypeA extra` | `E_SYN_TRAILING_INPUT` | 17 | `unexpected trailing input` |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| None currently identified. | Parsing layer | New tests are added with the contract change that requires them. |

## Deferred by design

| Gap | Reason |
|---|---|
| Adversarial nesting depth and stack-exhaustion behaviour. | The grammar defines no implementation limits. The parser is recursive descent; proving behaviour for pathological depth would require an explicit-stack implementation or a stress category, both outside this deterministic contract suite. |
| Performance, allocation, and large-input behaviour. | The suite proves language semantics, not non-functional characteristics. |
| Concurrency. | Lexer, cursor, and parser instances are not documented for concurrent use; the deterministic corpus reuses one parser sequentially. |
| Unicode normalization and culture-sensitive comparison. | The grammar explicitly performs no normalization and uses ASCII identifier rules; scalar values are preserved exactly. |
| Registry, resolution, canonical printing, compilation, and bootstrap. | Separate subsystems and suites. |
| Public exposure of `FormulaToken` or internal lexer helpers. | `FormulaToken` is a DTO; `FormulaLexer`, `TokenCursor`, `FormulaParser`, `ParsingErrorFactory`, and `TokenDisplay` are internal. Tests reach them through `InternalsVisibleTo`, not through new public API. |

## Notes

- Every fixture carries `[Category("TransportFormula.Parsing")]`.
- Tests are deterministic and isolated: no time, sleep, sockets, files, threads,
  randomness, or background tasks.
- The parser is exercised without a registry, type catalog, or transport type.
- `FactoryError.Span!.Value.Start` is the asserted offset. The error span is
  zero-length in the current implementation; only the offset is normative.
- The robustness test uses a fixed corpus and a fixed character alphabet, so its
  generated inputs are deterministic across runs.
