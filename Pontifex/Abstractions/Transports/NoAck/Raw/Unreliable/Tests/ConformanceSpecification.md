# NoAckRawUnreliable Carrier-Independent Core Conformance Profile

| Field | Value |
| --- | --- |
| **Component** | `Pontifex.NoAck.Raw.Unreliable` |
| **Profile** | Carrier-Independent Core Conformance |
| **Status** | Draft |
| **Companion specification** | [`../Specification.md`](../Specification.md) |

## 1. Purpose

This profile defines the deterministic, runtime-testable subset of the
NoAckRawUnreliable transport contract. It is intended for a conformance suite
that must run against implementations using their ordinary, uncontrolled
physical carrier.

The profile does not control, inject, capture, replace, or schedule packets.
It does not inspect logging or memory-rental behavior. Consequently, it cannot
certify the complete NoAckRawUnreliable specification. It certifies only local
API behavior whose outcome does not depend on a carrier delivering a packet.

A passing result is named **Carrier-Independent Core Conformant**. It is not a claim of full
NoAckRawUnreliable conformance.

## 2. Relationship to the transport specification

[`Specification.md`](../Specification.md) remains the normative contract for a
NoAckRawUnreliable transport. This profile neither weakens nor replaces it.

The full contract includes delivery behavior, inbound validation, callback
behavior, routing, logging, and buffer ownership. Those areas require
deterministic data-plane, logging, or memory instrumentation to certify
strictly. They are outside this profile because such instrumentation is not
currently available or permitted.

Normative requirements in this profile use the same meaning of **MUST**,
**MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**, and **MAY** as the
companion specification.

## 3. Terms

| Term | Meaning |
| --- | --- |
| **adapter** | Implementation-specific test infrastructure that constructs configured client and server instances for the common suite. |
| **instrumented instance** | A transport created by an adapter with the local conformance control enabled. |
| **ordinary instance** | A transport created for normal application use, without conformance instrumentation. |
| **core certification** | A passing result for every requirement in this profile. |
| **smoke observation** | A non-certifying result obtained from real carrier traffic. It may reveal a defect but cannot prove the absence of one. |

## 4. Required adapter and control

### 4.1 Adapter

Every implementation seeking Carrier-Independent Core Conformance **MUST** provide an
implementation-specific adapter for the common test suite. The adapter is test
infrastructure; it is not a production transport API.

The adapter **MUST**:

- construct valid, isolated client and server instances using ordinary physical
  carrier configuration;
- provide ordinary payload creation sufficient for empty, small, exactly-limit,
  and one-byte-over-limit valid messages;
- create instrumented instances when a local-conformance test requests them;
- avoid routing the test pair through a replacement carrier, packet relay, or
  in-memory delivery path; and
- allow the common suite to obtain all transport-local controls exclusively
  through `ITransport.GetControls`.

The adapter is not required to supply a recording `ILogger`, recording
`IMemoryRental`, packet injector, carrier proxy, or message capture facility.

### 4.2 Local operation control

An instrumented instance **MUST** expose exactly one
`INoAckRawUnreliableConformanceControl` through
`ITransport.GetControls`. The control is defined by
[`INoAckRawUnreliableConformanceControl.cs`](INoAckRawUnreliableConformanceControl.cs).

An ordinary instance **MAY** omit the control. If it exposes the control, the
control **MUST** remain inert unless the instance was explicitly constructed or
configured for conformance testing.

The control and every returned checkpoint gate **MUST** be safe for concurrent
use. The control **MUST NOT**:

- inject, suppress, capture, duplicate, reorder, or otherwise control packets;
- directly invoke application callbacks;
- fabricate `SendResult` values;
- inspect or alter application-owned message buffers; or
- alter the physical carrier selected by the implementation.

Ordinary instances **MUST NOT** incur a conformance-control hot-path branch,
allocation, lock acquisition, or scheduling operation. An implementation may
choose construction-time instrumentation or pre-start activation for an
instrumented instance.

## 5. Certifiable requirements

### 5.1 Initial state and successful start

A newly constructed valid adapter-created transport **MUST** report
`IsValid == true` and `IsStarted == false`.

Given valid adapter configuration and no armed failure, concurrent `Start`
calls **MUST** be safe. Exactly one call **MUST** return `true`; every racing or
later call **MUST** return `false`. After the successful call returns, the
transport **MUST** report `IsStarted == true` and `IsValid == true`.

### 5.2 Failed start and terminal invalidation

When `FailNextStart()` is armed, the next `Start` call **MUST** return `false`.
The transport **MUST** become invalid, **MUST NOT** report started, and **MUST
NOT** invoke the `onStopped` callback supplied to that call.

After a failed start, later `Start` calls **MUST** return `false`. The instance
**MUST NOT** become running again.

### 5.3 Stop semantics

Calling `Stop` before a successful start **MUST** return `true`, **MUST NOT**
invoke `onStopped`, and **MUST NOT** prevent a later valid `Start`.

For a successfully started valid transport, the first `Stop` call **MUST**
return `true`, transition `IsStarted` to `false`, and subsequently invoke the
callback supplied to the successful `Start` exactly once. Later `Stop` calls on
that valid stopped instance **MUST** return `true` and **MUST NOT** cause an
additional callback.

A stopped instance **MUST NOT** be restarted. Every later `Start` call **MUST**
return `false`.

This profile does not require the callback reason to be the same object passed
to `Stop`; the companion specification permits a transport-generated wrapper.

### 5.4 Forced unrecoverable failure

Calling `InjectUnrecoverableFailure()` on a running instrumented transport
**MUST** cause the ordinary unrecoverable-failure lifecycle:

- `IsStarted` becomes `false`;
- `IsValid` becomes `false`;
- the successful `Start` callback runs exactly once; and
- later `Stop` calls return `false`.

The callback is controlled by `BeforeStoppedCallback`. Repeated `Stop` calls
while that gate is paused **MUST NOT** cause duplicate callback dispatch.

### 5.5 Status properties

`IsValid`, `IsStarted`, and `MessageMaxByteSize` **MUST** be safe for concurrent
reads while other threads start, stop, or force failure. Reads **MUST NOT**
throw or deadlock.

`MessageMaxByteSize` **MUST** remain immutable for the lifetime of the
instance.

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

When `BeforeTrySendStateDecision` is held, `Stop` completes its state
transition, and the send is then released, that send **MUST** return
`NotConnected`.

When `BeforeStopStateTransition` is held, a valid in-limit send reaches and
completes its state decision, and stop is then released, that send **MUST NOT**
return `NotConnected` solely because of that later stop. Any other applicable
synchronous result remains permitted.

The checkpoints **MUST** be located before the competing operation obtains an
exclusive state lock. Tests may use a watchdog to diagnose deadlock, but they
**MUST NOT** assert a scheduler-specific duration.

## 6. Required test procedure and result classification

The common suite runs the local certification cases against adapter-created
instrumented instances. Each test uses the public transport API, the adapter's
ordinary payload corpus, and only the local operation control defined by this
profile.

The suite **MUST NOT** treat a missing required local operation control as a
pass or skip. Results are classified as follows:

| Result | Meaning |
| --- | --- |
| **Carrier-Independent Core Conformant** | Every requirement in section 5 passed using the required control. |
| **Baseline Only** | Public API checks ran, but the required local operation control was unavailable. This is not core certification. |
| **Failed** | A required test ran and observed behavior that violates this profile. |
| **Smoke Observation** | A real-carrier test produced an informational result. It has no certification effect. |

## 7. Non-certifying physical-carrier smoke observations

An adapter may run a client and server directly over their ordinary physical
carrier. Such observations may detect defects in practical operation, including
delivery, corruption, duplicate delivery, reordering, source filtering, and
callback behavior.

They **MUST NOT** be used to certify those properties. A packet may be lost,
duplicated, delayed, reordered, or delivered by a prior test independently of a
test's intended schedule. Timeouts may serve only as a watchdog for a hung test;
they **MUST NOT** establish that a transport delivered or failed to deliver a
message.

## 8. Full-specification requirements outside this profile

The following companion-specification areas are intentionally not locally
certified:

| Area | Reason |
| --- | --- |
| Delivery count, ordering, complete-message boundaries, and logical content | The suite cannot deterministically cause or schedule carrier delivery. |
| Callback ordering, serialization, non-reentrancy, no-handler dropping, and handler-exception recovery | These require deterministic inbound message delivery. |
| Client source filtering, server source acceptance, and issued-endpoint reply routing | These require controlled source identities and inbound traffic. |
| Malformed and oversized inbound discard | These require raw inbound-frame injection. |
| Required malformed-frame logging | Logging observation is intentionally outside the current profile. |
| Send and receive ownership, release counts, and access after release | Memory instrumentation is intentionally outside the current profile. |
| Discard of accepted but undelivered work during stop or failure | It requires data-plane and ownership observation. |

## 9. Why this document exists and how to use it

NoAckRawUnreliable intentionally permits loss, duplication, and reordering. A
test suite that communicates directly through an uncontrolled physical carrier
cannot force a particular packet to arrive, nor can it prevent unrelated carrier
activity from affecting a test. Treating such tests as full conformance would
produce timing-dependent and misleading certification results.

This document exists to make that boundary explicit. It provides a stable,
strictly testable local contract today without pretending that uncontrolled
carrier observations prove data-plane correctness.

Use this profile as follows:

1. Implement the companion `Specification.md` in full.
2. Provide an adapter and the required local operation control for any
   implementation that seeks Carrier-Independent Core Conformance.
3. Run the common local conformance suite and report its classification exactly
   as defined in section 6.
4. Run physical-carrier smoke tests separately and report their observations
   without promoting them to certification.
5. Add future profiles for deterministic data-plane, logging, and memory
   conformance when their required instrumentation becomes available.
