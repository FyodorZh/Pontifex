# RawReliableWrapper Coverage

Layer: `RawReliableWrapper`
Subject: `Pontifex.Raw.Reliable.Protocols.RawReliableWrapperClient`,
`Pontifex.Raw.Reliable.Protocols.RawReliableWrapperServer`, and
`Pontifex.Raw.Reliable.Protocols.RawReliableWrapperLogic`
Directory: `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol`

Contract references:
[RawReliableWrapperClient.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperClient.cs),
[RawReliableWrapperServer.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperServer.cs),
[IRawReliableWrapperLogic.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/IRawReliableWrapperLogic.cs),
[RawReliableWrapperLogic.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperLogic.cs),
[RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs),
[NegotiationSession.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs).

## Covered

| Test | Contract behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `AcceptedNegotiation_AdmitsBothSidesAndForwardsDataInBothDirections` | Admission raises `OnConnected` on both sides only after a valid `Accepted` response; admitted data is forwarded in both directions. | The flat request is `[1, "layer", Request]` and the response is `[1, "layer", Accepted]`; both user handlers connect once; client-to-server and server-to-client payloads arrive exactly once. | The fake endpoint reports `MessageMaxByteSize = 4096`; the wrapper proxies report the inner endpoint value. Outbound fake payloads are cloned for assertion. |
| `VersionMismatch_StopsClientWithoutOnConnected` | A response with a different version is a protocol mismatch. | The client receives exactly one `OnStopped` whose reason is `ProtocolMismatch`, never `OnConnected`, and the inner transport is stopped. | The client sends its request in `OnConnected`; the test drives the response directly through the fake. |
| `NameMismatch_StopsClientWithoutOnConnected` | A response with a different name is a protocol mismatch. | The client receives exactly one `OnStopped` with `ProtocolMismatch` and no `OnConnected`. | Name comparison is by UTF-8 content. |
| `HandshakeTimeout_StopsClientWithoutOnConnected` | The negotiation deadline is ten seconds by default and stops an unanswered client. | A scheduled ten-second deadline exists after connect; advancing the manual clock produces exactly one `OnStopped` with `TimeOut`, no `OnConnected`, and no pending timers. | Uses the internal `INegotiationClock` seam; no wall-clock time or real timer is used. |
| `RejectLinger_IsExactlyTwoSecondsByDefault` | A rejecting server keeps the inner session open for exactly two seconds after sending the reject envelope. | The default linger is `TimeSpan.FromSeconds(2)`; the reject envelope is `[1, "layer", Rejected, "rejected"]`; no disconnect happens before the clock advances; exactly one disconnect happens at two seconds. | The session is exercised directly because the linger is a session concern. The reason is `HandshakeRejected`. |
| `PayloadBeforeAdmission_IsNeverDeliveredToUserHandler` | Application data must not reach the user handler before admission. | A non-negotiation payload before admission is released and stops the client with `ProtocolMismatch`; the user `OnReceived` count stays zero. | The session returns `false` without releasing; the wrapper owns and releases the buffer on that path. |
| `SendBeforeAdmission_IsNotRoutedToInnerTransport` | The wrapper gates `Send` until admission. | A send through the session's endpoint surface before admission returns `NotConnected`, releases the payload, and the inner endpoint observes only the negotiation request. | The proxy endpoint is obtained from the inner handler the wrapper installed; this is the same `IRawReliableEndpoint` the user would receive on admission. |
| `CustomTail_RoundTripsFromClientToServerAndBack` | Custom layer elements follow the status on both request and response. | The server `CheckNegotiationData` observes exactly `["k"]`; the client `OnNegotiationAccepted` observes exactly `["v"]`; the wire envelopes are `[1, "layer", Request, "k"]` and `[1, "layer", Accepted, "v"]`. | Hooks receive a session-owned tail. They must neither retain nor release it; the recorder converts it to strings immediately. |
| `CustomTailRejection_StopsClientWithHandshakeRejectedAndSkipsFactory` | A failed server validation rejects the connection before the user factory is called. | The client stops with `HandshakeRejected` and no `OnConnected`; the deferred server factory is never called; the server transport stays running; the two-second linger is pending. | The server reject is sent over the already-admitted inner session. Stopping the server transport cancels the linger. |
| `ServerVersionMismatch_RejectsClientSkipsFactoryAndDisconnectsAfterLinger` | The server rejects a request whose version differs from its own. | The server sends `[1, "layer", Rejected, "version mismatch"]`, the factory is skipped, the client stops with `HandshakeRejected`, and the 2 s linger disconnects the inner session. | Advancing the manual clock proves the linger delegate actually runs; the pending timer is gone afterwards. |
| `ServerNameMismatch_RejectsClientSkipsFactory` | The server rejects a request whose name differs from its own. | The server sends `[1, "layer", Rejected, "name mismatch"]`, the factory is skipped, and the client stops with `HandshakeRejected`. | Same shared rejection path, distinct validation branch. |
| `CustomTailExceedingHandshakeMaxByteSize_IsRejectedNotTruncated` | An oversized negotiation envelope is rejected, never truncated. | A client tail that pushes the request past a 16-byte maximum produces no outbound request and one `ProtocolMismatch` stop. | The header alone fits the configured maximum; only the appended tail exceeds it. The client stops locally, so no inner send occurs. |
| `ServerResponseTailExceedingMax_IsRejectedAndNeverDeliveredAsAccepted` | A server response that exceeds the maximum is replaced by a reject. | The factory runs once and creates a handler; the handler receives exactly one `OnStopped` and no `OnConnected`; the server sends a `Rejected` envelope, never an `Accepted` one; the client stops with `HandshakeRejected`. | Proves stop-once cleanup for a handler created before the response failure. The reject text is `response too large`. |
| `CustomIntTailThatLooksLikeAStatus_IsParsedAsCustomData` | The status is positional; a custom value equal to an enum value is still custom data. | A client tail `int 2` (the `Rejected` value) is delivered to `CheckNegotiationData` as `["i:2"]` and the connection is admitted. | Guards against status sniffing anywhere in the tail. |
| `AdmittedClientSession_ReceivesConnectedDisconnectedStoppedExactlyOnce` | An admitted session observes `OnConnected` → `OnDisconnected` → `OnStopped` exactly once. | Both callbacks receive the same stop-reason instance; a later transport stop produces no duplicate callbacks; the logic sees one connected and one disconnected hook. | The fake remote disconnects after admission; the terminal stop is deduplicated by the wrapper. |
| `NeverConnectedClientSession_ReceivesExactlyOneStoppedAndNoDisconnected` | A never-admitted session observes exactly one `OnStopped` and no `OnDisconnected`. | Stopping the client before a response produces `["stopped"]` only, with no `OnConnected` and no disconnect reason. | Covers the pre-admission stop path, not the rejection path. |
| `AdmittedServerSession_ReceivesConnectedDisconnectedStoppedExactlyOnce` | An admitted server session observes `OnConnected` → `OnDisconnected` → `OnStopped` exactly once. | Stopping the inner session produces the ordered lifecycle with the same reason instance; stopping the server transport afterwards does not repeat any callback; the server logic sees one connected and one disconnected hook. | Server sessions are independent; a session stop does not stop the server transport. |

## Planned

| Missing test | Owning layer |
| --- | --- |
| Concurrent accepted sessions on one wrapper server observing the serialized `TryCreate` contract. | `RawReliableWrapper` (needs a concurrency harness; the synchronous fake cannot overlap factory calls deterministically). |
| Multiple simultaneous sessions on one wrapper server with independent proxies and stop latches. | `RawReliableWrapper`. |
| Zero reject linger producing a synchronous inner disconnect. | `RawReliableWrapper` (explicit zero-linger test). |
| Server-side first payload that is a valid envelope with the wrong status (`Accepted`/`Rejected`) and the resulting reject text. | `RawReliableWrapper`. |
| A user handler exception in `OnConnected`/`OnReceived`/`OnDisconnected` observed through the wrapper stop path. | `RawReliableWrapper`. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timers, deadline accuracy, and wall-clock behaviour | The contract exposes a time seam specifically so the default suite is deterministic. Real-time accuracy belongs to a categorised integration suite. |
| Real TCP/Direct carriers, sockets, and wire framing | Covered by the carrier integration suites; this suite tests the reusable wrapper base only. |
| Zip, Reconnectable, and Api negotiation datasets | Each protocol owns its own suite; this suite proves only the shared driver and base. |
| Logging and diagnostics text | Requires an observable log sink; the `ILogger` is used only as a dependency. |
| Buffer reference-count internals | Tests assert observable ownership outcomes (`IsAlive`, no duplicate delivery) through the public contract, not private counters. |
| Security properties (authentication, confidentiality) | Out of scope for the negotiation envelope. |
| Performance and allocation counts | Not part of this contract. |
