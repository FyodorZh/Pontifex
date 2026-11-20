# Converters Test Suite

Contract tests for the built-in `RawUnreliableNoAck` to `RawReliable` transport
converter. The suite proves that the converter performs its own handshake as the
first delivery-managed message, rejects version and name mismatches without
creating an application session, negotiates the minimum of the two delivery
manager maxima, and mirrors the reliable terminal lifecycle exactly once.

## Suite type

**Test Suit.** The subject is a small tightly coupled group of non-abstract
converter types:

- `RawUnreliableNoAckToRawReliableConverter`
- `RawUnreliableNoAckToRawReliableClient`
- `RawUnreliableNoAckToRawReliableServer`

The internal `ConverterHandshake` helper is part of the same subject group because the client and
server encode and decode their handshake through it. The suite file abbreviates the group to
`RawReliableConverterTests`.

Root subject: the built-in converter group above.

## Contract references

- [RawUnreliableNoAckToRawReliableConverter.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableConverter.cs)
- [RawUnreliableNoAckToRawReliableClient.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableClient.cs)
- [RawUnreliableNoAckToRawReliableServer.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableServer.cs)
- [ConverterHandshake.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/ConverterHandshake.cs)
- [ConvertersGraph.cs](../../../Pontifex/Converters/ConvertersGraph.cs)
- [IRawReliableClient.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClient.cs),
  [IRawReliableServer.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServer.cs),
  [IRawReliableEndpoint.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableEndpoint.cs)
- [IRawUnreliableNoAckClient.cs](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/NoAck/IRawUnreliableNoAckClient.cs),
  [IRawUnreliableNoAckServer.cs](../../../Pontifex/Abstractions/Transports/Raw/Unreliable/NoAck/IRawUnreliableNoAckServer.cs)

## Layers

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Converters` | Built-in RawUnreliableNoAck to RawReliable converter group | `Tests/Pontifex.Factory.Tests/Converters` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Converters`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Test methods = `Method_Scenario_ExpectedResult`.
- Namespace = `Pontifex.Factory.Tests.Converters` for every suite test file.
- Harness types live in `Pontifex.Factory.Tests.Converters.Harness`.
- Every fixture carries `[Category("Factory.Converters")]`.

## Directory layout

```
Converters/
├── README.md
├── Coverage.md
├── Harness/
│   └── FakeRawUnreliableTransport.cs
└── Tests/
    └── RawReliableConverterTests.cs
```

`Tests/` belongs to the single `Converters` layer.

## Groups

There are no groups. All test files live directly under `Tests/`.

## Harness layout and sharing

`Harness/` contains hand-written doubles only:

- `FakeRawUnreliableNetwork`, `FakeEndPoint`, `FakeRawUnreliableNoAckClient`,
  `FakeRawUnreliableNoAckServer`, and the client/server endpoints — a
  synchronous in-process raw-unreliable pair. They create no sockets or
  background threads; every send is delivered on the caller's thread.
- `RawUnreliableDeliveryPeer` — a scripted raw-unreliable peer that speaks the
  delivery-manager wire format directly. It lets a test send a handcrafted
  handshake request or withhold/replace a handshake response without a second
  converter.
- `RecordingRawReliableClientHandler`, `RecordingRawReliableServerHandler`, and
  `RecordingRawReliableServerHandlerFactory` — recording application handlers
  that expose lifecycle counts, ordered lifecycle entries, endpoint references,
  and received-payload clones.

The harness is local to this suite. There are no harness self-tests, so the
`Factory.Converters.Harness` category is reserved but unused.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `Factory.Converters`. Every fixture in this suite carries
  it.
- Additional filtering categories: none currently.
- Harness category: `Factory.Converters.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --filter "TestCategory=Factory.Converters"
```

Filter to the converter fixture:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --filter "FullyQualifiedName~RawReliableConverterTests"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if
   the suite grows cohesive subgroups.
2. Exercise the converter through its `IRawReliableClient`/`IRawReliableServer`
   and endpoint contracts. Replace nondeterministic inner transports with the
   suite's hand-written fakes.
3. Keep new doubles local to this suite; sibling suites must not share harness.
4. Keep the default run deterministic: wait on explicit handler events, not on
   arbitrary sleeps.
5. Record the test in [Coverage.md](Coverage.md).

## Adding a layer

This suite has one layer; adding a layer is not applicable. If the subject
changes shape, see [Tests/RULES.md](../../RULES.md).

## Out of scope

- Generic converter-graph path selection and compilation wiring; the loose
  `Tests/Pontifex.Factory.Tests/ConvertersGraphTests.cs` covers path selection,
  and bootstrap/compilation wiring belongs to the `Factory.Bootstrap` and
  `Factory.Compilation` suites.
- Real unreliable carriers (UDP, direct), real sockets, loss, reordering, and
  duplication. This suite uses a synchronous pair and exercises the handshake
  and lifecycle contracts only.
- The transport-tier `RawReliableHandshake` codec; the converter owns a separate
  `ConverterHandshake` dataset.
- Performance and allocation limits.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
