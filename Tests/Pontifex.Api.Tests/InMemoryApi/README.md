# InMemoryApi Test Suite

Black-box contract tests for the Pontifex API declaration and pipe subsystem over
`InMemoryPipeSystem`. The suite proves request/response delivery, message-only
calls, and graceful shutdown propagation between a client and server root API.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
production types:

- `RRDecl<TRequest, TResponse>`
- `C2SMessageDecl<TMessage>`
- `S2CMessageDecl<TMessage>`
- `InMemoryPipeSystem`

Root subject: the in-memory API declaration and pipe group above.

The tests drive this group through `IApiRoot`. Concrete API roots and payloads
under `Harness/` are test-only implementations, not additional suite subjects.

## Contract references

- [ApiRoot.cs](../../../Api/Pontifex.Api/Logic/Api/ApiRoot.cs)
- [IApiRoot.cs](../../../Api/Pontifex.Api/Logic/Api/IApiRoot.cs)
- [RRDecl.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/RequestResponse/RRDecl.cs)
- [IRequester.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/RequestResponse/IRequester.cs)
- [IResponder.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/RequestResponse/IResponder.cs)
- [C2SMessageDecl.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/MessageOnly/Typed/C2SMessageDecl.cs)
- [S2CMessageDecl.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/MessageOnly/Typed/S2CMessageDecl.cs)
- [InMemoryPipeSystem.cs](../../../Api/Pontifex.Api/Logic/PipeSystem/InMemoryPipeSystem.cs)

## Layers

The layer table is the authoritative declaration of the single root layer.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `InMemoryApi` | In-memory API declaration and pipe group | `Tests/Pontifex.Api.Tests/InMemoryApi` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `InMemoryApi`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Method_Scenario_ExpectedResult`.
- Namespace = `Pontifex.Api.Tests.InMemoryApi` for suite tests.
- Harness namespace = `Pontifex.Api.Tests.InMemoryApi.Harness`.
- Every suite fixture carries `[Category("Pontifex.Api.InMemoryApi")]`.

## Directory layout

```text
InMemoryApi/
├── README.md
├── Coverage.md
├── Harness/
│   ├── BigApi.cs
│   └── EmptyApi.cs
└── Tests/
    └── InMemoryApiTests.cs
```

`Coverage.md` belongs to the single root layer. `Tests/` contains only suite
test files and currently has no groups.

## Groups

There are no groups. All test files live directly under `Tests/`.

## Harness layout and sharing

`Harness/` contains test-only API roots and payload types:

- `EmptyApi.cs` provides an empty root for lifecycle-only tests.
- `BigApi.cs` provides a nested request/response declaration and typed
  client-to-server/server-to-client message declarations.

There is no `Harness/Tests/` directory and no harness self-test category. The
suite uses hand-written test doubles and no mocking library.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every suite test, the contract behaviour it proves,
and known gaps.

## Categories

- Suite root category: `Pontifex.Api.InMemoryApi`. Every suite fixture carries it.
- Additional filtering categories: none currently.
- Harness self-tests and the corresponding harness category: none currently.

## Running

Run the whole test project:

```sh
dotnet test Tests/Pontifex.Api.Tests/Pontifex.Api.Tests.csproj
```

Filter to this suite:

```sh
dotnet test Tests/Pontifex.Api.Tests/Pontifex.Api.Tests.csproj --filter "TestCategory=Pontifex.Api.InMemoryApi"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if
   the suite grows cohesive subgroups.
2. Exercise the declaration and pipe group through public API contracts and
   the existing hand-written harness.
3. Keep the default run deterministic: do not add sleeps, real clocks, sockets,
   networks, timers, randomness, or uncontrolled background tasks.
4. Use the naming and category conventions above.
5. Record the test in [Coverage.md](Coverage.md) in the same change.

## Adding a layer

This Test Suit has exactly one layer, so adding a layer is not applicable. If
the subject requires nested layers, split it into separate Test Suits or
convert it according to [Tests/RULES.md](../../RULES.md).

## Out of scope

- API negotiation, `ClientSideApi`, `ServerSideApiInstance`, and transport
  wrappers.
- Raw pipes, serialization/deserialization, and wire-format compatibility. The
  in-memory model pipes carry typed values directly.
- Real transports, sockets, networks, and integration timing.
- Graceful-shutdown timeout behaviour and other timer-driven paths. The default
  tests pass a null timeout so shutdown completes synchronously.
- Concurrency, allocation, load, and stress behaviour.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
