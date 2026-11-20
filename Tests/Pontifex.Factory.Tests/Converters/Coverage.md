# Converters Layer Coverage

- **Layer:** Converters (root layer)
- **Subject:** Built-in converter group: `RawUnreliableNoAckToRawReliableConverter`,
  `RawUnreliableNoAckToRawReliableClient`, `RawUnreliableNoAckToRawReliableServer`,
  and the internal `ConverterHandshake` helper.
- **Directory:** `Tests/Pontifex.Factory.Tests/Converters`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:**
  [RawUnreliableNoAckToRawReliableConverter.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableConverter.cs),
  [RawUnreliableNoAckToRawReliableClient.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableClient.cs),
  [RawUnreliableNoAckToRawReliableServer.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableServer.cs),
  [ConverterHandshake.cs](../../../Pontifex/Converters/RawUnreliableNoAck/RawReliable/ConverterHandshake.cs),
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
| `Client_WithNameMismatchReject_StopsWithProtocolMismatchAndNoConnected` | Reject mapping and terminal lifecycle | A reject carrying a different server name produces `ProtocolMismatch`, no `OnConnected`, no `OnDisconnected`, and exactly one `OnStopped`. | The test must send the reject through the delivery manager; a raw transport payload would not be decoded by the converter. |
| `Server_WithVersionMismatch_RejectsWithoutCreatingASession` | Server validates version before the application factory | The server returns a `Rejected` envelope with the server version and a version-mismatch text, the factory is not called, and no application handler is connected. | The malformed request is handcrafted; `ConverterHandshake.BuildRequest` always emits the current version. |
| `Server_WithNameMismatch_RejectsWithoutCreatingASession` | Server validates name before the application factory | The server returns a `Rejected` envelope with the server name and a name-mismatch text, the factory is not called, and no application handler is connected. | Complements the version case; both use the same scripted client peer. |
| `Handshake_NegotiatesTheMinimumDeliveryManagerMaximum` | Capacity negotiation at the converter layer | Both endpoints report the minimum of the two `DeliveryManager` maxima, and sending `min + 1` returns `MessageTooBig` on both sides. | Expected maxima are computed with a real `DeliveryManager`, because the converter's local limit is defined in terms of that layer rather than the inner transport limit. |
| `AdmittedSession_DeliversTheOrderedLifecycleExactlyOnce` | Reliable terminal lifecycle | An admitted session receives `OnConnected` then `OnDisconnected` then `OnStopped` exactly once on both sides; repeated transport stops add no callbacks. | Mirrors the reliable endpoint lifecycle; the rejected/never-connected case is covered by the client reject test. |

## Planned

None.

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
