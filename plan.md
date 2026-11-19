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
- `zip`, `reconnectable`, and `api` each add an independent negotiation layer
  with a flat `UnionDataList` envelope
  `[version, name, ...custom-layer-elements]`. They can be stacked.
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
  are used until Stage 10 to avoid duplicate registrations. The handshake
  `name` is always the final contract name (`"tcp"`, `"direct"`), independent
  of the binding scheme; it is an explicit constructor parameter on both
  client and server.
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
11. The `RawReliableClientHandlerBase` interface is a temporary migration
    artifact and is removed in Stage 10.
12. The handshake `name` is the final contract name (`"tcp"`, `"direct"`),
    not the temporary binding scheme name.

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
   - Keep the Ack loggers/binding until Stage 9.
8. Update every reference to the renamed types across:
   - `Pontifex`, `Pontifex.Factory`, `Pontifex.TestUI`,
     `Pontifex.Transport.Net`, `Pontifex.Transport.Direct`, `Protocols`,
     `Api`, and all test projects.
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
- `grep -R -n --include='*.cs' --include='*.csproj' -E "RawReliableNoAck|IRawReliableNoAck" .` returns no matches.

**Definition of done.** The solution builds; all affected tests pass; no
`NoAck` reliable names remain; `RawReliableAck` still compiles and its tests
still pass.

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
   - `internal enum HandshakeStatus : byte { Rejected, VersionMismatch, NameMismatch, CapacityExceeded, Timeout }`
   - `BuildRequest(string name, int messageMaxByteSize)` returns a
     `UnionDataList` with elements `[Version, name, messageMaxByteSize]`.
   - `TryParseRequest(UnionDataList data, out string name, out int max)`
     validates exactly three elements and the types.
   - `BuildResponse(string name, int agreedMax)` and
     `TryParseResponse(UnionDataList data, out string name, out int max)`.
   - `BuildReject(short serverVersion, string serverName, HandshakeStatus status, string? text)`
     and `TryParseReject(...)`.
   - Name comparison uses UTF-8 content equality.
   - Add `internal readonly struct HandshakeOutcome` with
     `HandshakeStatus Status`, `IRawReliableServerHandler? Handler`,
     `UnionDataList? Response`, `int AgreedMaxByteSize`.
2. Add `InternalsVisibleTo` in `Pontifex/Pontifex.csproj` for
   `Pontifex.Transport.Net` and `Pontifex.Transport.Direct` (for the internal
   handshake types), plus `Pontifex.Protocol`, `Pontifex.Protocol.Zip`,
   `Pontifex.Protocol.Reconnectable`, `Pontifex.Api`, and the test assemblies
   used by later stages.
3. In `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`:
   - Replace the get-only `MessageMaxByteSize` with a
     `private volatile int _messageMaxByteSize;` field, a public getter, and
     a protected `SetMessageMaxByteSizeOnce(int value)`.
   - The constructor sets the field.
   - `ScheduleToSend` keeps reading the property, so the negotiated value is
     enforced by the existing size check.
   - `SetMessageMaxByteSizeOnce` throws `InvalidOperationException` if the
     value is less than 1 or if it is called after the endpoint has reached
     the connected/stopping stage.
4. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - Add `HandshakeRequestMessage = 7`, `HandshakeResponseMessage = 8`,
     `HandshakeRejectMessage = 9` to `MessageType`. Keep 5/6 for the Ack
     endpoints until Stage 10.
5. In `RawReliableHandshakeClientEndpoint.cs`:
   - Add constructor parameters `string handshakeName`,
     `int handshakeMaxByteSize = 128`, `TimeSpan? handshakeTimeout = null`
     (default 10 s). Validate
     `messageMaxByteSize >= handshakeMaxByteSize` at construction.
   - On low-level `OnConnected`, do **not** call `base.OnConnected()` (that
     starts keep-alive). Build the request with `handshakeName` and the
     configured max, send it via `ScheduleToSend`, and set stage
     `HandshakeSent`. Start a `System.Threading.Timer` for
     `handshakeTimeout`.
   - In `ProcessInboundMessage`, handle `HandshakeResponseMessage` by
     scheduling a completion action on `_actionQueue`: validate version,
     validate name, call `SetMessageMaxByteSizeOnce(min(local, server))`,
     set stage `LogicallyConnected`, call `base.OnConnected()` (start
     keep-alive), then call the handler's `OnConnected(this)`.
   - Handle `HandshakeRejectMessage` by scheduling a stop action: parse the
     status, map `VersionMismatch`/`NameMismatch` to `ProtocolMismatch`,
     `Rejected`/`CapacityExceeded` to `HandshakeRejected`, `Timeout` to
     `TimeOut`, and do not call `OnConnected`.
   - On timeout, schedule the same stop action with `TimeOut`.
   - `GetIsConnected` returns `IsLowLevelConnected && stage == LogicallyConnected`.
   - `FailHard`/`BeginDisconnect` set the stage to disconnected and release
     the timer.
6. In `RawReliableHandshakeServerEndpoint.cs`:
   - Add `SetPendingHandshakeResponse(UnionDataList response)` and
     `ReleasePendingHandshakeResponse()`.
   - On `OnConnected`, send the pending response and only then call the
     handler's `OnConnected`; if the response is missing, stop with
     `TextFail`.
   - Release the pending response on `FailHard`, `BeginDisconnect`, and the
     terminal `Stopped` event.
7. In `RawReliableAdmissionServerTransport.cs`:
   - Add constructor parameters `string handshakeName`,
     `int handshakeMaxByteSize = 128`, `TimeSpan? handshakeTimeout = null`
     (10 s), `TimeSpan? rejectLinger = null` (2 s).
   - Add `internal HandshakeOutcome TryHandshake(IEndPoint remote, UnionDataList request)`:
     validate version and name first; on mismatch return the status without
     calling the factory; compute `min(request.max, MessageMaxByteSize)`;
     call the factory through the existing serialized `_factoryLock` path;
     wrap the handler in `StopOnceServerHandler`; build the response with the
     agreed max; return the outcome. The caller releases the request.
   - The server endpoint is constructed with `AgreedMaxByteSize`.
8. In `RawReliableTcpServerEndpoint.cs` and `RawReliableTcpServerTransport.cs`:
   - Port the `BeginAccept`/`AcceptHandshake` flow from
     `RawReliableAckTcpServerEndpoint`:
     read the length prefix, enforce `HandshakeMaxByteSize`, deserialize the
     request, call `TryHandshake`, build the response or reject, create the
     endpoint with the pending response and `AgreedMaxByteSize`, and invoke
     the completion.
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
      response before `OnConnected` is expected.
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
    - reject linger: configure 100 ms, measure the server-side close time and
      assert it is at least the configured linger; Direct has its own 0-linger
      test in Stage 3.
    - handshake frames are not delivered through `OnReceived`.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes (existing tests migrated).
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `grep -R -n --include='*.cs' -E "HandshakeRequestMessage|HandshakeResponseMessage|HandshakeRejectMessage" .` shows only `RawReliableEndpoint.cs`, `RawReliableHandshakeClientEndpoint.cs`, `RawReliableHandshakeServerEndpoint.cs`, `RawReliableTcpServerEndpoint.cs`, and the new test file.

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
     synchronously and the channel is closed immediately.
   - Binding scheme `"direct-noack"`; type `TransportType.RawReliable`;
     parameters `id`, `handshake_max_byte_size` (default 128),
     `handshake_timeout` (default 10).
   - Keep the Ack Direct files until Stage 10; the new binding uses the
     temporary scheme to avoid a duplicate `"direct"`.
2. Add `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/RawReliable/RawReliableDirectTransportTests.cs`,
   ported from `RawReliableAckDirectTransportTests`:
   - successful handshake and `OnConnected`;
   - bidirectional order;
   - version/name mismatch -> reject without `OnConnected`;
   - factory null -> reject;
   - max-size negotiation;
   - zero linger (the channel closes immediately).
3. Do not change TestUI in this stage; it still uses the Ack Direct carrier.
   Stage 7 migrates TestUI as a whole and registers the RawReliable bindings.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `grep -R -n --include='*.cs' -E "RawReliableDirect" Transports Tests` shows the new classes and tests.

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
     envelope `[version, name, ...custom]`, validates it, and builds the
     reject envelope `[serverVersion, serverName, status, text?]`.
   - `NegotiationSession.cs` (internal): a client/server state machine that
     sends a request from `OnConnected`, waits for a response/reject, enforces
     `HandshakeMaxByteSize` (default 128) and `HandshakeTimeout` (default
     10 s), and exposes callbacks for admitted/rejected. The client **sends
     the request immediately** from `OnConnected`; it does not wait for an
     incoming payload. The server consumes the first payload as the request.
   - `NegotiationStatus` enum.
2. In `Protocols/Pontifex.Protocol/Raw/Reliable/` (namespace
   `Pontifex.Raw.Reliable.Protocols`), add:
   - `IRawReliableWrapperLogic` with `OnConnected`, `OnDisconnected`,
     `ProcessReceivedData`, `ProcessSentData`, `GetControls`, and the
     negotiation hooks. The hooks receive the **custom tail** after
     `version`/`name`, not the full envelope:
     `void FillNegotiationData(UnionDataList customData)` (client),
     `UnionDataList CreateNegotiationResponse()` (server),
     `bool CheckNegotiationData(UnionDataList customData)` (server).
   - `RawReliableWrapperLogic` abstract base with `Log`, `Memory`, and
     virtual/abstract versions of the above.
   - `RawReliableWrapperClient`/`RawReliableWrapperServer`, implementing
     `IRawReliableClient`/`IRawReliableServer` and wrapping one
     `IRawReliableClient`/`IRawReliableServer`.
   - Client/server handler proxies that forward `Send`/`Disconnect`/
     `IsConnected`/`RemoteEndPoint`/`MessageMaxByteSize` and the negotiation.
   - The wrapper uses `NegotiationSession` on the first payload after the
     inner `OnConnected`; it must not deliver `OnReceived` or report
     `IsConnected = true` before admission. On reject, it sends the reject
     envelope, waits `RejectLinger` (default 2 s, configurable), then
     disconnects the inner transport.
3. Add unit tests in
   `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol/RawReliableWrapperTests.cs`
   (fixture `RawReliableWrapperTests`, namespace `Pontifex.Tests.Raw.Reliable.Protocol`)
   with a hand-written fake `IRawReliableClient/Server`:
   - accepted negotiation;
   - version mismatch;
   - name mismatch;
   - timeout;
   - reject linger (manual driver/clock, assert 2 s);
   - no `OnReceived` before `OnConnected`;
   - `Send` before `OnConnected` is not routed to the inner transport.
4. Add an explicit `ProjectReference` to `Pontifex.Protocol` in
   `Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj`.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableWrapperTests"` passes and reports at least 7 tests.
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
     `RawReliableWrapperLogic`; implement `IRawReliableWrapperLogic`. The
     client `FillNegotiationData` adds no custom elements; the server
     `CheckNegotiationData` returns true (the session already validated the
     name `"zip"`); `CreateNegotiationResponse` returns an empty response.
     `OnConnected`/`OnDisconnected` release the compressor state;
     `ProcessReceivedData` decompresses; `ProcessSentData` compresses.
   - `CompressorLogic` base changes from `RawReliableAckWrapperLogic` to
     `RawReliableWrapperLogic`.
2. Delete the Ack Zip files and the Ack Zip binding.
3. Update `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - Replace the Zip registration with `RawReliableZipBinding`.
   - Change the `zip(tcp(...))` cases to `zip(tcp-noack(...))` and expect
     `RawReliable`.
   Do not change TestUI in this stage; Stage 7 migrates TestUI as a whole.
4. Add `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip/RawReliableZipTests.cs`
   (fixture `RawReliableZipTests`) with a fake inner transport:
   - `zip(tcp-noack(...))` connects and round-trips data;
   - **compression is proven** by asserting the inner transport observed a
     smaller/different byte stream for a compressible payload (inspect the
     fake inner transport's captured send buffer), not only that the
     application received the original bytes;
   - a raw client connecting to a `zip` server is rejected by the `"zip"`
     negotiation;
   - a version mismatch is rejected.
5. Update the Zip coverage/README docs.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableZipTests"` passes and reports at least 4 tests.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~FactoryBindings"` passes.

**Definition of done.** `zip(nested)` works over RawReliable; the Ack Zip
binding/logic are gone; compression is proven; `FactoryBindingsTests` passes.

**Risks / notes.** Zip compression can expand incompressible data; keep the
existing `MessageMaxByteSize` semantics (the wrapper reports the inner max)
and let the inner transport reject oversized compressed output as today.

---

### Stage 6 — Migrate Reconnectable to a RawReliable wrapper

**Objective.** `reconnectable(nested)` is a `RawReliable` wrapper with a
`"reconnectable"` negotiation dataset carrying session id, generation,
secret, and the composed response; reattach works; the Ack Reconnectable
binding is deleted.

**Inputs.** Stage 5 complete.

**Actions.**

1. Replace `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Ack/*`
   with `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/*`:
   - `RawReliableReconnectableBinding`: scheme `"reconnectable"`, type
     `TransportType.RawReliable`, nested `Exactly(RawReliable)`,
     `disconnect_timeout`, and `handshake_max_byte_size` (default 128) to
     allow the session payload.
   - `RawReliableReconnectableClient`/`Server`: implement
     `IRawReliableClient`/`IRawReliableServer`. The client needs a fresh
     inner transport per reconnect attempt, so it uses `NegotiationSession`
     directly (or a wrapper variant that accepts
     `Func<IRawReliableClient?>`); do not force the single-instance
     `RawReliableWrapperClient` base. The server may use the wrapper base or
     the session directly.
   - `ReconnectableClientLogic`: `FillNegotiationData` puts
     `[sessionId.Id, sessionId.Generation, secret]`; the response carries
     `[id, generation, secret, ...user response]`; expose `OnConnected` only
     after the negotiation.
   - `ReconnectableServerLogic`: `CheckNegotiationData` parses the request,
     reattaches an existing session or creates a new one, and
     `CreateNegotiationResponse` returns the composed
     `[id, generation, secret, ...user response]`. On rejection, release the
     inner session/handler exactly once (stop-once).
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
   - replace `ReconnectableInfo.AckOKResponse` assertions with the new
     negotiation response schema (no marker element; metadata order is
     id, generation, secret, user response);
   - keep the release-lifecycle assertions.
5. Update `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - replace the Reconnectable registration with the new binding;
   - change `reconnectable(30, tcp(...))` to
     `reconnectable(30, tcp-noack(...))` and expect `RawReliable`.
   Do not change TestUI in this stage; Stage 7 migrates TestUI as a whole.
6. Add `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable/RawReliableReconnectableTests.cs`
   (fixture `RawReliableReconnectableTests`):
   - first connect establishes a session and transfers data;
   - drop and reconnect with the session id/secret reattaches;
   - wrong secret is rejected and the inner session/handler is released
     exactly once (assert stop-once);
   - version/name mismatch is rejected;
   - queued messages survive reattach as today.
7. Update coverage/README docs.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableReconnectableTests"` passes and reports at least 5 tests.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~ReconnectableNegotiationResponseTests"` passes.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~FactoryBindings"` passes.

**Definition of done.** Reconnectable works over RawReliable with its own
negotiation; the Ack Reconnectable binding/logic are gone; reattach behaviour
is preserved; the Ack-response tests are migrated.

**Risks / notes.** The response can exceed 128 bytes if the user response is
large; raise `handshake_max_byte_size` for this wrapper in the binding/tests.
Do not change the `DeliverySystem` reliability semantics.

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
     `"api"`, version `1`, and an optional `AppendNegotiationData` hook; send
     the request.
   - Until admitted, `OnReceived` feeds the negotiation; after admission,
     start the `TransportPipeSystem` and raise `Connected`.
   - On negotiation reject/timeout, disconnect the endpoint and raise
     `Disconnected` once; preserve the existing `_wasConnectedEver` logic.
   - Remove `FillAckData`, `AppendAckData`, and the 777/7777 hash.
2. In `Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs`:
   - Implement `IRawReliableServerHandlerFactory<IRawReliableServerHandler>`
     instead of `IRawReliableAckServerAcknowledger`.
   - `TryCreate(IEndPoint remote)` returns the `ServerSideApiInstance<TApi>`
     (or null).
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
     rename `RawReliableAckServerLogic`/`ClientLogic` to
     `RawReliableServerLogic`/`ClientLogic`, implement
     `IRawReliableServerHandler`/`IRawReliableClientHandler`, remove
     `FillAckData`/`TryAck` and the `AckRequest`/`AckResponse` strings from
     `RawReliableAckCommonLogic` (rename to `RawReliableCommonLogic`).
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
7. Run `Api/Pontifex.Api.Tests` unchanged; it uses in-memory pipes.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Api/Pontifex.Api.Tests/Pontifex.Api.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes (30 tests today; the migrated plans must all pass).
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
2. In the client:
   - `Init(IRawReliableClientHandler)` stores the handler.
   - When the inner unreliable endpoint is available, send the handshake
     request `[1, "converter", messageMaxByteSize]` through the
     `DeliveryManager` as the first delivery-managed message.
   - The converter's endpoint layer must intercept the first delivered
     message as the handshake response; do not pass it to the application
     handler. Call `OnConnected` only after the response validates.
   - On reject/timeout, stop with `ProtocolMismatch`/`TimeOut`.
   - Remove `FillAckData` and the fabricated `7777` response.
3. In the server:
   - On a new inner session, create the `DeliveryManager`; the first
     delivery-managed message is the handshake request. Validate it, call
     `TryCreate(remote)` through a serialized path (one admission at a time,
     consistent with the transport factory contract), send the response, and
     only then call the handler's `OnConnected`.
   - Remove `TryAck` and the discarded response.
4. Use `RawReliableHandshake` for the request/response codec; the converter is
   in the `Pontifex` assembly, so the internal type is accessible. Release
   the request/response buffers exactly once after they are consumed.
5. Update `ConvertersGraph.Default` to the new converter.
6. Update test data:
   - `BootstrapTestData`, `CompilationTestData`, `CanonicalizationTestData`,
     `RegistryTestData`, `SemanticsTestData`: keep two distinct types.
     Set `TypeA = TransportType.RawUnreliableAck` and
     `TypeB = TransportType.RawReliable` (or another distinct pair), so the
     injected-graph cast test stays a real conversion. Update the
     "no conversion path from RawReliableAck" test to the new target.
7. Add `Tests/Pontifex.Factory.Tests/Converters/RawReliableConverterTests.cs`
   (fixture `RawReliableConverterTests`) with a fake
   `IRawUnreliableNoAckClient/Server`:
   - handshake accepted and messages delivered;
   - version/name mismatch rejected;
   - no `OnConnected` before the handshake response;
   - the handshake message is not delivered to the application.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliableConverterTests"` passes and reports at least 4 tests.

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
     `RawAckServerSideEndpointWrapper` to `RawReliable*`.
   - In `RawReliableServerHandlerLogger.cs`, change the implemented
     interface to `IRawReliableServerHandler` only (the Ack marker is gone).
   - Update `RawReliableServerHandlerLoggerTests.cs`:
     `RawAckServerSideEndpointWrapper` -> `RawReliableServerSideEndpointWrapper`.
   - The logger stays transparent: no negotiation, no extra round trip, no
     name/version, no `MessageMaxByteSize` change.
2. Update `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`:
   - Remove the Ack logger registration; register only
     `RawReliableLoggerBinding` (`log-noack`).
   - Remove the `log(tcp(...))` case (the Ack `log` scheme is gone).
   - Keep `log-noack(tcp-noack(...))` and expect `RawReliable`.
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
   - Remove `RawReliableClientHandlerBase` and make
     `IRawReliableClientHandler` the generic constraint; update the generic
     transport bases and the `RawReliableClientEndpoint` constructor.
   - Remove `AckRejected`; map any remaining protocol rejections to
     `ProtocolMismatch`/`HandshakeRejected`.
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
6. Update documentation:
   - Replace/publish a single `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md`
     describing the handshake, reject, version/name/max, wrapper negotiation,
     and the no-ACK architecture. Delete the old Ack/NoAck spec files.
   - Update the Protocol/Api/Transport readmes and `Coverage.md` files.
7. Final verification:
   - `dotnet build Pontifex.sln -v minimal` succeeds.
   - `dotnet test Pontifex.sln --no-build --logger "console;verbosity=minimal"` passes. State the expected totals in the stage report; the old 67 ignored Ack conformance tests are gone and the new handshake/wrapper tests are included.
   - `grep -R -n --include='*.cs' --include='*.csproj' -E "RawReliableAck|RawReliableNoAck|IRawReliableAck|IRawReliableNoAck|AckRejected" .` returns no matches.
   - `grep -R -n --include='*.cs' -E "tcp-noack|direct-noack|log-noack" .` returns no matches.
   - `grep -R -n --include='*.cs' -E "HandshakeRequestMessage|HandshakeResponseMessage|HandshakeRejectMessage" .` shows only the RawReliable handshake code and tests.

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
- No reliable `Ack`/`NoAck` types, bindings, enum values, or tests remain.
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
