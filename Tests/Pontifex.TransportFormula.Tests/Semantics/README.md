# Semantics Test Suite

Contract tests for the Transport Formula resolution subsystem. The suite proves
that resolution implements `parsing.md` §7, §8, §12.2-12.3, §13.3, and the
conformance matrix in §14.1-14.3: constructor selection, argument binding,
parameter and struct validation, cast resolution, semantic error codes, exact
messages, offsets, and resolved-tree invariants.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
internal types that form one subsystem:

- `FormulaResolver` (implements `IResolver`)
- `ConstructorSelector`
- `ArgumentBinder`
- `ParameterValidator`
- `StructValidator`
- `SemanticErrorFactory`
- `SemanticAbort` (internal error channel between the components and the resolver)

Root subject: the resolution subsystem group above.

Internal implementations are tested through the same assembly boundary used by
production code. `InternalsVisibleTo("Pontifex.TransportFormula.Tests")` is part of the
production project; no test-only production seam exists.

## Contract references

Paths below reflect the split layout: formula sources live under
`Pontifex.TransportFormula`, runtime sources under `Pontifex.Factory`.

- [parsing.md](../../../Pontifex.TransportFormula/parsing.md) — resolution entry points and
  expected types (§7.1), casts (§7.2), transport selection (§7.3), binding
  (§7.4), parameter validation (§7.5), expected-type guarantees (§7.6), struct
  resolution (§7.8), determinism order (§7.9), the resolved tree (§8), offset
  rules (§12.2), error precedence (§12.3), semantic errors (§13.3), and the
  conformance matrix (§14.1-14.3).
- Public contract and DTOs:
  [IResolver.cs](../../../Pontifex.TransportFormula/Semantics/IResolver.cs),
  [ResolvedDtos.cs](../../../Pontifex.TransportFormula/Semantics/ResolvedDtos.cs),
  [AstDtos.cs](../../../Pontifex.TransportFormula/Parsing/AstDtos.cs),
  [IRegistry.cs](../../../Pontifex.TransportFormula/Registry/IRegistry.cs),
  [ITransportTypeCatalog.cs](../../../Pontifex.TransportFormula/Registry/ITransportTypeCatalog.cs),
  [MetadataDtos.cs](../../../Pontifex.TransportFormula/Registry/MetadataDtos.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs),
  [SourceSpan.cs](../../../Pontifex.TransportFormula/SourceSpan.cs).
- Internal subjects:
  [FormulaResolver.cs](../../../Pontifex.TransportFormula/Semantics/Internal/FormulaResolver.cs),
  [ConstructorSelector.cs](../../../Pontifex.TransportFormula/Semantics/Internal/ConstructorSelector.cs),
  [ArgumentBinder.cs](../../../Pontifex.TransportFormula/Semantics/Internal/ArgumentBinder.cs),
  [ParameterValidator.cs](../../../Pontifex.TransportFormula/Semantics/Internal/ParameterValidator.cs),
  [StructValidator.cs](../../../Pontifex.TransportFormula/Semantics/Internal/StructValidator.cs),
  [SemanticErrorFactory.cs](../../../Pontifex.TransportFormula/Semantics/Internal/SemanticErrorFactory.cs),
  [SemanticAbort.cs](../../../Pontifex.TransportFormula/Semantics/Internal/SemanticAbort.cs).

## Layers

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Semantics` | Resolver, selector, binder, parameter and struct validation, and semantic error group | `Tests/Pontifex.TransportFormula.Tests/Semantics` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Semantics`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Method_Scenario_ExpectedResult`. Property, lifecycle, and
  precedence tests may omit a separate scenario and use
  `Subject_ExpectedResult` when the subject itself is the scenario, for example
  `Bind_ChecksTooManyPositionalArgumentsBeforeNamedArguments`.
- Namespace = `Pontifex.TransportFormula.Tests.Semantics` for every suite test file.
- Harness types live in `Pontifex.TransportFormula.Tests.Semantics.Harness`.
- Every fixture carries `[Category("TransportFormula.Semantics")]`.

## Directory layout

```
Semantics/
├── README.md
├── Coverage.md
├── Harness/
│   ├── SemanticsAssertions.cs
│   └── SemanticsTestData.cs
└── Tests/
    ├── ArgumentBinderTests.cs
    ├── ConstructorSelectorTests.cs
    ├── FormulaResolverTests.cs
    ├── ParameterValidatorTests.cs
    ├── SemanticErrorTests.cs
    └── StructValidatorTests.cs
```

`Tests/` belongs to the single `Semantics` layer. It contains no groups.

## Groups

There are no groups. All test files live directly under `Tests/`.

## Harness layout and sharing

`Harness/` contains two test-only helpers:

- `SemanticsTestData` — hand-written metadata factories, the standard registry
  fixture from `parsing.md` §14 plus named harness extensions, parsing helpers,
  and resolver construction.
- `SemanticsAssertions` — centralised success and failure assertions. Resolver,
  selector, and binder errors assert the exact `FactoryError` code, message,
  phase `Resolution`, offset, and zero-length span. Parameter- and struct-
  validator helpers currently assert code and message; their offset/span
  coverage comes from the end-to-end semantic error catalogue. The internal
  `SemanticAbort` channel is the documented component-test seam for this suite.

The standard registry mirrors `parsing.md` §14: `alpha`, `beta`, `gamma`, `outer`,
`structy`, the `alpha`-as-alias-of-`beta` contract, and the additional fixture
schemes `shared`, `validated`, `mixed`, `opt`, `bag`, `emptyStruct`, `single`,
`dual`, and `deep`. It adds explicitly named harness extensions for contract
branches the §14 table does not reach:

- `multi` — an alias group over three transport types, used for post-filter
  ambiguity and variant ordering;
- `onlybeta` — a single-candidate alias group;
- `needsC` — an alias-only `shared` group resolved by an expected type;
- `empty` — a scheme with no parameters, for the empty parameter expectation;
- `pair` — two required struct fields, for missing-before-kind ordering;
- `listB` — a transport list with a non-`Any` expected element type;
- `fieldValidated`, `validatedEmpty`, `validatedThrow` — empty-reason and throwing
  scalar validators for parameters and struct fields;
- `numbers` — integer and decimal parameters with narrow ranges;
- `numberStruct` — integer and decimal struct fields, including a nested integer
  field;
- `arrays` — string, integer, and decimal array fields with count ranges;
- `nestedArrays` — an array inside a nested struct field.

There are no harness self-tests. The harness is local to this suite.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `TransportFormula.Semantics`. Every fixture in this suite carries
  it.
- Additional filtering categories: none currently.
- Harness category: `TransportFormula.Semantics.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj --filter "TestCategory=TransportFormula.Semantics"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if the
   suite grows cohesive subgroups.
2. Use `IResolver` for end-to-end behaviour and the internal component contracts
   exposed by `InternalsVisibleTo` for unit-level behaviour.
3. For failures, assert the exact `FactoryError` code, message, phase
   `Resolution`, and offset. Use `SemanticsAssertions` so the zero-length span
   convention is checked consistently.
4. When several inputs can fail at once, assert the first failure required by
   `parsing.md` §7.9 and §12.3.
5. Keep registries deterministic and small; build metadata through
   `SemanticsTestData` rather than sharing state between tests.
6. Record the test in [Coverage.md](Coverage.md).

## Adding a layer

This suite has one layer; adding a layer is not applicable. If the subject
changes shape, see [Tests/RULES.md](../../RULES.md).

## Contract notes

- Resolution starts only after a syntactically valid AST. The suite parses with
  the real `FormulaParser`; it never bypasses the syntax layer except when a test
  builds an `AstValue` through the probe-call helper.
- The top-level expected set is `Any`. A cast resolves its inner transport with
  `Any`; casts never select variants. Aliases and canonical names select
  variants, and canonical preference applies after expected-type filtering.
- An identity cast collapses to the inner transport and keeps the full cast
  expression span. A non-identity cast keeps the inner call span and the cast
  span.
- `Provided` contains exactly the supplied parameters in constructor declaration
  order; no defaults are synthesized.
- Expected-type membership after resolution is guaranteed by `ResolveExpr`; the
  resolver has no user-visible error for it. Component tests assert the positive
  invariant instead of looking for a missing error code.
- List arity is checked before elements, elements are resolved in order, and
  homogeneity is checked after all elements. No backtracking occurs.
- Struct diagnostics use dot-separated paths: known segments are canonical
  lowercase and an unknown leaf keeps its raw input spelling.

## Out of scope

- Lexing, parsing, and AST construction beyond using the real parser as a
  fixture.
- Registry registration, metadata validation, and catalog lookup rules; those
  belong to the `TransportFormula.Registry` suite.
- Canonical printing, canonicalization invariants, and round-trip properties.
- Compilation, the direct transport compiler, and compiler invariant errors;
  those belong to the `Factory.Compilation` suite.
- Bootstrap wiring and `ITransportFactory`.
- Transport building, converter paths, concrete transports, and runtime
  construction.
- `E_CMP_*`, `E_PRN_*`, `E_REG_*`, and `E_LEX_*`/`E_SYN_*` catalogues.
- Host policy for aliases, schemes, and transport types.
- Performance, allocation, concurrency, and adversarial depth. The grammar and
  resolver define no implementation limits; this suite exercises a deterministic
  corpus rather than stress input.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
