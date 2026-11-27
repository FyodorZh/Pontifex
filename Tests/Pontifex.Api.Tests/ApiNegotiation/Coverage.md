# ApiNegotiation Coverage

Layer: `ApiNegotiation`
Subject: `Pontifex.Api.ClientSideApi`, `Pontifex.Api.ServerSideApiFactory<TApi>`,
and `Pontifex.Api.ServerSideApiInstance<TApi>`
Directory: `Tests/Pontifex.Api.Tests/ApiNegotiation`

Contract references:
[ClientSideApi.cs](../../../Api/Pontifex.Api/Transport/Transport/ClientSideApi.cs),
[ServerSideApi.cs](../../../Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs),
[ServerSideApiInstance.cs](../../../Api/Pontifex.Api/Transport/Transport/ServerSideApiInstance.cs),
[NegotiationSession.cs](../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs),
[RawReliableNegotiation.cs](../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs).

## Covered

| Test | Contract behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ServerApi_WithRawApplicationPayloadInsteadOfRequest_RejectsAndDoesNotStartApi` | The Api server consumes the first inbound payload as the `"api"` negotiation request and rejects anything else. | A non-negotiation first payload produces one `Rejected` envelope (`[1, "api", Rejected, text]`), never starts the Api (`ApiStarted` count zero), leaves the server transport running, and makes the client raise no `Connected` and exactly one `Disconnected` with `HandshakeRejected`. | The test replaces the client request with a raw application payload on the fake link. The server-side rejection linger is left pending and cancelled when the pair is disposed. |
| `ClientApi_WithWrongVersionResponse_RaisesDisconnectedOnceAndNoConnected` | A response version different from the requested version is a protocol mismatch. | The client never raises `Connected`, raises `Disconnected` exactly once, and the reason is `ProtocolMismatch`. | The server-side envelope is replaced before delivery, so the server still accepts its own side; only the client validates the version. |
| `ClientApi_WithWrongNameResponse_RaisesDisconnectedOnceAndNoConnected` | A response name different from `"api"` is a protocol mismatch. | The client never raises `Connected`, raises `Disconnected` exactly once with `ProtocolMismatch`. | Name comparison is by UTF-8 content. |
| `ClientApi_FirstOutboundPayload_IsTheApiNegotiationRequest` | The client sends its Api negotiation request immediately on transport connect. | The first and only outbound payload is `[1, "api", Request]` before any application traffic. | The fake peer drops the request, so the Api stays unadmitted. The pending negotiation deadline is disposed with the transport. |
| `ServerApi_WithAppendedNegotiationElements_AdmitsTheSession` | The server validates name/version only and ignores appended elements. | A client whose `AppendNegotiationData` appends `int 42` sends `[1, "api", Request, 42]`; the server admits the session, the client raises `Connected` once, the Api starts once, and no disconnect callback fires. | The hook is append-only and its count is one per connect. Proves the reserved custom-tail space cannot break admission and that admission produces no spurious terminal callback. |
| `ClientApi_WithManualClock_TimesOutAtTheNegotiationDeadline` | The Api client negotiation completes or times out at the 10 s deadline. | With an injected manual timer factory, only the client deadline remains armed after the server accepts; advancing 10 s raises exactly one `Disconnected` with `TimeOut`, no `Connected`, and leaves no timer armed. | Uses the internal `ClientSideApi` timer overload; the public constructor keeps `SystemTimerFactory`. |
| `ServerApi_WithManualClock_RejectsAtTheDeadlineAndLingersTwoSeconds` | The server negotiation deadline rejects with `"negotiation timeout"` and lingers 2 s before disconnecting. | Both sides arm 10 s deadlines; advancing to the deadline sends exactly one `Rejected` envelope with the timeout text, does not start the Api, keeps the session open for 2 s, then disconnects and leaves no timer armed. | The client request is dropped so the server stays unnegotiated; the linger is the shared session default. |
| `ServerApiFactory_CreatesAFreshInstancePerAdmittedSession` | `ServerSideApiFactory` creates a fresh `ServerSideApiInstance` per admitted session. | Two sessions on one factory produce two distinct handlers and instances; each starts its own Api (`ApiStarted` fires twice), and the existing session's instance is untouched. | The harness factory now returns a new instance per call; the second response is dropped because the first client is already admitted. |

## Planned

| Missing test | Owning layer |
| --- | --- |
| A user `AppendNegotiationData` hook exception and the resulting stop path. | `ApiNegotiation`. |
| A fabricated status value in the third element (unknown discriminator) rejected before the custom tail is parsed. | `ApiNegotiation` (not covered by the wrapper suite; it is a genuine gap here). |
| `ClientSideApi` delivering admitted application data into the `TransportPipeSystem`. | `ApiNegotiation` (requires a declared Api pipe; the Api pipe suite covers pipes over in-memory transports). |
| Concurrent Api sessions on one `ServerSideApiFactory` with independent instances. | `ApiNegotiation` (only the overlap clause needs a concurrency harness; sequential per-session freshness is now covered). |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timers and negotiation deadline accuracy | Deadline and linger ordering are covered deterministically with a manual clock. Wall-clock accuracy of the production timer belongs to the shared-negotiation and carrier integration suites; this suite never waits on real time. |
| Real TCP/Direct carriers, sockets, and wire framing | Covered by the carrier integration suites; this suite tests the Api layer only. |
| Zip, Reconnectable, and logger wrappers | Each wrapper owns its suite; the Api is a sibling protocol layer. |
| Admission payloads, authentication, and encryption | Out of scope for the Api negotiation envelope. |
| Logging and diagnostics text | Requires an observable log sink; the logger is used only as a dependency. |
| Performance and allocation counts | Not part of this contract. |
