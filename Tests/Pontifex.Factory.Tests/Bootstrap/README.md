# Bootstrap Test Suite

Contract tests for the Transport Formula composition root and public facade. The
suite proves that the only public entry point — `TransportFactoryBootstrap` —
builds a frozen formula pipeline, exposes it through `ITransportFactory`, and
translates each stage according to the documented result/exception policy:
`Resolve` returns a result DTO, while `Canonicalize` and `Compile` throw
`FactoryException` carrying the stage's `FactoryError`. The suite also covers
registering core transport constructors and creating the direct
`ITransportCompiler`: one `RegisterTransport(ITransportRegistration)` call feeds
both the formula metadata and the runtime constructor registry.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
internal types plus the public creation seam:

- `TransportFactoryBootstrap` (public seam, root namespace)
- `TransportFactoryBuilder`
- `TransportFactory`
- `FactoryComponentFactory` (exercised through `Build`)
- `CoreRegistrationAdapter` (exercised through `RegisterTransport`)

Root subject: the bootstrap wiring group above.

The suite intentionally uses only the public surface (`TransportFactoryBootstrap`,
`ITransportFactoryBuilder`, `ITransportFactory`, `IRegistryBuilder`,
`IRegistry`, `ITransportRegistration`, `ITransportCompiler`, `TransportRole`)
together with the public metadata DTOs. It does not construct internal components
directly; `InternalsVisibleTo` is not used to reach bootstrap internals.

## Contract references

Paths below reflect the split layout: formula sources live under
`Pontifex.TransportFormula`, runtime sources under `Pontifex.Factory`.

- [parsing.md](../../../Pontifex.TransportFormula/parsing.md) — parse, resolution, and
  printing error phases (§12–13).
- Public contract:
  [TransportFactoryBootstrap.cs](../../../Pontifex.Factory/TransportFactoryBootstrap.cs),
  [ITransportFactoryBuilder.cs](../../../Pontifex.Factory/ITransportFactoryBuilder.cs),
  [ITransportFactory.cs](../../../Pontifex.Factory/ITransportFactory.cs),
  [IRegistry.cs](../../../Pontifex.TransportFormula/Registry/IRegistry.cs),
  [MetadataDtos.cs](../../../Pontifex.TransportFormula/Registry/MetadataDtos.cs),
  [ResolvedDtos.cs](../../../Pontifex.TransportFormula/Semantics/ResolvedDtos.cs),
  [ICompiler.cs](../../../Pontifex.TransportFormula/Compilation/ICompiler.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.TransportFormula/FactoryExceptions.cs).
- Core registration contract:
  [ITransportRegistration.cs](../../../Pontifex/Factory/Registration/ITransportRegistration.cs),
  [ITransportBuildContext.cs](../../../Pontifex/Factory/Registration/ITransportBuildContext.cs),
  [TransportParameter.cs](../../../Pontifex/Factory/Registration/TransportParameter.cs).
- Runtime compilation surface:
  [ITransportCompiler.cs](../../../Pontifex.Factory/Compilation/ITransportCompiler.cs),
  [TransportCompiler.cs](../../../Pontifex.Factory/Compilation/TransportCompiler.cs),
  [ITransportConstructorRegistry.cs](../../../Pontifex.Factory/ITransportConstructorRegistry.cs),
  [TransportConstructorRegistry.cs](../../../Pontifex.Factory/TransportConstructorRegistry.cs).
- Converter contract:
  [ConvertersGraph.cs](../../../Pontifex/Converters/ConvertersGraph.cs).
- Internal subjects:
  [TransportFactoryBuilder.cs](../../../Pontifex.Factory/Bootstrap/TransportFactoryBuilder.cs),
  [TransportFactory.cs](../../../Pontifex.Factory/Bootstrap/TransportFactory.cs),
  [FactoryComponentFactory.cs](../../../Pontifex.Factory/Bootstrap/FactoryComponentFactory.cs),
  [CoreRegistrationAdapter.cs](../../../Pontifex.Factory/Bootstrap/CoreRegistrationAdapter.cs).

## Layers

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Bootstrap` | Public seam, factory builder, factory facade, registration adapter, and component wiring | `Tests/Pontifex.Factory.Tests/Bootstrap` | — (root) | [`Coverage.md`](Coverage.md) |

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
│   ├── BootstrapTestData.cs
│   ├── FakeRawUnreliableNoAckClient.cs
│   ├── FakeTransport.cs
│   └── RecordingConvertersGraph.cs
└── Tests/
    ├── TransportFactoryBuilderTests.cs
    └── TransportFactoryTests.cs
```

`Tests/` belongs to the single `Bootstrap` layer. It currently contains no
groups.

## Groups

There are no groups. All test files live directly under `Tests/`.

## Harness layout and sharing

`Harness/` contains test-only helpers:

- `BootstrapTestData` — hand-written formula/core registration helpers plus two
  compiler doubles: a recording compiler and a compiler that throws a
  caller-supplied exception.
- `FakeTransport` — a minimal live `ITransport` double used to identify the
  exact instance a core constructor returned.
- `RecordingConvertersGraph` — a converter-graph double that records
  `TryConvert` arguments and returns a scripted conversion factory.
- `FakeRawUnreliableNoAckClient` — a non-I/O raw-unreliable client used only to
  exercise the built-in converter through the default graph.

There are no harness self-tests. The harness is local to this suite: the
metadata and transport doubles resemble the compilation suite's fixtures but are
separate copies.

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
3. Use the suite naming convention: `Method_Scenario_ExpectedResult`.
4. Assert the result DTO or the exception type, `Error.Code`, and `Error.Phase`;
   assert exact messages only for new templates.
5. Record the test in [Coverage.md](Coverage.md).

## Adding a layer

This suite has one layer; adding a layer is not applicable. If the subject
changes shape, see [Tests/RULES.md](../../RULES.md).

## Out of scope

- Parsing, registry, semantics, and canonicalization algorithms themselves; they
  are covered by their own suites. This suite asserts only wiring and stage
  translation.
- Compiler internals, `E_CMP_*` invariants, and the full `E_BLD_*` matrix; those
  belong to the `Factory.Compilation` suite. This suite asserts only the
  integration-level build error (`E_BLD_CONSTRUCTOR_NOT_FOUND`) and that
  compiler-raised exceptions pass through unchanged.
- Thread safety of `Build`. The contract defines no concurrency guarantee.
- Performance, allocation, and adversarial input limits.

The runtime facade exposes `ITransport`, `ITransportCompiler`, logging, and
memory types. The formula assembly must not reference them; that is a
project-reference property verified by dependency review rather than by a
runtime test.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
