# NoAckRawUnreliable Carrier-Independent Core Conformance Profile

| Field | Value |
| --- | --- |
| **Component** | `Pontifex.NoAck.Raw.Unreliable` |
| **Profile** | Carrier-Independent Core Conformance |
| **Status** | Draft |
| **Companion specification** | [`../Specification.md`](../Specification.md) |
| **Base profile** | [`../../../ConformanceSpecification.md`](../../../ConformanceSpecification.md) |

This profile extends the base [Transport Carrier-Independent Core Conformance
Profile](../../../ConformanceSpecification.md) with NoAckRawUnreliable-specific
certifiable requirements. The base profile's sections 1–4, 5.1–5.5, and 6–9
apply without modification.

## 5. Certifiable requirements

Sections 5.1 (initial state and successful start), 5.2 (failed start and
terminal invalidation), 5.3 (stop semantics), 5.4 (forced unrecoverable
failure), and 5.5 (status properties) of the base profile apply.

### 5.6 Local message-size validation

The adapter-provided payload corpus **MUST** include a valid message whose
serialized `UnionDataList` size equals `MessageMaxByteSize` and a valid message
whose serialized size is one byte greater.

While running, `TrySend` of the exact-limit message **MUST NOT** return
`MessageTooBig`. `TrySend` of the one-byte-over-limit message **MUST** return
`MessageTooBig` and **MUST NOT** stop or invalidate the transport.

The profile does not require the exact-limit send to return `Ok`: an independent
locally applicable result, such as `BufferOverflow` or `Error`, may still occur.

### 5.7 Local synchronous rejection

For both client and server transports, `TrySend` before successful start, while
stopping, and after stopping **MUST** return `NotConnected`.

While running, passing a null message **MUST** return `InvalidMessage` and
**MUST NOT** stop or invalidate the transport.

While running, a server `TrySend` given an endpoint that was not issued by that
running server and is not equal to an issued endpoint **MUST** return
`InvalidAddress` and **MUST NOT** stop or invalidate the transport.

Every naturally reachable synchronous non-`Ok` result **MUST** be non-fatal. A
test may exercise `BufferOverflow` or `Error` only when the implementation can
produce that result without fabrication by the adapter or control.

### 5.8 Send-versus-stop ordering

The local operation control is used to construct both permitted orderings.

When `BeforeSendCommitGate` is held, `Stop` completes its state
transition, and the send is then released, that send **MUST** return
`NotConnected`.

When `BeforeStopStateTransitionGate` is held, a valid in-limit send reaches and
completes its state decision, and stop is then released, that send **MUST NOT**
return `NotConnected` solely because of that later stop. Any other applicable
synchronous result remains permitted.

The checkpoints **MUST** be located before the competing operation obtains an
exclusive state lock. Tests may use a watchdog to diagnose deadlock, but they
**MUST NOT** assert a scheduler-specific duration.
