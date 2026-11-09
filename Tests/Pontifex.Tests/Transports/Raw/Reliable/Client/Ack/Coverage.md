# RawReliableAckClientTransport Layer Coverage

- **Layer:** RawReliableAckClientTransport
- **Subject:** `Pontifex.Raw.Reliable.Ack.RawReliableAckClientTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw/Reliable/Client/Ack`
- **Inheritance parent:** `Pontifex.Raw.Reliable.RawReliableClientTransport<TClientHandler>`
- **Contract:** [RawReliableAckClientTransport.cs](../../../../../../../Pontifex/Implementations/Raw/Reliable/Ack/RawReliableAckClientTransport.cs),
  [IRawReliableAckClient.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Client/IRawReliableAckClient.cs),
  [IRawReliableAckClientHandler.cs](../../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Client/IRawReliableAckClientHandler.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `AckClientTransport_ExposesReliableAckClientContract` | The Ack client facade narrows the client transport to the Ack contracts. | It is assignable to `IRawReliableAckClient`, `IRawReliableClientTransport<IRawReliableAckClientHandler>`, `IRawReliableTransport`, `IRawTransport`, and `ITransport`. | — |
| `AckClientTransport_HasAckTransportType` | The facade fixes the transport type. | `Type` is `TransportType.RawReliableAck`. | — |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| — | — | No known missing tests for this layer. |

## Deferred by design

| Gap | Reason |
|---|---|
| Inherited init, connect state machine, endpoint ownership, and terminal notifications. | Covered once by `RawReliableClientTransportTests`; not repeated here. |
| Conformance controls. | The conformance subsystem is out of scope and planned for a rewrite. |
| Acknowledger hand-off and Ack-response handling. | Those belong to the Ack endpoint/server layers and the `Endpoints` suite. |

## Notes

- `TestRawReliableAckClientTransport` supplies the inherited abstract members and adds no
  behaviour; it exists only so the facade can be instantiated.
