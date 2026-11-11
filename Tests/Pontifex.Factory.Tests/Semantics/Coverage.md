# Semantics Layer Coverage

- **Layer:** Semantics (root layer)
- **Subject:** Transport Formula resolution subsystem group:
  `FormulaResolver`, `ConstructorSelector`, `ArgumentBinder`,
  `ParameterValidator`, `StructValidator`, `SemanticErrorFactory`, and the
  internal `SemanticAbort` channel.
- **Directory:** `Tests/Pontifex.Factory.Tests/Semantics`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [parsing.md](../../../Pontifex.Factory/parsing.md) §7, §8,
  §12.2-12.3, §13.3, §14.1-14.3,
  [implementation-plan.md](../../../Pontifex.Factory/implementation-plan.md) §9,
  [IResolver.cs](../../../Pontifex.Factory/Semantics/IResolver.cs),
  [ResolvedDtos.cs](../../../Pontifex.Factory/Semantics/ResolvedDtos.cs),
  [IRegistry.cs](../../../Pontifex.Factory/Registry/IRegistry.cs),
  [ITransportTypeCatalog.cs](../../../Pontifex.Factory/Registry/ITransportTypeCatalog.cs),
  [MetadataDtos.cs](../../../Pontifex.Factory/Registry/MetadataDtos.cs),
  [FactoryError.cs](../../../Pontifex.Factory/FactoryError.cs),
  [SourceSpan.cs](../../../Pontifex.Factory/SourceSpan.cs)

## Covered tests

| Test | Subject | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|---|
| `Resolve_ForCanonicalScheme_SelectsCanonicalConstructorAndTransport` | `FormulaResolver` | §7.3, §8 | A canonical scheme resolves to its constructor, `ResolvedTransport.Type == Constructor.Type`, and the nested transport keeps its own type. | — |
| `Resolve_ForTwoPositionalArguments_BindsPositionalPrefixInDeclarationOrder` | `FormulaResolver` | §7.4, §14.1 | Two positionals fill the first two positional-capable parameters in declaration order. | — |
| `Resolve_ForNamedOnlyParameter_BindsItByName` | `FormulaResolver` | §7.4, §14.1 | A named-only parameter is fillable by name after the positional prefix. | — |
| `Resolve_ForPositionalPrefixAndNamedSuffix_BindsBoth` | `FormulaResolver` | §7.4, §14.1 | A positional prefix followed by a named suffix binds all supplied parameters. | — |
| `Resolve_ForAliasSelectedByExpectedType_ReturnsAliasConstructor` | `FormulaResolver` | §7.3, §14.1 | `outer` expects `Exactly(TypeB)`, so the alias group for `alpha` filters out canonical alpha and selects beta. | Alias selection is context-sensitive. |
| `Resolve_ForAliasOnlySchemeWithExpectedType_SelectsMatchingAlias` | `FormulaResolver` | §7.3, §7.6 | An alias-only scheme with two candidates is narrowed by the parameter expected set to the surviving constructor. | Harness `needsC` provides the expected `TypeC` context. |
| `Resolve_ForUnionExpectedSet_PrefersCanonicalAfterFiltering` | `FormulaResolver` | §7.3 | When canonical and alias candidates both survive a `OneOf` filter, canonical preference selects the canonical constructor. | — |
| `Resolve_ForCastThatChangesType_ReturnsResolvedCast` | `FormulaResolver` | §7.2, §8 | A changing cast produces `ResolvedCast` with `Type == Target` and `Inner.Type != Target`. | — |
| `Resolve_ForIdentityCast_CollapsesToInnerTransportWithFullCastSpan` | `FormulaResolver` | §7.2, §8 | An identity cast collapses to the inner transport and retains the full cast expression span. | No cast node exists in the resolved tree. |
| `Resolve_ForCastInsideTransportParameter_ResolvesInnerWithAny` | `FormulaResolver` | §7.2 | A cast nested in a typed parameter resolves its inner transport with `Any`, not with the cast target. | Proves casts do not select variants. |
| `Resolve_ForNonIdentityCast_RetainsInnerCallAndFullCastSpans` | `FormulaResolver` | §7.2, §8 | A non-identity cast keeps the inner call span and the full cast span. | — |
| `Resolve_ForNestedTransport_RetainsEachSpan` | `FormulaResolver` | §8 | Every resolved node retains the source span from its AST node. | — |
| `Resolve_ForHomogeneousList_ResolvesElementsInOrder` | `FormulaResolver` | §7.5, §14.1 | A homogeneous list resolves each element in source order with one shared type. | — |
| `Resolve_ForNonHomogeneousList_PreservesElementOrder` | `FormulaResolver` | §7.5, §8 | A non-homogeneous list preserves element order and each element type. | — |
| `Resolve_ForListWithExpectedSet_ResolvesElementsWithParameterExpectedType` | `FormulaResolver` | §7.1, §7.5 | List elements are resolved with the parameter's expected set, so an alias selects the expected variant. | Harness `listB` supplies `Exactly(TypeB)`. |
| `Resolve_ForNestedStruct_ResolvesFieldsInSchemaOrder` | `FormulaResolver` | §7.8, §14.1 | Struct fields are resolved and returned in schema order with nested struct values. | — |
| `Resolve_ForStructSchemaOrderDifferentFromSourceOrder_OrdersFieldsBySchema` | `FormulaResolver` | §7.8, §8 | Source field order does not affect the resolved field order. | — |
| `Resolve_ForDeepNestedStruct_ProducesNestedFieldValues` | `FormulaResolver` | §7.8 | Three-level nested structs produce nested `StructFieldBinding` values. | — |
| `Resolve_ForEmptyList_ProducesEmptyElementList` | `FormulaResolver` | §7.5 | An empty list is a valid `TransportListValue` when the range allows it. | Harness `bag` has `Min = 0`. |
| `Resolve_ForEmptyStruct_ProducesEmptyFieldList` | `FormulaResolver` | §7.8 | An empty struct against an empty schema yields no field bindings. | — |
| `Resolve_ForSingleElementList_ResolvesTheElement` | `FormulaResolver` | §7.5 | A one-element list satisfies `Min = 1, Max = 1`. | — |
| `Resolve_ForNamedArgumentsInReverseOrder_StoresProvidedInDeclarationOrder` | `FormulaResolver` | §7.4, §8 | `Provided` follows declaration order, not source order. | — |
| `Resolve_ForNamedOnlyBeforePositionalParameter_StoresProvidedInDeclarationOrder` | `FormulaResolver` | §7.4, §8 | A named-only parameter declared before a positional parameter stays first in `Provided`. | Harness `mixed` has `flag` before `input`. |
| `Resolve_ForNamedPositionalCapableParameter_BindsItWithoutSynthesizingEarlierOnes` | `FormulaResolver` | §7.4, §8 | A positional-capable parameter may be supplied by name without synthesizing earlier optional parameters. | — |
| `Resolve_WhenOptionalParametersAreAbsent_DoesNotSynthesizeDefaults` | `FormulaResolver` | §7.4, §8 | Absent optional parameters are absent; no defaults are inserted. | — |
| `Resolve_ForUppercaseSchemeAndArgumentNames_ProducesCanonicalMetadata` | `FormulaResolver` | §7, §8 | Scheme and named-argument lookup are case-insensitive and the resolved tree exposes canonical lowercase metadata. | — |
| `Resolve_ForQuotedScalarWithComma_PreservesDecodedText` | `FormulaResolver` | §7.5, §8 | Scalar text is the decoded source text, including commas. | — |
| `Resolve_ForEmptyQuotedScalar_PreservesEmptyText` | `FormulaResolver` | §7.5, §8 | An empty quoted scalar is preserved as an empty string. | — |
| `Resolve_ForBareNumericScalar_AcceptsItAsString` | `FormulaResolver` | §7.5 | String parameters accept bare scalars such as `1`; no numeric conversion exists. | — |
| `Resolve_BindsProvidedParametersToConstructorParameterInstances` | `FormulaResolver` | §8 | Every `ParamBinding.Param` is reference-equal to a constructor parameter. | Reference identity, not structural equality. |
| `Resolve_BindsStructFieldsToSchemaFieldInstances` | `FormulaResolver` | §8 | Every `StructFieldBinding.Field` is reference-equal to a schema field. | Reference identity, not structural equality. |
| `Resolve_ForRequiredParameters_ReportsEveryProvidedTypeIsInParameterExpectedSet` | `FormulaResolver` | §7.6, §8 | A resolved transport value satisfies the parameter's expected set. | The resolver has no user-visible membership error; the positive invariant is asserted instead. |
| `Select_WithUppercaseScheme_NormalizesBeforeLookup` | `ConstructorSelector` | §7.3 | Scheme normalization is ASCII lowercase before candidate lookup. | — |
| `Select_ForCanonicalSchemeWithAliasUnderAny_PrefersCanonical` | `ConstructorSelector` | §7.3 | Under `Any`, a canonical candidate wins even when an alias candidate exists. | Registry candidate order is canonical first. |
| `Select_ForCanonicalSchemeWithMatchingExpectedType_PrefersCanonical` | `ConstructorSelector` | §7.3 | `Exactly` keeps the canonical candidate when its type matches. | — |
| `Select_ForCanonicalSchemeWhoseExpectedSetExcludesIt_FallsBackToAlias` | `ConstructorSelector` | §7.3 | Expected filtering removes canonical alpha and selects the TypeB alias. | — |
| `Select_ForOneOfExpectedSetWhereCanonicalSurvives_PrefersCanonicalAfterFiltering` | `ConstructorSelector` | §7.3 | Canonical preference runs after filtering for `OneOf` sets. | — |
| `Select_ForSingleAliasCandidateUnderAny_ReturnsAliasConstructor` | `ConstructorSelector` | §7.3 | An alias-only scheme with one candidate is not ambiguous. | Harness `onlybeta`. |
| `Select_ForAliasOnlySchemeWithAny_ReturnsAmbiguousWithPostFilterCandidates` | `ConstructorSelector` | §7.3, §13.3 | Two alias candidates under `Any` produce `E_SEM_AMBIGUOUS_SCHEME` with post-filter candidates. | Exact message asserted. |
| `Select_ForAliasOnlySchemeWithExpectedType_SelectsSurvivingCandidate` | `ConstructorSelector` | §7.3 | Expected filtering resolves an alias-only group deterministically. | — |
| `Select_WhenExpectedFilterLeavesMultipleCandidates_ReportsPostFilterCandidates` | `ConstructorSelector` | §7.3, §13.3 | Ambiguity reports candidates after filtering, not the original alias group. | Harness `multi` starts from three candidates. |
| `Select_ForUnknownScheme_ReportsSortedKnownSchemeNames` | `ConstructorSelector` | §7.3, §13.3 | An empty candidate list produces `E_SEM_UNKNOWN_SCHEME` with sorted known scheme names. | Uses the alpha-only registry for a stable message. |
| `Select_ForUnknownScheme_PreservesRawSchemeSpelling` | `ConstructorSelector` | §13.3 | `<scheme>` in the unknown-scheme message is the raw input spelling. | — |
| `Select_WhenNoCandidateSurvivesExpectedFilter_ReportsPreFilterAvailableVariants` | `ConstructorSelector` | §7.3, §13.3 | `available` is formatted from candidates before expected filtering. | Exact message asserted. |
| `Select_WhenExpectedSetHasSeveralTypes_FormatsTypeSetSorted` | `ConstructorSelector` | §13.3 | `FormatTypeSet` uses distinct canonical names sorted and joined with ` or `. | Input set order was deliberately reversed. |
| `Select_ForAliasOfTheSameTypeAsTheCanonicalConstructor_PrefersCanonical` | `ConstructorSelector` | §6.7 rule 9, §7.3 | An alias may share the canonical constructor's type; canonical preference resolves the overlap. | Custom two-constructor registry. |
| `Bind_ForPositionalArguments_FillsPositionalCapableParametersInDeclarationOrder` | `ArgumentBinder` | §7.4 | Positionals fill positional-capable parameters in declaration order. | — |
| `Bind_ForNamedArguments_BindsByNormalizedName` | `ArgumentBinder` | §7.4 | Named arguments match normalized parameter names case-insensitively. | — |
| `Bind_ForPositionalPrefixThenNamedSuffix_ProducesDeclarationOrder` | `ArgumentBinder` | §7.4 | Bindings are produced in declaration order, not source order. | — |
| `Bind_ExcludesNamedOnlyParametersFromPositionalSlots` | `ArgumentBinder` | §7.4 | Named-only parameters do not consume positional slots. | Harness `mixed`. |
| `Bind_ForNamedOnlyParameter_IncludesIt` | `ArgumentBinder` | §7.4 | A named-only parameter yields a binding when supplied. | — |
| `Bind_ForTooManyPositionalArguments_ReportsFirstExtraValue` | `ArgumentBinder` | §7.4, §12.2 | The offset is the first extra positional value. | Exact offset 21. |
| `Bind_ForUnknownNamedArgument_ReportsRawNameAndScheme` | `ArgumentBinder` | §7.4, §13.3 | Unknown named arguments report the raw name and scheme and the declared parameter expectation. | — |
| `Bind_ForUnknownNamedArgument_PreservesRawSpelling` | `ArgumentBinder` | §13.3 | `<name>` in `E_SEM_UNKNOWN_PARAM` is the raw input spelling. | — |
| `Bind_ForUnknownNamedArgumentWhenSchemeHasNoParameters_ReportsEmptyExpectation` | `ArgumentBinder` | §13.3 | `FormatParamExpectation` renders `the scheme has no parameters`. | Harness `empty`. |
| `Bind_ForNamedArgumentTargetingPositionallyBoundParameter_ReportsDuplicate` | `ArgumentBinder` | §7.4, §13.3 | A named argument targeting an already-filled parameter is a duplicate. | — |
| `Bind_ForRepeatedNamedArgument_ReportsDuplicateAtSecondName` | `ArgumentBinder` | §7.4, §12.2 | The duplicate offset is the second name token. | Exact offset 26. |
| `Bind_ForMissingRequiredParameter_ReportsClosingParenthesis` | `ArgumentBinder` | §7.4, §12.2 | The missing-required offset is the closing `)`. | — |
| `Bind_ForEmptyArgumentsAndNoRequiredParameters_ReturnsEmptyBindings` | `ArgumentBinder` | §7.4 | A no-argument call with no required parameters yields no bindings. | — |
| `Bind_ChecksUnknownNamedArgumentsBeforeDuplicates` | `ArgumentBinder` | §7.9 | Named arguments are checked in source order, name before duplicate. | — |
| `Bind_ChecksTooManyPositionalArgumentsBeforeNamedArguments` | `ArgumentBinder` | §7.9 | Too-many-positional precedes all named-argument errors. | — |
| `Validate_ForStringParameterWithValidText_ProducesScalarValue` | `ParameterValidator` | §7.5 | A string parameter produces `ScalarValue` with the original text and a reference-equal parameter. | — |
| `Validate_ForStringParameterWithNullValidator_AcceptsAnyText` | `ParameterValidator` | §6.4, §7.5 | A null validator accepts any scalar, including empty text. | — |
| `Validate_ForStringParameterWithFailingValidator_ReportsValidatorReason` | `ParameterValidator` | §6.4, §7.5, §13.3 | A non-null validator result becomes `E_SEM_VALIDATOR_FAILED` with the validator reason. | — |
| `Validate_ForStringParameterWithEmptyValidatorReason_ReportsValidationFailed` | `ParameterValidator` | §6.4, §13.3 | An empty validator result is invalid and uses `validation failed`. | Harness `validatedEmpty`. |
| `Validate_ForStringParameterWithThrowingValidator_ReportsValidationFailed` | `ParameterValidator` | §6.4, §13.3 | A throwing validator is caught and uses `validation failed`. | Harness `validatedThrow`. |
| `Validate_ForStringParameterReceivingTransport_ReportsExpectedScalar` | `ParameterValidator` | §7.5, §13.3 | A transport value for a string parameter reports shape `transport`. | — |
| `Validate_ForStringParameterReceivingList_ReportsExpectedScalarWithListShape` | `ParameterValidator` | §7.5, §13.3 | A list value for a string parameter reports shape `transport list`. | — |
| `Validate_ForStringParameterReceivingStruct_ReportsExpectedScalarWithStructShape` | `ParameterValidator` | §7.5, §13.3 | A struct value for a string parameter reports shape `struct`. | — |
| `Validate_ForTransportParameterReceivingScalar_ReportsExpectedTransport` | `ParameterValidator` | §7.5, §13.3 | A scalar for a transport parameter reports shape `scalar`. | — |
| `Validate_ForTransportParameterReceivingList_ReportsExpectedTransport` | `ParameterValidator` | §7.5, §13.3 | A list for a transport parameter reports shape `transport list`. | — |
| `Validate_ForTransportParameterReceivingStruct_ReportsExpectedTransport` | `ParameterValidator` | §7.5, §13.3 | A struct for a transport parameter reports shape `struct`. | — |
| `Validate_ForListParameterReceivingTransport_ReportsExpectedTransportList` | `ParameterValidator` | §7.5, §13.3 | A transport for a list parameter reports shape `transport`. | — |
| `Validate_ForListParameterReceivingScalar_ReportsExpectedTransportList` | `ParameterValidator` | §7.5, §13.3 | A scalar for a list parameter reports shape `scalar`. | — |
| `Validate_ForStructParameterReceivingScalar_ReportsExpectedStruct` | `ParameterValidator` | §7.5, §13.3 | A scalar for a struct parameter reports shape `scalar`. | — |
| `Validate_ForStructParameterReceivingList_ReportsExpectedStruct` | `ParameterValidator` | §7.5, §13.3 | A list for a struct parameter reports shape `transport list`. | — |
| `Validate_ForListBelowMinimum_ReportsMinimumAndCount` | `ParameterValidator` | §7.5, §13.3 | `E_SEM_LIST_MIN` reports the declared minimum and actual count. | — |
| `Validate_ForListAboveMaximum_ReportsMaximumAndCount` | `ParameterValidator` | §7.5, §13.3 | `E_SEM_LIST_MAX` reports the declared maximum and actual count. | — |
| `Validate_ForListWithUnboundedMaximum_AcceptsLargeList` | `ParameterValidator` | §7.5 | A null `Max` is unbounded and accepts any count above the minimum. | — |
| `Validate_ForHomogeneousListWithDifferentTypes_ReportsTypesInElementOrder` | `ParameterValidator` | §7.5, §13.3 | Homogeneity failure lists distinct element types in element order. | Order is not sorted. |
| `Validate_ForHomogeneousListWithFirstElementTypeSecond_ReportsOrderFromFirstElement` | `ParameterValidator` | §7.5, §13.3 | The reported type list starts with the first element's type, proving first-encounter order. | — |
| `Validate_ForListElementsWithExpectedSet_ResolvesElementAgainstParameterExpectedType` | `ParameterValidator` | §7.1, §7.5 | List elements resolve with the parameter expected set. | Harness `listB`. |
| `Validate_ChecksListArityBeforeResolvingElements` | `ParameterValidator` | §7.9 | Minimum arity is checked before element resolution. | The single element would also fail shape validation. |
| `Validate_ChecksListMaximumBeforeResolvingElements` | `ParameterValidator` | §7.9 | Maximum arity is checked before element resolution. | Every element would also fail shape validation. |
| `Validate_ResolvesListElementsInOrderBeforeCheckingHomogeneity` | `ParameterValidator` | §7.9 | Element resolution errors precede homogeneity. | — |
| `Validate_VisitsBoundParametersInDeclarationOrder` | `ParameterValidator` | §7.5, §7.9 | The first bound parameter in declaration order reports its error first. | `alpha(1, 2)` would fail both parameters. |
| `Validate_DelegatesStructParametersToStructValidation` | `ParameterValidator` | §7.5, §7.8 | Struct parameters delegate to `StructValidator` and surface its error. | — |
| `Validate_ForStructParameterWithValidFields_ProducesStructValue` | `ParameterValidator` | §7.5 | A valid struct parameter produces `StructValue`. | — |
| `Validate_ForTransportParameter_ProducesResolvedTransportValue` | `ParameterValidator` | §7.5 | A transport parameter produces `TransportValue` containing a resolved expression. | — |
| `Validate_SkipsUnboundOptionalParameters` | `ParameterValidator` | §7.5 | Only bound parameters are visited and produced. | — |
| `Validate_ForProvidedFields_ReturnsBindingsInSchemaOrder` | `StructValidator` | §7.8 | Provided struct fields are returned in schema order. | — |
| `Validate_ForUnknownField_ReportsRawLeafPathAndSchemaExpectation` | `StructValidator` | §7.8, §13.3 | An unknown top-level field uses its raw spelling and the schema's field expectation. | — |
| `Validate_ForUnknownNestedField_ReportsFullPathAndNestedExpectation` | `StructValidator` | §7.8, §13.3 | An unknown nested field reports the full path and the nested schema expectation. | The top-level parameter name is preserved. |
| `Validate_ForUnknownField_PreservesRawLeafSpelling` | `StructValidator` | §13.3 | Unknown path leaves keep raw input spelling. | — |
| `Validate_ForDuplicateField_ReportsCanonicalPath` | `StructValidator` | §7.8, §13.3 | Duplicate detection is case-insensitive and the path uses canonical segments. | — |
| `Validate_ForDuplicateNestedField_ReportsFullPath` | `StructValidator` | §7.8, §13.3 | Duplicate detection applies recursively and reports the full path. | — |
| `Validate_ForMissingRequiredField_ReportsPathAndParameter` | `StructValidator` | §7.8, §13.3 | Missing required fields report the canonical path and top-level parameter. | — |
| `Validate_ForMissingNestedRequiredField_ReportsFullPath` | `StructValidator` | §7.8, §13.3 | A missing nested required field reports the full dot-separated path. | — |
| `Validate_ChecksMissingRequiredFieldsBeforeProvidedFieldKinds` | `StructValidator` | §7.8, §7.9 | Missing-required checks precede field-kind validation. | The provided field would also fail its kind check. |
| `Validate_ReportsUnknownFieldBeforeMissingRequiredField` | `StructValidator` | §7.8, §7.9 | Source-order binding errors precede missing-required errors. | — |
| `Validate_ForStringFieldReceivingTransport_ReportsExpectedScalar` | `StructValidator` | §7.8, §13.3 | A transport value for a string field reports the field shape error. | — |
| `Validate_ForStringFieldReceivingStruct_ReportsExpectedScalar` | `StructValidator` | §7.8, §13.3 | A struct value for a string field reports shape `struct`. | — |
| `Validate_ForNestedStructFieldReceivingScalar_ReportsExpectedStruct` | `StructValidator` | §7.8, §13.3 | A scalar for a nested struct field reports shape `scalar`. | — |
| `Validate_ForNestedStringFieldReceivingTransport_ReportsFullPath` | `StructValidator` | §7.8, §13.3 | A nested string-field kind error reports the full path. | — |
| `Validate_ForFieldValidatorFailure_ReportsFieldPathAndParameter` | `StructValidator` | §6.4, §7.8, §13.3 | A struct-field validator failure uses the field form of the message. | — |
| `Validate_ForThrowingFieldValidator_ReportsValidationFailed` | `StructValidator` | §6.4, §13.3 | A throwing struct-field validator uses `validation failed`. | — |
| `Validate_ForEmptyFieldValidatorReason_ReportsValidationFailed` | `StructValidator` | §6.4, §13.3 | An empty struct-field validator result uses `validation failed`, including nested paths. | — |
| `Validate_ForEmptySchemaWithNoFields_AcceptsEmptyStruct` | `StructValidator` | §7.8 | An empty schema accepts an empty struct. | — |
| `Validate_ForEmptySchemaWithProvidedField_ReportsNoFieldsExpectation` | `StructValidator` | §13.3 | `FormatFieldExpectation` renders `the struct has no fields`. | — |
| `Validate_ForNestedStruct_ProducesNestedStructFieldValue` | `StructValidator` | §7.8 | Nested structs produce `NestedStructFieldValue` recursively. | — |
| `Validate_BindsFieldsToSchemaFieldInstances` | `StructValidator` | §8 | Struct field bindings are reference-equal to schema fields. | Reference identity, not structural equality. |
| `Validate_ForCaseInsensitiveFieldName_UsesCanonicalSchemaField` | `StructValidator` | §7.8, §8 | Field lookup is case-insensitive and stores the canonical schema field. | — |
| `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` | `SemanticErrorFactory` | §13.3, §14.3 | Parameterized catalogue of semantic errors with exact code, offset, and message. | Forty cases cover every `E_SEM_*` template and the §14.3 matrix. |
| `Resolve_ForUnknownScheme_ReportsSortedKnownSchemes` | `SemanticErrorFactory` | §13.3 | `FormatKnownSchemes` sorts names and renders the known-scheme list. | Uses the alpha-only registry. |
| `Resolve_ForAliasOnlySchemeWithNoSurvivingCandidate_ReportsPreFilterAvailableVariants` | `SemanticErrorFactory` | §13.3 | No-variant `available` is formatted before expected filtering. | — |
| `Resolve_ChecksUnknownCastTargetBeforeExpectedMismatch` | `FormulaResolver` | §7.2, §7.9 | An unknown cast target is reported before an expected-set mismatch. | — |
| `Resolve_ChecksCastExpectedMismatchBeforeResolvingInnerTransport` | `FormulaResolver` | §7.2, §7.9 | The cast target is checked against the expected set before the inner transport is resolved. | The inner scheme is deliberately unknown. |
| `Resolve_ChecksBindingErrorsBeforeParameterValidation` | `FormulaResolver` | §7.9 | Binding errors precede parameter value validation. | — |
| `Resolve_ChecksTooManyPositionalArgumentsBeforeNamedArguments` | `FormulaResolver` | §7.9 | Too-many-positional precedes named-argument errors end to end. | — |
| `Resolve_ChecksUnknownNamedArgumentBeforeDuplicate` | `FormulaResolver` | §7.9 | Unknown-name checks precede duplicate checks end to end. | — |
| `Resolve_ChecksStructBindingErrorsBeforeFieldValueValidation` | `FormulaResolver` | §7.9 | Source-order struct binding errors precede field value validation. | The first field value would also fail its kind check. |
| `Resolve_RunsTwiceOnTheSameInput_ProducesTheSameError` | `FormulaResolver` | §7.9 | Resolution is deterministic for the same input and registry. | — |
| `Resolve_ForSeveralErrors_ReturnsOnlyTheFirstError` | `FormulaResolver` | §12.1, §12.3 | Resolution returns one error and no partial tree, with no recovery. | — |

## Semantic error catalogue

Every `E_SEM_*` code from `parsing.md` §13.3 is asserted with its exact message,
phase `Resolution`, offset, and zero-length span.

| Code | Tests |
|---|---|
| `E_SEM_UNKNOWN_SCHEME` | `Select_ForUnknownScheme_ReportsSortedKnownSchemeNames`, `Select_ForUnknownScheme_PreservesRawSchemeSpelling`, `Resolve_ForUnknownScheme_ReportsSortedKnownSchemes` |
| `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE` | `Select_WhenNoCandidateSurvivesExpectedFilter_ReportsPreFilterAvailableVariants`, `Select_WhenExpectedSetHasSeveralTypes_FormatsTypeSetSorted`, `Resolve_ForAliasOnlySchemeWithNoSurvivingCandidate_ReportsPreFilterAvailableVariants`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`NoVariantForExpectedType`) |
| `E_SEM_AMBIGUOUS_SCHEME` | `Select_ForAliasOnlySchemeWithAny_ReturnsAmbiguousWithPostFilterCandidates`, `Select_WhenExpectedFilterLeavesMultipleCandidates_ReportsPostFilterCandidates`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`AmbiguousAliasOnlyScheme`, `AmbiguousAliasGroup`) |
| `E_SEM_UNKNOWN_PARAM` | `Bind_ForUnknownNamedArgument_ReportsRawNameAndScheme`, `Bind_ForUnknownNamedArgument_PreservesRawSpelling`, `Bind_ForUnknownNamedArgumentWhenSchemeHasNoParameters_ReportsEmptyExpectation`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`UnknownParameter`, `UnknownParameterWhenSchemeHasNoParameters`, `UnknownParameterPreservesRawSpelling`) |
| `E_SEM_DUPLICATE_PARAM` | `Bind_ForNamedArgumentTargetingPositionallyBoundParameter_ReportsDuplicate`, `Bind_ForRepeatedNamedArgument_ReportsDuplicateAtSecondName`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`DuplicateParameter`) |
| `E_SEM_MISSING_REQUIRED_PARAM` | `Bind_ForMissingRequiredParameter_ReportsClosingParenthesis`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`MissingRequiredParameter`, `MissingRequiredParameterPreservesRawSchemeSpelling`) |
| `E_SEM_TOO_MANY_POSITIONAL` | `Bind_ForTooManyPositionalArguments_ReportsFirstExtraValue`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`TooManyPositionalArguments`) |
| `E_SEM_EXPECTED_SCALAR` | `Validate_ForStringParameterReceivingTransport_ReportsExpectedScalar`, `Validate_ForStringParameterReceivingList_ReportsExpectedScalarWithListShape`, `Validate_ForStringParameterReceivingStruct_ReportsExpectedScalarWithStructShape`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ExpectedScalar`) |
| `E_SEM_EXPECTED_TRANSPORT` | `Validate_ForTransportParameterReceivingScalar_ReportsExpectedTransport`, `Validate_ForTransportParameterReceivingList_ReportsExpectedTransport`, `Validate_ForTransportParameterReceivingStruct_ReportsExpectedTransport`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ExpectedTransport`) |
| `E_SEM_EXPECTED_TRANSPORT_LIST` | `Validate_ForListParameterReceivingTransport_ReportsExpectedTransportList`, `Validate_ForListParameterReceivingScalar_ReportsExpectedTransportList`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ExpectedTransportList`) |
| `E_SEM_EXPECTED_STRUCT` | `Validate_ForStructParameterReceivingScalar_ReportsExpectedStruct`, `Validate_ForStructParameterReceivingList_ReportsExpectedStruct`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ExpectedStruct`) |
| `E_SEM_LIST_MIN` | `Validate_ForListBelowMinimum_ReportsMinimumAndCount`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ListMinimum`) |
| `E_SEM_LIST_MAX` | `Validate_ForListAboveMaximum_ReportsMaximumAndCount`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ListMaximum`) |
| `E_SEM_LIST_HOMOGENEITY` | `Validate_ForHomogeneousListWithDifferentTypes_ReportsTypesInElementOrder`, `Validate_ForHomogeneousListWithFirstElementTypeSecond_ReportsOrderFromFirstElement`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ListHomogeneity`, `ListHomogeneityPreservesElementOrder`) |
| `E_SEM_UNKNOWN_STRUCT_FIELD` | `Validate_ForUnknownField_ReportsRawLeafPathAndSchemaExpectation`, `Validate_ForUnknownNestedField_ReportsFullPathAndNestedExpectation`, `Validate_ForUnknownField_PreservesRawLeafSpelling`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`UnknownStructField`, `UnknownNestedStructField`, `UnknownStructFieldWhenSchemaHasNoFields`) |
| `E_SEM_DUPLICATE_STRUCT_FIELD` | `Validate_ForDuplicateField_ReportsCanonicalPath`, `Validate_ForDuplicateNestedField_ReportsFullPath`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`DuplicateStructField`) |
| `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` | `Validate_ForMissingRequiredField_ReportsPathAndParameter`, `Validate_ForMissingNestedRequiredField_ReportsFullPath`, `Validate_ChecksMissingRequiredFieldsBeforeProvidedFieldKinds`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`MissingRequiredStructField`, `MissingRequiredNestedStructField`, `MissingRequiredFieldCheckedBeforeFieldKinds`) |
| `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | `Validate_ForStringFieldReceivingTransport_ReportsExpectedScalar`, `Validate_ForStringFieldReceivingStruct_ReportsExpectedScalar`, `Validate_ForNestedStringFieldReceivingTransport_ReportsFullPath`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`StructFieldExpectedScalarFromTransport`, `StructFieldExpectedScalarFromStruct`, `NestedStructFieldExpectedScalar`) |
| `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` | `Validate_ForNestedStructFieldReceivingScalar_ReportsExpectedStruct`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`StructFieldExpectedStruct`) |
| `E_SEM_UNKNOWN_TRANSPORT_TYPE` | `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`UnknownTransportType`, `UnknownTransportTypePreservesRawSpelling`), `Resolve_ChecksUnknownCastTargetBeforeExpectedMismatch` |
| `E_SEM_CAST_TYPE_MISMATCH` | `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`CastTypeMismatch`, `CastTypeMismatchUnderExactly`, `CastTypeMismatchUnderOneOf`), `Resolve_ChecksCastExpectedMismatchBeforeResolvingInnerTransport` |
| `E_SEM_VALIDATOR_FAILED` | `Validate_ForStringParameterWithFailingValidator_ReportsValidatorReason`, `Validate_ForStringParameterWithEmptyValidatorReason_ReportsValidationFailed`, `Validate_ForStringParameterWithThrowingValidator_ReportsValidationFailed`, `Validate_ForFieldValidatorFailure_ReportsFieldPathAndParameter`, `Validate_ForThrowingFieldValidator_ReportsValidationFailed`, `Validate_ForEmptyFieldValidatorReason_ReportsValidationFailed`, `Resolve_ForSemanticError_ReturnsExactCodeOffsetAndMessage` (`ParameterValidatorFailed`, `ParameterValidatorThrew`, `ParameterValidatorReturnedEmptyReason`, `StructFieldValidatorFailed`, `StructFieldValidatorThrew`, `NestedStructFieldValidatorReturnedEmptyReason`) |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| None currently identified. | Semantics layer | New tests are added with the contract change that requires them. |

## Deferred by design

| Gap | Reason |
|---|---|
| `ResolvedCast` whose `Inner` is itself a `ResolvedCast`. | The grammar permits at most one cast, so no such tree can be produced by a correct resolver. |
| User-visible expected-type membership errors after resolution. | `parsing.md` §7.6 and §13.3 define this as a compiler invariant (`E_CMP_INCONSISTENT_TREE`), not a semantic error. The positive membership invariant is asserted instead. |
| Converter-path validation during casts. | Build-time concern (`E_BLD_NO_CONVERSION_PATH`); the parser and resolver must not validate converter availability. |
| Schema-based overload selection and converter tie-breaking. | Explicitly forbidden by `parsing.md` §7.3 and the approved decisions in `implementation-plan.md` §3. |
| Default-value synthesis. | The metadata model defines no defaults; constructors apply their own defaults after resolution. |
| `E_REG_*`, `E_PRN_*`, `E_CMP_*`, `E_BLD_*`, `E_LEX_*`, and `E_SYN_*`. | Owned by the registry, parsing, canonicalization, compilation, and build suites. |
| Cyclic struct schemas in resolution. | Registration permits shared and cyclic schemas; a cyclic schema cannot terminate on a finite source struct, and resolving one is outside the §14 matrix. Registration-cycle behavior belongs to the registry suite. |
| Concurrency, allocation, performance, and adversarial depth. | The resolver is a pure deterministic function over an immutable registry; proving throughput or stack behavior is non-functional and belongs to a stress category. |
| Host alias and scheme policy. | Which aliases a host registers is configuration; resolution tests pin the rules, not the policy. |

## Notes

- Every fixture carries `[Category("Factory.Semantics")]`.
- Tests are deterministic and isolated: no time, sleep, sockets, files, threads,
  randomness, or background tasks.
- No mocking library is used. `Harness/SemanticsTestData` builds metadata and
  parses formulas with the real parser; `Harness/SemanticsAssertions` centralises
  success and error assertions.
- Internal implementations are reached through
  `InternalsVisibleTo("Pontifex.Factory.Tests")`, not through public production
  API or reflection.
- Component-level failure assertions go through an internal `SemanticAbort`, the
  same channel `FormulaResolver` converts into `FormulaResolveResult.Error`. The
  helper asserts phase `Resolution` and the zero-length offset span on every
  error.
- Parameterized methods count as one row in this table; their `TestCaseData`
  rows cover the individual error templates listed in the description.
- The standard registry fixture is the `parsing.md` §14 registry plus named
  harness extensions documented in the suite README. Extensions exist only to
  reach contract branches the §14 table does not exercise.
