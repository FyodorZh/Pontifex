# RawReliableNoAckClientTransport Layer Coverage

- **Layer:** RawReliableNoAckClientTransport
- **Subject:** `Pontifex.Raw.Reliable.NoAck.RawReliableNoAckClientTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/NoAck`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableClientTransport<TClientHandler>`
- **Contract:** [RawReliableNoAckClientTransport.cs](../../../../../../../Pontifex/Implementations/Raw/Reliable/NoAck/RawReliableNoAckClientTransport.cs),
  [IRawReliableNoAckClient.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Client/IRawReliableNoAckClient.cs),
  [IRawReliableNoAckClientHandler.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/NoAck/Client/IRawReliableNoAckClientHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `NoAckClientTransport_ExposesReliableNoAckClientContract` | The NoAck client facade narrows the client transport to the NoAck contracts. | It is assignable to `IRawReliableNoAckClient`, `IRawReliableClientTransport<IRawReliableNoAckClientHandler>`, `IRawReliableTransport`, `IRawTransport`, and `ITransport`. | — |
| `NoAckClientTransport_HasNoAckTransportType` | The facade fixes the transport type. | `Type` is `TransportType.RawReliableNoAck`. | — |
| `Start_CreatesAndStartsEndpoint` | The facade creates and attaches one endpoint and starts it. | One endpoint is created, it is the attached endpoint, and connectivity comes from that endpoint only; completing the endpoint connect raises the NoAck handler's `OnConnected` and makes the transport connected. | Reuses the Endpoints-suite endpoint and handler doubles. |
| `Stop_DestroysAttachedEndpoint` | Teardown stops the attached endpoint. | The endpoint receives the stop with the transport reason, observes `OnDisconnected` and `OnStopped` once, and the transport is detached and no longer started. | The endpoint double needs an explicit disconnect completion, as in the Endpoints suite. |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited init, start/connect handoff, failed-start non-notification, and endpoint attach/detach ownership. | Covered once by `RawReliableClientTransportTests`; not repeated here. |
| Direct NoAck carrier. | Direct NoAck is deferred; only the TCP `tcp-noack` carrier is implemented. |
| NoAck conformance controls and suite. | NoAck conformance controls and the conformance suite are deferred. |
| Handler factory, admission, and server-session hand-off. | Belongs to the NoAck server layer and the Endpoints suite. |

## Notes

- `TestRawReliableNoAckClientTransport` supplies the endpoint factory and exposes `CreateEndpointCalls`,
  `LastCreatedEndpoint`, and `AttachedEndpoint`.
- The endpoint and handler doubles are reused from
  `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/NoAck/Harness`; both suites share one test
  assembly.
