# RawTransport Layer Coverage

- **Layer:** RawTransport
- **Subject:** `Pontifex.Raw.RawTransport`
- **Directory:** `Tests/Pontifex.Tests/Transports/Raw`
- **Inheritance parent:** `Pontifex.AnyTransport`
- **Contract:** [RawTransport.cs](../../../../Pontifex/Implementations/Raw/RawTransport.cs),
  [IRawTransport.cs](../../../../Pontifex/Abstractions/Transports/Raw/IRawTransport.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_ForwardsBaseState` | The typed constructor forwards its name and memory rental to `AnyTransport`. | `Name` and `Memory` expose the supplied values and `Log` is non-null. | Forwarding is asserted only through the base observables. |
| `MessageMaxByteSize_ReflectsDerivedImplementation` | `MessageMaxByteSize` is supplied by the derived transport. | The configured value is returned for 1, 1024, and 65536 bytes. | Parametrised over the lower bound, a typical size, and a large size. |
| `RawTransport_IsAssignableToIRawTransport` | `RawTransport` is an `IRawTransport` and an `ITransport`. | Both interface contracts are exposed. | — |
| `IRawTransport_ExposesMessageMaxByteSize` | The interface exposes the message limit. | The value is reachable through `IRawTransport`. | — |

## Planned

| Missing layer or test | Owner | Reason |
|---|---|---|
| `RawUnreliableTransport` layer | This layer | Abstract descendant of `RawTransport` with no layer yet; the nearest existing ancestor is this layer. |
| `RawUnreliableClientTransport` layer | This layer | Abstract descendant of `RawUnreliableTransport`; parent layer does not exist yet. |
| `RawUnreliableServerTransport<TFactory>` layer | This layer | Abstract descendant of `RawUnreliableTransport`; parent layer does not exist yet. |
| `RawUnreliableAckServerTransport` layer | This layer | Abstract descendant of `RawUnreliableServerTransport<TFactory>`; parent layer does not exist yet. |
| `RawUnreliableNoAckServerTransport` layer | This layer | Abstract descendant of `RawUnreliableServerTransport<TFactory>`; parent layer does not exist yet. |

## Deferred by design

| Gap | Reason |
|---|---|
| Conformance controls. | The conformance subsystem is out of scope and planned for a rewrite. |
| Concrete carrier transports (Direct, TCP, UDP). | They live in the `Transports` projects, not in the abstract hierarchy this suite covers. |
| Real I/O, time, and thread-affinity behaviour. | The default suite is deterministic and exercises no carrier. |

## Notes

- This layer covers only the delta introduced by `RawTransport`; the inherited `AnyTransport`
  lifecycle is covered by the root layer and is not repeated.
- `TestRawTransport : RawTransport` supplies the abstract `MessageMaxByteSize`, `Type`, and
  lifecycle hooks. It passes a trivial conformance control only because the production
  constructor requires one; the control is never armed, inspected, or asserted.
