# Zip Reliable Wrapper Test Suite

Contract tests for the `zip` reliable wrapper: it negotiates its own `"zip"`
dataset over any `RawReliable` transport and compresses/decompresses every
application payload in place. Every test runs over hand-written fake reliable
transports and a manual negotiation clock: no real time, timers, sockets, or
networks.

## Suite type

**Test Suit.** The subject is the small tightly coupled group formed by the
zip binding and the zip client/server logic it constructs.

Root subject: `Pontifex.Raw.Reliable.Zip.RawReliableZipBinding`,
`Pontifex.Raw.Reliable.Zip.RawReliableZipClientLogic`, and
`Pontifex.Raw.Reliable.Zip.RawReliableZipServerLogic`.

## Contract references

- [RawReliableZipBinding.cs](../../../../../Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipBinding.cs)
- [RawReliableZipClientLogic.cs](../../../../../Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipClientLogic.cs)
- [RawReliableZipServerLogic.cs](../../../../../Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipServerLogic.cs)
- [CompressorLogic.cs](../../../../../Protocols/Pontifex.Protocol.Zip/CompressorLogic.cs)
- [ZLibUtils.cs](../../../../../Protocols/Pontifex.Protocol.Zip/ZLibUtils.cs)
- [ZipInfo.cs](../../../../../Protocols/Pontifex.Protocol.Zip/ZipInfo.cs)
- [RawReliableWrapperClient.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperClient.cs)
- [RawReliableWrapperServer.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperServer.cs)
- [IRawReliableWrapperLogic.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/IRawReliableWrapperLogic.cs)
- [RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs)

The wrapper envelope is flat:
`[version (short), name (UTF-8 string), NegotiationStatus (byte), ...custom layer elements]`.
The zip layer uses version `1` and name `"zip"`; it appends no custom elements
in either direction. The application payload is the compressed dataset: the
compressible side serializes the flat list, deflates it, and replaces the list
with a single byte-array element.

## Layer

The layer table is the authoritative declaration of layer name, subject,
directory, and coverage file.

| Layer | Subject | Directory | Coverage |
|---|---|---|---|
| `RawReliableZip` | `RawReliableZipBinding`, `RawReliableZipClientLogic`, `RawReliableZipServerLogic` | `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip` | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Test file = `<Subject>Tests.cs`; fixture class = `<Subject>Tests`.
- Namespace = `Pontifex.Tests.Raw.Reliable.Zip`; harness types use
  `Pontifex.Tests.Raw.Reliable.Zip.Harness`.
- Test methods = `Scenario_ExpectedResult`, for example
  `VersionMismatch_StopsClientWithHandshakeRejectedAndSkipsFactory`.
- Every fixture carries `[Category("Zip")]`.

## Directory layout

```
Zip/
├── README.md
├── Coverage.md                              RawReliableZip layer
├── Harness/
│   ├── FakeZipTransport.cs
│   ├── ZipPair.cs
│   └── ZipTestSupport.cs
└── Tests/
    └── RawReliableZipTests.cs
```

There are no groups under `Tests/`: the suite has one layer and one test file.

## Harness layout

All harness types are hand-written (no mocking library) and test-only:

- `FakeZipTransport.cs` contains the scripted `IRawReliableClient`,
  `IRawReliableServer`, `IRawReliableEndpoint`, and one server-session driver.
  Delivery is synchronous and caller-driven; outbound payloads are cloned into
  `Sent` for assertions and released with `ReleaseSent`.
- `ZipTestSupport.cs` contains the manual `Pontifex.Utils.ITimerFactory` seam, a minimal
  `IEndPoint`, and payload build/describe helpers.
- `ZipPair.cs` links fake clients and one fake server through the public
  `RawReliableWrapperClient`/`RawReliableWrapperServer` bases and the internal
  `RawReliableZipClientLogic`/`RawReliableZipServerLogic`, including the deferred
  server factory and recording application handlers. The server receives a
  logic factory, exactly like the production binding, so each accepted session
  gets its own compressor; `ConnectClient()` adds a client to a running server
  to exercise sequential sessions.

There are no harness self-tests, so the `Zip.Harness` category is not used.

## Coverage

Coverage is recorded per layer, next to the layer's tests:

- [`Coverage.md`](Coverage.md) — `RawReliableZip`

A layer's `Coverage.md` lists current covered tests, planned gaps, and
deferred-by-design gaps.

## Categories

- Suite root category: `Zip`. Every fixture carries it.
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
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --filter TestCategory=Zip
```

Filter to the fixture:

```
dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --filter "FullyQualifiedName~RawReliableZipTests"
```

## Adding tests

1. Put the test in `Tests/RawReliableZipTests.cs`; the suite has exactly one
   layer.
2. Drive connection, inbound traffic, and time through the harness. Do not use
   real clocks, timers, sockets, or threads.
3. Assert observable results only: callback order and counts, stop reasons,
   captured envelopes and inner send buffers, send results, and ownership
   outcomes.
4. Release every captured `UnionDataList` that the test owns.
5. Record the test in [`Coverage.md`](Coverage.md) in the same change.

This is a Test Suit, so it must not acquire nested layers. If the subject
becomes an inheritance tree, convert the suite according to the rules.

## Out of scope

- The `zip(nested)` factory formula and the binding parameter surface; those
  are covered by `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`.
- The reusable wrapper negotiation driver, admission gate, lifecycle, and
  rejection linger; those belong to the
  [`RawReliableWrapper` suite](../Protocol/README.md).
- Reconnectable and Api negotiation datasets; those are separate protocol
  suites.
- Real TCP/Direct transports, sockets, real time, and concurrency stress.
- Wire encoding and carrier framing.
- Logging output and performance.

## Rules

This suite follows [Tests/RULES.md](../../../../RULES.md).
