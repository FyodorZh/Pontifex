# Reliable Api Negotiation Test Suite

Black-box contract tests for the Api negotiation layer that `Pontifex.Api` runs
on top of an already-admitted reliable raw transport: `ClientSideApi`,
`ServerSideApiFactory`, and `ServerSideApiInstance`. Every test runs over
hand-written fake reliable transports with synchronous, caller-driven
delivery: no real time is awaited, and there are no real sockets or networks.
The shared negotiation session accepts a clock seam; the tests inject
`ManualApiNegotiationClock` through the Api's internal constructor overloads,
so deadlines and the rejection linger are driven explicitly. The public
constructors keep the production clock, and disposal still cancels its timers.

## Suite type

**Test Suit.** The subject is the small tightly coupled group formed by
`ClientSideApi`, `ServerSideApiFactory<TApi>`, and
`ServerSideApiInstance<TApi>`.

Root subject: `Pontifex.Api.ClientSideApi`,
`Pontifex.Api.ServerSideApiFactory<TApi>`, and
`Pontifex.Api.ServerSideApiInstance<TApi>`.

## Contract references

- [ClientSideApi.cs](../../../../../Api/Pontifex.Api/Transport/Transport/ClientSideApi.cs)
- [ServerSideApi.cs](../../../../../Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs)
- [ServerSideApiInstance.cs](../../../../../Api/Pontifex.Api/Transport/Transport/ServerSideApiInstance.cs)
- [NegotiationSession.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs)
- [RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs)
- [IRawReliableClientHandler.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClientHandler.cs)
- [IRawReliableServerHandlerFactory.cs](../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServerHandlerFactory.cs)

The Api negotiation envelope is the flat reliable-wrapper envelope
`[version (short), name (UTF-8 string), NegotiationStatus (byte), ...custom elements]`
with version `1` and name `"api"`. The Api server compares only the name and
version and ignores appended elements; the client hook
`AppendNegotiationData` is reserved for future metadata.

## Layer

The layer table is the authoritative declaration of layer name, subject,
directory, and coverage file.

| Layer | Subject | Directory | Coverage |
|---|---|---|---|
| `RawReliableApiNegotiation` | `ClientSideApi`, `ServerSideApiFactory<TApi>`, `ServerSideApiInstance<TApi>` | `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api` | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Namespace = `Pontifex.Tests.Raw.Reliable.Api`.
- Test methods = `Scenario_ExpectedResult`, for example
  `ClientApi_WithWrongVersionResponse_RaisesDisconnectedOnceAndNoConnected`.
- Every fixture carries `[Category("RawReliableApiNegotiation")]`.

## Directory layout

```
Api/
├── README.md
├── Coverage.md                              RawReliableApiNegotiation layer
├── Harness/
│   └── ApiNegotiationHarness.cs
└── Tests/
    └── RawReliableApiNegotiationTests.cs
```

## Harness layout

All harness types are hand-written (no mocking library) and test-only:

- `FakeApiEndpoint` implements `IRawReliableEndpoint`. Delivery is synchronous
  and caller-driven; outbound payloads are cloned into `Sent` for assertions
  and released with `ReleaseSent`.
- `FakeApiClient` and `FakeApiServer` are scripted `IRawReliableClient`/
  `IRawReliableServer` implementations whose connection, inbound traffic, and
  server sessions are driven by the test.
- `ApiNegotiationPair` links the real `ClientSideApi` and
  `ServerSideApiInstance<TApi>` over the fake pair and records the observable
  callbacks and the Api server start count. Its `ClientToServer`/
  `ServerToClient` hooks let a test replace or drop one direction.
- `TailAppendingClientSideApi` overrides `AppendNegotiationData` to prove the
  append-only hook.
- `ManualApiNegotiationClock` implements `Pontifex.Utils.ITimerFactory`; tests advance it
  to fire the negotiation deadline and the rejection linger without real time.

There are no harness self-tests, so the
`RawReliableApiNegotiation.Harness` category is not used.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [`Coverage.md`](Coverage.md) — `RawReliableApiNegotiation`

A layer's `Coverage.md` lists current covered tests, planned gaps, and
deferred-by-design gaps.

## Categories

- Suite root category: `RawReliableApiNegotiation`. Every fixture carries it.
- Additional filtering categories: none.
- Harness category: not used (no harness self-tests).

Non-default facets (real I/O, load, real time, sockets) must not be added to
this suite; they belong to a carrier or integration suite with their own
category.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj --filter TestCategory=RawReliableApiNegotiation
```

Filter to the fixture:

```
dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj --filter "FullyQualifiedName~RawReliableApiNegotiationTests"
```

## Adding tests

1. Put the test in `Tests/RawReliableApiNegotiationTests.cs`; the suite has
   exactly one layer.
2. Drive connection, inbound traffic, and server sessions through the harness.
   Do not use real clocks, timers, sockets, or threads.
3. Assert observable results only: callback order and counts, stop reasons,
   captured envelopes, and Api start counts.
4. Release every captured `UnionDataList` that the test owns.
5. Record the test in [`Coverage.md`](Coverage.md) in the same change.

This is a Test Suit, so it must not acquire nested layers. If the subject
becomes an inheritance tree, convert the suite according to the rules.

## Out of scope

- The transport handshake and carrier admission; those belong to the core
  endpoint and carrier suites.
- The `TransportPipeSystem` and the declared Api pipes; the Api tests in
  `Api/Pontifex.Api.Tests` cover them over in-memory pipes.
- Real TCP/Direct transports, sockets, real time, and concurrency stress.
- Zip, Reconnectable, and logger wrappers.
- Logging output and performance.

## Rules

This suite follows [Tests/RULES.md](../../../../RULES.md).
