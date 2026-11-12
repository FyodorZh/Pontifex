# Canonicalization Layer Coverage

- **Layer:** Canonicalization (root layer)
- **Subject:** Transport Formula canonicalization subsystem group:
  `FormulaCanonicalizer`, `PositionalFormatter`, `ScalarFormatter`, `DecimalText`,
  `PrinterInvariantValidator`, and `CanonicalizationErrorFactory`.
- **Directory:** `Tests/Pontifex.Factory.Tests/Canonicalization`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [parsing.md](../../../Pontifex.TransportFormula/parsing.md) §10, §11,
  §13.5, §14.4, §14.7, §14.8,
  [implementation-plan.md](../../../Pontifex.TransportFormula/implementation-plan.md) §10,
  [ICanonicalizer.cs](../../../Pontifex.TransportFormula/Canonicalization/ICanonicalizer.cs),
  [ResolvedDtos.cs](../../../Pontifex.TransportFormula/Semantics/ResolvedDtos.cs),
  [MetadataDtos.cs](../../../Pontifex.TransportFormula/Registry/MetadataDtos.cs),
  [ITransportTypeCatalog.cs](../../../Pontifex.TransportFormula/Registry/ITransportTypeCatalog.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.TransportFormula/FactoryExceptions.cs),
  [SourceSpan.cs](../../../Pontifex.TransportFormula/SourceSpan.cs)

## Covered tests

### FormulaCanonicalizer

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `Canonicalize_ForTransportWithNoProvidedParameters_PrintsEmptyParentheses` | `FormulaCanonicalizer` | §10.2 | A transport with no provided parameters prints `name()`. | — |
| `Canonicalize_ForLongestPositionalPrefix_PrintsItPositionally` | `FormulaCanonicalizer` | §10.2 | Two provided positional-capable parameters print positionally without names. | — |
| `Canonicalize_ForOmittedFirstPositional_PrintsLaterParameterByName` | `FormulaCanonicalizer` | §10.2 | A provided positional-capable parameter after a gap prints named. | — |
| `Canonicalize_ForNamedOnlyParameterDeclaredBeforePositional_PrintsPositionalFirst` | `FormulaCanonicalizer` | §10.2 | The positional prefix prints before a named-only parameter declared earlier. | Hand-built tree; declaration order is not print order. |
| `Canonicalize_ForAbsentOptionalParameters_OmitsThem` | `FormulaCanonicalizer` | §10.2 | Absent optional parameters are omitted; no defaults are synthesized. | — |
| `Canonicalize_ForTransportList_PreservesElementOrder` | `FormulaCanonicalizer` | §10.3, §11.1 | List elements print in their resolved order inside `[...]`. | — |
| `Canonicalize_ForEmptyListAndEmptyStruct_PrintsEmptyContainers` | `FormulaCanonicalizer` | §10.3 | Empty lists print `[]` and empty structs print `{}`. | — |
| `Canonicalize_ForNestedStruct_PrintsFieldsInSchemaOrder` | `FormulaCanonicalizer` | §10.3 | Struct fields print in schema order with nested braces. | — |
| `Canonicalize_ForNestedStructWithEmptyNestedStruct_PrintsBraces` | `FormulaCanonicalizer` | §10.3 | An empty nested struct prints `name={}`. | — |
| `Canonicalize_ForNonIdentityCast_PrintsCanonicalTarget` | `FormulaCanonicalizer` | §10.1, §10.5 | A changing cast prints `inner as CanonicalTarget` with single spaces. | — |
| `Canonicalize_ForCastNestedInAnArgument_PrintsTheCastInsideTheArgument` | `FormulaCanonicalizer` | §10.1, §10.3 | A cast value prints recursively inside an argument. | — |
| `Canonicalize_ForCastInsideTransportList_PrintsTheCast` | `FormulaCanonicalizer` | §10.1, §10.3 | A cast value prints recursively as a transport-list element. | — |
| `Canonicalize_ForScalarVariants_UsesMinimalQuoting` | `FormulaCanonicalizer` | §10.4, §10.5 | Printing picks bare, quoted, escaped, empty, and Unicode scalar forms. | Integration-level check of scalar formatting inside transports. |
| `Canonicalize_ForPrintedOutput_HasNoTrailingWhitespaceOrNewline` | `FormulaCanonicalizer` | §10.1, §10.3 | Output has no prefix, trailing whitespace, or newline. | — |
| `Canonicalize_ForParsingMdPrinterExamples_ProducesExpectedOutput` | `FormulaCanonicalizer` | §14.4 | All twenty-seven canonical printer rows normalize through the real parser and resolver; all except `emptyStruct` match the table literally. | Parameterized method; one row per §14.4 example. `emptyStruct` prints as `emptystruct` because registration lowercases scheme names. |
| `Canonicalize_ForEquivalentSources_ProducesTheSameOutputAndTree` | `FormulaCanonicalizer` | §11.1, §11.3 | Case, whitespace, named order, identity casts, aliases, struct order, quoting, and CR/LF variants collapse to one tree and one output. | Parameterized method; nine pairs. |
| `Canonicalize_ForRepresentativeFormula_IsIdempotentAndPreservesTheTree` | `FormulaCanonicalizer` | §11.1, §11.2 | Parse-resolve-canonicalize-parse-resolve-canonicalize is stable and structurally equal to the first tree. | Cross-subsystem round trip; twenty-nine sources. Output is checked for raw CR/LF. |
| `Canonicalize_ForIntegerAndDecimalParameters_PrintsCanonicalScalars` | `FormulaCanonicalizer` | §10.3, §10.5 | Integer parameters print bare and decimal parameters print with trailing zeros trimmed. | — |
| `Canonicalize_ForPositiveSignedInteger_PrintsUnsignedCanonicalText` | `FormulaCanonicalizer` | §10.3 | A leading `+` is not printed for integers. | — |
| `Canonicalize_ForExponentInput_PrintsPlainInteger` | `FormulaCanonicalizer` | §10.3 | Exponent input is canonicalized to plain integer text. | — |
| `Canonicalize_ForSmallestScaleDecimal_PrintsPlainForm` | `FormulaCanonicalizer` | §10.3 | The smallest representable decimal prints without exponent notation. | — |
| `Canonicalize_ForDecimalMinAndMax_PrintsPlainForm` | `FormulaCanonicalizer` | §10.3 | Decimal minimum and maximum print in plain form. | — |
| `Canonicalize_ForNegativeZeroAndTrailingZeros_NormalizesTheText` | `FormulaCanonicalizer` | §10.3, §10.5 | Negative zero prints as `0` and trailing zeros are trimmed. | — |
| `Canonicalize_ForQuotedNumericScalar_PrintsCanonicalNumber` | `FormulaCanonicalizer` | §10.3, §10.5 | Quoting is removed when the canonical number is safe. | — |
| `Canonicalize_ForIntegerAndDecimalStructFields_PrintsCanonicalScalars` | `FormulaCanonicalizer` | §10.3 | Numeric struct fields print canonically inside schema-ordered structs. | — |
| `Canonicalize_ForTopLevelNonIdentityCast_ProducesExpectedOutput` | `FormulaCanonicalizer` | §10.1, §10.5, §11.2 | A source-level non-identity cast prints its canonical scheme and target. | — |
| `Canonicalize_ForCrLfAndTabWhitespace_ProducesNoRawNewlines` | `FormulaCanonicalizer` | §10.1, §10.4 | CR/LF and tab whitespace outside scalars are removed and never emitted. | — |
| `Canonicalize_ForCaseInsensitiveCastType_UsesCatalogCanonicalSpelling` | `FormulaCanonicalizer` | §6.6, §10.1 | A case-insensitive cast type prints the catalog's canonical spelling. | — |
| `Canonicalize_ForDeepNestedStruct_RoundTripsInSchemaOrder` | `FormulaCanonicalizer` | §10.3, §11.1, §11.2 | A three-level nested struct prints and round-trips in schema order. | — |
| `Canonicalize_ForListOrder_PreservesTheOriginalElementOrderAcrossRoundTrip` | `FormulaCanonicalizer` | §10.3, §11.1 | A list's element order is preserved across canonicalization and re-resolution. | — |

### PositionalFormatter

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `Arrange_WhenAllPositionalParametersAreProvided_EmitsThemPositionally` | `PositionalFormatter` | §10.2 | The full provided positional prefix is emitted positionally; a named-only parameter is named. | — |
| `Arrange_WhenAnOptionalPositionalIsAbsent_EmitsLaterPositionalsByName` | `PositionalFormatter` | §10.2 | A gap in positional-capable parameters stops the prefix. | — |
| `Arrange_WhenTheFirstPositionalIsAbsent_EmitsALaterPositionalByName` | `PositionalFormatter` | §10.2 | A later provided positional after an absent first positional is named. | — |
| `Arrange_WhenBothOptionalPositionalsAreProvided_EmitsBothPositionally` | `PositionalFormatter` | §10.2 | Two consecutive provided optional positionals stay positional. | — |
| `Arrange_WhenANamedOnlyParameterPrecedesAPositionalParameter_EmitsPositionalFirst` | `PositionalFormatter` | §10.2 | Positional entries are ordered before the named remainder. | — |
| `Arrange_WhenOnlyANamedOnlyParameterIsProvided_EmitsItNamed` | `PositionalFormatter` | §10.2 | Named-only parameters are never positional. | — |
| `Arrange_WhenNothingIsProvided_ReturnsNoEntries` | `PositionalFormatter` | §10.2 | An empty provided list produces no print entries. | — |
| `Arrange_WhenBindingUsesAStructurallyEqualClone_DoesNotTreatItAsProvided` | `PositionalFormatter` | §10.2 | Parameter matching is by reference identity, not record equality or name. | Direct formatter contract; the validator rejects such a tree. |

### ScalarFormatter

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `Format_ForEmptyText_ReturnsQuotedEmptyString` | `ScalarFormatter` | §10.4 | Empty text prints `""`. | — |
| `Format_ForSafeBareText_ReturnsTextUnchanged` | `ScalarFormatter` | §10.4 | Plain text, addresses, symbols, Unicode, and a surrogate pair print bare. | Parameterized method; eight texts. |
| `Format_ForWhitespaceOrDelimiter_QuotesText` | `ScalarFormatter` | §10.4 | Every delimiter and both quoting whitespace characters trigger quoting. | Parameterized method; tab and space included. |
| `Format_ForTextWithDelimiters_QuotesAndPreservesText` | `ScalarFormatter` | §10.4 | Multi-character texts containing commas, equals, or braces are quoted without altering the text. | Parameterized method. |
| `Format_ForTextWithDoubleQuote_EscapesOnlyTheQuote` | `ScalarFormatter` | §10.4 | A double quote is escaped as `\"`. | — |
| `Format_ForTextWithBackslashAndWhitespace_EscapesOnlyTheBackslash` | `ScalarFormatter` | §10.4 | A backslash is escaped as `\\` only inside a quoted scalar. | — |
| `Format_ForTextWithBothEscapes_EscapesBackslashBeforeQuote` | `ScalarFormatter` | §10.4 | Escaping is applied per code unit without ambiguity. | — |
| `Format_ForTextWithLeadingAndTrailingSpaces_PreservesThemInsideQuotes` | `ScalarFormatter` | §10.4 | Leading and trailing spaces are preserved inside quotes. | — |
| `Format_ForTextWithTab_QuotesAndPreservesTab` | `ScalarFormatter` | §10.4 | HTAB is whitespace and is preserved literally inside quotes. | — |
| `Format_ForCarriageReturn_ThrowsInvalidScalarWithU000D` | `ScalarFormatter` | §10.4, §13.5 | A raw CR is rejected as `E_PRN_INVALID_SCALAR` with `U+000D`. | — |
| `Format_ForLineFeed_ThrowsInvalidScalarWithU000A` | `ScalarFormatter` | §10.4, §13.5 | A raw LF is rejected as `E_PRN_INVALID_SCALAR` with `U+000A`. | — |
| `Format_ForBothNewlineKinds_ReportsFirstCodeUnitInTextOrder` | `ScalarFormatter` | §10.4 | The error reports the first offending code unit in text order. | — |
| `Format_ForUnpairedHighSurrogate_ThrowsInvalidScalarWithUD800` | `ScalarFormatter` | §10.4, §13.5 | An unpaired high surrogate is rejected with its code unit. | — |
| `Format_ForUnpairedLowSurrogate_ThrowsInvalidScalarWithUDC00` | `ScalarFormatter` | §10.4, §13.5 | An unpaired low surrogate is rejected with its code unit. | — |
| `Format_ForSurrogatePair_PreservesBarePair` | `ScalarFormatter` | §10.4 | A valid surrogate pair is a Unicode scalar and prints bare. | — |
| `Format_ForCrBeforeUnpairedSurrogate_ReportsTheCr` | `ScalarFormatter` | §10.4 | CR earlier in the text wins over a later surrogate. | — |

### DecimalText

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `TryParse_ForExactLiteral_SucceedsWithValue` | `DecimalText` | §6.2 | Signs, decimal points, and exponents parse to exact decimal values. | Parameterized twelve literals. |
| `TryParse_ForDecimalMinValue_Succeeds` | `DecimalText` | §6.2 | The decimal minimum is exactly representable. | — |
| `TryParse_ForDecimalMaxValue_Succeeds` | `DecimalText` | §6.2 | The decimal maximum is exactly representable. | — |
| `TryParse_ForLargestScaleCoefficient_Succeeds` | `DecimalText` | §6.2 | A coefficient at the 96-bit bound with scale 1 is accepted. | — |
| `TryParse_ForTrailingZeroThatReducesToMaximumCoefficient_Succeeds` | `DecimalText` | §6.2 | Trailing-zero reduction runs before the coefficient and scale checks. | — |
| `TryParse_ForSmallestScale_Succeeds` | `DecimalText` | §6.2 | `1e-28` is representable. | — |
| `TryParse_ForTrailingZeroAtSmallestScale_Succeeds` | `DecimalText` | §6.2 | `1.0e-28` reduces to the smallest representable scale. | — |
| `TryParse_ForZeroWithAnyExponent_SucceedsAsZero` | `DecimalText` | §6.2 | Zero with any exponent normalizes to zero. | — |
| `TryParse_ForInexactOrOutOfRangeLiteral_Fails` | `DecimalText` | §6.2 | Overflow, underflow, precision loss, and coefficient overflow fail. | Parameterized edge cases. |
| `TryParse_ForNonNumericOrMalformedText_Fails` | `DecimalText` | §6.2 | Whitespace, malformed exponents, separators, and junk fail. | Parameterized including null and empty. |
| `Format_ForRepresentableValue_ReturnsCanonicalPlainText` | `DecimalText` | §10.3 | Trailing zeros are trimmed, negative zero becomes `0`, and output never uses an exponent. | Parameterized canonical forms. |
| `Format_ForDecimalMinValue_ReturnsPlainText` | `DecimalText` | §10.3 | The decimal minimum prints in plain form. | — |
| `Format_ForDecimalMaxValue_ReturnsPlainText` | `DecimalText` | §10.3 | The decimal maximum prints in plain form. | — |
| `Format_AfterExactParse_RoundTripsThroughParse` | `DecimalText` | §6.2, §10.3 | Canonical decimal text parses back to the same value and formats identically. | Parameterized. |

### PrinterInvariantValidator

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `Validate_WhenRequiredParameterIsAbsent_ReportsMissingRequiredParam` | `PrinterInvariantValidator` | §10, §13.5 | A missing required parameter reports `E_PRN_MISSING_REQUIRED_PARAM`. | — |
| `Validate_WhenMultipleRequiredParametersAreAbsent_ReportsFirstInDeclarationOrder` | `PrinterInvariantValidator` | §10, §13.5 | Multiple missing required parameters report the first in declaration order. | — |
| `Validate_WhenProvidedParameterIsNotDeclared_ReportsUnknownParam` | `PrinterInvariantValidator` | §10, §13.5 | A provided parameter absent from metadata reports `E_PRN_UNKNOWN_PARAM`. | — |
| `Validate_WhenDeclaredParameterNameIsRecreated_ReportsUnknownParamByReference` | `PrinterInvariantValidator` | §10.2, §13.5 | A structurally equal but recreated parameter is unknown; matching is by reference. | — |
| `Validate_WhenParameterIsProvidedTwice_ReportsDuplicateParam` | `PrinterInvariantValidator` | §10, §13.5 | A repeated parameter reference reports `E_PRN_DUPLICATE_PARAM`. | — |
| `Validate_WhenProvidedParametersAreOutOfDeclarationOrder_ReportsInconsistentTree` | `PrinterInvariantValidator` | §10, §13.5 | Out-of-order `Provided` reports detail `provided parameter order violation`. | — |
| `Validate_WhenSchemeNameIsNotCanonical_ReportsInconsistentTree` | `PrinterInvariantValidator` | §10.2, §13.5 | A non-lowercase scheme reports detail `non-canonical name`. | Precedence over simultaneous kind mismatch is not defined by §10; see `nonapproved-behaviour.md`. |
| `Validate_WhenParameterNameIsNotCanonical_ReportsInconsistentTree` | `PrinterInvariantValidator` | §10.2, §13.5 | A non-lowercase provided parameter name reports `non-canonical name`. | Precedence over simultaneous kind mismatch is not defined by §10; see `nonapproved-behaviour.md`. |
| `Validate_WhenStructFieldNameIsNotCanonical_ReportsInconsistentTree` | `PrinterInvariantValidator` | §10.2, §13.5 | A non-lowercase provided struct field name reports `non-canonical name`. | — |
| `Validate_WhenStringParameterReceivesTransport_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A transport value for a string parameter reports actual `transport`, expected `string`. | — |
| `Validate_WhenStringParameterReceivesList_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A transport list for a string parameter reports actual `transport list`, expected `string`. | — |
| `Validate_WhenStringParameterReceivesStruct_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A struct for a string parameter reports actual `struct`, expected `string`. | — |
| `Validate_WhenTransportParameterReceivesString_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for a transport parameter reports actual `string`, expected `transport`. | — |
| `Validate_WhenListParameterReceivesString_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for a list parameter reports expected `transport list`. | — |
| `Validate_WhenListParameterReceivesTransport_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A transport for a list parameter reports expected `transport list`. | — |
| `Validate_WhenListParameterReceivesStruct_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A struct for a list parameter reports expected `transport list`. | — |
| `Validate_WhenStructParameterReceivesString_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for a struct parameter reports expected `struct`. | — |
| `Validate_WhenStringParameterReceivesInteger_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | An integer value for a string parameter reports actual `integer`, expected `string`. | — |
| `Validate_WhenStringParameterReceivesDecimal_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A decimal value for a string parameter reports actual `decimal`, expected `string`. | — |
| `Validate_WhenIntegerParameterReceivesString_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for an integer parameter reports actual `string`, expected `integer`. | — |
| `Validate_WhenIntegerParameterReceivesDecimal_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A decimal value for an integer parameter reports actual `decimal`, expected `integer`. | — |
| `Validate_WhenDecimalParameterReceivesString_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for a decimal parameter reports actual `string`, expected `decimal`. | — |
| `Validate_WhenDecimalParameterReceivesInteger_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | An integer value for a decimal parameter reports actual `integer`, expected `decimal`. | — |
| `Validate_WhenIntegerParameterValueIsOutOfRange_ReportsValueOutOfRange` | `PrinterInvariantValidator` | §10, §13.5 | An out-of-range integer parameter reports `E_PRN_VALUE_OUT_OF_RANGE` with invariant text. | — |
| `Validate_WhenDecimalParameterValueIsOutOfRange_ReportsCanonicalValue` | `PrinterInvariantValidator` | §10, §13.5 | An out-of-range decimal parameter reports canonical decimal text. | — |
| `Validate_WhenIntegerFieldHasWrongKind_ReportsStructFieldKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A decimal value for an integer field reports expected `integer`. | — |
| `Validate_WhenStringFieldReceivesInteger_ReportsStructFieldKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | An integer value for a string field reports expected `string`. | — |
| `Validate_WhenDecimalFieldReceivesString_ReportsStructFieldKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for a decimal field reports expected `decimal`. | — |
| `Validate_WhenDecimalFieldValueIsOutOfRange_ReportsFullPath` | `PrinterInvariantValidator` | §10, §13.5 | An out-of-range decimal field reports its canonical value. | — |
| `Validate_WhenIntegerParameterHasWrongKindAndOutOfRange_ReportsKindMismatchFirst` | `PrinterInvariantValidator` | §10, §13.5 | Kind is validated before range for a numeric parameter. | — |
| `Validate_WhenNestedIntegerFieldValueIsOutOfRange_ReportsFullPath` | `PrinterInvariantValidator` | §10, §13.5 | A nested out-of-range field reports its full dot-separated path. | — |
| `Validate_WhenStructParameterReceivesTransport_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A transport for a struct parameter reports expected `struct`. | — |
| `Validate_WhenStructParameterReceivesList_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A transport list for a struct parameter reports expected `struct`. | — |
| `Validate_WhenListIsBelowMinimum_ReportsListArityRange` | `PrinterInvariantValidator` | §10, §13.5 | A short list reports the closed range `2..3`. | — |
| `Validate_WhenListIsAboveMaximum_ReportsListArityRange` | `PrinterInvariantValidator` | §10, §13.5 | A long list reports the closed range `2..3`. | — |
| `Validate_WhenUnboundedListExceedsMinimum_AcceptsLargeList` | `PrinterInvariantValidator` | §10, §13.5 | A null maximum is unbounded; a five-element list is accepted. | — |
| `Validate_WhenHomogeneousListMixesTypes_ReportsListHomogeneity` | `PrinterInvariantValidator` | §10, §13.5 | Mixed element types in a homogeneous list report `E_PRN_LIST_HOMOGENEITY`. | — |
| `Validate_WhenTransportExpressionIsOutsideExpectedSet_ReportsMembershipViolation` | `PrinterInvariantValidator` | §10, §13.5 | A transport outside the parameter expected set reports `expected-type membership violation`. | — |
| `Validate_WhenListElementIsOutsideExpectedSet_ReportsMembershipViolation` | `PrinterInvariantValidator` | §10, §13.5 | A list element outside the parameter expected set reports membership violation. | — |
| `Validate_WhenStructContainsUndeclaredField_ReportsUnknownField` | `PrinterInvariantValidator` | §10, §13.5 | An undeclared struct field reports its stored name. | — |
| `Validate_WhenNestedStructContainsUndeclaredField_ReportsFullPath` | `PrinterInvariantValidator` | §10, §13.5 | A nested unknown field reports the full dot-separated path. | — |
| `Validate_WhenStructBindingUsesARecreatedField_ReportsUnknownFieldByReference` | `PrinterInvariantValidator` | §10.2, §13.5 | Struct fields are matched by reference identity, not record equality or name. | — |
| `Validate_WhenRequiredStructFieldIsAbsent_ReportsMissingField` | `PrinterInvariantValidator` | §10, §13.5 | A missing required field reports `E_PRN_STRUCT_FIELD_MISSING`. | — |
| `Validate_WhenMultipleRequiredStructFieldsAreAbsent_ReportsFirstInSchemaOrder` | `PrinterInvariantValidator` | §10, §13.5 | Multiple missing required fields report the first in schema order. | — |
| `Validate_WhenNestedRequiredStructFieldIsAbsent_ReportsFullPath` | `PrinterInvariantValidator` | §10, §13.5 | A missing nested required field reports `outer.inner.value`. | — |
| `Validate_WhenStructFieldAppearsTwice_ReportsDuplicateField` | `PrinterInvariantValidator` | §10, §13.5 | A repeated field reference reports `E_PRN_STRUCT_FIELD_DUPLICATE`. | — |
| `Validate_WhenStructFieldHasWrongKind_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A nested value for a string field reports actual `struct`, expected `string`. | — |
| `Validate_WhenNestedStructFieldHasWrongKind_ReportsKindMismatch` | `PrinterInvariantValidator` | §10, §13.5 | A string value for a nested field reports actual `string`, expected `struct`. | — |
| `Validate_WhenStructFieldsAreOutOfSchemaOrder_ReportsStructOrderViolation` | `PrinterInvariantValidator` | §10, §13.5 | Out-of-order fields report detail `struct field order violation`. | — |
| `Validate_WhenStringFieldContainsCarriageReturn_ReportsInvalidScalar` | `PrinterInvariantValidator` | §10.4, §13.5 | Scalar printability is enforced for struct fields too. | — |
| `Validate_WhenCastTargetIsAbsentFromCatalog_ReportsInvalidCastTarget` | `PrinterInvariantValidator` | §10.1, §13.5 | An unknown cast target reports its `TransportType.ToString()` value. | — |
| `Validate_WhenCastIsAnIdentity_ReportsInconsistentTreeBeforeCatalogLookup` | `PrinterInvariantValidator` | §10.1, §13.5 | Identity casts report `identity cast node` before the target lookup. | The target is deliberately absent from the catalog. |
| `Validate_WhenCastInnerIsInvalid_ValidatesInnerAfterCastTarget` | `PrinterInvariantValidator` | §10, §13.5 | A valid cast target is checked before the inner transport's own invariants. | — |
| `Validate_WhenTransportTypeIsAbsentFromCatalog_ReportsInconsistentTree` | `PrinterInvariantValidator` | §8, §10, §13.5 | A transport type absent from the catalog reports `transport type not in catalog`. | — |
| `Validate_WhenRootIsMissing_ReportsUnspecifiedInvariantViolation` | `PrinterInvariantValidator` | §10, §13.5 | A malformed tree with no root reports `unspecified invariant violation`. | Hand-built null root; valid resolvers never produce it. See `nonapproved-behaviour.md`. |
| `Validate_ChecksMissingRequiredBeforeUnknownProvidedParameters` | `PrinterInvariantValidator` | §10, §13.5 | Required-parameter checks precede unknown-parameter checks. | — |
| `Validate_ChecksDuplicateBeforeProvidedParameterOrder` | `PrinterInvariantValidator` | §10, §13.5 | Duplicate checks precede declaration-order checks. | The provided sequence contains both violations. |
| `Validate_ChecksUnknownBeforeDuplicate` | `PrinterInvariantValidator` | §10, §13.5 | Unknown-parameter checks precede duplicate checks. | — |
| `Validate_ChecksProvidedOrderBeforeParameterKinds` | `PrinterInvariantValidator` | §10, §13.5 | Out-of-order `Provided` checks precede per-parameter value-kind checks. | — |
| `Validate_ChecksUnknownStructFieldBeforeMissingRequiredField` | `PrinterInvariantValidator` | §10, §13.5 | Struct unknown-field checks precede missing-required checks. | — |
| `Validate_ChecksMissingStructFieldBeforeDuplicateField` | `PrinterInvariantValidator` | §10, §13.5 | Struct missing-required checks precede duplicate-field checks. | — |
| `Validate_ChecksDuplicateStructFieldBeforeSchemaOrder` | `PrinterInvariantValidator` | §10, §13.5 | Struct duplicate checks precede schema-order checks. | — |
| `Validate_ChecksStructOrderBeforeFieldKinds` | `PrinterInvariantValidator` | §10, §13.5 | Struct schema-order checks precede field value-kind checks. | — |

### CanonicalizationErrorFactory

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `InconsistentTree_ForKnownDetail_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Every documented inconsistent-tree detail formats `resolved tree is inconsistent: <detail>`. | Parameterized method; all eight details including `constructor type mismatch`, which is structurally unreachable through the current DTO. |
| `MissingRequiredParam_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_MISSING_REQUIRED_PARAM` message. | — |
| `UnknownParam_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_UNKNOWN_PARAM` message. | — |
| `DuplicateParam_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_DUPLICATE_PARAM` message. | — |
| `ParamKindMismatch_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_PARAM_KIND_MISMATCH` message. | — |
| `ValueOutOfRange_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_VALUE_OUT_OF_RANGE` message. | — |
| `ListArity_WithMaximum_FormatsClosedRange` | `CanonicalizationErrorFactory` | §13.5 | `<range>` is `min..max` when `Max` is set. | — |
| `ListArity_WithoutMaximum_FormatsAtLeastRange` | `CanonicalizationErrorFactory` | §13.5 | `<range>` is `at least <min>` when `Max` is null. | — |
| `ListHomogeneity_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_LIST_HOMOGENEITY` message. | — |
| `StructFieldUnknown_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_STRUCT_FIELD_UNKNOWN` message. | — |
| `StructFieldMissing_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_STRUCT_FIELD_MISSING` message. | — |
| `StructFieldDuplicate_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_STRUCT_FIELD_DUPLICATE` message. | — |
| `StructFieldKindMismatch_FormatsTemplate` | `CanonicalizationErrorFactory` | §13.5 | Exact `E_PRN_STRUCT_FIELD_KIND_MISMATCH` message. | — |
| `InvalidCastTarget_UsesTransportTypeToString` | `CanonicalizationErrorFactory` | §13.5, §14.8 | An absent cast target uses `TransportType.ToString()` in the message. | — |
| `InvalidScalar_FormatsFirstOffendingCodeUnitAsFourUppercaseHexDigits` | `CanonicalizationErrorFactory` | §10.4, §13.5 | `U+XXXX` formatting is four uppercase hexadecimal digits. | Parameterized method; U+0000, U+000A, U+000D, U+D800, and U+DC00. |

## Printer error catalogue

Every `E_PRN_*` code from `parsing.md` §13.5 is asserted with its exact message,
phase `Printing`, and no span.

| Code | Tests |
|---|---|
| `E_PRN_INCONSISTENT_TREE` | `Validate_WhenProvidedParametersAreOutOfDeclarationOrder_ReportsInconsistentTree`, `Validate_ChecksProvidedOrderBeforeParameterKinds`, `Validate_WhenSchemeNameIsNotCanonical_ReportsInconsistentTree`, `Validate_WhenParameterNameIsNotCanonical_ReportsInconsistentTree`, `Validate_WhenStructFieldNameIsNotCanonical_ReportsInconsistentTree`, `Validate_WhenTransportExpressionIsOutsideExpectedSet_ReportsMembershipViolation`, `Validate_WhenListElementIsOutsideExpectedSet_ReportsMembershipViolation`, `Validate_WhenStructFieldsAreOutOfSchemaOrder_ReportsStructOrderViolation`, `Validate_ChecksStructOrderBeforeFieldKinds`, `Validate_WhenCastIsAnIdentity_ReportsInconsistentTreeBeforeCatalogLookup`, `Validate_WhenTransportTypeIsAbsentFromCatalog_ReportsInconsistentTree`, `Validate_WhenRootIsMissing_ReportsUnspecifiedInvariantViolation`, `InconsistentTree_ForKnownDetail_FormatsTemplate` |
| `E_PRN_MISSING_REQUIRED_PARAM` | `Validate_WhenRequiredParameterIsAbsent_ReportsMissingRequiredParam`, `Validate_WhenMultipleRequiredParametersAreAbsent_ReportsFirstInDeclarationOrder`, `Validate_WhenCastInnerIsInvalid_ValidatesInnerAfterCastTarget`, `Validate_ChecksMissingRequiredBeforeUnknownProvidedParameters`, `MissingRequiredParam_FormatsTemplate` |
| `E_PRN_UNKNOWN_PARAM` | `Validate_WhenProvidedParameterIsNotDeclared_ReportsUnknownParam`, `Validate_WhenDeclaredParameterNameIsRecreated_ReportsUnknownParamByReference`, `Validate_ChecksUnknownBeforeDuplicate`, `UnknownParam_FormatsTemplate` |
| `E_PRN_DUPLICATE_PARAM` | `Validate_WhenParameterIsProvidedTwice_ReportsDuplicateParam`, `Validate_ChecksDuplicateBeforeProvidedParameterOrder`, `DuplicateParam_FormatsTemplate` |
| `E_PRN_PARAM_KIND_MISMATCH` | `Validate_WhenStringParameterReceivesTransport_ReportsKindMismatch`, `Validate_WhenStringParameterReceivesList_ReportsKindMismatch`, `Validate_WhenStringParameterReceivesStruct_ReportsKindMismatch`, `Validate_WhenStringParameterReceivesInteger_ReportsKindMismatch`, `Validate_WhenStringParameterReceivesDecimal_ReportsKindMismatch`, `Validate_WhenTransportParameterReceivesString_ReportsKindMismatch`, `Validate_WhenIntegerParameterReceivesString_ReportsKindMismatch`, `Validate_WhenIntegerParameterReceivesDecimal_ReportsKindMismatch`, `Validate_WhenDecimalParameterReceivesString_ReportsKindMismatch`, `Validate_WhenDecimalParameterReceivesInteger_ReportsKindMismatch`, `Validate_WhenListParameterReceivesString_ReportsKindMismatch`, `Validate_WhenListParameterReceivesTransport_ReportsKindMismatch`, `Validate_WhenListParameterReceivesStruct_ReportsKindMismatch`, `Validate_WhenStructParameterReceivesString_ReportsKindMismatch`, `Validate_WhenStructParameterReceivesTransport_ReportsKindMismatch`, `Validate_WhenStructParameterReceivesList_ReportsKindMismatch`, `ParamKindMismatch_FormatsTemplate` |
| `E_PRN_VALUE_OUT_OF_RANGE` | `Validate_WhenIntegerParameterValueIsOutOfRange_ReportsValueOutOfRange`, `Validate_WhenDecimalParameterValueIsOutOfRange_ReportsCanonicalValue`, `Validate_WhenDecimalFieldValueIsOutOfRange_ReportsFullPath`, `Validate_WhenNestedIntegerFieldValueIsOutOfRange_ReportsFullPath`, `ValueOutOfRange_FormatsTemplate` |
| `E_PRN_LIST_ARITY` | `Validate_WhenListIsBelowMinimum_ReportsListArityRange`, `Validate_WhenListIsAboveMaximum_ReportsListArityRange`, `Validate_WhenUnboundedListExceedsMinimum_AcceptsLargeList`, `ListArity_WithMaximum_FormatsClosedRange`, `ListArity_WithoutMaximum_FormatsAtLeastRange` |
| `E_PRN_LIST_HOMOGENEITY` | `Validate_WhenHomogeneousListMixesTypes_ReportsListHomogeneity`, `ListHomogeneity_FormatsTemplate` |
| `E_PRN_STRUCT_FIELD_UNKNOWN` | `Validate_WhenStructContainsUndeclaredField_ReportsUnknownField`, `Validate_WhenNestedStructContainsUndeclaredField_ReportsFullPath`, `Validate_WhenStructBindingUsesARecreatedField_ReportsUnknownFieldByReference`, `Validate_ChecksUnknownStructFieldBeforeMissingRequiredField`, `StructFieldUnknown_FormatsTemplate` |
| `E_PRN_STRUCT_FIELD_MISSING` | `Validate_WhenRequiredStructFieldIsAbsent_ReportsMissingField`, `Validate_WhenMultipleRequiredStructFieldsAreAbsent_ReportsFirstInSchemaOrder`, `Validate_WhenNestedRequiredStructFieldIsAbsent_ReportsFullPath`, `Validate_ChecksMissingStructFieldBeforeDuplicateField`, `StructFieldMissing_FormatsTemplate` |
| `E_PRN_STRUCT_FIELD_DUPLICATE` | `Validate_WhenStructFieldAppearsTwice_ReportsDuplicateField`, `Validate_ChecksDuplicateStructFieldBeforeSchemaOrder`, `StructFieldDuplicate_FormatsTemplate` |
| `E_PRN_STRUCT_FIELD_KIND_MISMATCH` | `Validate_WhenStructFieldHasWrongKind_ReportsKindMismatch`, `Validate_WhenNestedStructFieldHasWrongKind_ReportsKindMismatch`, `Validate_WhenIntegerFieldHasWrongKind_ReportsStructFieldKindMismatch`, `Validate_WhenStringFieldReceivesInteger_ReportsStructFieldKindMismatch`, `Validate_WhenDecimalFieldReceivesString_ReportsStructFieldKindMismatch`, `StructFieldKindMismatch_FormatsTemplate` |
| `E_PRN_INVALID_CAST_TARGET` | `Validate_WhenCastTargetIsAbsentFromCatalog_ReportsInvalidCastTarget`, `InvalidCastTarget_UsesTransportTypeToString` |
| `E_PRN_INVALID_SCALAR` | `Format_ForCarriageReturn_ThrowsInvalidScalarWithU000D`, `Format_ForLineFeed_ThrowsInvalidScalarWithU000A`, `Format_ForBothNewlineKinds_ReportsFirstCodeUnitInTextOrder`, `Format_ForUnpairedHighSurrogate_ThrowsInvalidScalarWithUD800`, `Format_ForUnpairedLowSurrogate_ThrowsInvalidScalarWithUDC00`, `Format_ForCrBeforeUnpairedSurrogate_ReportsTheCr`, `Validate_WhenStringFieldContainsCarriageReturn_ReportsInvalidScalar`, `InvalidScalar_FormatsFirstOffendingCodeUnitAsFourUppercaseHexDigits` |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| None currently identified. | Canonicalization layer | New tests are added with the contract change that requires them. |

## Deferred by design

| Gap | Reason |
|---|---|
| A validator-level `constructor type mismatch` tree. | `ResolvedTransport.Type` is derived from `Constructor.Type` in the current resolved DTO, so the mismatch cannot be constructed through the public DTO contract. The validator keeps the check, and the error template is asserted directly. Changing the DTO is a semantics-layer decision, not a canonicalization change. |
| `E_PRN_INCONSISTENT_TREE` detail `constructor type mismatch` through `Candidate`/`Print`. | Same DTO limitation as above. |
| Printing a `ResolvedCast` whose inner is another cast. | `ResolvedCast.Inner` is typed `ResolvedTransport`, so a nested cast chain cannot be represented by the DTO. |
| Expected-type membership as a user-visible semantic error. | `parsing.md` §7.6 defines this as a printer/compiler invariant; the printer reports it as `E_PRN_INCONSISTENT_TREE`. |
| Converter-path validation during cast printing. | Build-time concern (`E_BLD_NO_CONVERSION_PATH`); the printer must not validate converter availability. |
| Default-value synthesis for absent optional parameters. | The metadata model defines no defaults; only provided parameters print. |
| `E_LEX_*`, `E_SYN_*`, `E_SEM_*`, `E_REG_*`, `E_CMP_*`, and `E_BLD_*`. | Owned by the parsing, semantics, registry, compilation, and build suites. |
| Concurrency, allocation, performance, and adversarial depth. | Canonicalization is a pure deterministic function over an immutable tree; proving throughput or stack behavior is non-functional and belongs to a stress category. |
| Host alias and scheme policy. | Which aliases and schemes a host registers is configuration; this suite pins the printer rules, not the policy. |

## Notes

- Every fixture carries `[Category("Factory.Canonicalization")]`.
- Tests are deterministic and isolated: no time, sleep, sockets, files, threads,
  randomness, or background tasks.
- No mocking library is used. `Harness/CanonicalizationTestData` builds metadata,
  resolved trees, and real parser/resolver pipelines;
  `Harness/CanonicalizationAssertions` centralises success, error, raw-newline,
  and structural-equality assertions.
- Internal implementations are reached through
  `InternalsVisibleTo("Pontifex.Factory.Tests")`, not through public production
  API or reflection.
- Every printer error assertion checks code, exact message, phase `Printing`, and
  a null span.
- Parameterized methods count as one row in this table; their `TestCaseData`
  rows cover the individual examples listed in the description.
- The standard registry fixture is the `parsing.md` §14 registry plus named
  harness extensions documented in the suite README. Extensions exist only to
  reach canonicalization branches the §14 table does not exercise.
- Registration lowercases scheme names. The §14 name `emptyStruct` is therefore
  printed as `emptystruct`; the expected output reflects the normalized metadata
  view required by `parsing.md` §6.1 and §10.2.
- Test assumptions that are not part of the normative contract are collected in
  `nonapproved-behaviour.md`.
