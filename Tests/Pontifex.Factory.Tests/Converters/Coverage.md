# Converters Layer Coverage

- **Layer:** Converters (root layer)
- **Subject:** Built-in converter group: `RawUnreliableToRawReliableConverter`,
  `RawUnreliableToRawReliableClient`, `RawUnreliableToRawReliableServer`,
  and the internal `ConverterHandshake` helper.
- **Directory:** `Tests/Pontifex.Factory.Tests/Converters`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:**
  [RawUnreliableToRawReliableConverter.cs](../../../Pontifex/Converters/RawUnreliable/RawUnreliableToRawReliableConverter.cs),
  [RawUnreliableToRawReliableClient.cs](../../../Pontifex/Converters/RawUnreliable/RawUnreliableToRawReliableClient.cs),
  [RawUnreliableToRawReliableServer.cs](../../../Pontifex/Converters/RawUnreliable/RawUnreliableToRawReliableServer.cs),
  [ConverterHandshake.cs](../../../Pontifex/Converters/RawUnreliable/ConverterHandshake.cs),
  [IRawReliableClient.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClient.cs),
  [IRawReliableServer.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/Server/IRawReliableServer.cs),
  [IRawReliableEndpoint.cs](../../../Pontifex/Abstractions/Transports/Raw/Reliable/IRawReliableEndpoint.cs)

The converter uses its own flat handshake dataset over the `DeliveryManager`, not the
transport-tier three-element frame codec. The local maximum is
`DeliveryManager.DeliveryMaxByteSize`; the server returns
`min(request.max, its own DeliveryManager.DeliveryMaxByteSize)` and both endpoints apply
the agreed value. The handshake request is the first delivery-managed message; the
application handler observes no handshake payload and is not connected before the response
validates.

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Handshake_Accepted_DeliversApplicationMessagesInBothDirections` | Converter client/server handshake and `IRawReliableEndpoint.Send` | An accepted handshake admits the session; application messages are delivered client-to-server and server-to-client; the factory is called once. | The suite replaces the inner raw-unreliable transport with a synchronous in-process pair; event waits are bounded by an explicit timeout. |
| `HandshakeMessage_IsNotDeliveredToApplicationHandlers` | The handshake is converter-internal | The first payload the server application handler observes is application data, and the client application handler receives no handshake echo. | The harness records cloned payloads so inspection outlives the transport's release point. |
| `Client_DoesNotRaiseOnConnectedBeforeTheHandshakeResponse` | Logical connection is established only after a validated accepted response | While the server withholds the response, the client handler has zero `OnConnected` and zero `OnStopped`; after the accepted response it connects exactly once. | The scripted peer decodes and encodes delivery-manager payloads directly, so the request can be observed without a second converter. |
| `Client_HandshakeRequest_DecodesVersionNameAndDeliveryMaximum` | Request dataset `[version, name, DeliveryMax]` | The first peer payload parses as a request carrying the converter version, name `converter`, and the client delivery-manager maximum. | The payload is captured through the peer's `UserReceived` hook and released by the subscriber; the expected maximum is recomputed with a real `DeliveryManager`. |
| `Client_WithRejectStatus_MapsToTheExpectedStopReason` | `MapReject` status mapping | `VersionMismatch`/`NameMismatch` map to `ProtocolMismatch`; `Rejected`/`CapacityExceeded` map to `HandshakeRejected`; `OnConnected` is never raised. | Parameterized over the four reject statuses; the reject text is non-empty in every case. |
| `Client_WithNameMismatchReject_StopsWithProtocolMismatchAndNoConnected` | Reject mapping and terminal lifecycle | A reject carrying a different server name produces `ProtocolMismatch`, no `OnConnected`, no `OnDisconnected`, and exactly one `OnStopped`. | The test must send the reject through the delivery manager; a raw transport payload would not be decoded by the converter. |
| `Server_WithVersionMismatch_RejectsWithoutCreatingASession` | Server validates version before the application factory | The server returns a `Rejected` envelope with the server version and a version-mismatch text, the factory is not called, and no application handler is connected. | The malformed request is handcrafted; `ConverterHandshake.BuildRequest` always emits the current version. |
| `Server_WithNameMismatch_RejectsWithoutCreatingASession` | Server validates name before the application factory | The server returns a `Rejected` envelope with the server name and a name-mismatch text, the factory is not called, and no application handler is connected. | Complements the version case; both use the same scripted client peer. |
| `Handshake_NegotiatesTheMinimumDeliveryManagerMaximum` | Capacity negotiation at the converter layer | Both endpoints report the minimum of the two `DeliveryManager` maxima, and sending `min + 1` returns `MessageTooBig` on both sides. | Expected maxima are computed with a real `DeliveryManager`, because the converter's local limit is defined in terms of that layer rather than the inner transport limit. The client maximum is the smaller side, so the client assertion is non-vacuous. |
| `Handshake_WhenServerMaximumIsSmaller_ClientLowersToIt` | Capacity negotiation at the converter layer | The client lowers its maximum when the server's limit is smaller. | Mirrors the negotiation direction: the client starts above the agreed value and must adopt the server clamp. |
| `AdmittedSession_DeliversTheOrderedLifecycleExactlyOnce` | Reliable terminal lifecycle | An admitted session receives `OnConnected` then `OnDisconnected` then `OnStopped` exactly once on both sides; repeated transport stops add no callbacks. | Mirrors the reliable endpoint lifecycle; the rejected/never-connected case is covered by the client reject test. |

## Planned

| Missing test or gap | Owner | Reason |
|---|---|---|
| Client malformed/invalid response branches. | This layer | Unparsable envelopes, wrong version/name, and `agreedMax < 1` are not scripted. |
| Server reject branches for malformed request, `max < 1`, null factory, and throwing factory. | This layer | Only version/name rejects are tested; the shared `Reject` helper's other reasons are unexercised. |
| Fresh server handler per admitted session and a null factory result not being cached. | This layer | The current factory test uses one session; the sequential freshness MUST is testable with the existing synchronous fakes. |
| `Start` returning `false` produces no handler callbacks. | This layer | Every test asserts a successful start. |

## Deferred by design

- **Handshake timeout.** The client uses a 10 s deadline checked by its delivery-manager
  loop. A default-run test would need to wait for real time, so the timeout path is not
  exercised here. It is reported with `TimeOut` by the same loop that handles rejects.
- **Real loss, duplication, and reordering.** The converter must run its handshake through
  the `DeliveryManager`; proving that the delivery layer itself retries belongs to the
  delivery-manager or carrier suites, not to this converter suite.
- **Factory concurrency under multiple simultaneous sessions.** The server ticks every
  session on one delivery-manager thread, so factory calls are serialized by construction.
  A multi-session concurrency test would not add a distinct converter contract assertion.
- **Buffer-release counting for every handshake branch.** The tests assert observable
  behaviour rather than reference counts; the converter relies on the documented
  `DeliveryManager.ScheduleDelivery` ownership transfer and releases received handshake
  payloads once after parsing.
