# Undocumented Behaviour Pinned by Tests

This document collects test expectations that are **not** stated by the normative
contracts (`grammar.md`, `parsing.md`, `implementation-plan.md`, `Tests/RULES.md`)
or are only stated by suite documentation. It exists so these expectations can be
accepted, rejected, or moved into the product contract later.

This document is descriptive, not normative. Do not use it as a contract
reference until its entries are explicitly promoted.

## 1. Registry

### 1.1 Null catalog lookup

- **Test:** `Registry/Tests/TransportTypeCatalogTests.cs` —
  `TryResolve_WithNullName_ReturnsFalseAndNoEntry`.
- **Pinned behaviour:** `TryResolve(null, ...)` returns `false` and no entry.
- **Contract status:** `parsing.md` §6.6 declares `TryResolve(string name, ...)`
  and describes name lookup; null is outside the non-nullable contract.
- **Recommendation:** either reject null with an exception or document “null is
  treated as absent”.

### 1.2 Missing-type accessor exception

- **Test:** `Registry/Tests/TransportTypeCatalogTests.cs` —
  `GetCanonicalName_ForUnknownType_ThrowsInvalidOperationException`.
- **Pinned behaviour:** `GetCanonicalName` throws `InvalidOperationException` when
  the type is absent from the catalog.
- **Contract status:** `parsing.md` §6.6 says `GetCanonicalName` **MUST only be
  called** for a catalog type. The failure mode is unspecified.
- **Recommendation:** document the exception, or drop the test and rely on
  `TryGetCanonicalName`.

### 1.3 Catalog order is registration order

- **Test:** `Registry/Tests/TransportTypeCatalogTests.cs` —
  `RegisterType_WithMultipleTypes_PreservesRegistrationOrderInTypesAndAllTypes`.
- **Pinned behaviour:** `Types` and `AllTypes` follow registration order.
- **Contract status:** `parsing.md` §6.6 says only “canonical order”; that term is
  not defined.
- **Recommendation:** define canonical order (registration order vs. sorted).

### 1.4 Repeated `Build` returns the same instance

- **Test:** `Registry/Tests/TransportRegistryTests.cs` —
  `Build_Repeated_ReturnsTheSameImmutableRegistry`.
- **Pinned behaviour:** `Build()` returns the same object on every call.
- **Contract status:** `implementation-plan.md` §8.2 only requires an immutable
  registry.
- **Recommendation:** document caching/idempotence, or assert only immutability.

### 1.5 Concrete read-only collection shapes

- **Test:** `Registry/Tests/TransportRegistryTests.cs` —
  `Build_ExposesReadOnlyCollectionViews`.
- **Pinned behaviour:** collection views implement `IList<T>` and throw
  `NotSupportedException` from `Clear`; `StructSchema.Fields` is not an
  `IList<StructField>`.
- **Contract status:** the contract requires immutable metadata after `Build`, not
  specific collection types. A `ReadOnlyCollection<T>` would be valid but would
  fail the schema-field assertion.
- **Recommendation:** assert immutability by behaviour (no mutation through the
  exposed API), not by collection shape.

### 1.6 Empty `Exactly` type set

- **Test:** `Registry/Tests/TransportRegistryBuilderTests.cs` —
  `RegisterTransport_WithEmptyExactlyTypeSet_ThrowsEmptyTypeSet`.
- **Pinned behaviour:** `new TransportTypeSet(Exactly, empty)` is rejected with
  `E_REG_EMPTY_TYPE_SET`.
- **Contract status:** `parsing.md` §6.3 and §6.7 rule 5 define the error only for
  an empty `OneOf`; an empty `Exactly` is a malformed DTO state.
- **Recommendation:** document it as defensive validation, or remove/guard the
  malformed state.

### 1.7 Shared and cyclic schema identity

- **Tests:** `Registry/Tests/TransportRegistryBuilderTests.cs` —
  `RegisterTransport_WithSharedStructSchema_NormalizesItOnceAndSharesTheInstance`,
  `RegisterTransport_WithCyclicStructSchema_CompletesAndNormalizesTheCycle`.
- **Pinned behaviour:** normalization preserves the same `StructSchema` instance
  and the cycle shape. The cyclic fixture is created by mutating the array passed
  to `StructSchema`.
- **Contract status:** `parsing.md` §6.7 rule 7 permits shared/cyclic references
  and a visited set, but does not require output reference identity or reliance on
  an input array not being copied.
- **Recommendation:** document the normalized-schema identity rule, or assert only
  that registration succeeds and field names are normalized.

### 1.8 `SuggestedAliases` are lowercased

- **Test:** `Registry/Tests/TransportRegistryBuilderTests.cs` —
  `RegisterTransport_NormalizesSuggestedAliasesButDoesNotRegisterThem`.
- **Pinned behaviour:** `SuggestedAliases` are stored lowercase.
- **Contract status:** `parsing.md` §6.7 rule 1 says to normalize “aliases”;
  rule 10 only says suggested aliases are not registered automatically.
- **Recommendation:** state explicitly whether the metadata view normalizes
  `SuggestedAliases`.

### 1.9 Constructor type is checked before expected types

- **Test:** `Registry/Tests/TransportRegistryBuilderTests.cs` —
  `RegisterTransport_WithConstructorAndExpectedTypesAbsent_ReportsConstructorTypeFirst`.
- **Pinned behaviour:** within rule 11, the constructor type is reported before an
  absent expected member.
- **Contract status:** `parsing.md` §6.7 rule 11 does not define an internal order.
- **Recommendation:** document the order, or avoid a combined-violation test.

### 1.10 Lifecycle guard before catalog name validation

- **Test:** `Registry/Tests/TransportTypeCatalogTests.cs` —
  `RegisterType_AfterBuild_WithInvalidName_ThrowsBuilderSealed`.
- **Pinned behaviour:** on a sealed builder, `E_REG_BUILDER_SEALED` wins over an
  invalid catalog type name.
- **Contract status:** `parsing.md` §6.7 says rule 12 is evaluated before rule 0,
  so this case is arguably documented. It is recorded here because it combines
  the lifecycle guard with a malformed registration.
- **Recommendation:** keep; consider citing the sentence in §6.7 rule 12.

## 2. Parsing

### 2.1 EOF token text

- **Test:** `Parsing/Tests/LexerTests.cs` —
  `TryLex_ForInputEndingInWhitespace_ReportsEndOfInputAtInputLength`.
- **Pinned behaviour:** `EOF.Text` and `EOF.RawText` are empty.
- **Contract status:** `parsing.md` §3.1 only defines `Start == End == input
  length` for EOF.
- **Recommendation:** document EOF text, or stop asserting it.

### 2.2 Sequential parser reuse

- **Test:** `Parsing/Tests/ParserTests.cs` —
  `Parse_ForDeterministicCorpus_NeverThrowsAndReturnsExactlyOneOutcome`.
- **Pinned behaviour:** one `FormulaParser` instance is reused sequentially for
  the corpus.
- **Contract status:** no document defines parser instance reuse; the suite
  previously described parser instances as single-use.
- **Recommendation:** document sequential reuse, or construct a parser per input.

## 3. Semantics

### 3.1 Internal `SemanticAbort` channel

- **Tests:** `Semantics/Tests/ArgumentBinderTests.cs`,
  `ConstructorSelectorTests.cs`, `ParameterValidatorTests.cs`,
  `StructValidatorTests.cs` via `SemanticsAssertions.CatchSemantic`.
- **Pinned behaviour:** component failures throw `SemanticAbort` and expose the
  `FactoryError` through `.Error`.
- **Contract status:** `parsing.md` and `implementation-plan.md` define
  `FormulaResolveResult`, but not the internal exception channel. It is listed as
  an internal subject in `Semantics/README.md`.
- **Recommendation:** treat `SemanticAbort` as an explicit internal test seam, or
  expose a documented component-result contract.

### 3.2 Cyclic struct schema resolution

- **Test:** `Semantics/Tests/FormulaResolverTests.cs` —
  `Resolve_ForCyclicStructSchema_ResolvesFiniteNestedStruct`.
- **Pinned behaviour:** a finite source against a cyclic optional schema resolves.
- **Contract status:** `parsing.md` §7.8 implies this, but the suite previously
  deferred cyclic resolution with an incorrect rationale. The schema fixture is
  built by mutating the array passed to `StructSchema`.
- **Recommendation:** keep the test; document the mutable-array construction
  assumption or provide a cycle-safe schema factory.

## 4. Canonicalization

### 4.1 Null root

- **Test:** `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` —
  `Validate_WhenRootIsMissing_ReportsUnspecifiedInvariantViolation`.
- **Pinned behaviour:** `new ResolvedFormula(null!)` reports
  `E_PRN_INCONSISTENT_TREE` detail `unspecified invariant violation`.
- **Contract status:** `parsing.md` §13.5 lists the detail but does not define a
  null root as its trigger.
- **Recommendation:** document the trigger or remove the test.

### 4.2 Non-canonical name versus value-kind precedence

- **Tests:** `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` —
  `Validate_WhenSchemeNameIsNotCanonical_ReportsInconsistentTree`,
  `Validate_WhenParameterNameIsNotCanonical_ReportsInconsistentTree`.
- **Pinned behaviour:** the non-canonical name error wins over a simultaneous
  value-kind mismatch.
- **Contract status:** `parsing.md` §10 does not place name checks in the explicit
  validation order; `implementation-plan.md` §10.2 lists per-parameter checks
  before canonical-name checks, which suggests the opposite.
- **Recommendation:** define the precedence or avoid combined violations in these
  tests.

## 5. Cross-cutting documentation inconsistencies

### 5.1 Interface-level entry points

- **Observation:** `Canonicalization`, `Semantics`, and `Registry` tests call the
  concrete internal implementations (`FormulaCanonicalizer`, `FormulaResolver`,
  `TransportRegistryBuilder`) rather than `ICanonicalizer`, `IResolver`, and
  `IRegistryBuilder`.
- **Contract status:** the suite READMEs instruct authors to use the public
  interfaces.
- **Recommendation:** add interface-level tests if the public entry points are to
  be verified; none are implemented yet.

## 6. Bootstrap

### 6.1 Null formula

- **Test:** `Bootstrap/Tests/TransportFactoryTests.cs` —
  `Resolve_WithNullFormula_ReturnsEmptyInputError`.
- **Pinned behaviour:** `ITransportFactory.Resolve(null)` returns
  `E_SYN_EMPTY_INPUT` with phase `Parsing` instead of throwing
  `ArgumentNullException`.
- **Contract status:** `ITransportFactory` declares `string formula`; the
  contract does not define null input. `FormulaLexer.TryLex` maps null to empty
  input, so the behaviour is inherited from the lexer.
- **Recommendation:** document “null is treated as empty input”, or add an
  argument guard at the facade and update the test.

### 6.2 Null compiler

- **Test:** `Bootstrap/Tests/TransportFactoryTests.cs` —
  `Compile_WithNullCompiler_ThrowsArgumentNullException`.
- **Pinned behaviour:** `ITransportFactory.Compile` throws
  `ArgumentNullException` for a null compiler before parsing.
- **Contract status:** `implementation-plan.md` §12 defines the compiler as
  caller-supplied but does not state argument-guard order.
- **Recommendation:** document the guard, or leave it as a conventional
  argument check.

