# RawReliableEndpointClientTransport Layer Coverage

- **Layer:** RawReliableEndpointClientTransport
- **Subject:** `Pontifex.Raw.Reliable.RawReliableEndpointClientTransport<TClientHandler>`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/RawReliableEndpointClientTransport`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableClientTransport<TClientHandler>`
- **Contract:** [RawReliableEndpointClientTransport.cs](../../../../../../../Pontifex/Implementations/Raw/Reliable/RawReliableEndpointClientTransport.cs),
  [IRawReliableClient.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClient.cs),
  [IRawReliableClientHandler.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Client/IRawReliableClientHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `EndpointClientTransport_ExposesReliableClientContract` | The reliable client facade narrows the client transport to the reliable contracts. | It is assignable to `IRawReliableClient`, `IRawReliableClientTransport<IRawReliableClientHandler>`, `IRawReliableTransport`, `IRawTransport`, and `ITransport`. | — |
| `EndpointClientTransport_HasRawReliableTransportType` | The facade fixes the transport type. | `Type` is `TransportType.RawReliable`. | — |
| `Start_CreatesAndStartsEndpoint` | The facade creates and attaches one endpoint and starts it. | One endpoint is created, it is the attached endpoint, and connectivity comes from that endpoint only; completing the endpoint connect and handshake raises the handler's `OnConnected` and makes the transport connected. | Reuses the Endpoints-suite handshake endpoint and handler doubles; the handshake response is supplied explicitly. |
| `Stop_DestroysAttachedEndpoint` | Teardown stops the attached endpoint. | The endpoint receives the stop with the transport reason, observes `OnDisconnected` and `OnStopped` once, and the transport is detached and no longer started. | The endpoint double needs an explicit disconnect completion, as in the Endpoints suite. |
| `Start_WhenCreateEndpointThrows_FailsWithoutHandlerNotification` | A throwing endpoint factory is isolated by `BeginConnect`. | `Start` returns false, the transport is invalid and not started, no endpoint is attached, and neither the handler nor the user callback is notified. | The factory is called exactly once; the exception is logged but not surfaced. |
| `Start_WhenEndpointStartReturnsFalse_StopsAttachedEndpointAndFailsWithoutHandlerNotification` | An endpoint that rejects its start fails the transport start. | The endpoint is attached before its start is requested, the inherited teardown stops it exactly once, the transport detaches, and neither the handler nor the user callback is notified. | Uses the local `FakeRawReliableClientEndpoint`; its `StartProbe` proves attachment at the moment `Start` runs. The failure is signalled only by the `false` return, so no terminal event races the start. |
| `Start_WhenEndpointStartThrows_StopsAttachedEndpointAndFailsWithoutHandlerNotification` | An endpoint whose start throws is isolated by `BeginConnect`. | Same teardown and non-notification as the false case, with the exception logged rather than surfaced. | Same fake and attachment probe. The inherited teardown still calls `Stop` once, so a throwing start cannot leak the endpoint. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited init and start/connect hand-off. | Covered once by `RawReliableClientTransportTests`; not repeated here. The endpoint-factory and endpoint-start failure paths are this layer's delta and are covered above. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |
| Handler factory, admission, and server-session hand-off. | Belongs to the reliable server layer and the Endpoints suite. |

## Notes

- `TestRawReliableEndpointClientTransport` supplies the endpoint factory and exposes `CreateEndpointCalls`,
  `LastCreatedEndpoint`, and `AttachedEndpoint`. It also supplies the failed-start knobs
  `CreateEndpointException`, `EndpointStartReturnsFalse`, and `EndpointStartException`, plus the
  `EndpointWasAttachedDuringStart` probe and `LastFakeEndpoint`.
- `FakeRawReliableClientEndpoint` is a minimal hand-written `IRawReliableClientEndpointImp` used only by
  the failed-start tests. It records `Start`/`Stop`, can reject or throw from `Start`, and raises its
  one-shot terminal `Stopped` notification from `Stop`.
- The endpoint and handler doubles are reused from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/Harness`; both suites share one test
  assembly.
