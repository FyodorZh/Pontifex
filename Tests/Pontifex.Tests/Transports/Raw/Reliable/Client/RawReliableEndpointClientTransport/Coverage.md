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

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited init, start/connect handoff, failed-start non-notification, and endpoint attach/detach ownership. | Covered once by `RawReliableClientTransportTests`; not repeated here. |
| `RawReliable` conformance controls and suite. | Conformance controls and the conformance suite are deferred. |
| Handler factory, admission, and server-session hand-off. | Belongs to the reliable server layer and the Endpoints suite. |

## Notes

- `TestRawReliableEndpointClientTransport` supplies the endpoint factory and exposes `CreateEndpointCalls`,
  `LastCreatedEndpoint`, and `AttachedEndpoint`.
- The endpoint and handler doubles are reused from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/Harness`; both suites share one test
  assembly.
