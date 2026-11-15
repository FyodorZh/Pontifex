# RawUnreliable Conformance Coverage

`RawUnreliableConformanceTests` is the shared, implementation-agnostic
conformance suite for both the Ack and NoAck RawUnreliable variants. Each
concrete adapter derives the test fixture and supplies a linked server-client
topology. Reliable-mode tests call `TryMakeReliable` before startup; a
dedicated guard test fails (never skips) when an adapter cannot provide the
mode, so the suite cannot silently degrade to ignores.

Variant-specific coverage is documented in the variant files:
[RawUnreliableAck](Ack/Coverage.md) and [RawUnreliableNoAck](NoAck/Coverage.md).

## Covered

| Specification area | Coverage |
| --- | --- |
| Lifecycle | Null and invalid init, one-time init, init after start, start-before-init invalidation, null start callback, pre-start stop, one-time start, concurrent start with a single winner, failed start, injected unrecoverable failure, client start without a running server, and concurrent status reads during stop. |
| Sending | Unavailable state after a normal stop, invalid (null) messages, oversized messages, exact-size-limit messages, concurrent sends with every accepted message delivered, and send/stop races. A send that cannot reach the carrier is released and dropped silently. Each client endpoint has a local byte budget; a server transport shares one byte-and-message budget across all of its session endpoints. The caps are declared in the carrier `*Info` classes (`MessageMaxByteSize * QueueCapacity` bytes, `QueueCapacity` messages) and wired through the transport budget hooks. |
| Delivery | Empty and complex content in both directions, exact-size-limit boundaries, client-to-server and server-to-client exact-once FIFO, retained server reply routes, and concurrent accepted sends. |
| Callbacks | Server and client exception isolation, server per-endpoint serialization (`ServerCallbacks_AreSerializedPerEndpoint` — callbacks of different endpoints may overlap and the suite neither requires nor forbids that), client serialization, endpoint non-reentrancy, `AcceptedSend_DoesNotBeginReceiveCallbackBeforeTrySendReturns` proving the commit is off the caller's `Send` path and the handler observes the send already returned, OnStarted-completes-before-OnReceived ordering, endpoint validity and usability once OnStarted completes, OnStarted exceptions stopping a client without OnStopped, stop from a receive callback, and stop while a pre-callback gate blocks. |
| Route lifecycle | Factory source equals the endpoint RemoteEndPoint, factory null/throwing declining a message and retrying later, OnStarted throwing dropping the trigger and recreating the route, endpoint stop recreating the route for later messages, and same-route messages queuing behind a blocked handler trigger. |
| Endpoint stop | Stop returning true once then false, null stop supplying an Unknown reason, endpoint stop stopping the owning client transport, server endpoint stop keeping the server running, OnStopped invoked exactly once per endpoint, and transport stop notifying every endpoint handler. The stop-transition gate is hit after the endpoint has already become invalid; the handler-stopped gate is hit while the stop is still pending. |
| Test controls | Lifecycle fault injection, transport-wide reliable debug mode, handler-factory/handler-started gates, relocated endpoint-stop/handler-stopped/send-commit/received gates, and armed-gate cleanup through fixture disposal. `TryMakeReliable_ClientAndServer_ReportSupported` FAILS rather than skips when an adapter cannot provide reliable debug mode, so the suite cannot silently degrade to ignores. |

## Notes

- Carrier drops are silent by design. A direct send with no registered server, a UDP queue overflow, or a send to a stopped shared sender releases the message without logging. Log silence is **not** asserted at the transport level; the only log-silence assertions in the repository are the endpoint-layer inbox-cap tests.
- Server callbacks are serialized per endpoint, not globally. Each endpoint owns its serial queue, so callbacks of different server endpoints may run concurrently. `ServerCallbacks_AreSerializedPerEndpoint` asserts only that one handler never runs two callbacks at once. Cross-endpoint overlap depends on scheduling two independent ingress paths and is not asserted; the test pins the per-endpoint bound, not the cross-endpoint concurrency permission.
- Outbound budget scope differs by role: a client endpoint has a local byte budget equal to `MessageMaxByteSize * QueueCapacity`; a server transport shares one `SendBudget` (bytes and messages) across every session endpoint. `BufferOverflow` is a best-effort rejection and is not forced by the shared suite. The caps are declared in the carrier `*Info` classes; every leaf sources its queue capacity, and the UDP leaves source both caps directly, from those constants. Loose per-carrier wiring tests in `Pontifex.Transport.Direct.Tests` and `Pontifex.Transport.Net.Tests` pin the derived values against `*Info`.
- The UDP client's inbound source filter is carrier-specific and is covered by `Tests/Pontifex.Transport.Net.Tests/Raw/Unreliable/RawUnreliableUdpClientEndpointFilterTests` (a real-loopback loose test), not by this carrier-independent suite. That test assumes cross-socket loopback FIFO for its ordering barrier; see its remarks. Direct has no inbound source selector: each client has exactly one channel to its configured server.

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Sent and received reference ownership, no-handler release, retained delivery references, and duplicate-reference independence | Requires the planned custom `IMemoryRental` test implementation. |
| Malformed and oversized carrier input, and spoofing | Requires future implementation-specific hostile-carrier controls. The UDP client's source filter is covered separately by the real-loopback test in `Pontifex.Transport.Net.Tests`. |
| BufferOverflow | The generic control deliberately does not force unknown carrier queue capacity. |
| Endpoint equality and hash consistency | The suite asserts only a one-directional identity check (`RemoteEndPoint.Equals(factory source)`); mutual equality and hash consistency are not exercised. |
| Supplied-stop-reason causal preservation and generic stop-cause inspection | Exact-once `OnStopped` notification is covered; that the supplied `StopReason` reaches `OnStopped` unchanged is not asserted. |
| Loss, duplication, reordering, carrier submission timing, callback scheduler affinity, and security guarantees | The contract either permits the behavior or defines it as implementation-specific. |
