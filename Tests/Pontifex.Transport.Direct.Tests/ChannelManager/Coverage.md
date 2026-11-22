# ChannelManager Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | `Channel` and `ChannelManager` group |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/ChannelManager/` |
| Inheritance parent | none |
| Dependency | `ChannelManager` is internal; the suite relies on `InternalsVisibleTo`. |
| Contract | [`RawReliable` Direct §6](../../../Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md), [`RawUnreliable` Direct §6](../../../Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SendToClient_InvokesTheClientHandler` | Client-bound delivery | The configured client handler receives the same message instance with its content | The default delivery system is synchronous. |
| `SendToServer_InvokesTheServerHandlerWithTheClientEndpoint` | Server-bound delivery | The server handler receives the configured client endpoint and the message | The endpoint is passed by identity, not reconstructed. |
| `SendToClient_WithoutAHandler_ReleasesTheMessage` | No-handler release | An unhandled client-bound message is released | `IsAlive` is the observable release signal. |
| `SendToServer_WithoutAHandler_ReleasesTheMessage` | No-handler release | An unhandled server-bound message is released | Companion to the client direction. |
| `SendToClient_AfterDispose_ReturnsNotConnectedAndReleasesTheMessage` | Disposed send | A client-bound send after dispose returns `NotConnected` and releases the message | The channel must not deliver after disposal. |
| `SendToServer_AfterDispose_ReturnsNotConnectedAndReleasesTheMessage` | Disposed send | A server-bound send after dispose returns `NotConnected` and releases the message | Companion to the client direction. |
| `Dispose_IsIdempotentAndRaisesTheEventOnce` | Disposal | Repeated dispose raises `Disposed` exactly once and leaves `IsDisposed` true | Pins the one-shot interlocked gate. |
| `Dispose_ContinuesWhenASubscriberThrows` | Disposal fault isolation | A throwing subscriber does not stop later subscribers | The channel has no logger, so failures are swallowed by design. |
| `Messages_AreDeliveredInSendOrder` | Delivery order | Five client-bound sends arrive in call order | The perfect delivery system invokes synchronously; no scheduler is involved. |
| `ClientEndPoint_IsTheConfiguredInstance` | Endpoint exposure | `ClientEp` returns the exact configured endpoint | The channel does not clone or wrap the endpoint. |
| `ReplacingTheClientDeliverySystem_ReleasesTheOldSystem` | Delivery-system ownership | Replacing the client direction clears and disposes the old system | A disposable system is disposed after clearing. |
| `ReplacingTheServerDeliverySystem_ReleasesTheOldSystem` | Delivery-system ownership | Replacing the server direction clears and disposes the old system | Companion to the client direction. |
| `DisposingTheChannel_ReleasesBothDeliverySystems` | Delivery-system ownership | Channel disposal releases both installed systems exactly once | The channel swaps in perfect systems before releasing the old ones. |
| `SettingTheSameClientDeliverySystem_DoesNotReleaseIt` | Delivery-system idempotence | Re-setting the current instance is a no-op | Guards the identity check that prevents self-disposal. |
| `RegisterServer_WithAUniqueEndPoint_ReturnsTrue` | Server registration | A unique server endpoint is accepted | Each test uses a unique name because the registry is process-wide. |
| `RegisterServer_WithADuplicateEndPoint_ReturnsFalse` | Server registration | A second registration of the same endpoint is rejected | The first registration remains in effect. |
| `RegisterServer_WithAnEqualEndPoint_ReturnsFalse` | Registration key | Equality, not object identity, rejects an equal endpoint | `StringEndPoint` equality is the registry key. |
| `Connect_WithoutARegisteredServer_ReturnsNull` | Client pairing | Connecting to an unknown server returns `null` | No channel is created. |
| `Connect_ToARegisteredServer_ReturnsAChannelAndInvokesTheCallback` | Client pairing | A connect returns the created channel and invokes the server callback with it | The callback runs before `Connect` returns. |
| `Connect_WithADuplicateClientEndPoint_ReturnsNullAndKeepsOneChannel` | Pairing key | A duplicate client endpoint is rejected and only one channel is registered | The rejected channel is disposed by the manager. |
| `Disconnect_RemovesAndDisposesTheChannel` | Client removal | Disconnect removes and disposes the channel; a repeated call returns `false` | The operation is keyed by the server and client endpoints. |
| `Disconnect_WithAnUnknownClient_ReturnsFalse` | Client removal | An unknown client is reported as absent | No side effect on the registry. |
| `Detach_RemovesWithoutDisposingTheChannel` | Channel hand-off | Detach removes the channel without disposing it and frees the pairing key | A later connect with the same client endpoint succeeds. |
| `Detach_WithAnUnknownClient_ReturnsFalse` | Channel hand-off | An unknown client is reported as absent | Companion to `Disconnect`. |
| `UnregisterServer_DisposesEveryConnectedChannel` | Server removal | Unregistering disposes all connected channels and removes the server | Later connects return `null`. |
| `UnregisterServer_WithAnUnknownEndPoint_DoesNotThrow` | Server removal | Unregistering an unknown endpoint is a no-op | The manager tolerates teardown races. |
| `ServerEndPointEquality_KeysTheRegistry` | Registry addressing | An equal but distinct server endpoint resolves the same registration | Equality and hash code are the only keys. |
| `Connect_DeliversMessagesInBothDirections` | End-to-end channel | A connected pair delivers both directions through the registered channel | Uses the default perfect systems. |
| `ConcurrentConnects_FromDistinctClients_CreateOneChannelEach` | Concurrent pairing | Thirty-two parallel connects create one channel per distinct client | No timing dependence; the assertion is on the final registry and callback counts. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| `ChannelManager.Connect` callback exception: the exception currently propagates and the channel stays registered. | Root |
| `Channel.SetClientDeliverySystem` racing with a delivery: messages already in flight may be undelivered but must be released. | Root |
| `ChannelManager.UnregisterServer` racing with `Connect`: the carrier layer re-checks, but the registry itself is not pinned. | Root |
| Message content with empty and multi-element lists through the channel. | Root |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Thread affinity and callback scheduling | The channel is synchronous; the transport suites own any asynchronous scheduling. |
| Registry reset between test runs | The manager is a process-wide singleton with no reset API. Tests isolate themselves with unique endpoint names. |
| Cross-process or socket behavior | The channel is in-process only. |
