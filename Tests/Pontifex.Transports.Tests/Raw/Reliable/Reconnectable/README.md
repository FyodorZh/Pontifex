# Reconnectable Reliable Wrapper Test Suite

Contract tests for the `reconnectable` reliable wrapper: it negotiates its own
`"reconnectable"` dataset over any `RawReliable` transport, keeps one logical
session alive across physical reconnections, and reattaches a returning client
by session id and secret. Every test runs over hand-written fake reliable
transports and a manual negotiation clock and periodic driver: no real time,
timers, sockets, or networks.

## Suite type

**Test Suit.** The subject is the small tightly coupled group formed by the
reconnectable binding, client, server, and the session logic they construct.

Root subject: `Pontifex.Raw.Reliable.Reconnectable.RawReliableReconnectableBinding`,
`Pontifex.Raw.Reliable.Reconnectable.RawReliableReconnectableClient`,
`Pontifex.Raw.Reliable.Reconnectable.RawReliableReconnectableServer`,
`Pontifex.Raw.Reliable.Reconnectable.ReconnectableClientLogic`, and
`Pontifex.Raw.Reliable.Reconnectable.ReconnectableServerLogic`.

## Contract references

- [RawReliableReconnectableBinding.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableBinding.cs)
- [RawReliableReconnectableClient.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableClient.cs)
- [RawReliableReconnectableServer.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableServer.cs)
- [ReconnectableClientLogic.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/ReconnectableClientLogic.cs)
- [ReconnectableServerLogic.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/ReconnectableServerLogic.cs)
- [ReconnectableBaseLogic.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Common/ReconnectableBaseLogic.cs)
- [DeliverySystem.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Common/DeliverySystem.cs)
- [SessionMap.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Common/SessionMap.cs)
- [ReconnectableInfo.cs](../../../../../Protocols/Pontifex.Protocol.Reconnectable/ReconnectableInfo.cs)
- [RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs)
- [NegotiationSession.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs)

The wrapper envelope is flat:
`[version (short), name (UTF-8 string), NegotiationStatus (byte), ...custom layer elements]`.
The reconnectable layer uses version `1` and name `"reconnectable"`. A first
connection appends `[invalidSessionId, 0, emptySecret]`; a reconnect appends
`[sessionId, generation, secret]`. The accepted response tail is
`[sessionId, generation, secret]`.

## Layer

The layer table is the authoritative declaration of layer name, subject,
directory, and coverage file.

| Layer | Subject | Directory | Coverage |
|---|---|---|---|
| `RawReliableReconnectable` | Reconnectable binding, client, server, and session logic | `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable` | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Namespace = `Pontifex.Tests.Raw.Reliable.Reconnectable`; harness types use
  `Pontifex.Tests.Raw.Reliable.Reconnectable.Harness`.
- Test methods = `Scenario_ExpectedResult`, for example
  `WrongSecret_IsRejectedAndStopsTheInnerSessionExactlyOnce`.
- Every fixture carries `[Category("Reconnectable")]`.

## Directory layout

```
Reconnectable/
├── README.md
├── Coverage.md                              RawReliableReconnectable layer
├── Harness/
│   ├── FakeReconnectableTransport.cs
│   ├── ReconnectablePair.cs
│   └── ReconnectableTestSupport.cs
└── Tests/
    └── RawReliableReconnectableTests.cs
```

There are no groups under `Tests/`: the suite has one layer and one test file.

## Harness layout

All harness types are hand-written (no mocking library) and test-only:

- `ReconnectableTestSupport.cs` contains the manual `INegotiationClock` seam,
  a hand-driven multi-logic `ILogicDriver`, endpoint and payload helpers, and
  recording application client/server handlers plus a recording server factory.
- `FakeReconnectableTransport.cs` contains the scripted `IRawReliableClient`,
  `IRawReliableServer`, endpoint, and physical server session. Delivery is
  synchronous and caller-driven; outbound payloads are cloned into `Sent` for
  assertions and released with `ReleaseSent`. The server session counts its
  handler's disconnect and stop callbacks so the stop-once contract is
  observable. An endpoint transform can rewrite the reattach request secret.
- `ReconnectablePair.cs` links a production reconnectable client and server
  through fake physical transports, the manual clock, and two manual periodic
  drivers. `DropPhysicalConnection()` stops the current physical session
  without stopping either logical transport; `Reconnect()` advances the
  reconnect period and establishes a new physical session.

There are no harness self-tests, so the `Reconnectable.Harness` category is not
used.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [`Coverage.md`](Coverage.md) — `RawReliableReconnectable`

A layer's `Coverage.md` lists current covered tests, planned gaps, and
deferred-by-design gaps.

## Categories

- Suite root category: `Reconnectable`. Every fixture carries it.
- Additional filtering categories: none.
- Harness category: not used (no harness self-tests).

Non-default facets (real I/O, load, real time, sockets) must not be added to
this suite; they belong to a carrier or integration suite with their own
category.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --filter TestCategory=Reconnectable
```

Filter to the fixture:

```
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --filter "FullyQualifiedName~RawReliableReconnectableTests"
```

## Adding tests

1. Put the test in `Tests/RawReliableReconnectableTests.cs`; the suite has
   exactly one layer.
2. Drive connection, physical drops, inbound traffic, reconnection, and time
   through the harness. Do not use real clocks, timers, sockets, or threads.
3. Assert observable results only: callback order and counts, stop reasons,
   captured envelopes and inner send buffers, send results, and ownership
   outcomes.
4. Release every captured `UnionDataList` that the test owns.
5. Record the test in [`Coverage.md`](Coverage.md) in the same change.

This is a Test Suit, so it must not acquire nested layers. If the subject
becomes an inheritance tree, convert the suite according to the rules.

## Out of scope

- The `reconnectable(disconnect_timeout, nested)` factory formula and its
  binding parameter surface; those are covered by
  `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`.
- The response-tail unit tests (`CreateNegotiationResponse` metadata and the
  full session envelope); those live in the loose
  `ReconnectableNegotiationResponseTests.cs`.
- The reusable wrapper negotiation driver and the generic wrapper base; those
  belong to the [`RawReliableWrapper` suite](../Protocol/README.md).
- Zip and Api negotiation datasets; those are separate protocol suites.
- Real TCP/Direct transports, sockets, real time, and concurrency stress.
- Wire encoding and carrier framing.
- Logging output and performance.

## Rules

This suite follows [Tests/RULES.md](../../../../RULES.md).
