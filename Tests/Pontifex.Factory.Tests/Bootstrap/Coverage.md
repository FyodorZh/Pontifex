# Bootstrap Layer Coverage

- **Layer:** Bootstrap (root layer)
- **Subject:** Composition root and public facade group:
  `TransportFactoryBootstrap` (public seam), `TransportFactoryBuilder`,
  `TransportFactory`, and `FactoryComponentFactory` (through `Build`).
- **Directory:** `Tests/Pontifex.Factory.Tests/Bootstrap`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [implementation-plan.md](../../../Pontifex.TransportFormula/implementation-plan.md)
  §5, §12, §16; [parsing.md](../../../Pontifex.TransportFormula/parsing.md) §12–13;
  [ITransportFactory.cs](../../../Pontifex.Factory/ITransportFactory.cs),
  [ITransportFactoryBuilder.cs](../../../Pontifex.Factory/ITransportFactoryBuilder.cs),
  [TransportFactoryBootstrap.cs](../../../Pontifex.Factory/TransportFactoryBootstrap.cs),
  [FactoryExceptions.cs](../../../Pontifex.TransportFormula/FactoryExceptions.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `CreateBuilder_FromPublicSeam_ReturnsBuilderWithEmptyRegistry` | implementation-plan §12.1 | The static seam returns a non-null builder and registry, and an empty registry is valid for `Build`. | This is the first consumer-accessible entry point; all other constructors are internal. |
| `CreateBuilder_CalledTwice_ReturnsIndependentBuilders` | implementation-plan §12.5 “no static registry or global state” | Two builders do not share registrations. | The first builder is built before the second is queried, so a hidden shared registry would surface as a successful resolve. |
| `Build_WithRegisteredTransports_ReturnsWorkingFactory` | implementation-plan §12.2, §12.5 | Registered metadata is carried into a factory that resolves a formula. | — |
| `Build_CalledTwice_ReturnsTheSameFactory` | implementation-plan §12.2 (idempotent build) | Repeated `Build` returns the same `ITransportFactory` instance. | Matches `TransportRegistryBuilder.Build`, which is also idempotent. |
| `Build_SealsTheRegistry_SoLaterRegistrationThrows` | `parsing.md` §6.7 rule 12; implementation-plan §12.2 | Building freezes the registry; later registration throws `RegistrationException` with `E_REG_BUILDER_SEALED` and phase `Registration`. | The sealed check wins over the duplicate/invalid type name in this fixture. |
| `Build_WithoutRegistrations_ReturnsFactoryThatReportsUnknownScheme` | implementation-plan §12.2 | An empty pipeline is usable and reports `E_SEM_UNKNOWN_SCHEME` with phase `Resolution`. | Proves the factory is wired even with no constructors. |
| `Resolve_WithValidFormula_ReturnsResolvedTransport` | implementation-plan §12.2 steps 1–4 | `Resolve` parses and resolves, returning a `ResolvedTransport` in the result DTO. | The resolved root type and constructor name are asserted through the public DTOs. |
| `Resolve_WithParseError_ReturnsResultInsteadOfThrowing` | implementation-plan §12.3 | Parse errors are returned as `FormulaResolveResult.Error`, not thrown. | Phase is `Parsing` for `E_SYN_UNEXPECTED_TOKEN`. |
| `Resolve_WithSemanticError_ReturnsResultInsteadOfThrowing` | implementation-plan §12.3 | Resolution errors are returned as `FormulaResolveResult.Error`, not thrown. | Phase is `Resolution`. |
| `Resolve_WithNullFormula_ReturnsEmptyInputError` | nonapproved-behaviour §6.1 | A null formula is treated as empty input, consistent with the lexer contract. | Assumption is recorded in `nonapproved-behaviour.md`; the normative contract declares a non-null string. |
| `Resolve_WithNullExpectedType_UsesAny` | implementation-plan §3 (top-level `Any`) | A null expected set is treated as `Any`. | — |
| `Resolve_WithExpectedTypeThatExcludesTheCandidate_ReturnsNoVariantError` | `parsing.md` §7.3; implementation-plan §12.2 | The expected set is passed through to resolution unchanged. | `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE`, phase `Resolution`. |
| `Resolve_WithMissingRequiredParameter_ReturnsMissingParameterError` | `parsing.md` §7.5 | Parameter binding and validation run inside `Resolve`. | `E_SEM_MISSING_REQUIRED_PARAM`, phase `Resolution`. |
| `Canonicalize_WithValidFormula_ReturnsCanonicalOutput` | implementation-plan §12.2 | Parse + resolve + print is wired end to end; scheme case is canonicalized. | `ALPHA(1)` prints as `alpha(1)`. |
| `Canonicalize_WithParseError_ThrowsFactoryExceptionWithParsingPhase` | implementation-plan §12.2–12.3 | The throwing entry point converts a parse error into `FactoryException` with the original code and phase. | Exact exception type is the base `FactoryException`, per plan §12.2. |
| `Canonicalize_WithSemanticError_ThrowsFactoryExceptionWithResolutionPhase` | implementation-plan §12.2–12.3 | The throwing entry point converts a resolution error into `FactoryException` with the original code and phase. | — |
| `Compile_WithValidFormula_PassesResolvedTreeToTheCompiler` | implementation-plan §12.2 | `Compile` resolves once and hands the tree to the caller's compiler, returning its output. | The compiler is opaque; bootstrap does not inspect `TOutput`. |
| `Compile_WithParseError_DoesNotInvokeTheCompiler` | implementation-plan §12.2–12.3 | The compiler is not called when parsing fails. | — |
| `Compile_WithSemanticError_DoesNotInvokeTheCompiler` | implementation-plan §12.2–12.3 | The compiler is not called when resolution fails; phase is `Resolution`. | — |
| `Compile_WithNullCompiler_ThrowsArgumentNullException` | nonapproved-behaviour §6.2 | A null compiler is rejected before parsing, by argument guard. | Assumption is recorded in `nonapproved-behaviour.md`. |
| `Compile_WhenTheCompilerThrows_PropagatesTheExceptionUnchanged` | implementation-plan §12.2 (compiler boundary) | Bootstrap does not relabel or wrap exceptions raised by the caller-supplied compiler. | Proven with a `CompilerException`; the same instance is rethrown and its phase stays `Compilation`. |

## Planned

- Core `ITransportRegistration` registration, formula/runtime registry sealing,
  `CreateCompiler`, and end-to-end compile integration, as listed in
  implementation-plan §12.4. Planned cases:
  - `RegisterTransport_WithCoreRegistration_RegistersFormulaMetadataAndRuntimeConstructor`
  - `RegisterTransport_WithCoreRegistration_NormalizesTheScheme`
  - `Build_SealsTheRuntimeConstructorRegistry`
  - `CreateCompiler_WithRole_ReturnsCompilerForThatRole`
  - `Compile_WithCoreRegistration_ReturnsLiveTransport`
  - `Compile_WithFormulaOnlyRegistration_ReportsConstructorNotFound`
  These land with Milestone 6.

## Deferred by design

- **`PrinterException` through the facade.** `Canonicalize` can only receive a
  tree produced by the resolver, and a correct resolver never produces an
  invalid tree. The printer invariant path is covered by
  `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` instead.
- **`E_CMP_*` and `E_BLD_*` compiler invariants.** These belong to the
  `Factory.Compilation` suite and its direct transport compiler. This suite
  proves only that a compiler-raised `CompilerException` passes through
  unchanged.
- **Formula assembly isolation.** The formula assembly must not reference
  transports, converters, logging, or memory. This is a project-reference and
  API-shape property. `Tests/RULES.md` TEST-01 forbids reflection, so it is
  verified by dependency review, not by a runtime test.
- **Thread safety of `Build`.** The contract defines no concurrency guarantee;
  the default suite run is deterministic and single-threaded.
