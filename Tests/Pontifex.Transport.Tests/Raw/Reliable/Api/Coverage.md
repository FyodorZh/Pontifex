# RawReliableApiNegotiation Coverage

Layer: `RawReliableApiNegotiation`
Subject: `Pontifex.Api.ClientSideApi`, `Pontifex.Api.ServerSideApiFactory<TApi>`,
and `Pontifex.Api.ServerSideApiInstance<TApi>`
Directory: `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api`

Contract references:
[ClientSideApi.cs](../../../../../Api/Pontifex.Api/Transport/Transport/ClientSideApi.cs),
[ServerSideApi.cs](../../../../../Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs),
[ServerSideApiInstance.cs](../../../../../Api/Pontifex.Api/Transport/Transport/ServerSideApiInstance.cs),
[NegotiationSession.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs),
[RawReliableNegotiation.cs](../../../../../Pontifex/Implementations/Raw/Reliable/Negotiation/RawReliableNegotiation.cs).

## Covered

| Test | Contract behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ServerApi_WithRawApplicationPayloadInsteadOfRequest_RejectsAndDoesNotStartApi` | The Api server consumes the first inbound payload as the `"api"` negotiation request and rejects anything else. | A non-negotiation first payload produces one `Rejected` envelope (`[1, "api", Rejected, text]`), never starts the Api (`ApiStarted` count zero), leaves the server transport running, and makes the client raise no `Connected` and exactly one `Disconnected` with `HandshakeRejected`. | The test replaces the client request with a raw application payload on the fake link. The server-side rejection linger is left pending and cancelled when the pair is disposed. |
| `ClientApi_WithWrongVersionResponse_RaisesDisconnectedOnceAndNoConnected` | A response version different from the requested version is a protocol mismatch. | The client never raises `Connected`, raises `Disconnected` exactly once, and the reason is `ProtocolMismatch`. | The server-side envelope is replaced before delivery, so the server still accepts its own side; only the client validates the version. |
| `ClientApi_WithWrongNameResponse_RaisesDisconnectedOnceAndNoConnected` | A response name different from `"api"` is a protocol mismatch. | The client never raises `Connected`, raises `Disconnected` exactly once with `ProtocolMismatch`. | Name comparison is by UTF-8 content. |
| `ClientApi_FirstOutboundPayload_IsTheApiNegotiationRequest` | The client sends its Api negotiation request immediately on transport connect. | The first and only outbound payload is `[1, "api", Request]` before any application traffic. | The fake peer drops the request, so the Api stays unadmitted. The pending negotiation deadline is disposed with the transport. |
| `ServerApi_WithAppendedNegotiationElements_AdmitsTheSession` | The server validates name/version only and ignores appended elements. | A client whose `AppendNegotiationData` appends `int 42` sends `[1, "api", Request, 42]`; the server admits the session, the client raises `Connected` once, and the Api starts once. | The hook is append-only and its count is one per connect. Proves the reserved custom-tail space cannot break admission. |

## Planned

| Missing test | Owning layer |
| --- | --- |
| A user `AppendNegotiationData` hook exception and the resulting stop path. | `RawReliableApiNegotiation`. |
| A fabricated status value in the third element (unknown discriminator) rejected before the custom tail is parsed. | `RawReliableApiNegotiation` (the shared session's behaviour is already covered by the wrapper suite). |
| `ClientSideApi` delivering admitted application data into the `TransportPipeSystem`. | `RawReliableApiNegotiation` (requires a declared Api pipe; the Api pipe suite covers pipes over in-memory transports). |
| Concurrent Api sessions on one `ServerSideApiFactory` with independent instances. | `RawReliableApiNegotiation` (the synchronous fake cannot overlap factory calls deterministically). |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timers and negotiation deadline accuracy | The Api uses the shared session defaults; deadline accuracy belongs to the shared-negotiation and carrier integration suites. This suite does not wait on real time. |
| Real TCP/Direct carriers, sockets, and wire framing | Covered by the carrier integration suites; this suite tests the Api layer only. |
| Zip, Reconnectable, and logger wrappers | Each wrapper owns its suite; the Api is a sibling protocol layer. |
| Admission payloads, authentication, and encryption | Out of scope for the Api negotiation envelope. |
| Logging and diagnostics text | Requires an observable log sink; the logger is used only as a dependency. |
| Performance and allocation counts | Not part of this contract. |
