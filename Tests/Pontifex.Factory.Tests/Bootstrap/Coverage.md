# Bootstrap Layer Coverage

- **Layer:** Bootstrap (root layer)
- **Subject:** Composition root and public facade group:
  `TransportFactoryBootstrap` (public seam), `TransportFactoryBuilder`,
  `TransportFactory`, `FactoryComponentFactory`, and `CoreRegistrationAdapter`
  (through `RegisterTransport`/`Build`).
- **Directory:** `Tests/Pontifex.Factory.Tests/Bootstrap`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [implementation-plan.md](../../../Pontifex.TransportFormula/implementation-plan.md)
  §5, §12, §16; [parsing.md](../../../Pontifex.TransportFormula/parsing.md) §12–13;
  [ITransportFactory.cs](../../../Pontifex.Factory/ITransportFactory.cs),
  [ITransportFactoryBuilder.cs](../../../Pontifex.Factory/ITransportFactoryBuilder.cs),
  [TransportFactoryBootstrap.cs](../../../Pontifex.Factory/TransportFactoryBootstrap.cs),
  [ITransportRegistration.cs](../../../Pontifex/Factory/Registration/ITransportRegistration.cs),
  [ITransportCompiler.cs](../../../Pontifex.Factory/Compilation/ITransportCompiler.cs),
  [TransportConstructorRegistry.cs](../../../Pontifex.Factory/TransportConstructorRegistry.cs),
  [ConvertersGraph.cs](../../../Pontifex/Converters/ConvertersGraph.cs),
  [FactoryExceptions.cs](../../../Pontifex.TransportFormula/FactoryExceptions.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `CreateBuilder_FromPublicSeam_ReturnsBuilderWithEmptyRegistry` | implementation-plan §12.1 | The static seam returns a non-null builder and registry, and an empty registry is valid for `Build`. | This is the first consumer-accessible entry point; all other constructors are internal. The optional converter argument defaults to `ConvertersGraph.Default`. |
| `CreateBuilder_WithInjectedConverterGraph_ReturnsBuilderThatBuilds` | implementation-plan §12.1, §12.4 | An explicitly supplied converter graph is accepted by the seam and the builder still wires a factory. | Converter use is proven separately by the facade cast tests. |
| `CreateBuilder_CalledTwice_ReturnsIndependentBuilders` | implementation-plan §12.5 “no static registry or global state” | Two builders do not share registrations. | The first builder is built before the second is queried, so a hidden shared registry would surface as a successful resolve. |
| `Build_WithRegisteredTransports_ReturnsWorkingFactory` | implementation-plan §12.2, §12.5 | Formula-only registered metadata is carried into a factory that resolves a formula. | Proves `Registry.RegisterTransport` remains the metadata-only path. |
| `Build_CalledTwice_ReturnsTheSameFactory` | implementation-plan §12.2 (idempotent build) | Repeated `Build` returns the same `ITransportFactory` instance. | Matches `TransportRegistryBuilder.Build`, which is also idempotent. |
| `Build_SealsTheRegistry_SoLaterRegistrationThrows` | `parsing.md` §6.7 rule 12; implementation-plan §12.2 | Building freezes the formula registry; later registration throws `RegistrationException` with `E_REG_BUILDER_SEALED` and phase `Registration`. | The sealed check wins over the duplicate/invalid type name in this fixture. |
| `Build_WithoutRegistrations_ReturnsFactoryThatReportsUnknownScheme` | implementation-plan §12.2 | An empty pipeline is usable and reports `E_SEM_UNKNOWN_SCHEME` with phase `Resolution`. | Proves the factory is wired even with no constructors. |
| `RegisterTransport_WithCoreRegistration_RegistersFormulaMetadataAndRuntimeConstructor` | implementation-plan §3.2 “one registration source”, §12.2 | One core registration produces resolvable formula metadata and a runtime constructor that `Compile` can reach. | The transport instance returned by `Compile` is the exact instance the core registration created. |
| `RegisterTransport_WithCoreRegistration_NormalizesTheScheme` | implementation-plan §12.2 step 2 | A mixed-case core scheme becomes the canonical lowercase scheme in resolution and printing. | Normalization belongs to the adapter/formula registry, not the runtime registry. |
| `RegisterTransport_WithCoreRegistration_PreservesAliases` | implementation-plan §12.2, §12.4 | Core aliases remain resolvable and print as the canonical scheme. | Alias order and deduplication are formula-registry rules; this test covers carriage through the adapter. |
| `RegisterTransport_WithCoreRegistration_MapsStringParameterMetadata` | implementation-plan §12.2 (adapter mapping), §12.4 | A required core string parameter becomes a required formula parameter: omission is a resolution error and supply succeeds. | Parameter names are normalized by the adapter and then by the formula registry. |
| `RegisterTransport_WithCoreRegistration_MapsIntegerAndDecimalParameterMetadata` | implementation-plan §12.2 (adapter mapping), §12.4 | Core integer and decimal parameters become typed formula parameters and resolve to `IntegerValue`/`DecimalValue`. | No range pre-validation occurs in the core DTO. |
| `RegisterTransport_WithCoreRegistration_MapsIntegerAndDecimalFieldMetadata` | implementation-plan §12.2 (adapter mapping), §12.4 | Core integer and decimal struct fields become typed formula fields and resolve to typed values. | — |
| `RegisterTransport_WithUnsortedCoreIntegerRange_ThrowsInvalidIntegerRange` | parsing.md §6.2, §6.7 rule 5, §13.4 | Unsorted core integer bounds reach `E_REG_INVALID_INTEGER_RANGE` through the adapter. | Proves the core constructor does not pre-validate. |
| `RegisterTransport_WithUnsortedCoreDecimalFieldRange_ThrowsInvalidDecimalRange` | parsing.md §6.5, §6.7 rule 7, §13.4 | Unsorted core decimal field bounds reach `E_REG_INVALID_DECIMAL_RANGE` through the adapter. | Proves the core constructor does not pre-validate. |
| `RegisterTransport_WithCoreRegistration_MapsArrayFieldMetadata` | implementation-plan §12.2 (adapter mapping), §12.4 | Core array fields preserve element type, count range, `StringType` validators, and resolve to `ArrayValue`. | — |
| `RegisterTransport_WithCoreRegistration_PreservesArrayStringValidator` | implementation-plan §12.2 (adapter mapping), §12.4 | A core `StringType` validator is forwarded and applied per array element. | — |
| `RegisterTransport_WithUnsortedCoreArrayRange_ThrowsInvalidArrayRange` | parsing.md §6.5, §6.7 rule 7, §13.4 | Unsorted core array bounds reach `E_REG_INVALID_ARRAY_RANGE`. | — |
| `RegisterTransport_WithUnsortedCoreArrayElementRange_ThrowsInvalidIntegerRange` | parsing.md §6.5, §6.7 rule 7, §13.4 | Unsorted core integer array element bounds reach `E_REG_INVALID_INTEGER_RANGE`. | — |
| `RegisterTransport_WithUnsortedCoreDecimalArrayElementRange_ThrowsInvalidDecimalRange` | parsing.md §6.5, §6.7 rule 7, §13.4 | Unsorted core decimal array element bounds reach `E_REG_INVALID_DECIMAL_RANGE`. | — |
| `RegisterTransport_WithNullRegistration_ThrowsArgumentNullException` | implementation-plan §12.2 (void registration is invalid) | A null core registration is rejected before any registry changes. | The guard is the builder's, matching the core registration contract. |
| `RegisterTransport_WithDuplicateCanonicalScheme_ThrowsRegistrationException` | `parsing.md` §6.7 rule 3; implementation-plan §12.2 | Two core registrations with the same normalized scheme surface the formula registry's duplicate error. | The duplicate is rejected before the runtime registry could drift. |
| `Build_SealsTheRuntimeRegistry_SoLaterCoreRegistrationThrows` | implementation-plan §12.2 “seals the runtime registry” | After `Build`, `RegisterTransport` fails with `E_REG_BUILDER_SEALED` and phase `Registration`. | The formula registry rejects first, so the runtime seal is not separately observable through the facade; the Compilation suite covers registry sealing directly. |
| `Resolve_WithValidFormula_ReturnsResolvedTransport` | implementation-plan §12.2 steps 1–4 | `Resolve` parses and resolves, returning a `ResolvedTransport` in the result DTO. | The resolved root type and constructor name are asserted through the public DTOs. |
| `Resolve_WithParseError_ReturnsResultInsteadOfThrowing` | implementation-plan §12.3 | Parse errors are returned as `FormulaResolveResult.Error`, not thrown. | Phase is `Parsing` for `E_SYN_UNEXPECTED_TOKEN`. |
| `Resolve_WithSemanticError_ReturnsResultInsteadOfThrowing` | implementation-plan §12.3 | Resolution errors are returned as `FormulaResolveResult.Error`, not thrown. | Phase is `Resolution`. |
| `Resolve_WithNullFormula_ReturnsEmptyInputError` | implementation-plan §12.3 | A null formula is treated as empty input. | Phase is `Parsing`; the facade result is `E_SYN_EMPTY_INPUT`. |
| `Resolve_WithNullExpectedType_UsesAny` | implementation-plan §3 (top-level `Any`) | A null expected set is treated as `Any`. | — |
| `Resolve_WithExpectedTypeThatExcludesTheCandidate_ReturnsNoVariantError` | `parsing.md` §7.3; implementation-plan §12.2 | The expected set is passed through to resolution unchanged. | `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE`, phase `Resolution`. |
| `Resolve_WithMissingRequiredParameter_ReturnsMissingParameterError` | `parsing.md` §7.5 | Parameter binding and validation run inside `Resolve`. | `E_SEM_MISSING_REQUIRED_PARAM`, phase `Resolution`. |
| `Canonicalize_WithValidFormula_ReturnsCanonicalOutput` | implementation-plan §12.2 | Parse + resolve + print is wired end to end; scheme case is canonicalized. | `ALPHA(1)` prints as `alpha(1)`. |
| `Canonicalize_WithParseError_ThrowsFactoryExceptionWithParsingPhase` | implementation-plan §12.2–12.3 | The throwing entry point converts a parse error into `FactoryException` with the original code and phase. | Exact exception type is the base `FactoryException`, per plan §12.2. |
| `Canonicalize_WithSemanticError_ThrowsFactoryExceptionWithResolutionPhase` | implementation-plan §12.2–12.3 | The throwing entry point converts a resolution error into `FactoryException` with the original code and phase. | — |
| `Compile_WithValidFormula_PassesResolvedTreeToTheCompiler` | implementation-plan §12.2 | `Compile` resolves once and hands the tree to the caller's compiler, returning its output. | The compiler is opaque; bootstrap does not inspect `TOutput`. |
| `Compile_WithParseError_DoesNotInvokeTheCompiler` | implementation-plan §12.2–12.3 | The caller-supplied compiler is not called when parsing fails. | — |
| `Compile_WithSemanticError_DoesNotInvokeTheCompiler` | implementation-plan §12.2–12.3 | The caller-supplied compiler is not called when resolution fails; phase is `Resolution`. | — |
| `Compile_WhenTheCompilerThrows_PropagatesTheExceptionUnchanged` | implementation-plan §12.2 (compiler boundary) | Bootstrap does not relabel or wrap exceptions raised by the caller-supplied compiler. | Proven with a `CompilerException`; the same instance is rethrown and its phase stays `Compilation`. |
| `CreateCompiler_WithServerRole_ReturnsCompilerForThatRole` | implementation-plan §3.2 “per-compiler role”, §12.2 | `CreateCompiler` carries the requested server role into the compiler. | Role is compiler configuration, not formula data. |
| `CreateCompiler_WithClientRole_ReturnsCompilerForThatRole` | implementation-plan §3.2 “per-compiler role”, §12.2 | `CreateCompiler` carries the requested client role into the compiler. | Confirms the role is not fixed by the factory. |
| `CreateCompiler_WithNullLogger_ThrowsArgumentNullException` | implementation-plan §12.2 (build environment) | The facade rejects a null logger before constructing the compiler. | `TransportCompiler` repeats the guard for direct construction. |
| `CreateCompiler_WithNullMemory_ThrowsArgumentNullException` | implementation-plan §12.2 (build environment) | The facade rejects null memory before constructing the compiler. | `TransportCompiler` repeats the guard for direct construction. |
| `Compile_WithCoreRegistration_ReturnsLiveTransportAndPassesStringParameter` | implementation-plan §12.4 “Compile through the facade returns a live transport” | Resolve, created compiler, runtime registry, and constructor context are wired end to end; the decoded string reaches the core constructor under its canonical name. | The transport is the exact fake instance created by the core callback. |
| `Compile_WithFormulaOnlyRegistration_ReportsConstructorNotFound` | implementation-plan §12.1, §12.4 | Metadata-only registration resolves but compilation reports `E_BLD_CONSTRUCTOR_NOT_FOUND` with phase `Building`. | Proves the formula registry and runtime registry are distinct and that formula-only tooling stays usable. |
| `Compile_WithCoreRegistration_MapsNestedListAndStructParameters` | implementation-plan §3.2 “lazy nested construction”, §12.2 (adapter mapping) | Nested transport, transport-list, struct, and nested-struct metadata survive the adapter and reach the build context in declaration order. | Nested factories are invoked lazily by the constructor; the test observes only the resulting names and field values. |
| `Resolve_WithCoreExactlyTypeSet_RejectsDifferentNestedType` | implementation-plan §12.2; parsing.md §7.5, §13.3 | A core `Exactly(TypeA)` nested parameter accepts `TypeA` and rejects a `TypeB` child with `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE`. | Proves the adapter preserves the `Exactly` type-set kind and its single accepted type, not just `Any`. |
| `Resolve_WithCoreOneOfTypeSet_AcceptsEitherNestedType` | implementation-plan §12.2; parsing.md §7.5 | A core `OneOf(TypeA, TypeB)` nested parameter accepts both child types. | Complements the `Exactly` rejection test. |
| `Resolve_WithCoreTransportListRangeAndHomogeneity_EnforcesAll` | implementation-plan §12.2; parsing.md §7.5, §13.3 | Core `Min`, `Max`, and `Homogeneous` values survive the adapter and are enforced by resolution. | Asserts `E_SEM_LIST_MIN`, `E_SEM_LIST_MAX`, and `E_SEM_LIST_HOMOGENEITY` in one fixture. |
| `Compile_WithInjectedConverterGraph_UsesTheGraphForTheCast` | implementation-plan §3.2 “converter policy”, §12.4 | The facade binds the injected converter graph; a changing cast asks it for the conversion and the compiler passes the inner transport factory. | Logger and memory are forwarded to `TryConvert`. |
| `Compile_WithDefaultConverterGraph_UsesTheBuiltInConversion` | implementation-plan §12.1, §12.4 | A builder created without an argument uses `ConvertersGraph.Default`; the built-in RawUnreliableNoAck → RawReliableAck path converts the transport. | Uses a non-I/O raw-unreliable client and a real `Scriba.Logger`, because the built-in wrapper requires `ILoggerExt`. |
| `Compile_WithParseError_DoesNotInvokeTheRuntimeConstructor` | implementation-plan §12.3 | A parse error prevents the created compiler from reaching the runtime constructor. | Complements the caller-supplied compiler tests: neither compiler kind runs on user-input errors. |

## Planned

None for the §12.4 bootstrap scope. Remaining compiler-invariant coverage is
owned by the `Factory.Compilation` suite.

## Deferred by design

- **`PrinterException` through the facade.** `Canonicalize` can only receive a
  tree produced by the resolver, and a correct resolver never produces an
  invalid tree. The printer invariant path is covered by
  `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` instead.
- **Compiler invariant matrix.** `E_CMP_*` and the full `E_BLD_*` matrix belong
  to the `Factory.Compilation` suite and its direct transport compiler. This
  suite proves the documented integration error plus pass-through of a
  caller-raised `CompilerException`.
- **Separate observation of the runtime registry seal.** `RegisterTransport`
  checks the formula registry first, so the facade always reports the formula
  registry's `E_REG_BUILDER_SEALED`. Direct seal behaviour of
  `TransportConstructorRegistry` is covered by the Compilation suite.
- **Formula assembly isolation.** The formula assembly must not reference
  transports, converters, logging, or memory. This is a project-reference and
  API-shape property. `Tests/RULES.md` TEST-01 forbids reflection, so it is
  verified by dependency review, not by a runtime test.
- **Thread safety of `Build`.** The contract defines no concurrency guarantee;
  the default suite run is deterministic and single-threaded.
