# Registry Test Suite

Contract tests for the Transport Formula registry subsystem. The suite proves
that transport-type catalogs and transport registrations implement
`parsing.md` §6.6-6.8 and §13.4: case-insensitive catalog lookup, normalized and
validated metadata, the numbered registration validation order, alias rules,
catalog membership, immutable build snapshots, and every `E_REG_*` code and
message.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
internal types that form one subsystem:

- `TransportTypeCatalog`
- `TransportRegistry`
- `TransportRegistryBuilder`
- `MetadataValidator`
- `RegistryErrorFactory`

Root subject: the registry subsystem group above.

Internal implementations are tested through the same assembly boundary used by
production code. `InternalsVisibleTo("Pontifex.Factory.Tests")` is part of the
production project; no test-only production seam exists.

## Contract references

- [parsing.md](../../../Pontifex.Factory/parsing.md) — registry and metadata
  (§6), registration rules (§6.7), lookup rules (§6.8), and registration errors
  (§13.4).
- [implementation-plan.md](../../../Pontifex.Factory/implementation-plan.md) —
  registry milestone, component responsibilities, and test expectations (§8).
- Public contract and DTOs:
  [IRegistry.cs](../../../Pontifex.Factory/Registry/IRegistry.cs),
  [ITransportTypeCatalog.cs](../../../Pontifex.Factory/Registry/ITransportTypeCatalog.cs),
  [MetadataDtos.cs](../../../Pontifex.Factory/Registry/MetadataDtos.cs),
  [FactoryError.cs](../../../Pontifex.Factory/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.Factory/FactoryExceptions.cs).
- Internal subjects:
  [TransportTypeCatalog.cs](../../../Pontifex.Factory/Registry/Internal/TransportTypeCatalog.cs),
  [TransportRegistry.cs](../../../Pontifex.Factory/Registry/Internal/TransportRegistry.cs),
  [TransportRegistryBuilder.cs](../../../Pontifex.Factory/Registry/Internal/TransportRegistryBuilder.cs),
  [MetadataValidator.cs](../../../Pontifex.Factory/Registry/Internal/MetadataValidator.cs),
  [RegistryErrorFactory.cs](../../../Pontifex.Factory/Registry/Internal/RegistryErrorFactory.cs).

## Layers

This is a Test Suit with exactly one layer at the suite root.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Registry` | Catalog, registry, builder, metadata validation, and registration error group | `Tests/Pontifex.Factory.Tests/Registry` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Registry`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `MethodName_Scenario_ExpectedResult`. Property, lifecycle, and
  error-catalogue tests may omit a separate scenario and use
  `Subject_ExpectedResult` when the subject itself is the scenario, for example
  `Types_ExposesTheCatalog`.
- Namespace = `Pontifex.Factory.Tests.Registry` for every suite test file.
- Harness types live in `Pontifex.Factory.Tests.Registry.Harness`.
- Every fixture carries `[Category("Factory.Registry")]`.

## Directory layout

```
Registry/
├── README.md
├── Coverage.md
├── Harness/
│   ├── RegistryAssertions.cs
│   └── RegistryTestData.cs
└── Tests/
    ├── TransportRegistryBuilderTests.cs
    ├── TransportRegistryTests.cs
    └── TransportTypeCatalogTests.cs
```

`Tests/` belongs to the single `Registry` layer. It currently contains no
groups.

## Groups

There are no groups. All test files live directly under `Tests/`. If grouping
is added later, groups must be subfolders of `Tests/`; they are organisational
folders, not layers, and they must not contain `Coverage.md` or `Harness/`.

## Harness layout and sharing

`Harness/` contains two test-only helpers:

- `RegistryTestData` — hand-written factories for type infos, constructors,
  registrations, parameters, type sets, and struct schemas.
- `RegistryAssertions` — asserts that a delegate throws `RegistrationException`
  with an exact code, message, phase, and no span.

There are no harness self-tests. Harness belongs to the root layer and is
available to every test in this suite. Sibling layers do not exist. The
repository does not use a mocking library; these doubles are hand-written.
Harness types expose only the surface required by the tests and never private
production state.

If harness self-tests are added later, they live in `Harness/Tests/` and carry
`Factory.Registry.Harness` instead of the suite root category.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `Factory.Registry`. Every fixture in this suite carries it.
- Additional filtering categories: none currently.
- Harness category: `Factory.Registry.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --filter "TestCategory=Factory.Registry"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if the
   suite grows cohesive subgroups.
2. Use the public `IRegistry`, `IRegistryBuilder`, and `ITransportTypeCatalog`
   contracts, and the internal implementations exposed by
   `InternalsVisibleTo`; do not use reflection or inspect private state.
3. Assert both the returned metadata and, for failures, the exact
   `RegistrationException` code, message, phase, and absent offset.
4. When several inputs can fail at once, assert the first failure required by
   the numbered order in `parsing.md` §6.7.
5. Update [Coverage.md](Coverage.md) in the same change.

## Adding a layer

A Test Suit has exactly one layer. If the subject becomes an inheritance tree or
requires genuinely separate layers, split this suite into multiple Test Suits or
convert it to an Abstract Test Suit instead of adding a nested layer here.

## Contract notes

- Lookup arguments are already-normalized lowercase scheme names. Uppercase
  lookups return no result; that is asserted, not normalized away.
- `FindCandidates` returns the canonical constructor first, then alias
  candidates sorted by canonical scheme name. It deliberately does not filter by
  expected type or apply canonical preference; semantics owns that.
- Aliases are registration-time policy. `SuggestedAliases` are hints and are never
  registered automatically. Normalization of the stored `SuggestedAliases` values
  is recorded in `nonapproved-behaviour.md`.
- Registration after `Build` throws `RegistrationException` with
  `E_REG_BUILDER_SEALED`. This lifecycle code is defined by `parsing.md` §6.7
  rule 12 and catalogued in §13.4; it has no source offset and phase
  `Registration`.
- `GetCanonicalName` for a type absent from the catalog throws
  `InvalidOperationException` in the current implementation. `parsing.md` §6.6
  only permits calling it for catalog types; the exception type is recorded in
  `nonapproved-behaviour.md`.

## Out of scope

- Parsing, lexing, and the AST.
- Semantic resolution, expected-type filtering, canonical preference, argument
  binding, and semantic errors.
- Canonical printing.
- Compilation and the `IDescription` adapter.
- Bootstrap wiring and `ITransportFactory`.
- Transport building, converters, and concrete transports.
- Choosing which types or schemes a host registers.
- Concurrency, performance, allocation, and large-catalog stress. The registry
  is documented as safe for concurrent reads after build; this suite proves the
  immutable snapshot shape, not throughput.
- Culture-sensitive comparison beyond the ASCII case-insensitive identifier
  rules; `parsing.md` §6.6 defines ASCII case-insensitivity only.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).

Known test assumptions that are not part of the normative contract are recorded in
[nonapproved-behaviour.md](../nonapproved-behaviour.md).
