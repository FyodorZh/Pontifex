# Compilation Layer Coverage

- **Layer:** Compilation (root layer)
- **Subject:** Direct-compilation group: `TransportCompiler`,
  `TransportConstructorRegistry`, and the internal `TransportBuildContext`
  (exercised through `ITransportBuildContext` from `Create`).
- **Directory:** `Tests/Pontifex.Factory.Tests/Compilation`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [implementation-plan.md](../../../Pontifex.TransportFormula/implementation-plan.md)
  §11; [parsing.md](../../../Pontifex.TransportFormula/parsing.md) §6.9, §9,
  §13.6; runtime
  [TransportCompiler.cs](../../../Pontifex.Factory/Compilation/TransportCompiler.cs),
  [TransportBuildContext.cs](../../../Pontifex.Factory/Compilation/Internal/TransportBuildContext.cs),
  [TransportConstructorRegistry.cs](../../../Pontifex.Factory/TransportConstructorRegistry.cs);
  core registration
  [ITransportRegistration.cs](../../../Pontifex/Factory/Registration/ITransportRegistration.cs),
  [ITransportBuildContext.cs](../../../Pontifex/Factory/Registration/ITransportBuildContext.cs).

The compiler treats a `CompilerException` raised by a build-context accessor as a
resolved-tree invariant violation: it is rethrown with phase `Compilation` and is
not relabeled as `E_BLD_CONSTRUCTOR_FAILED`. This keeps
`E_CMP_PARAM_KIND_MISMATCH` and the defensive `E_CMP_INCONSISTENT_TREE` value-kind
path observable through `Compile`. Constructor, converter-factory, and nested
build failures are still wrapped as build errors.

All fixtures use canonical lowercase parameter and field names, so build-context
lookup is exercised against the names the resolver guarantees
(parsing.md §6.9, §9.1). The suite does not pin any defensive normalization of
non-canonical names.

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Register_WithNewKey_ReturnsSameRegistrationFromFind` | implementation-plan §11.2 — registry keyed by `(scheme, type)` | A registered constructor is found by its exact key and returns the same instance. | The runtime registry is public; no bootstrap wiring is needed. |
| `Register_WithUppercaseScheme_IsFoundByLowercase` | implementation-plan §11.2; parsing.md §6.9 adapter normalization | Lookup is ASCII case-insensitive because registration normalizes the scheme. | Identity assertion proves the stored object, not a copy. |
| `Register_WithSameSchemeDifferentType_KeepsBothEntries` | implementation-plan §11.2 — key includes the core transport type | Two variants of one scheme coexist and are addressed independently. | — |
| `Register_WithDuplicateKey_ThrowsArgumentException` | implementation-plan §11.2 — duplicate key rejected | Registering the same `(scheme, type)` twice fails with `ArgumentException`. | The exception's `ParamName` is not asserted; the contract does not define it. |
| `Register_WithDuplicateKeyDifferentCase_ThrowsArgumentException` | implementation-plan §11.2 normalization | Duplicate detection uses the normalized scheme, not raw spelling. | Complements the lookup test: normalization affects every key operation. |
| `Register_WithNullRegistration_ThrowsArgumentNullException` | implementation-plan §11.2 argument guard | A null registration is rejected before any key work. | Guard order is observable only as the exception type. |
| `Register_AfterSeal_ThrowsInvalidOperationException` | implementation-plan §11.2 — `Seal` freezes registration | A sealed registry rejects further registrations. | `Seal` is called directly; bootstrap sealing is a separate suite. |
| `Find_AfterSeal_StillReturnsRegistration` | implementation-plan §11.2 — `Find` remains available | Sealing blocks writes only; lookups keep working. | — |
| `Find_WithUnknownKey_ReturnsNull` | implementation-plan §11.2 — unknown key returns `null` | An unknown scheme and a known scheme with an unknown type both return `null`. | — |
| `Compile_WithNullFormula_ThrowsArgumentNullException` | implementation-plan §11.4; parsing.md §9.3 first guard | A null formula is rejected before dereferencing the root. | The resolved-tree contract declares a non-null formula; the guard is conventional. |
| `Compile_WithNullRoot_ThrowsCompilerExceptionInconsistentTree` | implementation-plan §11.4, §11.5; parsing.md §13.6 | A null root raises `E_CMP_INCONSISTENT_TREE`, phase `Compilation`, with the exact documented message. | The tree is hand-built with `null!` because the resolver never emits it. |
| `Compile_WithUnknownNodeKind_ThrowsCompilerExceptionInconsistentTree` | implementation-plan §11.5; parsing.md §13.6 | An unknown `ResolvedExpr` subtype is rejected with the same invariant error. | The private double models a future resolver node or a corrupted caller tree. |
| `Compile_WithUnknownConstructor_ThrowsBuildExceptionConstructorNotFound` | implementation-plan §11.4; parsing.md §9.3, §13.6 | Missing runtime metadata raises `E_BLD_CONSTRUCTOR_NOT_FOUND`, phase `Building`, exact message. | The runtime registry is empty while the resolved metadata exists; this is the formula-only registration case at compiler level. |
| `Compile_WithUnknownConstructorInMixedCase_ReportsNormalizedScheme` | parsing.md §9.3 — `<scheme>` is canonical lowercase | The failure message reports the normalized scheme. | The registry is empty, so this row pins message normalization only; lookup normalization is covered by the next row. |
| `Compile_WithUppercaseResolvedScheme_FindsNormalizedRegistration` | implementation-plan §11.2 normalization; parsing.md §9.3 | A resolved constructor name in uppercase finds a lowercase runtime registration. | Proves the compiler normalizes the resolved scheme before lookup, not only during registration. |
| `Compile_WithRegisteredConstructor_CallsCreateExactlyOnce` | parsing.md §9.1 — `Create` called exactly once per expression | One resolved transport causes one `Create` call and one produced transport. | Counts are recorded by the registration double. |
| `Compile_WithRegisteredConstructor_ReturnsCreatedTransport` | implementation-plan §11.4 — return the constructor's result | The compiler returns the registration's transport unchanged. | Identity assertion excludes an intermediate wrapper transport. |
| `Compile_PassesRoleLoggerMemoryToCreate` | parsing.md §9.1 — role, logger, memory visible to `Create` | The build context exposes the compiler's role, logger, and memory. | Uses `Scriba.VoidLogger.Instance` and `Actuarius.Memory.MemoryRental.Shared`; nothing logs or rents real buffers. |
| `Compile_WithStringParameter_PassesDecodedValue` | parsing.md §6.9, §9.1 — decoded scalar parameter | `TryGetString` returns the resolved scalar text exactly, without re-escaping. | The tree already contains decoded text: decoding belongs to the lexer, outside this suite. Embedded quotes make re-escaping visible. |
| `Compile_WithAbsentOptionalString_ReturnsFalse` | parsing.md §9.1 — absent optional values return `false` | A missing optional string is distinguishable from an empty string. | The out value itself is not asserted because the contract defines only the boolean. |
| `Compile_WithNestedTransport_ReturnsLazyFactory` | parsing.md §9.1; implementation-plan §11.2 — lazy nested construction | `Create` receives a factory; the nested transport is not created while the parent is built. | Zero inner invocation before the factory call is the observable proof of laziness. |
| `Compile_WithNestedTransportFactory_InvokedTwice_CreatesTwoTransports` | parsing.md §9.1 — each invocation creates a fresh transport | Two factory calls compile the nested subtree twice and yield distinct instances. | Required by reconnect-style transports; counts and identity are both asserted. |
| `Compile_WithNestedTransportContainingCast_CompilesTheCast` | parsing.md §9.3 — casts compile depth-first | A cast inside a lazily compiled nested subtree compiles only when the factory runs. | No converter call and no inner creation occur during the parent's `Create`. |
| `Compile_WithTransportList_PreservesOrder` | parsing.md §9.1 — list order preserved, one factory per element | Three list elements produce factories in source order, each creating the matching transport. | `FakeTransport.Name` identifies order; each factory compiles afresh. |
| `Compile_WithEmptyTransportList_ReturnsEmptyList` | parsing.md §9.1 — present empty list | A present list with zero elements returns `true` and an empty factory list. | Distinguishes `false` (absent) from an empty present value. |
| `Compile_WithAbsentOptionalTransportList_ReturnsFalse` | parsing.md §9.1 — absent optional list returns `false` | A missing optional list is reported as absent. | The returned collection for the absent case is not asserted; the contract defines the boolean. |
| `Compile_WithStruct_ExposesStringAndNestedFields` | parsing.md §6.9, §9.1 — structs expose strings and nested structs | String and nested fields are readable recursively through `ITransportStructValue`. | The schema and resolved struct are hand-built; the compiler trusts resolver shape guarantees. |
| `Compile_WithAbsentOptionalStructField_ReturnsFalse` | parsing.md §9.1 — absent optional field returns `false` | Both an absent string field and an absent nested struct field return `false`. | Schema fields are declared but omitted from the resolved value. |
| `Compile_WithParameterKindMismatch_ThrowsCompilerExceptionParamKindMismatch` | parsing.md §13.6 — `E_CMP_PARAM_KIND_MISMATCH` | A scalar accessor over a transport value raises the compiler error with the exact message and phase. | Requires the compiler to rethrow `CompilerException` from `Create`; wrapping it as a build error would hide a compiler invariant. The tree is deliberately inconsistent. |
| `Compile_WithUnknownValueKind_ThrowsCompilerExceptionInconsistentTree` | implementation-plan §11.5; parsing.md §13.6 | An unknown `ResolvedValue` subtype reaches the defensive branch and raises `E_CMP_INCONSISTENT_TREE`. | A private value double models a corrupt tree; the exact unspecified-violation message is asserted. |
| `Compile_WhenConstructorThrows_WrapsAsBuildExceptionConstructorFailed` | implementation-plan §11.4; parsing.md §9.3, §13.6 | A constructor exception becomes `E_BLD_CONSTRUCTOR_FAILED`, phase `Building`, with the original exception as `InnerException`. | Identity assertion proves the exception is preserved, not copied or flattened. |
| `Compile_WhenNestedConstructorThrows_WrapsParentConstructorFailedWithNestedInner` | parsing.md §9.3 — nested build error wrapped by the parent boundary | The outer failure names the parent scheme; its inner exception is the nested `BuildException`, whose inner exception is the original marker. | Two observable levels prove the compiler does not flatten nested failures. |
| `Compile_WhenConverterThrows_WrapsAsBuildExceptionConstructorFailedForInnerScheme` | parsing.md §9.3, §13.6 — converter failure reported for the cast's inner scheme | A throwing conversion factory is wrapped as `E_BLD_CONSTRUCTOR_FAILED` named after the inner constructor. | The inner transport is compiled first; failure identity is preserved. |
| `Compile_WithNoConversionPath_ThrowsBuildExceptionNoConversionPath` | parsing.md §9.3, §13.6 | A `null` conversion factory raises `E_BLD_NO_CONVERSION_PATH` with core type names in the exact message. | The inner transport is still created before the path check; the message uses `TransportType.ToString()`. |
| `Compile_WithIdentityCast_ReturnsInnerWithoutCallingConverter` | parsing.md §9.4 — identity cast defense in depth | An identity cast returns the already compiled inner transport and never queries the converter graph. | The resolver collapses identity casts; this hand-built node covers the compiler's defensive branch. |
| `Compile_WithChangingCast_CallsConverterWithExpectedArguments` | parsing.md §9.3 — converter receives from, to, inner factory, logger, memory | The graph receives the compiled inner type, the target, a factory returning the same compiled instance, and the compiler's logger and memory. | Invoking the recorded inner factory returns the existing instance, proving no recompilation. |
| `Compile_WithChangingCast_ReturnsConvertedTransport` | parsing.md §9.3 | The conversion factory's transport is returned unchanged and carries the target type. | Identity assertion also proves no additional wrapping. |
| `Compile_ThroughFormulaPipeline_ProducesTransportFromRegisteredConstructor` | implementation-plan §11.6 integration; parsing.md §6.9 | A formula parsed and resolved through the real pipeline compiles through the runtime registry and constructor. | Uses internal `FormulaParser`, `FormulaResolver`, and `TransportRegistryBuilder` via `InternalsVisibleTo`. Uppercase source and metadata prove normalization in the formula registry, resolved metadata, runtime registry, and compiler. |

## Planned

- **Bootstrap integration.** `RegisterTransport(ITransportRegistration)`,
  `CreateCompiler` role binding, sealing after `Build`, and formula-only
  registration reporting `E_BLD_CONSTRUCTOR_NOT_FOUND` through the facade are
  owned by the Bootstrap suite, as implementation-plan §11.6 allows
  ("either this suite or the Bootstrap suite but not both"). Scheduled with
  Milestone 6.

## Deferred by design

- **Optional invariant checks not performed by the compiler.** Registration
  `Type` disagreeing with resolved metadata and the created transport's `Type`
  disagreeing with the registration type are optional (§11.5) and are not
  implemented; there is no observable path to test.
- **`E_CMP_DUPLICATE_PARAM` and `E_CMP_UNKNOWN_PARAM`.** The compiler has no path
  that produces these codes; the build context resolves by canonical name and
  does not enumerate duplicates. No test can reach them without inventing a
  second contract.
- **`IConvertersGraph.TryConvert` itself throwing.** The documented failure
  boundary covers the conversion factory, which is covered. The interface does
  not define a throwing `TryConvert`; the production compiler calls it outside
  the wrap. This is deliberately not pinned.
- **`Find(null)` and `Register` guard ordering.** The null-scheme guard and the
  precedence between the null-registration guard and the sealed guard are
  conventional argument checks not stated by the contract.
- **Core registration DTO validation.** `TransportParameter`, `TransportTypeSet`,
  and `StructSchema` constructor validation is core registration behaviour.
  implementation-plan §11.6 makes a separate suite for those DTOs optional and
  outside this test project; this layer covers only the compiler's use of them.
- **Concurrency and thread safety.** The compiler and registry declare no
  concurrency guarantee, and the default suite run is deterministic and
  single-threaded.
