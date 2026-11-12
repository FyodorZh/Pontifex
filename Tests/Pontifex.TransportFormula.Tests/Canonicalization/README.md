# Canonicalization Test Suite

Contract tests for the Transport Formula canonical printer. The suite proves that
canonicalization implements `parsing.md` §10 and §11: validation of resolved-tree
invariants, canonical scheme and type spelling, the longest-provided positional
prefix, declaration-order named remainder, minimal scalar quoting, schema-ordered
struct printing, canonical cast printing, every `E_PRN_*` failure, and the
round-trip properties `Print(ParseAndResolve(Print(t))) == Print(t)` and
`ParseAndResolve(Print(t)) == t` over a real parser, resolver, and registry.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
internal types that form one subsystem:

- `FormulaCanonicalizer` (implements `ICanonicalizer`)
- `PositionalFormatter`
- `ScalarFormatter`
- `DecimalText`
- `PrinterInvariantValidator`
- `CanonicalizationErrorFactory`

Root subject: the canonicalization subsystem group above.

Internal implementations are tested through the same assembly boundary used by
production code. `InternalsVisibleTo("Pontifex.TransportFormula.Tests")` is part of the
production project; no test-only production seam exists.

## Contract references

Paths below reflect the split layout: formula sources live under
`Pontifex.TransportFormula`, runtime sources under `Pontifex.Factory`.

- [parsing.md](../../../Pontifex.TransportFormula/parsing.md) — canonical printer (§10),
  printer examples (§10.6), round-trip and equality (§11), printer errors
  (§13.5), and the printer invariant matrix (§14.7).
- [implementation-plan.md](../../../Pontifex.TransportFormula/implementation-plan.md) —
  canonicalization milestone, component responsibilities, printing algorithm,
  and test expectations (§10).
- Public contract and DTOs:
  [ICanonicalizer.cs](../../../Pontifex.TransportFormula/Canonicalization/ICanonicalizer.cs),
  [ResolvedDtos.cs](../../../Pontifex.TransportFormula/Semantics/ResolvedDtos.cs),
  [MetadataDtos.cs](../../../Pontifex.TransportFormula/Registry/MetadataDtos.cs),
  [ITransportTypeCatalog.cs](../../../Pontifex.TransportFormula/Registry/ITransportTypeCatalog.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.TransportFormula/FactoryExceptions.cs),
  [SourceSpan.cs](../../../Pontifex.TransportFormula/SourceSpan.cs).
- Internal subjects:
  [FormulaCanonicalizer.cs](../../../Pontifex.TransportFormula/Canonicalization/Internal/FormulaCanonicalizer.cs),
  [PositionalFormatter.cs](../../../Pontifex.TransportFormula/Canonicalization/Internal/PositionalFormatter.cs),
  [ScalarFormatter.cs](../../../Pontifex.TransportFormula/Canonicalization/Internal/ScalarFormatter.cs),
  [DecimalText.cs](../../../Pontifex.TransportFormula/Internal/DecimalText.cs),
  [PrinterInvariantValidator.cs](../../../Pontifex.TransportFormula/Canonicalization/Internal/PrinterInvariantValidator.cs),
  [CanonicalizationErrorFactory.cs](../../../Pontifex.TransportFormula/Canonicalization/Internal/CanonicalizationErrorFactory.cs).

## Layers

This is a Test Suit with exactly one layer at the suite root.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Canonicalization` | Canonicalizer, positional and scalar formatting, invariant validation, and printer error group | `Tests/Pontifex.TransportFormula.Tests/Canonicalization` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Canonicalization`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Method_Scenario_ExpectedResult`. Property, lifecycle, and
  error-catalogue tests may omit a separate scenario and use
  `Subject_ExpectedResult` when the subject itself is the scenario, for example
  `MissingRequiredParam_FormatsTemplate`.
- Namespace = `Pontifex.TransportFormula.Tests.Canonicalization` for every suite test file.
- Harness types live in `Pontifex.TransportFormula.Tests.Canonicalization.Harness`.
- Every fixture carries `[Category("TransportFormula.Canonicalization")]`.

## Directory layout

```
Canonicalization/
├── README.md
├── Coverage.md
├── Harness/
│   ├── CanonicalizationAssertions.cs
│   └── CanonicalizationTestData.cs
└── Tests/
    ├── CanonicalizationErrorFactoryTests.cs
    ├── DecimalTextTests.cs
    ├── FormulaCanonicalizerTests.cs
    ├── PositionalFormatterTests.cs
    ├── PrinterInvariantValidatorTests.cs
    └── ScalarFormatterTests.cs
```

`Tests/` belongs to the single `Canonicalization` layer. It contains no groups.

## Groups

There are no groups. All test files live directly under `Tests/`. If grouping is
added later, groups must be subfolders of `Tests/`; they are organisational
folders, not layers, and they must not contain `Coverage.md` or `Harness/`. The
root layer's `Coverage.md` covers every test in every group.

## Harness layout and sharing

`Harness/` contains two test-only helpers:

- `CanonicalizationTestData` — hand-written metadata factories, hand-written
  resolved-tree factories, the standard registry fixture, parsing and resolution
  helpers, and canonicalizer construction.
- `CanonicalizationAssertions` — centralised success and failure assertions,
  printer-error phase/span checks, raw-newline checks, and the structural
  resolved-tree equality helper from `parsing.md` §11.1.

The standard registry mirrors `parsing.md` §14 (`alpha`, `beta`, `gamma`,
`outer`, `structy`, the `alpha`-as-alias-of-`beta` contract) and adds named
fixture schemes used by the printer tests: `mixed` (named-only parameter declared
before a positional one), `opt` (two optional positionals for prefix gaps), `bag`
(empty and unbounded list), `emptyStruct` (empty struct schema), `single`
(one-element bounded list), `deep` (three-level nested struct), `numeric`
(integer and decimal parameters), `numericStruct` (integer and decimal struct
fields), `wide` (full-range decimal), `numbers` (§14 numeric parameters),
`numberStruct` (§14 numeric struct fields), `arrays` (string, integer, and
decimal array fields), and `nestedArrays` (an array inside a nested struct).

There are no harness self-tests. Harness belongs to the root layer and is
available to every test in this suite. Sibling suites do not share this harness;
the parsing, registry, and semantics suites have their own fixtures, as
`Tests/RULES.md` requires. The repository does not use a mocking library; all
doubles are hand-written. Harness types expose only the surface required by the
tests and never private production state.

If harness self-tests are added later, they live in `Harness/Tests/` and carry
`TransportFormula.Canonicalization.Harness` instead of the suite root category.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `TransportFormula.Canonicalization`. Every fixture in this suite
  carries it.
- Additional filtering categories: none currently.
- Harness category: `TransportFormula.Canonicalization.Harness` is reserved for future
  harness self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj --filter "TestCategory=TransportFormula.Canonicalization"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if the
   suite grows cohesive subgroups.
2. Use `ICanonicalizer` for printing and round-trip behaviour and the internal
   component contracts exposed by `InternalsVisibleTo` for unit-level behaviour;
   do not use reflection or inspect private state.
3. For printer failures, assert the exact `FactoryError` code, message, phase
   `Printing`, and the absence of a span. Use `CanonicalizationAssertions` so the
   phase and span convention is checked consistently.
4. When several invariants are violated at once, assert the first failure
   required by the validation order in `parsing.md` §10.
5. Round-trip tests use the real parser, resolver, registry builder, and type
   catalog; do not reference another suite's harness.
6. Update [Coverage.md](Coverage.md) in the same change.

## Adding a layer

A Test Suit has exactly one layer. If the subject becomes an inheritance tree or
requires genuinely separate layers, split this suite into multiple Test Suits or
convert it to an Abstract Test Suite instead of adding a nested layer here.

## Contract notes

- The canonical printer validates the whole tree before printing. A correct
  resolver never produces a tree that fails validation; the failure tests build
  trees by hand to exercise the invariant catalogue.
- `Provided` is matched to constructor parameters by reference identity, and
  struct field bindings to schema fields by reference identity. Structurally
  equal clones with the same name are unknown bindings.
- The longest provided prefix of positional-capable parameters prints
  positionally; every other provided parameter prints named in declaration
  order. The positional prefix is emitted first even when a named-only parameter
  is declared earlier.
- Struct fields print in schema order, which equals `StructValue.Fields` order
  for a valid tree.
- Scalars are quoted only when empty or when they contain whitespace or a
  delimiter. Only `\` and `"` are escaped. CR, LF, and unpaired surrogates are
  rejected with the first offending UTF-16 code unit as `U+XXXX`.
- Integers print as invariant decimal text. Decimals parse exactly and print in
  plain canonical form: no exponent notation, trailing zeros trimmed, and
  negative zero normalized to `0`.
- Scheme names are lowercased by registration. The §14 fixture name
  `emptyStruct` therefore prints as `emptystruct`; §14.4 records that canonical
  spelling, and the test applies it through the real registry.
- `ResolvedTransport.Type` is derived from `Constructor.Type` in the current DTO,
  so a `constructor type mismatch` tree cannot be constructed without changing
  the resolved DTO. The validator keeps the documented check; the exact error
  template is covered by `CanonicalizationErrorFactoryTests`.

## Out of scope

- Lexing, parsing, and AST construction beyond using the real parser as a
  round-trip fixture.
- Registry registration, metadata validation, and catalog rules; those belong to
  the `TransportFormula.Registry` suite.
- Semantic resolution, candidate selection, binding, and semantic errors; those
  belong to the `TransportFormula.Semantics` suite.
- Compilation, the direct transport compiler, compiler invariant errors, and
  build-time conversion; those belong to the `Factory.Compilation` suite.
- Bootstrap wiring and `ITransportFactory`.
- Transport building, converter paths, concrete transports, and runtime
  construction.
- `E_LEX_*`, `E_SYN_*`, `E_SEM_*`, `E_REG_*`, `E_CMP_*`, and `E_BLD_*`
  catalogues.
- Host policy for aliases, schemes, and transport types.
- Performance, allocation, concurrency, and adversarial depth. The grammar and
  printer define no implementation limits; this suite exercises a deterministic
  corpus rather than stress input.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
