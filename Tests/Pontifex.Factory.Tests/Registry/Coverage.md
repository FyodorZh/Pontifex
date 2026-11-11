# Registry Layer Coverage

- **Layer:** Registry (root layer)
- **Subject:** Transport Formula registry subsystem group:
  `TransportTypeCatalog`, `TransportRegistry`, `TransportRegistryBuilder`,
  `MetadataValidator`, and `RegistryErrorFactory`.
- **Directory:** `Tests/Pontifex.Factory.Tests/Registry`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [parsing.md](../../../Pontifex.Factory/parsing.md) §6.6-6.8 and
  §13.4, [implementation-plan.md](../../../Pontifex.Factory/implementation-plan.md)
  §8, [IRegistry.cs](../../../Pontifex.Factory/Registry/IRegistry.cs),
  [ITransportTypeCatalog.cs](../../../Pontifex.Factory/Registry/ITransportTypeCatalog.cs),
  [MetadataDtos.cs](../../../Pontifex.Factory/Registry/MetadataDtos.cs),
  [FactoryError.cs](../../../Pontifex.Factory/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.Factory/FactoryExceptions.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `RegisterType_WithValidName_ResolvesCaseInsensitively` | `parsing.md` §6.6 | Every ASCII case variant resolves to the same catalog entry and the caller-supplied canonical spelling is preserved. | Lookup is ASCII case-insensitive only. |
| `RegisterType_WithMultipleTypes_PreservesRegistrationOrderInTypesAndAllTypes` | `parsing.md` §6.6 | `Types` and `AllTypes` preserve registration order. | “Canonical order” is not otherwise defined by the contract; registration order is an implementation interpretation. See `nonapproved-behaviour.md`. |
| `TryResolve_WithUnknownName_ReturnsFalseAndNoEntry` | `ITransportTypeCatalog` contract | Missing name lookup is total and returns `false` with no entry. | — |
| `TryResolve_WithNullName_ReturnsFalseAndNoEntry` | `ITransportTypeCatalog` contract | A null name is treated as an absent name rather than an exception. | Defensive behavior outside the non-nullable annotation. See `nonapproved-behaviour.md`. |
| `TryResolve_WithDifferentCaseAndDifferentSpelling_ReturnsCatalogSpelling` | `parsing.md` §6.6 | Hyphen, underscore, and digit identifier characters work and lookup returns the stored spelling. | — |
| `TryGetCanonicalName_ForKnownType_ReturnsCatalogSpelling` | `parsing.md` §6.6 | Type-to-name lookup returns the exact printer spelling. | — |
| `TryGetCanonicalName_ForUnknownType_ReturnsFalseAndNull` | `parsing.md` §6.6 | Missing type lookup returns `false` and no name. | This is the total lookup used by registration and semantics. |
| `GetCanonicalName_ForKnownType_ReturnsCatalogSpelling` | `parsing.md` §6.6 | The non-total accessor returns the canonical spelling for a catalog type. | — |
| `GetCanonicalName_ForUnknownType_ThrowsInvalidOperationException` | `parsing.md` §6.6; `ITransportTypeCatalog` contract | The non-total accessor is not required to tolerate absent types and fails loudly. | The contract says it MUST only be called for catalog types; the exception type is an implementation choice. See `nonapproved-behaviour.md`. |
| `RegisterType_WithDuplicateNameDifferentCase_ThrowsDuplicateTypeName` | §6.6, §6.7 rule 0, §13.4 | Names are unique case-insensitively and the error message uses the canonical lowercase name. | Exact message asserted. |
| `RegisterType_WithInvalidName_ThrowsInvalidTypeName` | §6.6, §6.7 rule 0, §13.4 | Non-identifiers are rejected before uniqueness and the message preserves the supplied spelling. | Parameterized: `1Type`, `Type A`, `Type.A`, empty, `_Type`. |
| `RegisterType_WithIdentifierVariants_AcceptsThem` | `grammar.md` §3; `parsing.md` §6.6 | The identifier shape `[A-Za-z][A-Za-z0-9_-]*` is accepted for catalog names. | Parameterized: `Type_1`, `Type-1`, `t1`, `T`. |
| `RegisterType_AfterBuild_ThrowsBuilderSealed` | `parsing.md` §6.7 rule 12, §13.4; implementation-plan §8.2 | A built registry cannot gain catalog entries. | Lifecycle guard `E_REG_BUILDER_SEALED`; no offset. |
| `RegisterType_AfterBuild_WithInvalidName_ThrowsBuilderSealed` | `parsing.md` §6.7 rule 12, §13.4 | The lifecycle guard is evaluated before catalog name validation. | Rule 12 is evaluated before rule 0. See `nonapproved-behaviour.md` for the combined-order case. |
| `RegisterTransport_NormalizesCanonicalNameAndParameterNames` | §6.1, §6.7 rules 1-2 | Canonical and parameter names are stored lowercase regardless of caller spelling. | Registry exposes only normalized metadata. |
| `RegisterTransport_NormalizesAliasNames` | §6.7 rules 1, 8 | Aliases are normalized to lowercase and uppercase lookups do not match. | Both lookup methods require an already-normalized scheme. |
| `RegisterTransport_NormalizesStructFieldNamesRecursively` | §6.5, §6.7 rules 1, 7 | Nested struct-field names are normalized in the stored schema. | Field order is preserved. |
| `RegisterTransport_NormalizesTypeSetAndRemovesDuplicates` | §6.3, §6.7 rule 5 | `OneOf` duplicates are removed while the kind and first-encounter order are preserved. | — |
| `RegisterTransport_NormalizesSuggestedAliasesButDoesNotRegisterThem` | §6.7 rule 10 | `SuggestedAliases` are hints and never appear in alias lookup. | The metadata normalization of suggested aliases is not explicitly stated by `parsing.md`; see `nonapproved-behaviour.md`. |
| `RegisterTransport_WithInvalidCanonicalName_ThrowsInvalidCanonicalName` | §6.7 rule 2, §13.4 | Illegal canonical names fail before every other registration rule. | Parameterized: `1alpha`, `alpha beta`, empty. |
| `RegisterTransport_WithDuplicateCanonicalNameDifferentCase_ThrowsDuplicateCanonicalName` | §6.7 rule 3, §13.4 | Canonical names are unique case-insensitively and the message is canonical lowercase. | — |
| `RegisterTransport_WithInvalidParameterName_ThrowsInvalidParameterName` | §6.7 rule 2, §13.4 | An invalid parameter identifier is rejected. | — |
| `RegisterTransport_WithTwoInvalidParameterNames_ReportsFirstInDeclarationOrder` | §6.7 rule 2, §13.4 | Parameter identifiers are validated in declaration order. | First-failure order asserted. |
| `RegisterTransport_WithDuplicateParameterNamesDifferentCase_ThrowsDuplicateParameterName` | §6.7 rule 4, §13.4 | Duplicate parameter names are case-insensitive and the message names the scheme. | — |
| `RegisterTransport_WithEmptyOneOfTypeSet_ThrowsEmptyTypeSet` | §6.3, §6.7 rule 5, §13.4 | An empty `OneOf` is rejected. | Exact message asserted. |
| `RegisterTransport_WithEmptyExactlyTypeSet_ThrowsEmptyTypeSet` | §6.3, §6.7 rule 5, §13.4 | A degenerate `Exactly` with no type is also rejected as an empty set. | Defensive extension; not a documented `OneOf` case. See `nonapproved-behaviour.md`. |
| `RegisterTransport_WithNegativeMinimum_ThrowsInvalidListRangeWithUnboundedMaximum` | §6.7 rule 6, §13.4 | Negative `Min` is rejected and an absent `Max` renders as `unbounded`. | Exact message asserted. |
| `RegisterTransport_WithMaximumBelowMinimum_ThrowsInvalidListRange` | §6.7 rule 6, §13.4 | `Max < Min` is rejected with both numeric bounds. | Exact message asserted. |
| `RegisterTransport_WithZeroMinimumAndUnboundedMaximum_Registers` | §6.7 rule 6 | `Min = 0` and `Max = null` are valid and preserved. | — |
| `RegisterTransport_WithInvalidStructFieldName_ThrowsInvalidStructFieldName` | §6.5, §6.7 rule 7, §13.4 | Illegal struct-field names are rejected and the message preserves caller spelling. | Parameterized: `1name`, `na me`, empty. |
| `RegisterTransport_WithInvalidNestedStructFieldName_ThrowsInvalidStructFieldName` | §6.7 rule 7 | Struct validation recurses into nested schemas. | — |
| `RegisterTransport_WithTwoInvalidStructFieldNames_ReportsFirstInSchemaOrder` | §6.7 rule 7, §13.4 | Struct field names are validated in schema order. | First-failure order asserted. |
| `RegisterTransport_WithDuplicateStructFieldsDifferentCase_ThrowsDuplicateStructField` | §6.5, §6.7 rule 7, §13.4 | Field names are unique case-insensitively within one schema; message is lowercase. | — |
| `RegisterTransport_WithSharedStructSchema_NormalizesItOnceAndSharesTheInstance` | §6.7 rule 7 | Distinct shared schema instances are normalized and the result is shared by both parameters. | Reference identity of the normalized schemas is an implementation detail. See `nonapproved-behaviour.md`. |
| `RegisterTransport_WithCyclicStructSchema_CompletesAndNormalizesTheCycle` | §6.7 rule 7 | Cyclic schema references are permitted and normalized. | The same normalized instance is retained and the cycle is built through an array-backed `StructSchema`; see `nonapproved-behaviour.md`. |
| `RegisterTransport_WithAliasEqualToOwnCanonicalName_IgnoresTheAlias` | §6.7 rule 8 | A self-alias never becomes an alias mapping. | Lookup returns only the canonical constructor. |
| `RegisterTransport_WithDuplicateAliasesInOneRegistration_RegistersThemOnce` | §6.7 rule 8 | Duplicate aliases in one registration are ignored after normalization. | Case variants are covered. |
| `RegisterTransport_WithSameAliasForDifferentTypes_AllowsBothAliases` | §6.7 rule 9 | One alias may map to multiple constructors when their types differ. | Candidates are sorted by scheme name. |
| `RegisterTransport_WithSameAliasForSameType_ThrowsDuplicateAliasForType` | §6.7 rule 9, §13.4 | The same alias cannot name two constructors of the same type; message uses the catalog type name. | Exact message asserted. |
| `RegisterTransport_WithAliasEqualToAnotherCanonicalScheme_AllowsIt` | §6.6, §6.7 rule 9 | An alias may reuse another scheme's canonical name; canonical lookup still wins first. | `FindCandidates` returns `beta` then alias `alpha`. |
| `RegisterTransport_WithAliasTypeEqualToCanonicalType_AllowsCanonicalOverlap` | §6.7 rule 9; §7.3 | An alias may target the same type as the scheme's canonical constructor because the canonical constructor is not an alias mapping. | The registry exposes both candidates; semantics owns canonical preference. |
| `RegisterTransport_WithConstructorTypeAbsentFromCatalog_ThrowsTypeNotInCatalog` | §6.6, §6.7 rule 11, §13.4 | Constructor types must be in the catalog; absent types render with `ToString()`. | Exact message asserted. |
| `RegisterTransport_WithTransportExpectedTypeAbsentFromCatalog_ThrowsTypeNotInCatalog` | §6.6, §6.7 rule 11, §13.4 | Transport parameter expected sets are checked for catalog membership. | — |
| `RegisterTransport_WithListExpectedTypeAbsentFromCatalog_ThrowsTypeNotInCatalog` | §6.6, §6.7 rule 11, §13.4 | Transport-list expected sets are checked member by member. | — |
| `RegisterTransport_WithAnyExpectedSet_DoesNotRequireCatalogMembership` | §6.3, §6.6 | `Any` has no members and therefore adds no catalog requirement. | — |
| `RegisterTransport_WithInvalidCanonicalNameAmongOtherErrors_ReportsCanonicalNameFirst` | §6.7 rule 2 | Within rule 2 the canonical name is checked before parameters and aliases. | First-failure order asserted. |
| `RegisterTransport_WithInvalidParameterAndInvalidAlias_ReportsParameterFirst` | §6.7 rule 2 | Parameter names are checked in declaration order before alias names. | — |
| `RegisterTransport_WithInvalidAliasAndDuplicateCanonical_ReportsAliasFirst` | §6.7 rules 2-3 | All rule-2 name checks, including aliases, precede the duplicate-canonical check. | — |
| `RegisterTransport_WithTwoInvalidAliases_ReportsFirstInInputOrder` | §6.7 rule 2, §13.4 | Alias names are validated in input order. | First-failure order asserted. |
| `RegisterTransport_WithDuplicateCanonicalAndDuplicateParameters_ReportsDuplicateCanonicalFirst` | §6.7 rules 3-4 | Duplicate canonical names are reported before duplicate parameters. | — |
| `RegisterTransport_WithDuplicateParameterAndEmptyTypeSet_ReportsDuplicateParameterFirst` | §6.7 rules 4-5 | Duplicate parameters are reported before an empty type set. | — |
| `RegisterTransport_WithEmptyTypeSetAndInvalidListRange_ReportsEmptyTypeSetFirst` | §6.7 rules 5-6 | Type-set normalization is reported before list-range validation. | — |
| `RegisterTransport_WithInvalidListRangeAndInvalidStructField_ReportsListRangeFirst` | §6.7 rules 6-7 | List ranges are validated across all parameters before struct schemas. | Cross-parameter pass order. |
| `RegisterTransport_WithInvalidStructFieldAndDuplicateAliasForType_ReportsStructFieldFirst` | §6.7 rules 7, 9 | Struct validation precedes alias-to-type checks. | — |
| `RegisterTransport_WithDuplicateAliasAndExpectedTypeAbsent_ReportsDuplicateAliasFirst` | §6.7 rules 9, 11 | Alias-to-type checks precede catalog membership. | — |
| `RegisterTransport_WithConstructorAndExpectedTypesAbsent_ReportsConstructorTypeFirst` | §6.7 rule 11 | The constructor type is checked before expected-set members. | The internal order within rule 11 is not specified by the contract. See `nonapproved-behaviour.md`. |
| `RegisterTransport_AfterBuild_ThrowsBuilderSealed` | `parsing.md` §6.7 rule 12, §13.4; implementation-plan §8.2 | A built registry cannot gain transports. | Lifecycle guard `E_REG_BUILDER_SEALED`; no offset. |
| `Build_WithNoRegistrations_ReturnsEmptyRegistry` | `IRegistry` contract | An empty build produces empty collections and no candidates. | — |
| `Build_ReturnsConstructorsInRegistrationOrder` | implementation-plan §8.2 | `Constructors` is the ordered constructor list, not a map order. | — |
| `FindCanonical_WithNormalizedScheme_ReturnsTheConstructor` | §6.8 | Canonical lookup returns the constructor whose normalized name equals the scheme. | — |
| `FindCanonical_WithUppercaseScheme_ReturnsNullBecauseLookupRequiresNormalization` | §6.8 | Lookup does not normalize its argument. | The caller must normalize. |
| `FindCanonical_WithUnknownScheme_ReturnsNull` | §6.8 | Unknown canonical lookup returns `null`. | — |
| `FindCandidates_ForSchemeWithCanonicalAndAliases_ReturnsCanonicalFirstThenAliasesSortedByName` | §6.8; implementation-plan §8.4 | Candidate order is canonical first, then aliases sorted by canonical scheme name. | Registration order was deliberately scrambled. |
| `FindCandidates_ForCanonicalSchemeThatIsAlsoAnAlias_ReturnsCanonicalFirst` | §6.8; §7.3 | Canonical membership in the candidate list is independent of alias membership and comes first. | — |
| `FindCandidates_ForAliasOnlyScheme_ReturnsAliasesSortedByName` | §6.8; implementation-plan §8.4 | An alias-only scheme yields sorted alias candidates. | — |
| `FindCandidates_DoesNotFilterByTypeOrApplyCanonicalPreference` | §6.8; §7.3 | The registry returns all candidates; expected-type filtering and preference belong to semantics. | Alias candidates have distinct types. |
| `FindCandidates_WithUppercaseScheme_ReturnsEmptyBecauseLookupRequiresNormalization` | §6.8 | Alias lookup also requires a normalized scheme. | — |
| `FindCandidates_WithUnknownScheme_ReturnsEmpty` | §6.8 | Unknown and alias-free schemes have no candidates. | — |
| `AllSchemeNames_IsSortedUnionOfCanonicalAndAliasNames` | §6.8 | The union of canonical and alias names is sorted ordinally and contains each name once. | Expected list asserted exactly. |
| `AllSchemeNames_ContainsAnAliasThatDuplicatesAnotherSchemeNameOnlyOnce` | §6.8 | A name that is both canonical and an alias of another scheme appears once. | — |
| `Types_ExposesTheCatalog` | `IRegistry` contract | The built registry exposes the same type catalog used for registration checks. | — |
| `Constructors_ExposeOnlyNormalizedMetadata` | §6.1; implementation-plan §8.2 | Registry metadata never reveals caller spelling for names and parameter names. | Suggested-alias normalization is asserted separately. |
| `Build_Repeated_ReturnsTheSameImmutableRegistry` | implementation-plan §8.2 | The current implementation returns the same immutable snapshot on repeated calls. | Reference identity is not required by the contract. See `nonapproved-behaviour.md`. |
| `Build_ExposesReadOnlyCollectionViews` | implementation-plan §8.2; acceptance criteria | The tested collection views reject mutation. | Concrete collection shapes and `NotSupportedException` are implementation details; see `nonapproved-behaviour.md`. |
| `Build_CopiesAliases_SoLaterSourceMutationDoesNotAffectTheRegistry` | implementation-plan §8.2; acceptance criteria | The registry does not retain caller-owned mutable arrays. | Source array mutated after registration. |

## Registration error catalogue

Every `E_REG_*` code from `parsing.md` §13.4 is asserted with its exact message,
phase `Registration`, and no span.

| Code | Test |
|---|---|
| `E_REG_INVALID_CANONICAL_NAME` | `RegisterTransport_WithInvalidCanonicalName_ThrowsInvalidCanonicalName` |
| `E_REG_DUPLICATE_CANONICAL_NAME` | `RegisterTransport_WithDuplicateCanonicalNameDifferentCase_ThrowsDuplicateCanonicalName` |
| `E_REG_INVALID_PARAM_NAME` | `RegisterTransport_WithInvalidParameterName_ThrowsInvalidParameterName` |
| `E_REG_DUPLICATE_PARAM_NAME` | `RegisterTransport_WithDuplicateParameterNamesDifferentCase_ThrowsDuplicateParameterName` |
| `E_REG_INVALID_ALIAS_NAME` | `RegisterTransport_WithTwoInvalidAliases_ReportsFirstInInputOrder` |
| `E_REG_DUPLICATE_ALIAS_FOR_TYPE` | `RegisterTransport_WithSameAliasForSameType_ThrowsDuplicateAliasForType` |
| `E_REG_EMPTY_TYPE_SET` | `RegisterTransport_WithEmptyOneOfTypeSet_ThrowsEmptyTypeSet` |
| `E_REG_INVALID_LIST_RANGE` | `RegisterTransport_WithNegativeMinimum_ThrowsInvalidListRangeWithUnboundedMaximum`, `RegisterTransport_WithMaximumBelowMinimum_ThrowsInvalidListRange` |
| `E_REG_DUPLICATE_STRUCT_FIELD` | `RegisterTransport_WithDuplicateStructFieldsDifferentCase_ThrowsDuplicateStructField` |
| `E_REG_INVALID_STRUCT_FIELD_NAME` | `RegisterTransport_WithInvalidStructFieldName_ThrowsInvalidStructFieldName` |
| `E_REG_INVALID_TYPE_NAME` | `RegisterType_WithInvalidName_ThrowsInvalidTypeName` |
| `E_REG_DUPLICATE_TYPE_NAME` | `RegisterType_WithDuplicateNameDifferentCase_ThrowsDuplicateTypeName` |
| `E_REG_TYPE_NOT_IN_CATALOG` | `RegisterTransport_WithConstructorTypeAbsentFromCatalog_ThrowsTypeNotInCatalog`, `RegisterTransport_WithTransportExpectedTypeAbsentFromCatalog_ThrowsTypeNotInCatalog` |
| `E_REG_BUILDER_SEALED` | `RegisterType_AfterBuild_ThrowsBuilderSealed`, `RegisterType_AfterBuild_WithInvalidName_ThrowsBuilderSealed`, `RegisterTransport_AfterBuild_ThrowsBuilderSealed` |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| None currently identified. | Registry layer | New tests are added with the contract change that requires them. |

## Deferred by design

| Gap | Reason |
|---|---|
| Two catalog entries with the same `TransportType` value and different names. | `parsing.md` §6.6 requires unique names, not unique type values; the behavior for repeated values is unspecified, so the suite does not pin it. The implementation keeps the first canonical name for type-to-name lookup. |
| Unsupported custom `Param` or `StructField` subclasses. | The public DTO contract defines `StringParam`, `TransportParam`, `TransportListParam`, `StructParam` and `StringField`, `NestedStructField` only; a foreign subtype is outside the contract. |
| Invalid `SuggestedAliases` identifiers. | `SuggestedAliases` are hints and are not registered or validated as part of the registration catalogue; only aliases passed to `RegisterTransport` are policy. |
| Concurrency and concurrent reads. | The built registry is an immutable snapshot, but proving thread-safety, throughput, and allocation behavior is non-functional and belongs to a stress category. |
| Culture-sensitive case folding. | `grammar.md` §3 and `parsing.md` §6.6 define ASCII identifiers and ASCII case-insensitive lookup. |
| Host registration policy. | Which schemes, aliases, and types an application registers is configuration, not part of the registry contract. |
| Parsing, resolution, canonicalization, compilation, bootstrap, and transport building. | Separate subsystems and suites. |

## Notes

- Every fixture carries `[Category("Factory.Registry")]`.
- Tests are deterministic and isolated: no time, sleep, sockets, files, threads,
  randomness, or background tasks.
- No mocking library is used. `Harness/RegistryTestData` builds metadata by hand;
  `Harness/RegistryAssertions` centralises exception assertions.
- Internal implementations are reached through
  `InternalsVisibleTo("Pontifex.Factory.Tests")`, not through public production
  API or reflection.
- Every registration failure asserts the derived exception type, code, exact
  message, phase `Registration`, and `Span == null`.
- Parameterized methods count as one row in this table; their `[TestCase]` rows
  cover the invalid-name and identifier-shape variants listed in the
  description.
- Test assumptions that are not part of the normative contract are collected in
  `nonapproved-behaviour.md`.
