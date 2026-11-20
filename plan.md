# Replace the RawReliableAck/RawReliableNoAck split with one handshake-bearing RawReliable transport

## Goal

After this plan is complete, `Pontifex` has exactly one reliable transport
contract, `RawReliable`, with a mandatory transport handshake
(`version + name + messageMaxByteSize`) that confirms connection, rejects
version/name mismatches, and negotiates `MessageMaxByteSize` as the minimum.
The `RawReliableAck`/`RawReliableNoAck` split, all ACK contracts, all ACK
carriers, and all ACK tests are deleted. `tcp` and `direct` are the two
`RawReliable` carriers. `zip` and `reconnectable` are `RawReliable` wrappers
with their own negotiation datasets. `log` is a transparent `RawReliable`
wrapper. `Pontifex.Api` uses `RawReliable` and its own `"api"` negotiation.
The `RawUnreliableNoAck -> RawReliableAck` converter becomes a
`RawUnreliableNoAck -> RawReliable` converter with the same handshake over its
`DeliveryManager`.

## Context

The repository currently contains two sibling reliable raw transports:

- `Pontifex.Raw.Reliable.Ack` — ACK admission payloads, ACK response,
  `IRawReliableAckServerAcknowledger`, ACK client/server handlers.
- `Pontifex.Raw.Reliable.NoAck` — server handler factory, no payload.

They share the generic transport bases (`RawReliableClientTransport<T>`,
`RawReliableServerTransport<T>`), the endpoint model, delivery guarantees,
buffer ownership, and lifecycle rules. Their carriers duplicate each other:
`RawReliableNoAckTcpClientEndpoint` is ~98% identical to
`RawReliableAckTcpClientEndpoint`, and the ACK TCP server endpoint contains a
~750-line `AcceptHandshake` that the NoAck carrier lacks.

The agreed target is:

- `RawReliable` is the only reliable transport. It performs a fixed,
  transport-owned handshake before `OnConnected`:
  `version (short) + name (string) + messageMaxByteSize (int)`.
- The handshake is a flat `UnionDataList`, not a DTO.
- `HandshakeMaxByteSize` defaults to 128 bytes and is configurable per layer.
- Version is a single number per layer; any mismatch is rejected.
- `MessageMaxByteSize` is negotiated as `min(client, server)` at the transport
  layer.
- `Reject` carries the server version, server name, status, and optional text.
- Reject linger: 2 s for TCP, 0 for Direct, 2 s for wrappers; configurable.
- The Tier-1 transport handshake uses separate frame types
  (`HandshakeRequestMessage`/`HandshakeResponseMessage`/`HandshakeRejectMessage`)
  and a flat `UnionDataList` dataset.
- `zip`, `reconnectable`, and `api` each add an independent negotiation layer
  with a flat `UnionDataList` envelope
  `[version, name, NegotiationStatus, ...custom-layer-elements]`. The status is
  the discriminator, so a custom `int` cannot be mistaken for a status. They
  can be stacked.
- `log` is transparent: no negotiation, no extra round trip, no name/version.
- There is no ACK contract, no ACK payload, no acknowledger, no `ack(...)`
  scheme, and no `TransportType.RawReliableAck`.
- `RawUnreliableAck`, `RawUnreliableNoAck`, `RR*`, and UDP are out of scope.

### Naming decisions

| Current | Target |
| --- | --- |
| `Pontifex.Raw.Reliable.NoAck` namespace | removed; types move to `Pontifex.Raw.Reliable` |
| `Pontifex.Raw.Reliable.Ack` namespace | removed |
| `IRawReliableNoAckClient` | `IRawReliableClient` |
| `IRawReliableNoAckClientHandler` | `IRawReliableClientHandler` (with `OnConnected(IRawReliableEndpoint)`) |
| neutral `IRawReliableClientHandler` | `IRawReliableClientHandlerBase` (temporary; removed in Stage 10) |
| `IRawReliableNoAckServer` | `IRawReliableServer` |
| `IRawReliableNoAckServerHandler` | removed; `IRawReliableServerHandler` is used |
| `IRawReliableNoAckServerHandlerFactory` | `IRawReliableServerHandlerFactory` |
| `IRawReliableNoAckClientEndpointImp` | removed; `IRawReliableClientEndpointImp` |
| `IRawReliableNoAckServerEndpointImp` | removed; `IRawReliableServerEndpointImp` |
| `RawReliableNoAckClientTransport` | `RawReliableEndpointClientTransport<TClientHandler>` (non-generic usage in carriers) |
| `RawReliableNoAckServerTransport` | `RawReliableAdmissionServerTransport` (non-generic) |
| `RawReliableNoAckClientEndpoint` | `RawReliableHandshakeClientEndpoint` |
| `RawReliableNoAckServerEndpoint` | `RawReliableHandshakeServerEndpoint` |
| `StopOnceNoAckServerHandler` | `StopOnceServerHandler` |
| `RawReliableNoAckTcp*` | `RawReliableTcp*` |
| `RawReliableAckTcp*` | deleted |
| `RawReliableAckDirect*` | replaced by `RawReliableDirect*` |
| `IRawReliableAck*` | deleted |
| `RawReliableAck*` | deleted |
| `TransportType.RawReliableNoAck` | `TransportType.RawReliable` |
| `TransportType.RawReliableAck` | deleted |
| `RawUnreliableNoAckToRawReliableAck*` | `RawUnreliableNoAckToRawReliable*` |
| Test layer `.../Client/NoAck` | `.../Client/RawReliableEndpointClientTransport` |
| Test layer `.../Server/NoAck` | `.../Server/RawReliableAdmissionServerTransport` |
| Endpoint test layer `.../Client/NoAck` | `.../Client/RawReliableHandshakeClientEndpoint` |
| Endpoint test layer `.../Server/NoAck` | `.../Server/RawReliableHandshakeServerEndpoint` |

### Why each stage is a separate checkpoint

| Stage | Reason for a separate review boundary |
| --- | --- |
| 1 | Purely mechanical rename; isolates the high-churn type/path changes before any behavior change. |
| 2 | First end-to-end handshake on a real carrier; the handshake engine and TCP integration are one vertical slice. |
| 3 | Direct exercises synchronous in-process handshake and zero linger; distinct verification from TCP. |
| 4 | New reusable negotiation utility and wrapper base; unit-tested with a fake transport before any protocol consumes it. |
| 5 | First real wrapper consumer (Zip); proves the wrapper base and compression wiring. |
| 6 | Reconnectable adds session resume/reattach, the most complex custom tail; distinct verification. |
| 7 | Api, TestUI, and the Api transport stacks are coupled by the Api type change; one vertical slice ending in Api tests. |
| 8 | Converter changes reliability emulation and the default converter graph; distinct factory/formula verification. |
| 9 | Logger merge is a small independent surface with its own transparency tests. |
| 10 | Final deletion and factory/scheme cleanup; only safe once every consumer is migrated. |

## Constraints

- `Pontifex`, `Pontifex.Factory`, `Pontifex.Transport.Net`,
  `Pontifex.Transport.Direct`, `Pontifex.Protocol`, `Pontifex.Protocol.Zip`,
  `Pontifex.Protocol.Reconnectable`, and `Pontifex.Api` target
  `netstandard2.1` with `LangVersion` 9. Do not use C# 10+ syntax in them.
- Test projects and `Pontifex.TestUI` target `net10.0` with `LangVersion` 14
  and `ImplicitUsings` enabled.
- Libraries use `Nullable=enable` and `TreatWarningsAsErrors=true`; keep
  warnings at zero in library projects.
- Do not change `RawUnreliableAck`, `RawUnreliableNoAck`, `RR*`, UDP, or their
  tests, except where a shared enum/type rename forces a reference update.
- Do not preserve backward compatibility for `RawReliableAck`/`RawReliableNoAck`.
- The transport handshake and all negotiation datasets are internal to the
  layer that owns them. External users must not observe the dataset; only a
  coarse `StopReason` and the agreed `MessageMaxByteSize` are public.
- Each stage must leave `Pontifex.sln` buildable and the affected tests green.
- The migration is a strangler: the core Ack contract/carrier family remains
  until Stage 10, while the new RawReliable family is built alongside it.
  Protocol layers (Zip, Reconnectable, Api, converter) are migrated in Stages
  5–8 and their Ack implementations are deleted there.
- Temporary binding scheme names `tcp-noack`, `direct-noack`, and `log-noack`
  are used until Stage 10 to avoid duplicate registrations. For the `tcp` and
  `direct` bindings the handshake `name` is the final contract name
  (`"tcp"`, `"direct"`), independent of the binding scheme; it is an explicit
  constructor parameter on both client and server. The synthetic converter
  transport uses `"converter"` as its handshake name.
- `grep -R` is the search tool; `rg` is not installed. Use:
  `grep -R -n --include='*.cs' --include='*.csproj' -E "<pattern>" .`

## Non-goals

- No wire compatibility and no source/binary compatibility.
- No new full carrier-independent conformance suite. The ignored Ack
  conformance suite is deleted, not ported.
- No changes to `RawUnreliable*` or `RR*` contracts or carriers beyond enum
  reference updates.
- No performance tuning beyond avoiding new per-message allocations.
- No new TestUI features; only migration and compilation.
- No authentication, encryption, or admission payloads in the transport
  handshake.

## Assumptions

1. `UnionDataList` supports the required flat elements (`short`, `int`,
   `string` stored as UTF-8 array, byte arrays). Evidence:
   `Pontifex/Utils/UnionData/UnionData.cs` and
   `Pontifex/Utils/UnionData/UnionDataList/UnionDataList.cs`.
2. `UnionDataList` does not support nested `UnionDataList` as an element, so
   the envelope is flat. Evidence: `UnionData.cs` constructors.
3. `RawEndpoint.MessageMaxByteSize` is a read-only, non-virtual
   `public int { get; }` set in the base constructor and read by
   `ScheduleToSend`; the negotiated value must be applied there, not in a
   derived shadow property. Evidence:
   `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`.
4. The Ack TCP accept handshake can be reused as the transport handshake
   engine. Evidence:
   `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckTcpServerEndpoint.cs`
   (`AcceptHandshake`).
5. The Ack/NoAck carriers are near-duplicates. Evidence: `diff` counts in the
   conversation; `NoAck/Specification.md` §7.
6. The converter is the only registered converter and currently targets
   `RawReliableAck`. Evidence: `Pontifex/Converters/ConvertersGraph.cs`.
7. `AppendAckData` has no overrides and `ServerSideApiFactory` instance
   factories ignore the ACK data. Evidence: `grep -R AppendAckData` and
   `Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs`.
8. Baseline is green: `dotnet build Pontifex.sln` succeeds and all test
   projects pass (2274 passed, 67 skipped). Evidence: baseline run.
9. `HandshakeMaxByteSize = 128` is a default, configurable per layer; a layer
   may raise it if its payload needs more.
10. `Reject` carries both server version and server name.
11. The `IRawReliableClientHandlerBase` interface is a temporary migration
    artifact and is removed in Stage 10.
12. For the `tcp`/`direct` bindings the handshake `name` is the final
    contract name (`"tcp"`, `"direct"`), not the temporary binding scheme
    name; the synthetic converter transport uses `"converter"`.

## Prerequisites

- .NET SDK 10.0.102 (or compatible) installed.
- Clean working tree:
  `git status --porcelain` prints nothing.
- Baseline:
  `dotnet build Pontifex.sln -v minimal` succeeds.
- Baseline tests (optional but recommended):
  `dotnet test Pontifex.sln --no-build --logger "console;verbosity=minimal"`.

## Stages

### Stage 1 — Rename the NoAck reliable family to RawReliable

**Objective.** The NoAck reliable contract, implementation, carrier, logger,
and test layers are renamed to `RawReliable`; `RawReliableAck` remains
present and working; no behavior changes.

**Inputs.** Baseline green tree.

**Actions.**

1. In `Pontifex/Abstractions/Transports/Raw/Reliable`:
   - First rename the current neutral `IRawReliableClientHandler.cs` to
     `IRawReliableClientHandlerBase.cs` (no members). Do this before adding
     the new file with the old name.
   - Move `NoAck/Client/IRawReliableNoAckClient.cs` to
     `Client/IRawReliableClient.cs` and rename the interface.
   - Move `NoAck/Client/IRawReliableNoAckClientHandler.cs` to
     `Client/IRawReliableClientHandler.cs`; keep
     `OnConnected(IRawReliableEndpoint)`.
   - Move `NoAck/Server/IRawReliableNoAckServer.cs` to
     `Server/IRawReliableServer.cs` and rename it.
   - Delete `NoAck/Server/IRawReliableNoAckServerHandler.cs`; use the existing
     `IRawReliableServerHandler`.
   - Move `NoAck/Server/IRawReliableNoAckServerHandlerFactory.cs` to
     `Server/IRawReliableServerHandlerFactory.cs` and rename it.
   - Update `IRawReliableClientTransport<TClientHandler>` constraint from
     `IRawReliableClientHandler` to `IRawReliableClientHandlerBase`.
   - Repoint `Ack/Client/IRawReliableAckClientHandler.cs` to
     `: IRawReliableClientHandlerBase`; otherwise the name lookup binds it to
     the new two-member `IRawReliableClientHandler` and every Ack client
     handler is forced to implement `OnConnected` (CS0535).
   - Repoint both neutral recording doubles
     (`Tests/Pontifex.Tests/Endpoints/RawReliable/Client/Harness/RecordingRawReliableClientHandler.cs`
     and
     `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/Harness/RecordingRawReliableClientHandler.cs`)
     and the `TestRawReliableClientTransport` generic argument to
     `IRawReliableClientHandlerBase`.
   - In `Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs`,
     change the class constraint on `RawReliableClientTransport<TClientHandler>`
     from `IRawReliableClientHandler` to `IRawReliableClientHandlerBase` too;
     otherwise the renamed `RawReliableEndpointClientTransport<T>` (constrained
     to the base) cannot derive from it (CS0311). Stage 10 restores it to
     `IRawReliableClientHandler`.
   - Move `NoAck/Specification.md` to `Raw/Reliable/Specification.md`
     (temporary merged spec; Stage 10 edits it). Delete the empty `NoAck`
     code folder.
2. In `Pontifex/Implementations/Raw/Reliable`:
   - Rename `NoAck/RawReliableNoAckClientTransport.cs` to
     `RawReliableEndpointClientTransport.cs`; rename the class to
     `RawReliableEndpointClientTransport<TClientHandler>` and update the
     constraint to `IRawReliableClientHandlerBase`.
   - Rename `NoAck/RawReliableNoAckServerTransport.cs` to
     `RawReliableAdmissionServerTransport.cs`; keep it non-generic, deriving
     from `RawReliableServerTransport<IRawReliableServerHandler>`.
   - Rename `NoAck/StopOnceNoAckServerHandler.cs` to
     `StopOnceServerHandler.cs`; implement `IRawReliableServerHandler` only.
   - Delete the `NoAck` folder.
3. In `Pontifex/Implementations/Endpoints/Raw/Reliable`:
   - Rename `NoAck/RawReliableNoAckClientEndpoint.cs` to
     `RawReliableHandshakeClientEndpoint.cs`; rename the class and use
     `IRawReliableClientHandler` as its handler type.
   - Rename `NoAck/RawReliableNoAckServerEndpoint.cs` to
     `RawReliableHandshakeServerEndpoint.cs`; rename the class and use
     `IRawReliableServerHandler`.
   - Delete the two NoAck endpoint marker interfaces; use
     `IRawReliableClientEndpointImp`/`IRawReliableServerEndpointImp`.
   - Change the `RawReliableClientEndpoint` constructor parameter from
     `IRawReliableClientHandler` to `IRawReliableClientHandlerBase` so the Ack
     endpoint (which passes `IRawReliableAckClientHandler`) still compiles.
   - Delete the `NoAck` folder.
4. In `Pontifex/Implementations/Raw/Reliable/Ext/IRawReliableServerHandler_Ext.cs`:
   - Add `Test`/`GetSafe` overloads for `IRawReliableServerHandler` (the
     current file has only Ack and NoAck overloads).
   - Keep the private `SafeServerHandler`/`TestServerHandler` implementing
     `IRawReliableAckServerHandler` (which extends `IRawReliableServerHandler`)
     until Stage 10, but also expose them as `IRawReliableServerHandler` so
     the base overloads work. Do not remove the Ack marker yet; the Ack
     transport still needs the Ack-specific return types.
   - Remove the `IRawReliableNoAckServerHandler` overloads.
   - In `Pontifex/Wrappers/Logger/RawReliableServerHandlerLogger.cs`, change
     the implemented interfaces from
     `IRawReliableAckServerHandler, IRawReliableNoAckServerHandler` to
     `IRawReliableAckServerHandler` (which extends `IRawReliableServerHandler`);
     the NoAck marker is gone.
5. In `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck`:
   - Rename the namespace to `Pontifex.Raw.Reliable.Tcp`.
   - Rename classes/files:
     `RawReliableNoAckTcpClientTransport` -> `RawReliableTcpClientTransport`,
     `RawReliableNoAckTcpServerTransport` -> `RawReliableTcpServerTransport`,
     `RawReliableNoAckTcpClientEndpoint` -> `RawReliableTcpClientEndpoint`,
     `RawReliableNoAckTcpServerEndpoint` -> `RawReliableTcpServerEndpoint`,
     `RawReliableNoAckTcpBinding` -> `RawReliableTcpBinding`,
     `TcpNoAckInfo` -> `RawReliableTcpInfo`.
   - `RawReliableTcpInfo` gets `public const string TransportName = "tcp-noack";`
     (binding scheme) and `public const string HandshakeName = "tcp";`.
   - Binding scheme stays `"tcp-noack"`; type `TransportType.RawReliable`.
6. In `Pontifex/Abstractions/TransportType.cs`:
   - Rename `RawReliableNoAck` to `RawReliable` and keep its numeric value.
   - Keep `RawReliableAck` until Stage 10.
   - Update the enum comment.
7. Rename the NoAck loggers to the final names now (this makes the Stage 1
   search gate pass):
   - `RawReliableNoAckClientLogger` -> `RawReliableClientLogger`.
   - `RawReliableNoAckServerLogger` -> `RawReliableServerLogger`.
   - `RawReliableNoAckLoggerBinding` -> `RawReliableLoggerBinding`; keep
     scheme `"log-noack"` temporarily; nested `Exactly(RawReliable)`.
   - Move the three files from
     `namespace Pontifex.Raw.Reliable.NoAck.Logger` to
     `namespace Pontifex.Raw.Reliable.Logger`; update the `using` in the
     client logger.
   - Keep the Ack loggers/binding until Stage 9.
8. Update every reference to the renamed types across:
   - `Pontifex`, `Pontifex.Factory`, `Pontifex.TestUI`,
     `Pontifex.Transport.Net`, `Pontifex.Transport.Direct`, `Protocols`,
     `Api`, and all test projects.
   - All pre-existing `IRawReliableClientHandler` identifiers that are not
     the new RawReliable handler (the Ack marker, both neutral recorders, the
     parent harness generic arguments) become
     `IRawReliableClientHandlerBase`; Stage 10 re-types them to the final
     handler. Do not add dead one-argument `OnConnected` stubs to Ack
     handlers.
   - Use the search gate in Verification to find leftovers.
9. Test layer rename (mechanical, preserving the Abstract Test Suit model):
   - `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/NoAck` ->
     `.../Client/RawReliableEndpointClientTransport`; rename the fixture to
     `RawReliableEndpointClientTransportTests` and the harness to
     `TestRawReliableEndpointClientTransport`.
   - `.../Server/NoAck` ->
     `.../Server/RawReliableAdmissionServerTransport`; fixture
     `RawReliableAdmissionServerTransportTests`, harness
     `TestRawReliableAdmissionServerTransport`.
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/NoAck` ->
     `.../Client/RawReliableHandshakeClientEndpoint`; fixture
     `RawReliableHandshakeClientEndpointTests`, harness
     `TestRawReliableHandshakeClientEndpoint`.
   - `.../Server/NoAck` -> `.../Server/RawReliableHandshakeServerEndpoint`;
     fixture `RawReliableHandshakeServerEndpointTests`, harness
     `TestRawReliableHandshakeServerEndpoint`.
   - Do not merge into the parent `Client`/`Server` layers; they own the
     generic base subjects and already contain their own harnesses.
   - Update `Tests/Pontifex.Tests/Transports/README.md`,
     `Tests/Pontifex.Tests/Endpoints/README.md`, and the affected
     `Coverage.md` files with the new layer paths/names.
   - Move `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/NoAck/*` to
     `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/RawReliable/*` and
     rename namespaces/fixtures/classes.
10. Loose test files:
    - `Tests/Pontifex.Tests/RawReliableServerHandlerExtTests.cs`: retarget
      the NoAck region to `IRawReliableServerHandler` (the new base
      overloads); keep the Ack region until Stage 10.
    - `Tests/Pontifex.Tests/RawReliableNoAckClientLoggerTests.cs` ->
      `RawReliableClientLoggerTests.cs`; server equivalent.
    - `Tests/Pontifex.Tests/RawReliableServerHandlerLoggerTests.cs` still
      asserts the Ack endpoint wrapper; leave it until Stage 9.
11. `Pontifex.TestUI` does not reference any `NoAck` type (it uses the Ack
    carriers and ACK handlers); no TestUI change is required in this stage.
    TestUI is migrated as a whole in Stage 7, where the Api and the transport
    stacks also change.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds with 0 errors.
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Pontifex.sln --no-build --logger "console;verbosity=minimal"` passes (covers Factory/Formula/Api/Transport.Tests, whose harness data was edited).
- `grep -R -n --include='*.cs' --include='*.csproj' -E "RawReliableNoAck|IRawReliableNoAck" .` returns no matches.

**Definition of done.** The solution builds; all affected tests pass; no
`NoAck` reliable **code** names remain (the moved
`Raw/Reliable/Specification.md` still contains the old term and is rewritten
in Stage 10); `RawReliableAck` still compiles and its tests still pass.

**Risks / notes.** The first rename of the neutral client handler to
`IRawReliableClientHandlerBase` must happen before the NoAck handler takes the
old name, or the target file already exists. The test layers stay separate
because their subjects are distinct abstract types.

---

### Stage 2 — Implement the transport handshake in the core and the TCP carrier

**Objective.** Every RawReliable TCP connection performs the mandatory
handshake before either handler sees `OnConnected`; version/name mismatches,
factory rejection, timeout, and `MessageMaxByteSize` negotiation are
implemented; reject linger is 2 s and configurable.

**Inputs.** Stage 1 complete.

**Actions.**

1. Add `Pontifex/Implementations/Raw/Reliable/Handshake/RawReliableHandshake.cs`
   (internal static):
   - `public const short Version = 1;`
   - `public const int DefaultMaxByteSize = 128;`
   - `internal enum HandshakeStatus : byte { Accepted = 0, Rejected, VersionMismatch, NameMismatch, CapacityExceeded, Timeout }`
   - `BuildRequest(string name, int messageMaxByteSize)` returns a
     `UnionDataList` with elements `[Version, name, messageMaxByteSize]`.
   - `TryParseRequest(UnionDataList data, out string name, out int max)`
     validates exactly three elements, the types, and `max >= 1`.
     `TryHandshake` then rejects with `CapacityExceeded` when
     `max < HandshakeMaxByteSize` (the server must be able to send its
     response).
   - `BuildResponse(short version, string name, int agreedMax)` returns
     `[version, name, agreedMax]`, and
     `TryParseResponse(UnionDataList data, out short version, out string name, out int max)`
     validates exactly three elements and the types. The client validates the
     server version from the response.
   - `BuildReject(short serverVersion, string serverName, HandshakeStatus status, string? text)`
     and `TryParseReject(...)`.
   - Name comparison uses UTF-8 content equality.
   - Add `internal readonly struct HandshakeOutcome` with
     `HandshakeStatus Status`, `IRawReliableServerHandler? Handler`,
     `UnionDataList? Response`, `int AgreedMaxByteSize`. The success
     predicate is `Status == HandshakeStatus.Accepted && Handler != null`;
     every other status is a reject. The carriers branch only on that
     predicate.
2. Add `InternalsVisibleTo` in `Pontifex/Pontifex.csproj` for
   `Pontifex.Transport.Net` and `Pontifex.Transport.Direct` (for the internal
   handshake types), `Pontifex.Protocol`, `Pontifex.Protocol.Zip`,
   `Pontifex.Protocol.Reconnectable`, `Pontifex.Api`, and the test assemblies
   `Pontifex.Tests`, `Pontifex.Transport.Net.Tests`,
   `Pontifex.Transport.Direct.Tests`, `Pontifex.Transports.Tests`,
   `Pontifex.Transport.Tests`, `Pontifex.Factory.Tests`, and
   `Pontifex.Api.Tests`.
3. In `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`:
   - Replace the get-only `MessageMaxByteSize` with a
     `private volatile int _messageMaxByteSize;` field, a public getter, and
     a protected `SetMessageMaxByteSizeOnce(int value)`.
   - The constructor sets the field.
   - `ScheduleToSend` keeps reading the property, so the negotiated value is
     enforced by the existing size check.
   - `SetMessageMaxByteSizeOnce` uses a one-shot latch
     (`Interlocked.Exchange` on a `_messageMaxByteSizeSet` field). It throws
     `InvalidOperationException` if the value is less than 1, if it was
     already set, or if the base endpoint stage is `Stopping`/`Stopped`.
     It must **not** key off the reliable `LogicallyConnected` stage: the
     client is already in the low-level `Connected` stage while the handshake
     response is processed. Allowed states are `Starting` and `Started`.
     Add `protected void MarkMessageMaxByteSizeSet()` (and make the latch
     field private) so the server endpoint can seal the value. The base
     constructor sets the value **without** arming the latch; the server
     endpoint passes the agreed value to the base constructor and then calls
     `MarkMessageMaxByteSizeSet()`, bypassing the state-checked setter (the
     base stage is `Constructed` at construction time).
     The client completion action first checks that the endpoint is still
     running/started; if a stop won the race, it returns without calling the
     setter instead of throwing `InvalidOperationException` (which would turn
     a normal stop race into `ExceptionFail`).
     Validate `messageMaxByteSize >= handshakeMaxByteSize` in the concrete
     `RawReliableTcpServerTransport`/`RawReliableDirectServerTransport`
     constructor, **after** `_messageMaxByteSize` is resolved, so the server
     can always send the handshake response. The base
     `RawReliableAdmissionServerTransport` constructor does not receive the
     message max and must not perform this check.
4. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - Add `HandshakeRequestMessage = 7`, `HandshakeResponseMessage = 8`,
     `HandshakeRejectMessage = 9` to `MessageType`. Keep 5/6 for the Ack
     endpoints until Stage 10.
5. In `RawReliableHandshakeClientEndpoint.cs`:
   - Add a separate private logical `_stage` field
     (`Constructed`, `HandshakeSent`, `LogicallyConnected`,
     `LogicallyDisconnected`), exactly like `RawReliableAckClientEndpoint`.
     Do not add these values to `RawReliableEndpoint.State`:
     `IsLowLevelConnected`, `ProcessInboundMessage`, and the sealed
     dispatcher must keep using the reliable `Connected` state so the
     handshake response is dispatched, not dropped.
     `GetIsConnected` returns
     `IsLowLevelConnected && _stage == LogicallyConnected`.
   - Add constructor parameters `string handshakeName`,
     `int handshakeMaxByteSize = 128`, `TimeSpan? handshakeTimeout = null`
     (default 10 s). Validate
     `messageMaxByteSize >= handshakeMaxByteSize` at construction.
   - On low-level `OnConnected`, do **not** call `base.OnConnected()` (that
     starts keep-alive). Build the request with `handshakeName` and the
     configured max, send it via `ScheduleToSend`, and set stage
     `HandshakeSent`. Start the timeout through a
     `protected virtual IDisposable? CreateHandshakeTimer(TimeSpan timeout,
     Action onTimeout)` seam; the default returns a `System.Threading.Timer`,
     tests inject a manual timer, and Direct overrides it to return `null`.
   - In `ProcessInboundMessage`, handle `HandshakeResponseMessage` by
     scheduling a completion action on `_actionQueue`: **stop and dispose the
     handshake timer first**, validate version, validate name, call
     `SetMessageMaxByteSizeOnce(min(local, server))`, set stage
     `LogicallyConnected`, call `base.OnConnected()` (start keep-alive), then
     call `NotifyConnectedCore(() => _handler.OnConnected(this))` (this arms
     the connected latch, so the later `OnDisconnected` is delivered; do not
     call the handler directly).
   - Handle `HandshakeRejectMessage` by scheduling a stop action: stop and
     dispose the timer, parse the status, map
     `VersionMismatch`/`NameMismatch` to `ProtocolMismatch`,
     `Rejected`/`CapacityExceeded` to `HandshakeRejected`, `Timeout` to
     `TimeOut`, and do not call `OnConnected`.
   - On timeout, schedule the same stop action with `TimeOut` and dispose the
     timer.
   - Add a test that keeps a healthy connection alive past a short configured
     `handshakeTimeout` and asserts it is still connected with no `OnStopped`
     (proves the timer is stopped on success).
   - `GetIsConnected` returns `IsLowLevelConnected && stage == LogicallyConnected`.
   - `FailHard`/`BeginDisconnect` set the stage to disconnected and release
     the timer.
6. In `RawReliableHandshakeServerEndpoint.cs`:
   - Add `SetPendingHandshakeResponse(UnionDataList response)` and
     `ReleasePendingHandshakeResponse()`.
   - On `OnConnected`, send the pending response; if it is missing or
     `ScheduleToSend` returns anything other than `SendResult.Ok`, stop with
     `TextFail` and do **not** call `NotifyConnected` (a session whose
     response could not be sent is not admitted). Otherwise call
     `NotifyConnected()`; if it returns `false` (the handler threw), return
     without scheduling the watchdog; then call `base.OnConnected()` so the
     inherited idle-session watchdog initializes.
   - Release the pending response on `FailHard`, `BeginDisconnect`, and the
     terminal `Stopped` event.
7. In `RawReliableAdmissionServerTransport.cs`:
   - Add constructor parameters `string handshakeName`,
     `int handshakeMaxByteSize = 128`, `TimeSpan? handshakeTimeout = null`
     (10 s), `TimeSpan? rejectLinger = null` (2 s).
   - Add `internal HandshakeOutcome TryHandshake(IEndPoint remote, UnionDataList request)`:
     validate version and name first; on mismatch return the status without
     calling the factory; reject with `CapacityExceeded` if
     `request.max < HandshakeMaxByteSize` or `< 1` (do not create an
     endpoint that cannot send the response); compute
     `min(request.max, MessageMaxByteSize)`; call the factory through the
     existing serialized `_factoryLock` path; wrap the handler in
     `StopOnceServerHandler`; build the response with the agreed max; return
     the outcome. The caller releases the request.
   - The server endpoint is constructed with `AgreedMaxByteSize`.
8. In `RawReliableTcpServerEndpoint.cs` and `RawReliableTcpServerTransport.cs`:
   - Add `string handshakeName`, `int handshakeMaxByteSize`,
     `TimeSpan handshakeTimeout`, and `TimeSpan rejectLinger` parameters to
     `RawReliableTcpServerTransport` and forward them to the base constructor
     and the accept path.
   - Port the `BeginAccept`/`AcceptHandshake` flow from
     `RawReliableAckTcpServerEndpoint`:
     read the length prefix, enforce `HandshakeMaxByteSize`, deserialize the
     frame and verify its leading frame type is `HandshakeRequestMessage`
     before parsing, call `TryHandshake`, build the response or reject, create
     the endpoint with the pending response and `AgreedMaxByteSize`, and
     invoke the completion.
   - The server transport's accept path calls `TryBeginAdmission`, tracks the
     handshake socket, calls `BeginAccept`, and on completion calls
     `CompleteAdmission`/`AbandonAdmission`.
   - On a reject, write the reject frame using the same length-prefixed
     framing, wait `RejectLinger` (default 2 s), then close the socket.
   - Track handshake sockets and close them on transport stop.
   - Remove the old immediate-admission path.
9. In `RawReliableTcpClientEndpoint.cs` and
   `RawReliableTcpClientTransport.cs`:
   - Add the handshake name/max/timeout constructor parameters and pass them
     to the endpoint.
   - The endpoint sends the request on low-level connect and waits for the
     response.
   - Add binding parameters to `RawReliableTcpBinding`:
     `handshake_max_byte_size` (integer, default 128),
     `handshake_timeout` (integer seconds, default 10),
     `reject_linger` (integer milliseconds, default 2000). Scheme stays
     `"tcp-noack"` until Stage 10.
10. Add `Pontifex/Abstractions/StopReason/StopReasons.cs` entries:
    - `public sealed class ProtocolMismatch : StopReason` with an optional
      `Text`.
    - `public sealed class HandshakeRejected : StopReason` with an optional
      `Text`.
    - Reuse `TimeOut` for handshake timeout.
11. Migrate the existing NoAck tests to answer the handshake:
    - `Tests/Pontifex.Tests/Endpoints/.../Harness/TestRawReliableHandshakeClientEndpoint.cs`:
      add `CompleteHandshake(...)` that feeds a `HandshakeResponseMessage` via
      `ExposedProcessInbound` and drains the queue. Update the endpoint tests
      that currently call `CompleteConnect` and expect `OnConnected` to call
      `CompleteHandshake` instead.
    - `.../TestRawReliableHandshakeServerEndpoint.cs`: add a
      `SetHandshakeResponse` helper; tests that call `CompleteConnect` must
      set the pending response first.
    - `Tests/Pontifex.Tests/Transports/...` transport doubles: their
      `CreateEndpoint` must supply the handshake name and tests must drive
      the handshake response.
    - `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/RawReliable/RawReliableTcpClientEndpointIntegrationTests.cs`:
      the raw peer must read the handshake request and write a valid
      response before `OnConnected` is expected. Raw-peer tests must use
      symbolic frame constants derived from `MessageType` (for example a
      derived helper exposing `(byte)MessageType.HandshakeRequestMessage`),
      not hard-coded `7`/`8`/`9`: Stage 10 renumbers those members to
      `5`/`6`/`7`.
    - Update the owning `Coverage.md` files for the migrated
      handshake-completion cases (`Tests/Pontifex.Tests/Endpoints/...` and
      `.../Transports/...`) per `Tests/RULES.md` COVERAGE-11.
12. Add `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/RawReliable/RawReliableTcpHandshakeIntegrationTests.cs`
    with `[Category("Integration")]`:
    - successful handshake;
    - version mismatch -> client `OnStopped` without `OnConnected`, factory
      not called, reason `ProtocolMismatch`;
    - name mismatch -> same;
    - factory null -> reason `HandshakeRejected`;
    - handshake timeout (short configured value) -> `TimeOut`;
    - max-size negotiation chooses the min; both endpoints report the min;
      a send of `min + 1` is rejected on both sides;
    - reject linger: expose an internal `RejectLinger` property on
      `RawReliableAdmissionServerTransport`; a constructor-without-parameter
      test asserts it is exactly 2 s. A second test configures 100 ms and
      asserts the server-side close time is `>=` 100 ms and `< 1 s` (both a
      lower and an upper bound, so an infinite or zero linger fails).
      Direct has its own 0-linger test in Stage 3.
    - an oversized handshake length prefix is rejected before body allocation
      and closes the peer (port the existing Ack
      `OversizePrefix_...` case).
    - a peer that sends `messageMaxByteSize < HandshakeMaxByteSize` (or `0`)
      is rejected with `CapacityExceeded`/`HandshakeRejected`; the factory is
      not called and no session is created.
    - a pending response that cannot be sent (forced `ScheduleToSend`
      failure) stops the session with `TextFail` and never calls
      `OnConnected`.
    - handshake frames are not delivered through `OnReceived`.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes (existing tests migrated).
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableTcpHandshakeIntegrationTests"` passes and reports at least 12 tests.
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes (the `tcp-noack` binding path now handshakes).
- `grep -R -n --include='*.cs' -E "Handshake(Request|Response|Reject)Message" Pontifex/Implementations Transports` shows only `RawReliableEndpoint.cs`, `RawReliableHandshakeClientEndpoint.cs`, `RawReliableHandshakeServerEndpoint.cs`, and `RawReliableTcpServerEndpoint.cs`. Test files may reference the frame types; the gate is production-only.

**Definition of done.** RawReliable TCP pairs require the handshake; mismatch,
reject, timeout, max negotiation, and 2 s linger are covered; the existing
NoAck tests are migrated; the Ack TCP path still compiles and its tests pass.

**Risks / notes.** The `AcceptHandshake` code is ~750 lines; port it rather
than rewriting it. Keep the request/response buffers inside
`HandshakeMaxByteSize` and validate the length prefix before allocation. The
client endpoint must set the negotiated max before `OnConnected`, and the
completion must run on the serial action queue. Do not start keep-alive before
the handshake response.

---

### Stage 3 — Implement the transport handshake in the Direct carrier

**Objective.** `RawReliable` Direct pairs perform the same handshake, with
reject linger 0 and in-process synchronous delivery; Direct tests are ported
from the Ack Direct tests.

**Inputs.** Stage 2 complete.

**Actions.**

1. Create `Transports/Pontifex.Transport.Direct/Raw/Reliable/RawReliable/`:
   - `RawReliableDirectClientTransport`, `RawReliableDirectServerTransport`,
     `RawReliableDirectClientEndpoint`, `RawReliableDirectServerEndpoint`,
     `RawReliableDirectBinding`, based on `RawReliableAckDirect*`.
   - Implement `IRawReliableClient`/`IRawReliableServer` and the RawReliable
     handshake. The client sends the request as the first channel message;
     the server validates it, calls `TryHandshake`, and creates the endpoint
     with the pending response; `RawReliableHandshakeServerEndpoint.OnConnected`
     sends it. `RejectLinger = TimeSpan.Zero`; the reject frame is delivered
     synchronously and the channel is closed immediately. Use
     `handshakeName = "direct"` on both sides (the binding scheme is
     `"direct-noack"`; the handshake name is the final contract name).
   - `RawReliableDirectClientEndpoint` overrides
     `ProcessInboundMessage(MessageType, UnionDataList)` and handles
     `HandshakeRejectMessage` synchronously: parse the status, call
     `Stop(mappedReason, false)`, and return `true`. This runs before the
     channel-dispose callback can win the stop CAS; otherwise the client
     observes `UnknownRemoteIntention` instead of
     `ProtocolMismatch`/`HandshakeRejected`.
   - `RawReliableDirectClientEndpoint` derives from
     `RawReliableHandshakeClientEndpoint` and passes
     `Timeout.InfiniteTimeSpan` for `handshakeTimeout`; add an explicit
     no-timer sentinel (for example a `protected virtual Timer?
     CreateHandshakeTimer(...)` that returns `null`, or an internal
     `HasHandshakeTimer`) so no `System.Threading.Timer` is created for
     Direct.
   - Binding scheme `"direct-noack"`; type `TransportType.RawReliable`;
     parameters `id`, `handshake_max_byte_size` (default 128). Do not add a
     Direct handshake timeout: the in-process handshake is synchronous and
     has no timer; an in-process factory that blocks is the application's
     responsibility.
   - Keep the Ack Direct files until Stage 10; the new binding uses the
     temporary scheme to avoid a duplicate `"direct"`.
2. Add `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/RawReliable/RawReliableDirectTransportTests.cs`,
   ported from all ten cases of `RawReliableAckDirectTransportTests`, plus
   the RawReliable handshake cases:
   - successful handshake and `OnConnected`;
   - bidirectional order;
   - version/name mismatch -> reject without `OnConnected`; assert the client
     stop reason is `ProtocolMismatch` (not `UnknownRemoteIntention`);
   - factory null -> reject with `HandshakeRejected`;
   - max-size negotiation;
   - zero linger (the channel closes immediately);
   - client disconnect notification and server-stop notification;
   - client start without a server;
   - oversized message rejection;
   - many concurrent clients;
   - `OnDisconnected` -> `OnStopped` ordering;
   - `Client_transport_stop_notifies_the_handler_and_the_server_session`.
3. Do not change TestUI in this stage; it still uses the Ack Direct carrier.
   Stage 7 migrates TestUI as a whole and registers the RawReliable bindings.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableDirectTransportTests"` passes and reports at least 12 tests.
- The Direct zero-linger test asserts
  `((RawReliableAdmissionServerTransport)server).RejectLinger == TimeSpan.Zero`
  and an observable close upper bound (`< 100 ms`); the Direct client endpoint
  creates no `System.Threading.Timer`.

**Definition of done.** Direct RawReliable pairs handshake and reject without
lingering; the Direct Ack tests still pass.

**Risks / notes.** Direct has no socket, so the handshake is synchronous; do
not use a timer unless the channel can stall. The server must not call the
factory until the request has been validated.

---

### Stage 4 — Add the reusable negotiation utility and the wrapper base

**Objective.** A reusable, internal negotiation driver and a public
`RawReliableWrapperClient`/`Server` base (with an abstract
`RawReliableWrapperLogic`) exist; they enforce the flat
`[version, name, ...custom-tail]` envelope, gate callbacks, time out, and are
unit-tested with a fake inner transport. No production protocol uses them yet.

**Inputs.** Stages 2–3 complete.

**Actions.**

1. In `Pontifex/Implementations/Raw/Reliable/Negotiation/`, add:
   - `RawReliableNegotiation.cs` (internal static): builds/parses the flat
     envelope with an explicit discriminator:
     `Request:  [version, name, NegotiationStatus.Request, ...custom]`,
     `Response: [version, name, NegotiationStatus.Accepted, ...custom]`,
     `Reject:   [serverVersion, serverName, NegotiationStatus.Rejected, text?]`.
     The third element is always the status, so a custom value (`int id`,
     etc.) cannot be mistaken for a status. Validate the status before
     parsing the custom tail.
   - `NegotiationSession.cs` (internal): a client/server state machine with a
     role-tagged constructor (`NegotiationRole.Client`/`Server`). The client
     **sends the request immediately** from `OnConnected`; it does not wait
     for an incoming payload. The server consumes the first payload as the
     request. The inbound surface is
     `bool OnReceived(UnionDataList message)`: it takes ownership of the
     message, releases it after processing, and returns `true` when the
     message was consumed as a negotiation response/request; it returns
     `false` only when the message is not a negotiation message, in which
     case it must not release it and the caller retains ownership. The
     session enforces `HandshakeMaxByteSize` (default 128) and
     `HandshakeTimeout` (default 10 s), and exposes callbacks for
     admitted/rejected.
     The session owns the request/response `UnionDataList`s and releases each
     once after the hook returns; hooks must not retain or release them.
     Accept an optional `Action<UnionDataList>? appendData` (the Api's
     `AppendNegotiationData` hook) with the same append-only ownership, and an
     optional `Func<DateTime>`/timer seam so the 2 s linger test is
     deterministic.
   - `NegotiationStatus` enum: `Request`, `Accepted`, `Rejected`.
2. In `Protocols/Pontifex.Protocol/Raw/Reliable/` (namespace
   `Pontifex.Raw.Reliable.Protocols`), add:
   - `IRawReliableWrapperLogic` with `OnConnected`, `OnDisconnected`,
     `ProcessReceivedData`, `ProcessSentData`, `GetControls`, and the
     negotiation hooks. The hooks receive the **custom tail** after
     `version`/`name`/`status`, not the full envelope:
     `void FillNegotiationData(UnionDataList customData)` (client request),
     `void OnNegotiationAccepted(UnionDataList customData)` (client response
     tail; the session owns and releases the buffer after the hook returns),
     `UnionDataList CreateNegotiationResponse()` (server response tail),
     `bool CheckNegotiationData(UnionDataList customData)` (server request
     tail).
   - `RawReliableWrapperLogic` abstract base with `Log`, `Memory`, and
     virtual/abstract versions of the above.
   - `RawReliableWrapperClient`/`RawReliableWrapperServer`, implementing
     `IRawReliableClient`/`IRawReliableServer` and wrapping one
     `IRawReliableClient`/`IRawReliableServer`.
   - Client/server handler proxies that forward `Send`/`Disconnect`/
     `IsConnected`/`RemoteEndPoint`/`MessageMaxByteSize` and the negotiation.
     Mirror the `RawReliableEndpoint` terminal lifecycle: for an admitted
     session `OnConnected` -> `OnDisconnected` -> `OnStopped` exactly once;
     a never-connected session gets exactly one `OnStopped` and no
     `OnDisconnected`. Add a stop-once assertion to
     `RawReliableWrapperTests`; do not copy the existing
     `HandlerWrapper.OnDisconnected` TODO pattern.
     Serialize the deferred user `TryCreate` calls through one lock in the
     wrapper/reconnectable server, so the
     `IRawReliableServerHandlerFactory` "never overlap" contract holds even
     when negotiation completes on different session queues.
   - The wrapper starts `NegotiationSession` when the inner `OnConnected`
     fires. The client sends the request immediately; the server consumes the
     first payload as the request. It must not deliver `OnReceived` or report
     `IsConnected = true` before admission. On reject, it sends the reject
     envelope, waits `RejectLinger` (default 2 s), then disconnects the inner
     transport. `RawReliableWrapperClient`/`Server` and `NegotiationSession`
     take a `TimeSpan rejectLinger = default 2 s` constructor parameter; the
     Zip/Reconnectable bindings may expose `reject_linger` in milliseconds.
3. Add unit tests in
   `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol/RawReliableWrapperTests.cs`
   (fixture `RawReliableWrapperTests`, namespace `Pontifex.Tests.Raw.Reliable.Protocol`)
   with a hand-written fake `IRawReliableClient/Server`:
   - accepted negotiation;
   - version mismatch;
   - name mismatch;
   - timeout;
   - reject linger (use the session's clock/timer seam; assert 2 s exactly);
   - no `OnReceived` before `OnConnected`;
   - `Send` before `OnConnected` is not routed to the inner transport;
   - **custom-tail round trip**: the client `FillNegotiationData` appends
     `["k"]`; the server `CheckNegotiationData` observes exactly that tail;
     the server `CreateNegotiationResponse` returns `["v"]`; the client's
     admitted callback observes exactly that tail;
   - **custom-tail rejection**: `CheckNegotiationData` returns `false`; the
     client receives a reject (distinct stop reason) and no `OnConnected`;
   - **custom-tail size limit**: a tail that makes the envelope exceed
     `HandshakeMaxByteSize` is rejected, not truncated.
4. Add an explicit `ProjectReference` to `Pontifex.Protocol` in
   `Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`.
5. Add `README.md` and `Coverage.md` for the new
   `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol` suite/layer per
   `Tests/RULES.md`.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableWrapperTests"` passes and reports at least 10 tests.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~FactoryBindings"` passes.

**Definition of done.** The wrapper base and negotiation utility compile,
are unit-tested with a fake transport, and are ready for Zip/Reconnectable.

**Risks / notes.** Keep the utility internal; only the wrapper base is public.
The client sends the request from `OnConnected`; waiting for the first
incoming payload would deadlock because the wrapper gates `Send` until
admission.

---

### Stage 5 — Migrate Zip to a RawReliable wrapper

**Objective.** `zip(nested)` is a `RawReliable` wrapper with its own `"zip"`
negotiation dataset; it compresses/decompresses over any RawReliable
transport; the Ack Zip binding is deleted.

**Inputs.** Stage 4 complete.

**Actions.**

1. Replace `Protocols/Pontifex.Protocol.Zip/Raw/Reliable/Ack/*` with
   `Protocols/Pontifex.Protocol.Zip/Raw/Reliable/*`:
   - `RawReliableZipBinding`: scheme `"zip"`, type `TransportType.RawReliable`,
     nested `Exactly(RawReliable)`, `compression_level`.
   - `RawReliableZipClientLogic`/`RawReliableZipServerLogic`: derive from
     `RawReliableWrapperLogic`; implement `IRawReliableWrapperLogic`; the
     negotiation version is `1` on both sides. The
     client `FillNegotiationData` adds no custom elements; the server
     `CheckNegotiationData` returns true (the session already validated the
     name `"zip"`); `CreateNegotiationResponse` returns an empty response.
     `OnConnected`/`OnDisconnected` release the compressor state;
     `ProcessReceivedData` decompresses; `ProcessSentData` compresses.
   - `CompressorLogic` base changes from `RawReliableAckWrapperLogic` to
     `RawReliableWrapperLogic`.
   - Add `InternalsVisibleTo` for `Pontifex.Transports.Tests` to
     `Pontifex.Protocol.Zip.csproj` so the tests can construct the internal
     Zip logic classes (or make those classes public).
2. Delete the Ack Zip files and the Ack Zip binding.
3. Update `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - Replace the Zip registration with `RawReliableZipBinding`.
   - Change the `zip(tcp(...))` cases to `zip(tcp-noack(...))` and expect
     `RawReliable`.
   - Update the `Canonicalize_WithEveryBinding_ReturnsCanonicalFormula`
     `ZIP(TCP(...))` case to `ZIP(TCP-NOACK(...))`.
4. Update `Pontifex.TestUI/TransportFactory.cs`: replace the Ack Zip
   registration/import with `RawReliableZipBinding`. The UI's Ack casts remain
   until Stage 7, so the Zip formula constructs no transport there
   temporarily; this build-only change keeps the solution buildable.
5. Add `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip/RawReliableZipTests.cs`
   (fixture `RawReliableZipTests`) with a fake `IRawReliableClient/Server`
   constructed through the public `RawReliableWrapperClient`/`Server` base and
   the internal Zip logic:
   - connects and round-trips data;
   - **compression is proven** by asserting the fake inner transport observed
     a smaller/different byte stream for a compressible payload (inspect the
     fake inner transport's captured send buffer), not only that the
     application received the original bytes;
   - a raw client (no `"zip"` negotiation) is rejected by the wrapper;
   - a version mismatch is rejected.
   Do not use the `zip(tcp-noack(...))` formula in this fake-transport
   fixture; the formula is covered by `FactoryBindingsTests` item 3.
6. Create `README.md` and `Coverage.md` for the new
   `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip` Test Suit (or a declared
   layer under the existing suite) per `Tests/RULES.md`, and update the
   layer/category tables.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableZipTests"` passes and reports at least 4 tests.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~FactoryBindings"` passes.
- `grep -R -n --include='*.cs' -E "RawReliableAckZip" Protocols/Pontifex.Protocol.Zip` returns no matches.

**Definition of done.** `zip(nested)` works over RawReliable; the Ack Zip
binding/logic are gone; compression is proven; `FactoryBindingsTests` passes.

**Risks / notes.** Zip compression can expand incompressible data; keep the
existing `MessageMaxByteSize` semantics (the wrapper reports the inner max)
and let the inner transport reject oversized compressed output as today.

---

### Stage 6 — Migrate Reconnectable to a RawReliable wrapper

**Objective.** `reconnectable(nested)` is a `RawReliable` wrapper with a
`"reconnectable"` negotiation dataset carrying session id, generation, and
secret; reattach works; the Ack Reconnectable binding is deleted.

**Inputs.** Stage 5 complete.

**Actions.**

1. Replace `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Ack/*`
   with `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/*`:
   - `RawReliableReconnectableBinding`: scheme `"reconnectable"`, type
     `TransportType.RawReliable`, nested `Exactly(RawReliable)`,
     `disconnect_timeout`, and `handshake_max_byte_size` (default 128) to
     allow the session payload.
   - `RawReliableReconnectableClient`/`Server`: implement
     `IRawReliableClient`/`IRawReliableServer`; the negotiation version is
     `1` on both sides. The client needs a fresh
     inner transport per reconnect attempt, so it uses `NegotiationSession`
     directly over a `Func<IRawReliableClient?>` inner-transport factory and
     starts/stops a fresh inner transport for each attempt; do not force the
     single-instance `RawReliableWrapperClient` base. The server may use the
     wrapper base or the session directly.
   - `ReconnectableClientLogic`: on the **first connection** the request
     custom tail is `[invalidSessionId, 0, emptySecret]`, where
     `emptySecret` is a zero-length `UnionData` array (not a literal null:
     `UnionDataList.TryPopFirst(out IMultiRefReadOnlyByteArray)` accepts only
     `UnionDataType.Array`; if you choose `NullArray` instead, state that
     `CheckNegotiationData` accepts it). The current code always writes
     id/generation and omits only the secret when `_sessionId.IsValid` is
     false; make `CheckNegotiationData` treat an invalid id as a new session.
     On **reattach** the tail is
     `[sessionId.Id, sessionId.Generation, secret]`. The accepted response
     tail is `[id, generation, secret]` (three elements); the session adds
     `[version, name, Accepted]` around it. Expose `OnConnected` only after
     the negotiation.
   - `ReconnectableServerLogic`: `CheckNegotiationData` parses the request,
     reattaches an existing session or creates a new one, and
     `CreateNegotiationResponse` returns `[id, generation, secret]`. There is
     no user response element: the old ACK-response source is deleted. On
     rejection, release the inner session/handler exactly once (stop-once).
   - The full envelope is
     `[version, name, NegotiationStatus.Request|Accepted, ...tail]`; the
     status is the discriminator from Stage 4.
   - Remove `AckRequest`/`AckOKResponse` markers from `ReconnectableInfo`;
     the wrapper name/version identifies the layer.
   - Replace the `AckRejected` stop reason in `ReconnectableClientLogic`
     with `ProtocolMismatch`/`HandshakeRejected`.
   - Preserve `DeliverySystem` and session map behaviour.
2. Add a `ProjectReference` from
   `Protocols/Pontifex.Protocol.Reconnectable/Pontifex.Protocol.Reconnectable.csproj`
   to `Pontifex.Protocol` if it is not already present.
3. Delete the Ack Reconnectable files and binding.
4. Migrate `Tests/Pontifex.Transports.Tests/ReconnectableAckResponseTests.cs`:
   - rename to `ReconnectableNegotiationResponseTests.cs`;
   - the logic exposes `CreateNegotiationResponse()` (returns the custom tail
     `[id, generation, secret]` only), `CloneNegotiationResponse()`, and
     `ReleaseNegotiationResponse()`; `CreateNegotiationResponse` no longer
     takes a user response;
   - the direct-logic test calls `CreateNegotiationResponse()` and asserts
     exactly three elements `[id, generation, secret]`; it does **not**
     assert the `Accepted` status (the session adds
     `[version, name, Accepted]` around the tail);
   - add or extend a session-level/wrapper test that asserts the full
     envelope `[version, name, Accepted, id, generation, secret]`;
   - keep the release-lifecycle assertions for
     `CloneNegotiationResponse`/`ReleaseNegotiationResponse`.
5. Update `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - replace the Reconnectable registration with the new binding;
   - change `reconnectable(30, tcp(...))` to
     `reconnectable(30, tcp-noack(...))` and expect `RawReliable`;
   - update the `Canonicalize_WithEveryBinding_ReturnsCanonicalFormula`
     `RECONNECTABLE(30, TCP(...))` case to
     `RECONNECTABLE(30, TCP-NOACK(...))`.
6. Update `Pontifex.TestUI/TransportFactory.cs`: replace the Ack
   Reconnectable registration/import with
   `RawReliableReconnectableBinding`. The UI's Ack casts remain until Stage 7,
   so the Reconnectable formula constructs no transport there temporarily;
   this build-only change keeps the solution buildable.
7. Add `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable/RawReliableReconnectableTests.cs`
   (fixture `RawReliableReconnectableTests`):
   - first connect establishes a session and transfers data;
   - drop and reconnect with the session id/secret reattaches;
   - wrong secret is rejected and the inner session/handler is released
     exactly once (assert stop-once);
   - version/name mismatch is rejected;
   - queued messages survive reattach as today.
8. Create `README.md` and `Coverage.md` for the new
   `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable` Test Suit (or
   a declared layer under the existing suite) per `Tests/RULES.md`, and update
   the layer/category tables.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableReconnectableTests"` passes and reports at least 5 tests.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~ReconnectableNegotiationResponseTests"` passes.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~FactoryBindings"` passes.
- `grep -R -n --include='*.cs' -E "RawReliableAckReconnectable" Protocols/Pontifex.Protocol.Reconnectable` returns no matches.

**Definition of done.** Reconnectable works over RawReliable with its own
negotiation; the Ack Reconnectable binding/logic are gone; reattach behaviour
is preserved; the Ack-response tests are migrated.

**Risks / notes.** The response tail is only `id, generation, secret`; the
default 128 bytes is sufficient. If a future user payload is added, raise
`handshake_max_byte_size` in the binding/tests. Do not change the
`DeliverySystem` reliability semantics.

---

### Stage 7 — Migrate Pontifex.Api, TestUI Api, and the transport test stacks

**Objective.** `Pontifex.Api` uses `IRawReliableClientHandler` /
`IRawReliableServerHandlerFactory` and its own `"api"` negotiation; TestUI is
fully migrated to RawReliable; the transport test registry/stacks produce
RawReliable transports.

**Inputs.** Stage 6 complete.

**Actions.**

1. In `Api/Pontifex.Api/Transport/Transport/ClientSideApi.cs`:
   - Implement `IRawReliableClientHandler` instead of
     `IRawReliableAckClientHandler`.
   - On `OnConnected(ep)`, create a `NegotiationSession` client with name
     `"api"`, version `1`, and an optional
     `protected virtual void AppendNegotiationData(UnionDataList customTail)`
     hook (replaces `AppendAckData`; append-only, the session owns/releases
     the envelope); send the request. The Api server admits on the `"api"`
     name/version only and ignores any appended elements; the hook exists for
     future metadata.
   - Until admitted, `OnReceived` feeds the negotiation; after admission,
     start the `TransportPipeSystem` and raise `Connected`.
   - On negotiation reject/timeout, disconnect the endpoint and raise
     `Disconnected` exactly once. Add a private one-shot
     `_disconnectedRaised` latch used by both `OnDisconnected` and
     `OnStopped`; set it before `endPoint.Disconnect(reason)`. Do not rely on
     `_wasConnectedEver` for once-only: it only suppresses the pre-connect
     `OnStopped` duplicate, and the negotiation path fires both callbacks.
   - Remove `FillAckData`, `AppendAckData`, and the 777/7777 hash.
   - Replace the old `AckRejected` reason in the Api with
     `ProtocolMismatch`/`HandshakeRejected`.
2. In `Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs`:
   - Implement `IRawReliableServerHandlerFactory<IRawReliableServerHandler>`
     instead of `IRawReliableAckServerAcknowledger`.
   - Declare the method as
     `IRawReliableServerHandler? TryCreate(IEndPoint remote)` (C# 9 has no
     covariant interface implementation) and return the
     `ServerSideApiInstance<TApi>` instance (or null).
   - Remove the `ackData` parameter and the `777` check.
3. In `Api/Pontifex.Api/Transport/Transport/ServerSideApiInstance.cs`:
   - It already implements `IRawReliableServerHandler`; start a
     `NegotiationSession` server on `OnConnected`.
   - On the request, validate name/version, reply, then start the API and
     raise `ApiStarted`.
   - Before admission, `OnReceived` feeds the negotiation; after, it feeds
     the pipe.
4. Update the `ServerSideApiFactory` constructor from
   `Func<UnionDataList, ServerSideApiInstance<TApi>?>` to
   `Func<ServerSideApiInstance<TApi>>`.
5. TestUI (migrate it as a whole in this stage; the Api type change forces
   the call sites, and the transport factory casts change with it):
   - `Pontifex.TestUI/TransportFactory.cs`: `ConstructServer`/`ConstructClient`
     cast to `IRawReliableServer`/`IRawReliableClient`; register the
     RawReliable TCP/Direct/Zip/Reconnectable bindings and the temporary
     `log-noack` binding; remove the Ack registrations.
   - `Pontifex.TestUI/Ui/Transport/ClientWindow.cs`: pass `ClientSideApi`
     to `IRawReliableClient.Init`.
   - `Pontifex.TestUI/Ui/Transport/ServerWindow.cs`: use
     `IRawReliableServer.Init` and update the `ServerSideApiFactory` lambda
     to the parameterless form (`() => new ServerSideApiInstance<...>`).
   - Rename `TestLogic/Raw/Reliable/Ack/*` to `TestLogic/Raw/Reliable/*`;
     rename `RawReliableAckServerLogic` to `RawReliableServerLogic` and make
     it implement
     `IRawReliableServerHandlerFactory<IRawReliableServerHandler>` with
     `TryCreate(IEndPoint remote)` returning a fresh nested `Handler` that
     implements `IRawReliableServerHandler` (`OnConnected`,
     `OnReceived`, `OnDisconnected`, `OnStopped`); remove
     `TryAck`/`FillAckData` and the ack response.
   - Rename `RawReliableAckClientLogic` to `RawReliableClientLogic` and make
     it implement `IRawReliableClientHandler` with `OnConnected(ep)`;
     remove `FillAckData`.
   - Rename `RawReliableAckCommonLogic` to `RawReliableCommonLogic`; remove
     the `AckRequest`/`AckResponse` strings.
   - Rename `TestApi/Brute/RawReliableAckProtocol.cs` (and the `_Client`/
     `_Server` classes) to `RawReliableProtocol*`; update `ApiFactory.cs`.
   - Rename `Ui/Controls/Types/RawReliableAckClientControlView.cs` to
     `RawReliableClientControlView.cs`; update `ControlViewFactory`.
   - Rename `IRawReliableAckClientControl`/`RawReliableAckClientControl` in
     `Pontifex/Elements/Controls/Client/` to
     `IRawReliableClientControl`/`RawReliableClientControl` over
     `IRawReliableClient`; update the view and factory to the new type.
   - `Ui/Transport/TransportFactoryWindow.cs`: build formulas with the
     temporary schemes `direct-noack(...)`, `tcp-noack(...)`, and
     `log-noack(...)`; keep `zip(...)`/`reconnectable(...)`.
   - Delete `IRawReliableAckTcpClientDebugControl` and its view (no transport
     implements it).
6. Transport test infrastructure:
   - `Tests/Pontifex.Transport.Tests/Core/TransportRegistry.cs`: register
     `RawReliableTcpBinding` and `RawReliableDirectBinding` (temporary
     schemes `tcp-noack`/`direct-noack`) in addition to the Ack bindings.
   - Add `Tests/Pontifex.Transport.Tests/TransportStacks/RawReliableStacks.cs`
     with `direct-noack(...)` and `tcp-noack(...)` stacks; keep
     `RawReliableAckStacks` for any plans not yet migrated, but migrate all
     five plans in this stage.
   - `Tests/Pontifex.Transport.Tests/Core/TestHarness.cs`: cast to
     `IRawReliableClient`/`IRawReliableServer`; update the
     `ServerSideApiFactory` lambda.
   - Migrate `Tests/Pontifex.Transport.Tests/TestPlans/Raw/Reliable/Ack/**`
     to `.../RawReliable/**`: replace Ack casts/handlers with RawReliable
     handlers; use `RawReliableStacks`; update `Connect_ServerGracefullDisconnect`
     and `HighLoadDataTransfer` to the RawReliable contract.
7. Add `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api/RawReliableApiNegotiationTests.cs`
   (fixture `RawReliableApiNegotiationTests`) with a fake
   `IRawReliableClient/Server`:
   - a raw handler that sends application data without the `"api"` request is
     rejected by the server; the Api raises no `Connected` and stops with
     `ProtocolMismatch`/`HandshakeRejected`;
   - a server response with a wrong version/name makes the Api raise
     `Disconnected` exactly once and no `Connected`;
   - the Api's first outbound payload is
     `[1, "api", NegotiationStatus.Request, ...]`.
   Add `README.md`/`Coverage.md` for the new
   `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api` layer per
   `Tests/RULES.md`.
8. Run `Api/Pontifex.Api.Tests` unchanged; it uses in-memory pipes.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Api/Pontifex.Api.Tests/Pontifex.Api.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes (30 tests today; the migrated plans must all pass).
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableApiNegotiationTests"` passes and reports at least 3 tests.
- `grep -R -n --include='*.cs' -E "IRawReliableAck|FillAckData|AppendAckData" Api` returns no matches.
- `grep -R -n --include='*.cs' -E "RawReliableAck|IRawReliableAck|FillAckData|TryAck" Pontifex.TestUI` returns no matches.

**Definition of done.** The Api and TestUI build/run over RawReliable; the
transport Api test plans pass over the RawReliable stacks; no Api Ack
references remain.

**Risks / notes.** The transport `OnConnected` fires before the Api handshake;
the Api must suppress its `Connected` event and inbound data until its own
negotiation completes. Preserve the exact `Disconnected`-once behaviour.

---

### Stage 8 — Migrate the unreliable-noack to reliable converter

**Objective.** `RawUnreliableNoAck -> RawReliableAck` becomes
`RawUnreliableNoAck -> RawReliable`; the converter performs the transport
handshake over its `DeliveryManager`; `ConvertersGraph.Default` uses the new
converter.

**Inputs.** Stage 7 complete.

**Actions.**

1. Rename `Pontifex/Converters/RawUnreliableNoAck/RawReliableAck/*` to
   `.../RawReliable/*`:
   - `RawUnreliableNoAckToRawReliableConverter`; `From =
     TransportType.RawUnreliableNoAck`, `To = TransportType.RawReliable`.
   - `RawUnreliableNoAckToRawReliableClient/Server`; implement
     `IRawReliableClient`/`IRawReliableServer`.
   - Replace any `AckRejected` use with `ProtocolMismatch`/
     `HandshakeRejected`.
2. In the client:
   - `Init(IRawReliableClientHandler)` stores the handler.
   - When the inner unreliable endpoint is available, send the handshake
     request `[1, "converter", messageMaxByteSize]` through the
     `DeliveryManager` as the first delivery-managed message.
   - The response/reject share the payload channel and need a discriminator:
     response `[version, name, HandshakeStatus.Accepted, agreedMax]`; reject
     `[serverVersion, serverName, HandshakeStatus.Rejected, text?]`. The
     client inspects the third element (the status) **before** parsing and
     never tries one parser destructively after the other.
   - The converter's endpoint layer must intercept the first delivered
     message as the handshake response; do not pass it to the application
     handler. Call `OnConnected` only after the response validates.
   - On reject/timeout, stop with `ProtocolMismatch`/`TimeOut`.
   - The converter endpoint/session mirrors the reliable terminal lifecycle:
     `OnConnected` -> `OnDisconnected` -> `OnStopped` exactly once; a
     rejected/never-connected session gets exactly one `OnStopped`.
   - Remove `FillAckData` and the fabricated `7777` response.
3. In the server:
   - On a new inner session, create the `DeliveryManager`; the first
     delivery-managed message is the handshake request. Validate it, call
     `TryCreate(remote)` through a serialized path (one admission at a time,
     consistent with the transport factory contract), send the response, and
     only then call the handler's `OnConnected`.
   - On validation failure or a null `TryCreate`, send a reject envelope over
     the session if the inner channel can carry it, then stop/cleanup the
     converter session and release its `DeliveryManager`; do not create or
     notify an application handler. The inner unreliable endpoint has no
     `Disconnect`, so cleanup happens on the converter side.
   - Remove `TryAck` and the discarded response.
4. Do **not** reuse `RawReliableHandshake.BuildResponse`/`TryParseResponse`
   for the converter: those are the 3-element frame-typed transport-tier
   codec. Add converter-specific helpers (`ConverterHandshake`):
   request `[version, "converter", messageMaxByteSize]`, accepted
   `[version, "converter", HandshakeStatus.Accepted, agreedMax]`, reject
   `[serverVersion, "converter", HandshakeStatus.Rejected, text?]`. The
   client inspects element 3 before parsing. Release the request/response
   buffers exactly once after they are consumed.
   The converter's local max is `_dm.DeliveryMaxByteSize` (the delivery-layer
   max, not the inner transport max): the client sends it in the handshake
   request; the server computes
   `min(request.max, its own _dm.DeliveryMaxByteSize)` and returns it; both
   converter endpoints apply the agreed min. The converter endpoint `Send`
   must check `bufferToSend.GetDataSize() > MessageMaxByteSize` and return
   `SendResult.MessageTooBig` (releasing the buffer), because the
   `DeliveryManager` only knows the inner max. Add a test with mismatched
   inner/DM maxes that asserts both endpoints report the min and a `min + 1`
   send is rejected on both sides. The handshake timeout is 10 s.
5. Update `ConvertersGraph.Default` to the new converter.
6. Update test data with per-harness values that stay mutually distinct:
   - `BootstrapTestData`: `TypeA = TransportType.RawReliable` (the converter
     target) and `TypeB = TransportType.RawUnreliableNoAck` (the source).
     `Compile_WithDefaultConverterGraph_UsesTheBuiltInConversion` registers
     `alpha` as `RawUnreliableNoAck` and compiles it as `TypeA = RawReliable`,
     so the default converter is exercised.
   - `CompilationTestData`: `TypeA = RawReliable`,
     `TypeB = RawUnreliableNoAck`, `TypeC = RawUnreliableAck`. The
     no-conversion test uses `TypeA as TypeC` (`RawReliable` ->
     `RawUnreliableAck`) and asserts the new message
     `"no conversion path from RawReliable to RawUnreliableAck"`.
   - `SemanticsTestData`: `TypeA = RawReliable`,
     `TypeB = RawUnreliableNoAck`, `TypeC = RawUnreliableAck`.
   - `RegistryTestData`: the same three plus
     `UnregisteredType = TransportType.RRReliableAck`.
   - `CanonicalizationTestData`: the same three plus
     `TypeNotInCatalog = TransportType.RRReliableAck`.
   All four values in each harness must remain distinct; the catalog and
   selector tests depend on that.
7. Add `Tests/Pontifex.Factory.Tests/Converters/RawReliableConverterTests.cs`
   (fixture `RawReliableConverterTests`) with a fake
   `IRawUnreliableNoAckClient/Server`:
   - handshake accepted and messages delivered;
   - version/name mismatch rejected;
   - no `OnConnected` before the handshake response;
   - the handshake message is not delivered to the application;
   - mismatched inner/DM maxes: both endpoints report the agreed min and a
     `min + 1` send is rejected on both sides;
   - a rejected/never-connected session receives exactly one `OnStopped` and
     no `OnDisconnected`, and an admitted session receives the ordered
     lifecycle exactly once.
8. Add `README.md` and `Coverage.md` for the new
   `Tests/Pontifex.Factory.Tests/Converters` suite/layer per `Tests/RULES.md`.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableConverterTests"` passes and reports at least 6 tests.

**Definition of done.** The converter targets RawReliable and performs the
handshake; the factory/formula tests pass; the converter test passes.

**Risks / notes.** The inner unreliable transport is lossy; the handshake
must go through the `DeliveryManager`, not raw `Send`, so it is reliably
retried. Do not deliver the handshake message to the application handler.
The factory call must remain serialized.

---

### Stage 9 — Merge the logging wrapper

**Objective.** There is one transparent `RawReliable` logging wrapper; the
Ack logger is deleted; the wrapper keeps the temporary `log-noack` scheme
until Stage 10.

**Inputs.** Stage 8 complete; TestUI is already on RawReliable (Stage 7).

**Actions.**

1. Merge the Ack logger into the RawReliable logger (the NoAck logger was
   renamed in Stage 1):
   - Delete `RawReliableAckClientLogger`/`RawReliableAckServerLogger`; keep
     `RawReliableClientLogger`/`RawReliableServerLogger`.
   - Delete `RawReliableAckLoggerBinding`; keep `RawReliableLoggerBinding`
     with scheme `"log-noack"` and nested `Exactly(RawReliable)`.
   - Rename `RawAckBaseEndpointWrapper`/`RawAckClientSideEndpointWrapper`/
     `RawAckServerSideEndpointWrapper` to `RawReliable*` and move them from
     `namespace Pontifex.Raw.Reliable.Ack.Logger` to
     `namespace Pontifex.Raw.Reliable.Logger`; update the `using` in the
     client/server loggers.
   - In `RawReliableServerHandlerLogger.cs`, change the implemented
     interface to `IRawReliableServerHandler` only (the Ack marker is gone).
   - Update `RawReliableServerHandlerLoggerTests.cs`:
     `RawAckServerSideEndpointWrapper` -> `RawReliableServerSideEndpointWrapper`.
   - Update the Stage-1 `RawReliableClientLoggerTests.cs` and
     `RawReliableServerLoggerTests.cs`: `RawAckClientSideEndpointWrapper`/
     `RawAckServerSideEndpointWrapper` -> `RawReliable*`.
   - Delete `Tests/Pontifex.Tests/RawReliableAckServerLoggerTests.cs`; its
     subject (`RawReliableAckServerLogger`/`IRawReliableAckServer`) is
     deleted. The Stage-1 `RawReliableServerLoggerTests.cs` keeps the merged
     logger coverage.
   - The logger stays transparent: no negotiation, no extra round trip, no
     name/version, no `MessageMaxByteSize` change.
   - Add a transparency unit test with a fake `IRawReliableClient/Server`
     that records `Send` calls and reports a distinctive
     `MessageMaxByteSize`: assert `OnConnected` performs no `Send`,
     `MessageMaxByteSize` is forwarded unchanged, and the user handler
     receives exactly one logical connection.
2. Update `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - Remove the Ack logger registration; register only
     `RawReliableLoggerBinding` (`log-noack`).
   - Remove the `log(tcp(...))` case (the Ack `log` scheme is gone).
   - Keep `log-noack(tcp-noack(...))` and expect `RawReliable`.
   - Update the `Canonicalize_WithEveryBinding_ReturnsCanonicalFormula`
     `LOG(TCP(...))` case to `LOG-NOACK(TCP-NOACK(...))`.
3. Update the logger coverage/readme docs.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds (TestUI included).
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter "FullyQualifiedName~Logger"` passes.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~FactoryBindings"` passes.
- `grep -R -n --include='*.cs' -E "RawReliableAck|RawAck" Pontifex/Wrappers/Logger` returns no matches.

**Definition of done.** One transparent logger exists with the temporary
`log-noack` scheme; the Ack logger is gone; the logger tests pass.

**Risks / notes.** The logger is the only wrapper without a negotiation; do
not route it through `RawReliableWrapperClient/Server`. The scheme switch to
`log` happens in Stage 10 together with the other scheme switches.

---

### Stage 10 — Delete the Ack family, finalize the factory, and clean up

**Objective.** No `RawReliableAck` reliable contract, carrier, binding, test,
or enum value remains; `tcp`, `direct`, and `log` are `RawReliable`; `zip` and
`reconnectable` are `RawReliable` wrappers; the factory and docs are final.

**Inputs.** Stage 9 complete; all consumers migrated.

**Actions.**

1. Delete the core Ack family:
   - `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/**`.
   - `Pontifex/Implementations/Raw/Reliable/Ack/**`.
   - `Pontifex/Implementations/Endpoints/Raw/Reliable/Ack/**`.
   - `Pontifex/TransportUtils/Raw/Reliable/Ack/SynchronizedRawReliableAckClientHandler.cs`.
   - `Pontifex/Elements/Controls/Client/RawReliableAckClientControl.cs` was
     already renamed to `RawReliableClientControl` in Stage 7; verify no Ack
     control remains.
   - `RawReliableAckClient`, `RawReliableAckServer`,
     `StopOnceAckServerHandler`, `RawReliableAckInfo`,
     `IRawAckClientHandler_Ext`, the Ack logger files/binding, the Ack
     conformance controls and endpoint wrappers.
   - The Ack-specific overloads in
     `IRawReliableServerHandler_Ext` (keep only the base overloads).
   - Verify `RawReliableServerHandlerLogger` implements only
     `IRawReliableServerHandler` (done in Stage 9).
2. Delete the Ack carriers:
   - `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/**` (including
     the Ack `TcpInfo`; rename `RawReliableTcpInfo` to `TcpInfo`).
   - `Transports/Pontifex.Transport.Direct/Raw/Reliable/Ack/**`.
   - `Protocols/Pontifex.Protocol/Raw/Reliable/Ack/**` (the shared wrapper
     base).
3. Delete the Ack tests and conformance suite:
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/**/Ack/**` and
     `.../Transports/.../Ack/**`.
   - `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/Ack/**`.
   - `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/Ack/**`.
   - `Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack/**` (the ignored
     conformance suite).
   - `Tests/Pontifex.Tests/RawReliableServerHandlerExtTests.cs` Ack regions
     (keep the base-handler regions).
   - Update `Coverage.md`/`README.md` and category filters.
4. Update factory and type surfaces:
   - Renumber `TransportType` to a dense 0..6 set with `RawReliable` included
     and `RawReliableAck` removed; update the enum comment and every
     expectation.
   - Remove the Ack bindings from `Pontifex.TestUI`, `TransportRegistry`,
     `FactoryBindingsTests`, and test data.
   - Switch temporary schemes to final: `tcp-noack` -> `tcp`,
     `direct-noack` -> `direct`, `log-noack` -> `log`. Update
     `RawReliableTcpInfo.TransportName`, `RawReliableDirectBinding`, and
     `RawReliableLoggerBinding`, plus every formula string in TestUI,
     `FactoryBindingsTests`, `TransportRegistry`, and the transport stacks.
   - Remove `IRawReliableClientHandlerBase` and make
     `IRawReliableClientHandler` the generic constraint; update the generic
     transport bases, the `RawReliableClientEndpoint` constructor, and the
     test doubles/harnesses (`TestRawReliableEndpointClientTransport`,
     `TestRawReliableClientTransport`,
     `TestRawReliableHandshakeClientEndpoint`,
     `RecordingRawReliableClientHandler`) to the final handler, adding
     `OnConnected` where the recording double needs it.
   - The dense `TransportType` renumber is the one explicit exception to the
     "do not change RawUnreliable/RR except for reference updates" constraint:
     their numeric values shift, but their names, contracts, carriers, and
     tests are unchanged.
   - Remove `AckRejected`; map any remaining protocol rejections to
     `ProtocolMismatch`/`HandshakeRejected`.
   - Remove the now-unused `AckRequestMessage`/`AckResponseMessage` from
     `RawReliableEndpoint.MessageType` and renumber
     `HandshakeRequestMessage`/`HandshakeResponseMessage`/
     `HandshakeRejectMessage` to 5/6/7. Update the neutral harnesses
     `TestRawReliableEndpoint`, `TestRawReliableClientEndpoint`, and
     `TestRawReliableServerEndpoint` to remove/replace `AckRequestFrame`/
     `AckResponseFrame` with the handshake frame constants. Also update the
     raw-peer integration tests
     `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/RawReliable/RawReliableTcpClientEndpointIntegrationTests.cs`
     and `RawReliableTcpHandshakeIntegrationTests.cs` if they contain numeric
     frame literals; the Stage 2 requirement is symbolic constants, and this
     is the bridge that keeps them valid after the renumber.
   - Update the test transports that hard-code `Type =>
     TransportType.RawReliableAck` (`TestAnyTransport`,
     `TestRawReliableTransport`, `TestRawReliableClientTransport`,
     `TestRawReliableServerTransport`) to `TransportType.RawReliable`.
   - Add a `ConvertersGraph` test for the highest enum value
     (`RRUnreliableNoAck`) and the converter target.
5. Delete the remaining Ack-named test plans/stacks:
   - Delete `Tests/Pontifex.Transport.Tests/TransportStacks/RawReliableAckStacks.cs`;
     the Stage 7 `RawReliableStacks.cs` is the final one.
   - Remove any remaining `TestPlans/Raw/Reliable/Ack/**` directories.
   - Update `TransportRegistry`, `TestHarness`, namespaces, categories.
6. Update documentation and comments:
   - Replace/publish a single `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md`
     describing the handshake, reject, version/name/max, wrapper negotiation,
     and the no-ACK architecture. Delete the old Ack/NoAck spec files.
   - Sweep comments and XML docs for old names so the final greps pass:
     the converter harness comment in
     `Tests/Pontifex.Factory.Tests/Bootstrap/Harness/FakeRawUnreliableNoAckClient.cs`,
     the `tcp-noack`/`log-noack` doc literals in the renamed carrier files,
     and the Ack-named XML docs in `IRawReliableServerHandler_Ext.cs`.
   - Update the Protocol/Api/Transport readmes and `Coverage.md` files.
7. Final verification:
   - `dotnet build Pontifex.sln -v minimal` succeeds.
   - `dotnet test Pontifex.sln --no-build --logger "console;verbosity=minimal"` passes. State the expected totals in the stage report; the old 67 ignored Ack conformance tests are gone and the new handshake/wrapper tests are included.
   - `grep -R -n --include='*.cs' --include='*.csproj' -E "RawReliableAck|RawReliableNoAck|IRawReliableAck|IRawReliableNoAck|AckRejected|AckRequestMessage|AckResponseMessage|AckRequestFrame|AckResponseFrame" .` returns no matches.
   - `grep -R -n --include='*.cs' -E "tcp-noack|direct-noack|log-noack" .` returns no matches.
   - `grep -R -n --include='*.cs' -E "Handshake(Request|Response|Reject)Message" Pontifex/Implementations Transports` shows only the RawReliable handshake production code.

**Definition of done.** The Ack reliable family is gone; the factory has one
reliable transport (`tcp`/`direct`) plus `zip`/`reconnectable`/`log`
wrappers; all tests pass; the specs and coverage documents describe the new
architecture.

**Risks / notes.** This is the largest deletion stage; run the full solution
tests. Do not delete `RawUnreliableAck`/`RawUnreliableNoAck`/`RR*`/UDP. Keep
the `RawUnreliableNoAck` converter and its tests. Renumbering `TransportType`
is mandatory because `ConvertersGraph` indexes by numeric value; add the
highest-value test.

---

## Global definition of done

- `dotnet build Pontifex.sln -v minimal` succeeds with 0 errors.
- `dotnet test Pontifex.sln --no-build --logger "console;verbosity=minimal"`
  passes for every test project.
- No reliable **RawReliable** `Ack`/`NoAck` types, bindings, enum values, or
  tests remain. The `RR*` family (`RRReliableAck`, `RRReliableNoAck`, and
  their interfaces) is explicitly out of scope and remains.
- `tcp(...)` and `direct(...)` produce `RawReliable` transports with the
  mandatory handshake.
- `zip(...)`, `reconnectable(...)`, and `log(...)` wrap `RawReliable`;
  `zip`/`reconnectable` perform their own negotiation; `log` is transparent.
- `Pontifex.Api` uses `RawReliable` and its own `"api"` negotiation.
- The `RawUnreliableNoAck -> RawReliable` converter works and is tested.
- Handshake tests cover version mismatch, name mismatch, factory rejection,
  timeout, `MessageMaxByteSize` min negotiation, and reject linger
  (2 s TCP, 0 Direct, 2 s wrappers).
- The specifications and coverage/README documents describe the final
  architecture.
