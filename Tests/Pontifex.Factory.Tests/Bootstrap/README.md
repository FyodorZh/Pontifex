# Bootstrap Test Suite

Contract tests for the Transport Formula composition root and public facade. The
suite proves that the only public entry point — `TransportFactoryBootstrap` —
builds a frozen formula pipeline, exposes it through `ITransportFactory`, and
translates each stage according to the documented result/exception policy:
`Resolve` returns a result DTO, while `Canonicalize` and `Compile` throw
`FactoryException` carrying the stage's `FactoryError`. After Milestone 6 the
suite also covers registering core transport constructors and creating the direct
`ITransportCompiler`.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
internal types plus the public creation seam:

- `TransportFactoryBootstrap` (public seam, root namespace)
- `TransportFactoryBuilder`
- `TransportFactory`
- `FactoryComponentFactory` (exercised through `Build`)

Root subject: the bootstrap wiring group above.

The suite intentionally uses only the public surface (`TransportFactoryBootstrap`,
`ITransportFactoryBuilder`, `ITransportFactory`, `IRegistryBuilder`,
`IRegistry`, `ITransportRegistration`, `ITransportCompiler`, `TransportRole`)
together with the public metadata DTOs. It does not construct internal components
directly; `InternalsVisibleTo` is not used to reach bootstrap internals.

## Contract references

Paths below reflect the pre-split layout. Milestone 5 moves formula sources to
`Pontifex.TransportFormula` and updates these links.

- [implementation-plan.md](../../../Pontifex.Factory/implementation-plan.md) —
  bootstrap subsystem (§12), dependency rules (§5), and definition of done (§16).
- [parsing.md](../../../Pontifex.Factory/parsing.md) — parse, resolution, and
  printing error phases (§12–13).
- Public contract:
  [TransportFactoryBootstrap.cs](../../../Pontifex.Factory/TransportFactoryBootstrap.cs),
  [ITransportFactoryBuilder.cs](../../../Pontifex.Factory/ITransportFactoryBuilder.cs),
  [ITransportFactory.cs](../../../Pontifex.Factory/ITransportFactory.cs),
  [IRegistry.cs](../../../Pontifex.Factory/Registry/IRegistry.cs),
  [MetadataDtos.cs](../../../Pontifex.Factory/Registry/MetadataDtos.cs),
  [ResolvedDtos.cs](../../../Pontifex.Factory/Semantics/ResolvedDtos.cs),
  [ICompiler.cs](../../../Pontifex.Factory/Compilation/ICompiler.cs),
  [FactoryError.cs](../../../Pontifex.Factory/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.Factory/FactoryExceptions.cs).
- Core registration contract:
  [ITransportRegistration.cs](../../../Pontifex/Factory/Registration/ITransportRegistration.cs),
  [ITransportBuildContext.cs](../../../Pontifex/Factory/Registration/ITransportBuildContext.cs),
  [TransportParameter.cs](../../../Pontifex/Factory/Registration/TransportParameter.cs).
- Planned runtime compilation surface, added in Milestone 5:
  `Pontifex.Factory/Compilation/ITransportCompiler.cs`,
  `Pontifex.Factory/Compilation/TransportCompiler.cs`,
  `Pontifex.Factory/ITransportConstructorRegistry.cs`.
- Internal subjects:
  [TransportFactoryBuilder.cs](../../../Pontifex.Factory/Bootstrap/TransportFactoryBuilder.cs),
  [TransportFactory.cs](../../../Pontifex.Factory/Bootstrap/TransportFactory.cs),
  [FactoryComponentFactory.cs](../../../Pontifex.Factory/Bootstrap/FactoryComponentFactory.cs).

## Layers

This is a Test Suit with exactly one layer at the suite root.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Bootstrap` | Public seam, factory builder, factory facade, and component wiring | `Tests/Pontifex.Factory.Tests/Bootstrap` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Bootstrap`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `MethodName_Scenario_ExpectedResult`.
- Namespace = `Pontifex.Factory.Tests.Bootstrap` for every suite test file.
- Harness types live in `Pontifex.Factory.Tests.Bootstrap.Harness`.
- Every fixture carries `[Category("Factory.Bootstrap")]`.

## Directory layout

```
Bootstrap/
├── README.md
├── Coverage.md
├── Harness/
│   └── BootstrapTestData.cs
└── Tests/
    ├── TransportFactoryBuilderTests.cs
    └── TransportFactoryTests.cs
```

`Tests/` belongs to the single `Bootstrap` layer. It currently contains no
groups.

## Groups

There are no groups. All test files live directly under `Tests/`. If grouping is
added later, groups must be subfolders of `Tests/`; they are organisational
folders, not layers, and they must not contain `Coverage.md` or `Harness/`.

## Harness layout and sharing

`Harness/` contains one test-only file:

- `BootstrapTestData` — hand-written registration helpers plus two compiler
  doubles: a recording compiler and a compiler that throws a caller-supplied
  exception.

There are no harness self-tests. Harness belongs to the root layer and is
available to every test in this suite. Sibling suites do not share harness
(`Tests/RULES.md` HARNESS-05), so the metadata helpers are local even though
they resemble the registry suite's fixtures. The repository does not use a
mocking library; these doubles are hand-written. Harness types expose only the
surface required by the tests and never private production state.

If harness self-tests are added later, they live in `Harness/Tests/` and carry
`Factory.Bootstrap.Harness` instead of the suite root category.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `Factory.Bootstrap`. Every fixture in this suite carries it.
- Additional filtering categories: none currently.
- Harness category: `Factory.Bootstrap.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --filter "TestCategory=Factory.Bootstrap"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if
   the suite grows cohesive subgroups.
2. Use only the public contract: `TransportFactoryBootstrap`,
   `ITransportFactoryBuilder`, `ITransportFactory`, `IRegistryBuilder`,
   `ITransportRegistration`, `ITransportCompiler`, and the metadata/resolution
   DTOs. Do not call internal bootstrap types directly.
3. Use one naming convention and descriptive scenario/expected-result names.
4. Assert the result DTO or the exception type, `Error.Code`, and `Error.Phase`;
   assert exact messages only for new templates.
5. Update [Coverage.md](Coverage.md) in the same change.

## Adding a layer

A Test Suit has exactly one layer. If the subject becomes an inheritance tree or
requires genuinely separate layers, split this suite into multiple Test Suits or
convert it to an Abstract Test Suit instead of adding a nested layer here.

## Out of scope

- Parsing, registry, semantics, and canonicalization algorithms themselves; they
  are covered by their own suites. This suite asserts only wiring and stage
  translation.
- `E_CMP_*` and `E_BLD_*` compiler invariants and direct compiler behaviour;
  those belong to the future `Factory.Compilation` suite. This suite proves only
  that compiler-raised exceptions pass through unchanged.
- Thread safety of `Build`. The contract defines no concurrency guarantee.
- Performance, allocation, and adversarial input limits.

After Milestone 6 the runtime facade exposes `ITransport`, `ITransportCompiler`,
logging, and memory types. The formula assembly must not reference them; that is
a project-reference property verified by dependency review rather than by a
runtime test.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).

Known test assumptions that are not part of the normative contract are recorded in
[nonapproved-behaviour.md](../nonapproved-behaviour.md).
