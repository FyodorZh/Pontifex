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

### Type and namespace map

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
| `RawReliableNoAckClientTransport` | `RawReliableEndpointClientTransport<TClientHandler>` |
| `RawReliableNoAckServerTransport` | `RawReliableAdmissionServerTransport<TServerHandler>` |
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
  tests.
- Do not preserve backward compatibility for `RawReliableAck`/`RawReliableNoAck`.
- The transport handshake and all negotiation datasets are internal to the
  layer that owns them. External users must not observe the dataset; only a
  coarse `StopReason` and the agreed `MessageMaxByteSize` are public.
- Each stage must leave `Pontifex.sln` buildable and the affected tests green.
- The migration is a strangler: the Ack family remains until Stage 10, while
  the new RawReliable family is built alongside it.
- Temporary scheme names (`tcp-noack`, `direct-noack`, `log-noack`) are used
  until Stage 10 to avoid duplicate registrations, then switched to the final
  names (`tcp`, `direct`, `log`).

## Non-goals

- No wire compatibility and no source/binary compatibility.
- No new full carrier-independent conformance suite. The ignored Ack
  conformance suite is deleted, not ported.
- No changes to `RawUnreliable*` or `RR*` contracts or carriers.
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
3. `RawEndpoint.MessageMaxByteSize` is currently read-only and must gain a
   settable-once path for client-side negotiation. Evidence:
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
   projects pass (99 + 747 + 3 + 786 + 306 + 30 + 250 + 53 = 2274 passed,
   67 skipped in `Pontifex.Transports.Tests`). Evidence: baseline run.
9. `HandshakeMaxByteSize = 128` is a default, configurable per layer; a layer
   may raise it if its payload needs more.
10. `Reject` carries both server version and server name.
11. The `RawReliableClientHandlerBase` interface is a temporary migration
    artifact and is removed in Stage 10.
12. The transport's `Name` in the handshake is the canonical scheme
    (`"tcp"`, `"direct"`), not the instance `ITransport.Name` label.

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

**Objective.** The NoAck reliable contract, implementation, carrier, and test
layer are renamed to `RawReliable`; `RawReliableAck` remains present and
working; no behavior changes.

**Inputs.** Baseline green tree.

**Actions.**

1. In `Pontifex/Abstractions/Transports/Raw/Reliable`:
   - Move `NoAck/Client/IRawReliableNoAckClient.cs` to
     `Client/IRawReliableClient.cs` and rename the interface.
   - Move `NoAck/Client/IRawReliableNoAckClientHandler.cs` to
     `Client/IRawReliableClientHandler.cs`. Rename it to
     `IRawReliableClientHandler`; keep `OnConnected(IRawReliableEndpoint)`.
   - Rename the current neutral `Reliable/IRawReliableClientHandler.cs` to
     `Reliable/IRawReliableClientHandlerBase.cs`; it keeps no members.
   - Move `NoAck/Server/IRawReliableNoAckServer.cs` to
     `Server/IRawReliableServer.cs` and rename it.
   - Delete `NoAck/Server/IRawReliableNoAckServerHandler.cs`; use the existing
     `IRawReliableServerHandler`.
   - Move `NoAck/Server/IRawReliableNoAckServerHandlerFactory.cs` to
     `Server/IRawReliableServerHandlerFactory.cs` and rename it.
   - Update `IRawReliableClientTransport<TClientHandler>` constraint from
     `IRawReliableClientHandler` to `IRawReliableClientHandlerBase`.
   - Delete the empty `NoAck` folder.
2. In `Pontifex/Implementations/Raw/Reliable`:
   - Rename `NoAck/RawReliableNoAckClientTransport.cs` to
     `RawReliableEndpointClientTransport.cs`; rename the class to
     `RawReliableEndpointClientTransport<TClientHandler>` and update the
     constraint to `IRawReliableClientHandlerBase`.
   - Rename `NoAck/RawReliableNoAckServerTransport.cs` to
     `RawReliableAdmissionServerTransport.cs`; rename the class.
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
   - Delete `NoAck/IRawReliableNoAckClientEndpointImp.cs` and
     `NoAck/IRawReliableNoAckServerEndpointImp.cs`; use
     `IRawReliableClientEndpointImp` and `IRawReliableServerEndpointImp`.
   - Delete the `NoAck` folder.
4. In `Pontifex/Implementations/Raw/Reliable/Ext/IRawReliableServerHandler_Ext.cs`:
   - Keep the `IRawReliableServerHandler` overloads; remove the
     `IRawReliableNoAckServerHandler` overloads.
   - Keep the Ack-specific overloads until Stage 10.
5. In `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/NoAck`:
   - Rename the namespace to `Pontifex.Raw.Reliable.Tcp`.
   - Rename classes/files:
     `RawReliableNoAckTcpClientTransport` -> `RawReliableTcpClientTransport`,
     `RawReliableNoAckTcpServerTransport` -> `RawReliableTcpServerTransport`,
     `RawReliableNoAckTcpClientEndpoint` -> `RawReliableTcpClientEndpoint`,
     `RawReliableNoAckTcpServerEndpoint` -> `RawReliableTcpServerEndpoint`,
     `RawReliableNoAckTcpBinding` -> `RawReliableTcpBinding`,
     `TcpNoAckInfo` -> `RawReliableTcpInfo`.
   - Keep the binding scheme `"tcp-noack"` temporarily; keep
     `TransportType.RawReliable` (see action 6).
6. In `Pontifex/Abstractions/TransportType.cs`:
   - Rename `RawReliableNoAck` to `RawReliable` and keep its numeric value.
   - Keep `RawReliableAck` until Stage 10.
   - Update the enum comment.
7. Update every reference to the renamed types across:
   - `Pontifex`, `Pontifex.Factory`, `Pontifex.TestUI`,
     `Pontifex.Transport.Net`, `Pontifex.Transport.Direct`, `Protocols`,
     `Api`, and all test projects.
   - Use `rg "RawReliableNoAck|IRawReliableNoAck"` to find leftovers.
8. Test layer rename (mechanical):
   - `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/NoAck` ->
     `.../Client` (the RawReliable layer); same for
     `.../Server/NoAck` -> `.../Server`.
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/NoAck` ->
     `.../Client`; same for `.../Server`.
   - Rename harness/fixture/class names from `...NoAck...` to `RawReliable...`
     and update namespaces.
   - Update `Tests/Pontifex.Tests/Transports/README.md` and
     `Tests/Pontifex.Tests/Endpoints/README.md` layer tables, plus the
     affected `Coverage.md` files.
   - Move `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/NoAck/*` to
     `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/RawReliable/*` and
     rename namespaces/fixtures.
9. Update `Pontifex.TestUI/TransportFactory.cs` and the UI casts/registrations
   to the renamed types.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds with 0 errors.
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --logger "console;verbosity=minimal"` passes.
- `rg "RawReliableNoAck|IRawReliableNoAck" --glob '*.cs' --glob '*.csproj'` returns no matches.

**Definition of done.** The solution builds; all affected tests pass; no
`NoAck` reliable names remain; `RawReliableAck` still compiles and its tests
still pass.

**Risks / notes.** The rename of `IRawReliableClientHandler` to
`IRawReliableClientHandlerBase` is the only subtle part: the generic transport
constraint must use the base, and both the Ack and RawReliable client handlers
must derive from it. Do not rename the test layer by copying files; move them
with `git mv` so history and coverage links stay consistent.

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
   - Name comparison uses UTF-8 content equality
     (`IMultiRefReadOnlyByteArray.EqualByContent`).
2. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`:
   - Add `HandshakeRequestMessage = 7`, `HandshakeResponseMessage = 8`,
     `HandshakeRejectMessage = 9` to `MessageType`. Keep 5/6 for the Ack
     endpoints until Stage 10.
   - Change `MessageMaxByteSize` to a `private volatile int _messageMaxByteSize`
     backing field with a public getter and a protected
     `SetMessageMaxByteSizeOnce(int value)` that validates `>= 1` and throws
     after the value has been observed by a connected endpoint.
3. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableHandshakeClientEndpoint.cs`:
   - Add constructor parameters `int handshakeMaxByteSize = 128` and
     `TimeSpan? handshakeTimeout = null` (default 10 s).
   - Replace the current `OnConnected` notification with a handshake stage:
     on low-level connected, build the request with the transport name and
     configured max, send it via `ScheduleToSend`, and set stage
     `HandshakeSent`.
   - In `ProcessInboundMessage`, handle `HandshakeResponseMessage`:
     validate version, validate name, call
     `SetMessageMaxByteSizeOnce(min(local, server))`, set stage
     `LogicallyConnected`, then call the handler's `OnConnected(this)`.
   - Handle `HandshakeRejectMessage`: parse the status, stop with
     `ProtocolMismatch` (new stop reason) or `TimeOut`, and do not call
     `OnConnected`.
   - Add a `System.Threading.Timer` or logic-driver timeout that stops the
     endpoint with `TimeOut` after `handshakeTimeout`.
   - `GetIsConnected` returns `IsLowLevelConnected && stage == LogicallyConnected`.
   - `FailHard`/`BeginDisconnect` set the stage to disconnected and release
     the timer.
4. In `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableHandshakeServerEndpoint.cs`:
   - Add `SetPendingHandshakeResponse(UnionDataList response)` and
     `ReleasePendingHandshakeResponse()`.
   - On `OnConnected`, send the pending response and only then call the
     handler's `OnConnected`; if the response is missing, stop with
     `TextFail`.
   - Release the pending response on `FailHard`, `BeginDisconnect`, and the
     terminal `Stopped` event.
5. In `Pontifex/Implementations/Raw/Reliable/RawReliableAdmissionServerTransport.cs`:
   - Add constructor parameters `int handshakeMaxByteSize = 128`,
     `TimeSpan? handshakeTimeout = null` (10 s),
     `TimeSpan? rejectLinger = null` (2 s), and expose them as protected
     properties.
   - Add `protected (IRawReliableServerHandler handler, UnionDataList response, HandshakeStatus status)? TryHandshake(IEndPoint remote, UnionDataList request)`:
     validate the request, compute `min(request.max, MessageMaxByteSize)`,
     call the configured factory, wrap the handler in
     `StopOnceServerHandler`, build the response, and return the result.
     A null handler returns `HandshakeStatus.Rejected`; a version/name
     mismatch returns the corresponding status; the transport must release
     the request after parsing.
   - Add `protected bool IsVersionCompatible(short version)` and
     `protected string HandshakeName` hooks; default `Version = 1` and
     `HandshakeName = "tcp"`/`"direct"` set by the carrier.
6. In `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/RawReliableTcpServerEndpoint.cs`
   and `RawReliableTcpServerTransport.cs`:
   - Port the `BeginAccept`/`AcceptHandshake` flow from
     `RawReliableAckTcpServerEndpoint` into `RawReliableTcpServerEndpoint`:
     read the length prefix, validate `HandshakeMaxByteSize`, deserialize the
     request, call `TryHandshake`, build the response or reject, create the
     endpoint with the pending response, and invoke the completion.
   - The server transport's accept path calls `TryBeginAdmission`, tracks the
     handshake socket, calls `BeginAccept`, and on completion calls
     `CompleteAdmission`/`AbandonAdmission`.
   - On a reject, send the reject frame, wait `RejectLinger` (default 2 s),
     then close the socket. Do not create a session.
   - Track handshake sockets and close them on transport stop.
   - Remove the old direct `OnClientConnected` immediate-admission path.
7. In `RawReliableTcpClientEndpoint.cs` and `RawReliableTcpClientTransport.cs`:
   - Add the handshake max/timeout constructor parameters and pass them to the
     endpoint.
   - The endpoint sends the request on low-level connect and waits for the
     response; the transport's `CreateEndpoint` remains unchanged.
   - Add `timeout`/`rejectLinger` parameters to the binding
     `RawReliableTcpBinding` (scheme stays `"tcp-noack"` until Stage 10).
8. Add `Pontifex/Abstractions/StopReason/StopReasons.cs` entries:
   - `public sealed class ProtocolMismatch : StopReason` with an optional
     `Text` (version/name details go to the log, not the public reason).
   - `public sealed class HandshakeRejected : StopReason` with an optional
     `Text`.
   - Reuse `TimeOut` for handshake timeout.
9. Add tests:
   - `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/RawReliable/RawReliableTcpHandshakeIntegrationTests.cs`
     with: successful handshake; version mismatch -> client `OnStopped`
     without `OnConnected` and server factory not called; name mismatch ->
     same; factory null -> reject; handshake timeout -> `TimeOut`; max-size
     negotiation chooses the min; reject linger does not block the client
     (client reads the reject and stops).
   - Extend the existing RawReliable TCP integration tests to assert the
     handshake is not delivered through `OnReceived` and that `OnConnected`
     follows the handshake.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliable"` passes.
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build` passes.
- `rg "HandshakeRequestMessage|HandshakeResponseMessage|HandshakeRejectMessage"` shows the new frame types only in the RawReliable endpoint/handshake code.

**Definition of done.** RawReliable TCP pairs require the handshake; mismatch,
reject, timeout, and max negotiation are covered; the old Ack TCP path still
compiles and its tests still pass.

**Risks / notes.** The `AcceptHandshake` code is ~750 lines; port it rather
than rewriting it. Keep the request/response buffers inside
`HandshakeMaxByteSize` and validate the length prefix before allocation. The
client endpoint must set the negotiated max before `OnConnected`; the
`SetMessageMaxByteSizeOnce` guard enforces that.

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
     `RawReliableDirectBinding`, based on the existing
     `RawReliableAckDirect*` classes.
   - Implement `IRawReliableClient`/`IRawReliableServer` and the RawReliable
     handshake. The client sends the request as the first channel message;
     the server validates it, calls `TryHandshake`, replies with the response
     or reject, and only then creates the session.
   - `RejectLinger = TimeSpan.Zero`; the reject frame is delivered
     synchronously and the channel is closed immediately.
   - The binding scheme is `"direct-noack"` temporarily; type
     `TransportType.RawReliable`; parameters `id`, `messageMaxSize`,
     `bufferCapacityBytes`.
2. Delete the `RawReliableAckDirect*` files after the new classes compile, or
   keep them until Stage 10? Delete them in this stage because no remaining
   consumer needs Direct Ack after the protocols/Api are still on Ack? Wait:
   the protocols/Api still use Ack until Stages 5–7, and the
   `Fair`/`FactoryBindingsTests` may register `RawReliableAckDirectBinding`.
   Keep the Ack Direct files until Stage 10 to avoid breaking those
   registrations; add the RawReliable Direct files alongside. Use the
   temporary scheme `"direct-noack"` to avoid duplicate `"direct"`.
3. Add `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/RawReliable/RawReliableDirectTransportTests.cs`,
   ported from `RawReliableAckDirectTransportTests`:
   - successful handshake and `OnConnected`;
   - bidirectional order;
   - version/name mismatch -> reject without `OnConnected`;
   - factory null -> reject;
   - max-size negotiation;
   - no linger.
4. Update `Pontifex.TestUI/TransportFactory.cs` only if it registers the
   Direct Ack binding; keep both registrations for now.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build --filter "FullyQualifiedName~RawReliable"` passes.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests/Pontifex.Transport.Direct.Tests.csproj --no-build` passes.

**Definition of done.** Direct RawReliable pairs handshake and reject without
lingering; the Direct Ack tests still pass.

**Risks / notes.** Direct has no socket, so the handshake is synchronous; do
not use a timer unless the channel can stall. The server must not call the
factory until the request has been validated.

---

### Stage 4 — Add the reusable negotiation utility and the RawReliable wrapper base

**Objective.** A reusable, internal negotiation driver and a public
`RawReliableWrapperClient`/`Server` base exist; they enforce the flat
`[version, name, ...custom]` envelope, gate callbacks, time out, and are
unit-tested with a fake inner transport. No production protocol uses them yet.

**Inputs.** Stages 2–3 complete.

**Actions.**

1. In `Pontifex/Implementations/Raw/Reliable/Negotiation/`, add:
   - `RawReliableNegotiation.cs` (internal static): builds/parses the flat
     envelope `[version, name, ...custom]`, validates it, and builds the
     reject envelope `[serverVersion, serverName, status, text?]`.
   - `NegotiationSession.cs` (internal): a client/server state machine that
     sends a request, waits for a response/reject, enforces
     `HandshakeMaxByteSize` (default 128), `HandshakeTimeout` (default 10 s),
     and exposes callbacks for admitted/rejected. It must not allocate per
     message beyond the request/response `UnionDataList`.
   - A `NegotiationStatus` enum equivalent to the transport statuses.
2. Add `InternalsVisibleTo` entries in `Pontifex/Pontifex.csproj` for
   `Pontifex.Protocol`, `Pontifex.Protocol.Zip`,
   `Pontifex.Protocol.Reconnectable`, `Pontifex.Api`,
   `Pontifex.Transports.Tests`, `Pontifex.Transport.Tests`,
   `Pontifex.Transport.Net.Tests`, `Pontifex.Transport.Direct.Tests`,
   `Pontifex.Tests`, `Pontifex.Factory.Tests`, `Pontifex.Api.Tests`.
3. In `Protocols/Pontifex.Protocol/Raw/Reliable/` (new namespace
   `Pontifex.Raw.Reliable.Protocols`), add:
   - `IRawReliableWrapperLogic` (client/server shared) with
     `void OnConnected()`, `void OnDisconnected()`,
     `bool ProcessReceivedData(UnionDataList)`,
     `bool ProcessSentData(UnionDataList)`,
     `void GetControls(List<IControl>, Predicate<IControl>?)`,
     and negotiation hooks:
     `void FillNegotiationData(UnionDataList data)`,
     `bool CheckNegotiationData(UnionDataList data)`,
     `UnionDataList CreateNegotiationResponse()`.
   - `RawReliableWrapperClient`/`RawReliableWrapperServer`, implementing
     `IRawReliableClient`/`IRawReliableServer` and wrapping
     `IRawReliableClient`/`IRawReliableServer`.
   - Client/server handler proxies that forward `Send`/`Disconnect`/
     `IsConnected`/`RemoteEndPoint`/`MessageMaxByteSize` and the negotiation.
   - The wrapper uses `NegotiationSession` on the first payload after the
     inner `OnConnected`; it must not deliver `OnReceived` or report
     `IsConnected = true` before admission. On reject, it sends the reject
     envelope, waits `RejectLinger` (default 2 s, configurable), then
     disconnects the inner transport.
4. Add unit tests in `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol/`
   with a hand-written fake `IRawReliableClient/Server`:
   - accepted negotiation;
   - version mismatch;
   - name mismatch;
   - timeout;
   - reject linger (manual driver/clock);
   - no `OnReceived` before `OnConnected`;
   - `Send` before `OnConnected` is not routed to the inner transport.
5. Add `Protocols/Pontifex.Protocol` as a project reference to
   `Tests/Pontifex.Transports.Tests` if not already available transitively
   (it references Zip/Reconnectable, which reference Protocol; add it
   explicitly for clarity).

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~Protocol"` passes.
- The existing Zip/Reconnectable tests (if any) and factory tests still pass.

**Definition of done.** The wrapper base and negotiation utility compile,
are unit-tested with a fake transport, and are ready for Zip/Reconnectable.

**Risks / notes.** Keep the utility internal; only the wrapper base is public.
The wrapper must not add a handshake when used by `log` (log does not derive
from this base; it is a separate transparent wrapper).

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
     nested `TransportTypeSet.Exactly(TransportType.RawReliable)`,
     `compression_level` parameter.
   - `RawReliableZipClientLogic`/`RawReliableZipServerLogic`: implement
     `IRawReliableWrapperLogic`; the client `FillNegotiationData` adds no
     custom elements; the server `CheckNegotiationData` accepts the name
     `"zip"` and returns an empty response; `OnConnected`/`OnDisconnected`
     release the compressor state; `ProcessReceivedData` decompresses;
     `ProcessSentData` compresses.
   - `CompressorLogic` base changes from `RawReliableAckWrapperLogic` to the
     new wrapper logic base.
2. Delete the Ack Zip files and the Ack Zip binding.
3. Update `Pontifex.TestUI/TransportFactory.cs` and
   `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs` (if it registers
   Zip) to the new binding.
4. Add a targeted test in `Tests/Pontifex.Transports.Tests` or
   `Tests/Pontifex.Transport.Net.Tests`:
   - `zip(tcp-noack(...))` connects, transfers compressed data in both
     directions, and the app receives the original bytes;
   - a raw client connecting to a `zip` server is rejected by the `"zip"`
     negotiation;
   - a version mismatch is rejected.
5. Update coverage/README docs for the new protocol layer.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~Zip"` passes.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --filter "FullyQualifiedName~Zip"` passes, if the test lives there.

**Definition of done.** `zip(nested)` works over RawReliable; the Ack Zip
binding/logic are gone; the compression behaviour is unchanged.

**Risks / notes.** Zip compression can expand incompressible data; keep the
existing `MessageMaxByteSize` semantics (the wrapper reports the inner max)
and let the inner transport reject oversized compressed output as today.

---

### Stage 6 — Migrate Reconnectable to a RawReliable wrapper

**Objective.** `reconnectable(nested)` is a `RawReliable` wrapper with a
`"reconnectable"` negotiation dataset carrying session id, generation,
secret, and the composed response; reattach works; the Ack Reconnectable
binding is deleted.

**Inputs.** Stage 5 complete (the wrapper base and Zip validate the pattern).

**Actions.**

1. Replace `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Ack/*`
   with `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/*`:
   - `RawReliableReconnectableBinding`: scheme `"reconnectable"`, type
     `TransportType.RawReliable`, nested `Exactly(TransportType.RawReliable)`,
     `disconnect_timeout` parameter.
   - `RawReliableReconnectableClient`/`Server`: implement
     `IRawReliableClient`/`IRawReliableServer`, wrap RawReliable, and use the
     wrapper base/negotiation.
   - `ReconnectableClientLogic`: `FillNegotiationData` puts
     `[sessionId.Id, sessionId.Generation, secret, ...user data]`; on the
     response, parse the id/generation/secret and user response; expose
     `OnConnected` only after the negotiation.
   - `ReconnectableServerLogic`: `CheckNegotiationData` parses the request,
     reattaches an existing session or creates a new one, and
     `CreateNegotiationResponse` returns the composed
     `[id, generation, secret, ...user response]`.
   - Remove `AckRequest`/`AckOKResponse` markers from
     `ReconnectableInfo`; the wrapper name/version identifies the layer.
   - Preserve the `DeliverySystem` and session map behaviour; only the
     admission exchange moves to the wrapper negotiation.
2. Delete the Ack Reconnectable files and binding.
3. Update `Pontifex.TestUI/TransportFactory.cs` and
   `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs`.
4. Add tests in `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable/`
   (with a fake or real Direct/TCP inner transport):
   - first connect establishes a session and transfers data;
   - drop and reconnect with the session id/secret reattaches;
   - wrong secret is rejected;
   - version/name mismatch is rejected;
   - queued messages survive reattach as today.
5. Update coverage/README docs.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Transports.Tests/Pontifex.Transports.Tests.csproj --no-build --filter "FullyQualifiedName~Reconnectable"` passes.
- `dotnet test Tests/Pontifex.Transport.Net.Tests/Pontifex.Transport.Net.Tests.csproj --no-build --filter "FullyQualifiedName~Reconnectable"` passes, if applicable.

**Definition of done.** Reconnectable works over RawReliable with its own
negotiation; the Ack Reconnectable binding/logic are gone; reattach behaviour
is preserved.

**Risks / notes.** The response can be larger than 128 bytes if the user
response is large; configure `HandshakeMaxByteSize` for this wrapper
accordingly (default 128, but raise it if a test needs it). Do not change the
`DeliverySystem` reliability semantics.

---

### Stage 7 — Migrate Pontifex.Api to RawReliable

**Objective.** `Pontifex.Api` uses `IRawReliableClientHandler` /
`IRawReliableServerHandlerFactory` and its own `"api"` negotiation; the ACK
interfaces, `FillAckData`, `AppendAckData`, and the 777/7777 hash are gone.

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
   - Remove the `ackData` parameter and the `777` check; the Api negotiation
     in the instance performs validation.
3. In `Api/Pontifex.Api/Transport/Transport/ServerSideApiInstance.cs`:
   - Implement `IRawReliableServerHandler` (it already does) and start a
     `NegotiationSession` server on `OnConnected`.
   - On the request, validate name/version, reply, then start the API and
     raise `ApiStarted`.
   - Before admission, `OnReceived` feeds the negotiation; after, it feeds
     the pipe.
4. Update the `ServerSideApiFactory` constructor from
   `Func<UnionDataList, ServerSideApiInstance<TApi>?>` to
   `Func<ServerSideApiInstance<TApi>>` (or keep the parameter but pass an
   empty list; prefer the simpler signature).
5. Update `Tests/Pontifex.Transport.Tests/Core/TestHarness.cs`:
   - Cast to `IRawReliableClient`/`IRawReliableServer`.
   - Update `ServerSideApiFactory` lambdas.
6. Update the Api test plans under
   `Tests/Pontifex.Transport.Tests/TestPlans/Raw/Reliable/*` (currently
   `.../Ack/...`) to the new transport and factory signatures.
7. `Api/Pontifex.Api.Tests` uses in-memory pipes and should pass unchanged;
   run it.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Api/Pontifex.Api.Tests/Pontifex.Api.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.Transport.Tests/Pontifex.Transport.Tests.csproj --no-build` passes.
- `rg "IRawReliableAck|FillAckData|AppendAckData" Api` returns no matches.

**Definition of done.** The Api runs over RawReliable with its own negotiation;
the Api tests and transport Api test plans pass.

**Risks / notes.** The transport `OnConnected` now fires before the Api
handshake; the Api must suppress its `Connected` event and inbound data until
its own negotiation completes. Preserve the exact `Disconnected`-once
behaviour.

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
   - `RawUnreliableNoAckToRawReliableAckConverter` ->
     `RawUnreliableNoAckToRawReliableConverter`; `From =
     TransportType.RawUnreliableNoAck`, `To = TransportType.RawReliable`.
   - `RawUnreliableNoAckToRawReliableAckClient/Server` ->
     `RawUnreliableNoAckToRawReliableClient/Server`; implement
     `IRawReliableClient`/`IRawReliableServer`.
2. In the client:
   - `Init(IRawReliableClientHandler)` stores the handler.
   - When the inner unreliable endpoint is available, send the handshake
     request `[1, "converter", messageMaxByteSize]` through the
     `DeliveryManager` as the first delivery-managed message.
   - Do not call `OnConnected` until the response arrives and validates.
   - On reject/timeout, stop with `ProtocolMismatch`/`TimeOut`.
   - Remove `FillAckData` and the fabricated `7777` response.
3. In the server:
   - On a new inner session, create the `DeliveryManager`, wait for the first
     delivery-managed message, validate the handshake, call
     `TryCreate(remote)`, send the response, and only then call the handler's
     `OnConnected`.
   - Remove `TryAck` and the discarded response.
4. Use `RawReliableHandshake` for the request/response codec; the converter is
   in the `Pontifex` assembly, so the internal type is accessible.
5. Update `ConvertersGraph.Default` to the new converter.
6. Update `Tests/Pontifex.Factory.Tests`/`Pontifex.TransportFormula.Tests`
   expectations:
   - `TransportType.RawReliableAck` -> `TransportType.RawReliable` where the
     converter target is asserted.
   - `BootstrapTestData.TypeA`/`TypeB` assignments if they rely on the old
     enum names.
   - Any "no conversion path from RawReliableAck" test becomes the new
     target.
7. Add a targeted converter test with a fake `IRawUnreliableNoAckClient`/
   `Server`:
   - handshake accepted and messages delivered;
   - version/name mismatch rejected;
   - no `OnConnected` before the handshake response.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds.
- `dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --no-build` passes.
- `dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj --no-build` passes.
- The targeted converter test passes.

**Definition of done.** The converter targets RawReliable and performs the
handshake; the factory/formula tests pass.

**Risks / notes.** The inner unreliable transport is lossy; the handshake
must go through the `DeliveryManager`, not raw `Send`, so it is reliably
retried. Do not deliver the handshake message to the application handler.

---

### Stage 9 — Migrate the logging wrapper and TestUI

**Objective.** There is one transparent `RawReliable` logging wrapper; TestUI
uses RawReliable types and logic; no ACK request/response remains in TestUI.

**Inputs.** Stage 8 complete.

**Actions.**

1. Merge the logger wrappers:
   - `RawReliableAckClientLogger`/`RawReliableNoAckClientLogger` ->
     `RawReliableClientLogger`; same for the server.
   - `RawReliableAckLoggerBinding`/`RawReliableNoAckLoggerBinding` ->
     `RawReliableLoggerBinding`; scheme stays `"log-noack"` temporarily
     (switch to `"log"` in Stage 10); type `TransportType.RawReliable`;
     nested `Exactly(TransportType.RawReliable)`.
   - Rename `RawAckBaseEndpointWrapper`/`RawAckClientSideEndpointWrapper`/
     `RawAckServerSideEndpointWrapper` to `RawReliable*` if the names imply
     Ack; they already implement `IRawReliableEndpoint`.
   - The logger must remain transparent: no negotiation, no extra round trip,
     no name/version, no `MessageMaxByteSize` change.
2. Update `Tests/Pontifex.Tests/RawReliableAckServerLoggerTests.cs` and
   `RawReliableNoAck*LoggerTests.cs`:
   - Merge into `RawReliableClientLoggerTests.cs` and
     `RawReliableServerLoggerTests.cs`.
   - Assert the transparent forwarding and the endpoint wrapper `Send`/
     `Disconnect` logging.
3. TestUI:
   - `Pontifex.TestUI/TransportFactory.cs`: register
     `RawReliableTcpBinding`, `RawReliableDirectBinding`,
     `RawReliableZipBinding`, `RawReliableReconnectableBinding`,
     `RawReliableLoggerBinding`; remove Ack registrations.
   - `ServerWindow.cs`/`ClientWindow.cs`: use `IRawReliableServer`/
     `IRawReliableClient`; rename the logic classes to
     `RawReliableServerLogic`/`RawReliableClientLogic`.
   - `TestLogic/Raw/Reliable/Ack/*` -> `TestLogic/Raw/Reliable/*`; implement
     `IRawReliableServerHandler`/`IRawReliableClientHandler`; remove
     `FillAckData`/`TryAck` and the `AckRequest`/`AckResponse` strings from
     `RawReliableAckCommonLogic` (rename to `RawReliableCommonLogic`).
   - `Ui/Controls/Types/RawReliableAckClientControlView.cs` ->
     `RawReliableClientControlView.cs`; update `ControlViewFactory`.
   - Delete `IRawReliableAckTcpClientDebugControl` and its view (no transport
     implements it; it is dead).
4. Update `Pontifex.TestUI/TestApi/Brute/RawReliableAckProtocol.cs` and
   `ApiFactory.cs` only if the Api type names change; the Api surfaces should
   stay compatible, so prefer no changes beyond imports.

**Verification.**

- `dotnet build Pontifex.sln -v minimal` succeeds (TestUI included).
- `dotnet test Tests/Pontifex.Tests/Pontifex.Tests.csproj --no-build --filter "FullyQualifiedName~Logger"` passes.
- `rg "RawReliableAck|FillAckData|TryAck" Pontifex.TestUI` returns no matches.

**Definition of done.** One transparent logger exists; TestUI builds and uses
RawReliable; the ACK request/response logic is gone from TestUI.

**Risks / notes.** The logger is the only wrapper without a negotiation; do
not route it through `RawReliableWrapperClient/Server`. Keep the endpoint
wrapper names consistent with the new contract.

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
   - `RawReliableAckClientTransport`, `RawReliableAckServerTransport`,
     `RawReliableAckClient`, `RawReliableAckServer`,
     `StopOnceAckServerHandler`, `RawReliableAckInfo`,
     `IRawAckClientHandler_Ext`, the Ack logger wrappers/binding, the Ack
     conformance controls and endpoint wrappers.
2. Delete the Ack carriers:
   - `Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/**` (including
     `TcpInfo`; keep the RawReliable `TcpInfo`).
   - `Transports/Pontifex.Transport.Direct/Raw/Reliable/Ack/**`.
   - `IRawReliableAckTcpClientDebugControl` and its TestUI view.
3. Delete the Ack tests and conformance suite:
   - `Tests/Pontifex.Tests/Endpoints/RawReliable/**/Ack/**` and
     `.../Transports/.../Ack/**`.
   - `Tests/Pontifex.Transport.Net.Tests/Raw/Reliable/Ack/**`.
   - `Tests/Pontifex.Transport.Direct.Tests/Raw/Reliable/Ack/**`.
   - `Tests/Pontifex.Transports.Tests/Raw/Reliable/Ack/**` (the ignored
     conformance suite).
   - Update `Coverage.md`/`README.md` and category filters.
4. Update factory and type surfaces:
   - Remove `TransportType.RawReliableAck`; update the enum comment and
     `ConvertersGraph` expectations.
   - Remove the Ack bindings from `Pontifex.TestUI`, `TransportRegistry`,
     `FactoryBindingsTests`, and any test data.
   - Switch temporary schemes to final: `tcp-noack` -> `tcp`,
     `direct-noack` -> `direct`, `log-noack` -> `log`.
   - Rename `RawReliableTcpInfo` -> `TcpInfo`; ensure no duplicate.
   - Remove `RawReliableClientHandlerBase` and make
     `IRawReliableClientHandler` the generic constraint; update the generic
     transport bases.
   - Remove `AckRejected` or replace it with the new `ProtocolMismatch`/
     `HandshakeRejected` reasons; update references.
5. Rename the remaining `Ack`-named test plans/stacks:
   - `Tests/Pontifex.Transport.Tests/TransportStacks/RawReliableAckStacks.cs`
     -> `RawReliableStacks.cs`.
   - `Tests/Pontifex.Transport.Tests/TestPlans/Raw/Reliable/Ack/**` ->
     `.../RawReliable/**`.
   - Update `TransportRegistry`, `TestHarness`, namespaces, categories.
6. Update documentation:
   - Replace `Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Specification.md`
     and `NoAck/Specification.md` with a single
     `Raw/Reliable/Specification.md` describing the handshake, reject,
     version/name/max, wrapper negotiation, and the no-ACK architecture.
   - Update the Protocol/Api/Transport readmes and `Coverage.md` files.
7. Final verification:
   - `dotnet build Pontifex.sln -v minimal` succeeds.
   - `dotnet test Pontifex.sln --no-build --logger "console;verbosity=minimal"` passes. The Ack conformance skips are gone; expected totals change accordingly.
   - `rg "RawReliableAck|RawReliableNoAck|IRawReliableAck|IRawReliableNoAck|AckRejected" --glob '*.cs' --glob '*.csproj'` returns no matches.
   - `rg "tcp-noack|direct-noack|log-noack" --glob '*.cs'` returns no matches.
   - `rg "HandshakeRequestMessage|HandshakeResponseMessage|HandshakeRejectMessage"` shows only the RawReliable handshake code.

**Definition of done.** The Ack reliable family is gone; the factory has one
reliable transport (`tcp`/`direct`) plus `zip`/`reconnectable`/`log`
wrappers; all tests pass; the specs and coverage documents describe the new
architecture.

**Risks / notes.** This is the largest deletion stage; run the full solution
tests. Do not delete `RawUnreliableAck`/`RawUnreliableNoAck`/`RR*`/UDP. Keep
the `RawUnreliableNoAck` converter and its tests. If a test still references
an Ack type, migrate it rather than deleting it unless it is an Ack-only
conformance test.

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
