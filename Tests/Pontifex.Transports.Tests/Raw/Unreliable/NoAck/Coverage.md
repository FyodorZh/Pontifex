# RawUnreliableNoAck Conformance Coverage

`RawUnreliableNoAckConformanceTests` is implementation-agnostic. Each concrete
adapter derives the test fixture and supplies a linked server-client topology.
Reliable-mode tests call `TryMakeReliable` before startup and are skipped when
the capability is unavailable.

## Covered

| Specification area | Coverage |
| --- | --- |
| Lifecycle | Null and invalid init, one-time init, init after start, start-before-init invalidation, null start callback, pre-start stop, one-time start, concurrent start with a single winner, failed start, injected unrecoverable failure, client start without a running server, and concurrent status reads during stop. |
| Sending | Unavailable state after a normal stop, invalid (null) messages, oversized messages, exact-size-limit messages, concurrent sends with every accepted message delivered, and send/stop races. |
| Delivery | Empty and complex content in both directions, exact-size-limit boundaries, client-to-server and server-to-client exact-once FIFO, retained server reply routes, and concurrent accepted sends. |
| Callbacks | Server and client exception isolation, server global serialization across clients, client serialization, endpoint non-reentrancy, callback start after an accepted send returns, OnStarted-completes-before-OnReceived ordering, endpoint validity and usability once OnStarted completes, OnStarted exceptions stopping a client without OnStopped, stop from a receive callback, and stop while a pre-callback gate blocks. |
| Route lifecycle | Factory source equals the endpoint RemoteEndPoint, factory null/throwing declining a message and retrying later, OnStarted throwing dropping the trigger and recreating the route, endpoint stop recreating the route for later messages, and same-route messages queuing behind a blocked handler trigger. |
| Endpoint stop | Stop returning true once then false, null stop supplying an Unknown reason, endpoint stop stopping the owning client transport, server endpoint stop keeping the server running, OnStopped invoked exactly once per endpoint, and transport stop notifying every endpoint handler. |
| Test controls | Lifecycle fault injection, transport-wide reliable debug mode, handler-factory/on-started/endpoint-stop/handler-stopped gates, and armed-gate cleanup through fixture disposal. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Sent and received reference ownership, no-handler release, retained delivery references, and duplicate-reference independence | Requires the planned custom `IMemoryRental` test implementation. |
| Malformed and oversized carrier input, client source filtering, and spoofing | Requires future implementation-specific hostile-carrier controls. |
| BufferOverflow | The generic control deliberately does not force unknown carrier queue capacity. |
| Endpoint equality and hash consistency | The suite asserts only a one-directional identity check (`RemoteEndPoint.Equals(factory source)`); mutual equality and hash consistency are not exercised. |
| Supplied-stop-reason causal preservation and generic stop-cause inspection | Exact-once `OnStopped` notification is covered; that the supplied `StopReason` reaches `OnStopped` unchanged is not asserted. |
| Loss, duplication, reordering, carrier submission timing, callback scheduler affinity, and security guarantees | The contract either permits the behavior or defines it as implementation-specific. |
