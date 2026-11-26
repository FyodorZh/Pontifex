# Reliable Wrapper Protocol Test Suite

Black-box contract tests for the reusable reliable-wrapper layer: the public
`RawReliableWrapperClient`/`RawReliableWrapperServer` transports, their
`IRawReliableWrapperLogic`/`RawReliableWrapperLogic` logic contract, and the
internal negotiation driver they share. Every test runs over hand-written
fake reliable transports, a manual clock, and synchronous delivery: no real
time, timers, sockets, or networks.

## Suite type

**Test Suit.** The subject is the small tightly coupled group formed by the
wrapper client, the wrapper server, and the logic base they consume.

Root subject: `Pontifex.Raw.Reliable.Protocols.RawReliableWrapperClient`,
`Pontifex.Raw.Reliable.Protocols.RawReliableWrapperServer`, and
`Pontifex.Raw.Reliable.Protocols.RawReliableWrapperLogic`.

## Contract references

- [RawReliableWrapperClient.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperClient.cs)
- [RawReliableWrapperServer.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperServer.cs)
- [IRawReliableWrapperLogic.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/IRawReliableWrapperLogic.cs)
- [RawReliableWrapperLogic.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperLogic.cs)
- [RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs)
- [NegotiationSession.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs)
- [NegotiationStatus.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationStatus.cs)
- [IRawReliableClient.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClient.cs)
- [IRawReliableServer.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServer.cs)
- [IRawReliableClientHandler.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClientHandler.cs)
- [IRawReliableServerHandler.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableServerHandler.cs)
- [IRawReliableServerHandlerFactory.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServerHandlerFactory.cs)
- [RawReliable Specification](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md) — normative envelope, deadline, linger, and `TryCreate` clauses.

The wrapper envelope is flat:
`[version (short), name (UTF-8 string), NegotiationStatus (byte), ...custom layer elements]`.
The status is always the third element, so a custom value can never be
mistaken for a status. Rejections carry the optional diagnostic text after the
`Rejected` status.

## Layer

The layer table is the authoritative declaration of layer name, subject,
directory, and coverage file.

| Layer | Subject | Directory | Coverage |
|---|---|---|---|
| `RawReliableWrapper` | `RawReliableWrapperClient`, `RawReliableWrapperServer`, `RawReliableWrapperLogic` | `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol` | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = `<Subject>`; this suite has one root layer, `RawReliableWrapper`.
- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Namespace = `Pontifex.Tests.Raw.Reliable.Protocol`.
- Test methods = `Scenario_ExpectedResult`, for example
  `CustomTailRejection_StopsClientWithHandshakeRejectedAndSkipsFactory`.
- Every fixture carries `[Category("Protocol")]`.
- There are no groups; all tests live directly under `Tests/`.

## Directory layout

```
Protocol/
├── README.md
├── Coverage.md                              RawReliableWrapper layer
├── Harness/
│   ├── FakeNegotiationSupport.cs
│   ├── FakeReliableTransport.cs
│   ├── RecordingWrapperLogic.cs
│   └── WrapperPair.cs
└── Tests/
    └── RawReliableWrapperTests.cs
```

## Harness layout

All harness types are hand-written (no mocking library) and test-only:

- The timer seam is the production `Pontifex.Utils.ManualTimerFactory`, advanced
  explicitly so the ten-second deadline and the two-second rejection linger fire
  without real time. `PendingDelays` and `PendingCount` expose what is scheduled;
  `Advance` fires due callbacks in due-time order.
- `FakeReliableTransport.cs` contains the scripted `FakeReliableClient`,
  `FakeReliableServer`, `FakeReliableEndpoint`, the `FakeServerSession` driver,
  and the recording
  `RecordingRawReliableClientHandler`/`RecordingRawReliableServerHandler`
  application handlers. Delivery is synchronous and caller-driven; outbound
  payloads are cloned into `Sent` for assertions and released with
  `ReleaseSent`.
- `FakeNegotiationSupport.cs` contains the minimal `FakeEndPoint` and the
  `Payload` build/describe helpers.
- `RecordingWrapperLogic` records every negotiation hook, the connected and
  disconnected hooks, and lets a test script the fill/check/response tails.
- `RecordingServerFactory` counts `TryCreate` calls and captures the remote.
- `WrapperPair` links one fake client and one fake server through the public
  wrapper transports, including the deferred server factory.

There are no harness self-tests, so the `Protocol.Harness` category is not
used.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [`Coverage.md`](Coverage.md) — `RawReliableWrapper`

A layer's `Coverage.md` lists current covered tests, planned gaps, and
deferred-by-design gaps.

## Categories

- Suite root category: `Protocol`. Every fixture carries it.
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
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --filter TestCategory=Protocol
```

Filter to the fixture:

```
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --filter "FullyQualifiedName~RawReliableWrapperTests"
```

## Adding tests

1. Put the test in `Tests/RawReliableWrapperTests.cs`; the suite has exactly
   one layer.
2. Drive connection, inbound traffic, and time through the harness. Do not use
   real clocks, timers, sockets, or threads.
3. Assert observable results only: callback order and counts, stop reasons,
   captured envelopes, send results, and ownership outcomes.
4. Release every captured `UnionDataList` that the test owns.
5. Record the test in [`Coverage.md`](Coverage.md) in the same change.

This is a Test Suit, so it must not acquire nested layers. If the subject
becomes an inheritance tree, convert the suite according to the rules.

## Out of scope

- The transport handshake and carrier admission; those belong to the
  `Pontifex.Tests` endpoint/transport suites and the carrier integration
  projects.
- Zip, Reconnectable, and Api protocol logic; those are separate protocol
  suites. This suite tests only the reusable wrapper base.
- Real TCP/Direct transports, sockets, real time, and concurrency stress.
- Wire encoding and carrier framing.
- Logging output and performance.

## Rules

This suite follows [Tests/RULES.md](../../../../RULES.md).
