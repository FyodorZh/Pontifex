# Compilation Test Suite

Contract tests for the direct transport compiler and the runtime constructor
registry that feeds it. The suite proves that a resolved formula tree is turned
into live `ITransport` instances without any descriptor adapter: constructor
lookup is keyed by the normalized scheme plus the core transport type, the build
context exposes decoded parameters and lazy nested-transport factories, and
build failures preserve the original exception.

## Suite type

**Test Suit.** The subject is one tightly coupled direct-compilation group of
non-abstract types:

- `TransportCompiler` (public implementation of `ITransportCompiler`)
- `TransportConstructorRegistry` (public implementation of
  `ITransportConstructorRegistry`)
- `TransportBuildContext` (internal `ITransportBuildContext` implementation,
  exercised through `ITransportRegistration.Create`)

Root subject: that direct-compilation group.

## Contract references

- [implementation-plan.md](../../../Pontifex.TransportFormula/implementation-plan.md)
  §11 — compilation subsystem, algorithm, invariants, and test matrix.
- [parsing.md](../../../Pontifex.TransportFormula/parsing.md) §6.9 — core
  transport registration contract; §9 — compiler boundary, build context,
  compilation algorithm, build-time casts; §13.6 — compiler and build error
  catalogue.
- Runtime compilation sources:
  [ITransportCompiler.cs](../../../Pontifex.Factory/Compilation/ITransportCompiler.cs),
  [TransportCompiler.cs](../../../Pontifex.Factory/Compilation/TransportCompiler.cs),
  [TransportBuildContext.cs](../../../Pontifex.Factory/Compilation/Internal/TransportBuildContext.cs),
  [ITransportConstructorRegistry.cs](../../../Pontifex.Factory/ITransportConstructorRegistry.cs),
  [TransportConstructorRegistry.cs](../../../Pontifex.Factory/TransportConstructorRegistry.cs).
- Core registration contract:
  [ITransportRegistration.cs](../../../Pontifex/Factory/Registration/ITransportRegistration.cs),
  [ITransportBuildContext.cs](../../../Pontifex/Factory/Registration/ITransportBuildContext.cs),
  [TransportParameter.cs](../../../Pontifex/Factory/Registration/TransportParameter.cs),
  [TransportTypeSet.cs](../../../Pontifex/Factory/Registration/TransportTypeSet.cs),
  [StructSchema.cs](../../../Pontifex/Factory/Registration/StructSchema.cs).
- Formula DTOs consumed by the compiler:
  [ResolvedDtos.cs](../../../Pontifex.TransportFormula/Semantics/ResolvedDtos.cs),
  [MetadataDtos.cs](../../../Pontifex.TransportFormula/Registry/MetadataDtos.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs),
  [FactoryExceptions.cs](../../../Pontifex.TransportFormula/FactoryExceptions.cs).

## Layers

This is a Test Suit with exactly one layer at the suite root.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Compilation` | Direct transport compiler, runtime constructor registry, and internal build context | `Tests/Pontifex.Factory.Tests/Compilation` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Compilation`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Method_Scenario_ExpectedResult`.
- Namespace = `Pontifex.Factory.Tests.Compilation` for every suite test file.
- Harness types live in `Pontifex.Factory.Tests.Compilation.Harness`.
- Every fixture carries `[Category("Factory.Compilation")]`.

## Directory layout

```
Compilation/
├── README.md
├── Coverage.md
├── Harness/
│   ├── CompilationTestData.cs
│   ├── FakeTransport.cs
│   ├── RecordingConvertersGraph.cs
│   └── RecordingRegistration.cs
└── Tests/
    ├── TransportCompilerTests.cs
    └── TransportConstructorRegistryTests.cs
```

`Tests/` belongs to the single `Compilation` layer.

## Groups

There are no groups. All test files live directly under `Tests/`. If grouping is
added later, groups must be subfolders of `Tests/`; they are organisational
folders, not layers, and they must not contain `Coverage.md` or `Harness/`.

## Harness layout and sharing

`Harness/` contains hand-written doubles and builders only:

- `CompilationTestData` — builders for formula metadata
  (`StringParam`, `IntegerParam`, `DecimalParam`, `TransportParam`,
  `TransportListParam`, `StructParam`, `Schema`, `StringField`, `IntegerField`,
  `DecimalField`, `ArrayField`, `NestedStructField`), core registration metadata,
  resolved trees, and compiler/registry construction.
- `FakeTransport` — minimal `ITransport` with settable type and name. It never
  performs real I/O and is never started by the suite.
- `RecordingRegistration` — `ITransportRegistration` that records each
  `Create` invocation, the received `ITransportBuildContext`, and the produced
  transport. Its creation callback scripts failures.
- `RecordingConvertersGraph` — `IConvertersGraph` that records every
  `TryConvert` argument and returns a scripted factory or `null`. A scripted
  factory can throw.

There are no harness self-tests, so the `Factory.Compilation.Harness` category is
reserved but unused. Harness belongs to the root layer and is available to every
test in this suite. Sibling suites do not share harness
(`Tests/RULES.md` HARNESS-05), so these helpers are local. The repository does
not use a mocking library. Harness types expose only the surface required by the
tests and never private production state.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `Factory.Compilation`. Every fixture in this suite carries
  it.
- Additional filtering categories: none currently.
- Harness category: `Factory.Compilation.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --filter "TestCategory=Factory.Compilation"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if
   the suite grows cohesive subgroups.
2. Exercise the subject through its public contract: `ITransportCompiler`,
   `ITransportConstructorRegistry`, `ITransportRegistration`, and
   `ITransportBuildContext`. The internal build context is inspected only through
   the interface a registration receives.
3. Build trees and registrations with `Harness/CompilationTestData`; keep new
   doubles hand-written and local to this suite.
4. Assert exact `Error.Code`, `Error.Phase`, and `Error.Message` where the
   contract defines them. Preserve exception identity assertions for wrapped
   failures.
5. Update [Coverage.md](Coverage.md) in the same change.

## Adding a layer

A Test Suit has exactly one layer. If the subject becomes an inheritance tree or
requires genuinely separate layers, split this suite into multiple Test Suits or
convert it to an Abstract Test Suit instead of adding a nested layer here.

## Out of scope

- Bootstrap registration and compiler-creation wiring (`RegisterTransport`,
  `CreateCompiler`, sealing after `Build`, formula-only registration). Those
  belong to the `Bootstrap` suite, as allowed by implementation-plan §11.6.
- Parser, resolver, registry, and canonicalization algorithms; they are covered
  by their own suites. This suite consumes their DTOs directly.
- Real transport behaviour, sockets, threads, timing, and resource ownership.
- Optional compiler invariants that the production compiler does not perform:
  registration type disagreeing with resolved metadata and created transport
  type disagreeing with the registration type.
- `E_CMP_DUPLICATE_PARAM` and `E_CMP_UNKNOWN_PARAM`: the current compiler has no
  path that produces them.
- Thread safety. The contract defines no concurrency guarantee.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
