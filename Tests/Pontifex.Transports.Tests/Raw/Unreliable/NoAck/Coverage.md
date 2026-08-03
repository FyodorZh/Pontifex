# RawUnreliableNoAck Conformance Coverage

`RawUnreliableNoAckConformanceTests` is implementation-agnostic. Each concrete
adapter derives the test fixture and supplies a linked server-client topology.
Reliable-mode tests call `TryMakeReliable` before startup and are skipped when
the capability is unavailable.

## Covered

| Specification area | Coverage |
| --- | --- |
| Lifecycle | Null callbacks, pre-start stop, concurrent start, one-time start, normal stop state, failed start, injected unrecoverable failure, and concurrent status reads during stop. |
| Sending | Unavailable state, invalid message, invalid endpoint, state and size precedence, oversized messages, exact-limit messages, post-stop sends, concurrent sends, and send/stop races. |
| Delivery | Empty and complex content, complete boundaries, client-to-server and server-to-client exact-once FIFO, retained server reply routes, and concurrent accepted sends. |
| Callbacks | Server and client exception isolation, server global serialization across clients, client serialization, non-reentrancy, callback start after accepted-send return, stop from a callback, and stop while a pre-callback gate blocks. |
| Test controls | Lifecycle fault injection and checkpoint cleanup through fixture disposal. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Sent and received reference ownership, no-handler release, retained delivery references, and duplicate-reference independence | Requires the planned custom `IMemoryRental` test implementation. |
| Malformed and oversized carrier input, client source filtering, and spoofing | Requires future implementation-specific hostile-carrier controls. |
| BufferOverflow | The generic control deliberately does not force unknown carrier queue capacity. |
| Endpoint equality and hash consistency | Explicitly excluded from the shared suite. |
| Exact-once stop notification and supplied-stop-reason causal preservation | Exact-once detection and generic cause inspection are explicitly excluded. |
| Loss, duplication, reordering, carrier submission timing, callback scheduler affinity, and security guarantees | The contract either permits the behavior or defines it as implementation-specific. |
