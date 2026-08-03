# RawReliableAck Transport Specification

## 1. Scope

RawReliableAck is a bidirectional, connection-oriented transport contract for
opaque `UnionDataList` messages. It defines:

- client admission through an ACK handshake;
- per-connection endpoint and callback lifecycles;
- ordered, duplicate-free delivery within a live logical connection;
- ownership rules for transport buffers; and
- responsibilities of application logic and transport implementations.

This specification is implementation-agnostic. It does **not** define wire
format, version negotiation, authentication, authorization, confidentiality,
integrity protection, timeout policy, keep-alive policy, or automatic
reconnection. Those concerns belong to the selected transport implementation
or to a protocol layered above RawReliableAck.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Terms and roles

| Term | Meaning |
| --- | --- |
| **client** | The side that initiates a connection and supplies ACK data. |
| **server** | The side that admits or rejects a client and creates a session handler. |
| **session** | One accepted server-side connection, represented by exactly one server handler. |
| **endpoint** | The transport-provided object used to send regular messages, disconnect, and inspect connection metadata. |
| **regular message** | An application `UnionDataList` sent through `Send`. It excludes handshake and transport-control traffic. |
| **logical connection** | The application-visible connection lifetime from `OnConnected` through `OnDisconnected`. It can end before an underlying physical transport is fully closed. |
| **accepted send** | A `Send` invocation that returns `SendResult.Ok`. It has entered the transport's outbound delivery responsibility. |
| **delivery prefix** | The zero or more earliest accepted sends that reach the peer before the connection terminates. |

`RemoteEndPoint` is addressing metadata only. It MUST NOT be treated as an
authenticated identity, authorization claim, or proof of peer ownership.

## 4. Responsibilities at a glance

| Topic | Guaranteed by RawReliableAck | Application responsibility | Implementation-defined |
| --- | --- | --- | --- |
| Admission | ACK data is presented to the server; a non-null handler accepts a session. | Authenticate and authorize inside `TryAck`. | How rejection is observed by the client. |
| Delivery | Accepted sends are delivered at most once, in per-direction order, until termination. | Add application receipts when confirmation is required. | Wire framing and transmission mechanism. |
| Backpressure | `BufferOverflow` is non-fatal. | Retry later using a newly created buffer and an application backpressure policy. | Queue capacity and drain rate. |
| Callbacks | Per-connection callbacks are serialized and non-reentrant. | Return promptly, release owned buffers, and protect state shared between sessions. | Callback thread or scheduler. |
| Failure | Synchronous send failures are non-fatal; asynchronous failures end the logical connection. | Handle teardown and create a new connection when needed. | Timeout, keep-alive, and failure-detection policy. |
| Security | No security property is implied. | Choose authenticated and protected transports or implement protection above this layer. | Any implementation-specific protection. |

## 5. Admission handshake

The handshake admits a client; it is not a per-message acknowledgement
protocol. Regular messages MUST NOT be exposed to application handlers before
their side has completed `OnConnected`.

```mermaid
sequenceDiagram
    participant C as Client handler
    participant CT as Client transport
    participant ST as Server transport
    participant A as Server acknowledger
    participant S as Server session handler

    C->>CT: FillAckData(transport-owned buffer)
    CT->>ST: ACK data
    ST->>A: TryAck(owned ACK data)
    alt rejected
        A-->>ST: null
        Note over ST,CT: Client-visible rejection result is implementation-defined
    else accepted
        A-->>ST: new session handler
        ST->>S: FillAckResponse(transport-owned buffer)
        ST-->>CT: ACK response
        ST->>S: OnConnected(server endpoint)
        CT->>C: OnConnected(client endpoint, owned ACK response)
    end
```

### 5.1 Client responsibilities

1. The client handler MUST populate the buffer supplied to `FillAckData`.
2. That buffer remains transport-owned scratch storage. The handler MUST NOT
   retain or release it.
3. The client endpoint is not available during `FillAckData`.
4. A client enters its logical connection only when its
   `OnConnected(IRawReliableAckClientSideEndpoint, UnionDataList)` callback is
   invoked.
5. If the populated ACK data exceeds `MessageMaxByteSize`, establishment MUST
   fail. The client MUST receive `OnStopped` without `OnConnected`; the
   failure reason is implementation-defined.

### 5.2 Server responsibilities

1. The server MUST invoke `TryAck` for each connection attempt.
2. Calls to `TryAck` on a server MUST NOT overlap concurrently.
3. `TryAck` MUST return a fresh server handler for each accepted session. A
   handler instance MUST NOT serve multiple sessions.
4. `TryAck` receives ownership of its ACK-data buffer and MUST release it
   exactly once after validation, including on an exceptional path.
5. Returning `null` rejects the connection attempt. The way that rejection
   reaches the client, including whether it is reported as `AckRejected`, is
   implementation-defined.
6. For an accepted session, the server MUST call `FillAckResponse` before its
   `OnConnected`. The ACK response MUST be accepted for outbound delivery
   before server `OnConnected` is invoked.
7. `FillAckResponse` receives transport-owned scratch storage. The handler
   MUST populate it but MUST NOT retain or release it.
8. If a callback fails before the server handler's `OnConnected`, the session
   never became logical and that handler receives no lifecycle callback.
9. If the populated ACK response exceeds `MessageMaxByteSize`, establishment
   MUST fail. The client MUST receive `OnStopped` without `OnConnected`, and
   the pre-connected server handler receives no lifecycle callback. The client
   failure reason is implementation-defined.

Server acceptance is local. Server `OnConnected` MAY run before the client
receives the ACK response or runs its own `OnConnected`.

### 5.3 Handshake callback discipline

`FillAckData`, `TryAck`, and `FillAckResponse` are handshake callbacks. No
endpoint operation is available in them. They MUST return promptly; expensive
authentication, I/O, or construction work SHOULD be delegated so admission
processing is not stalled.

Handshake and other transport-control traffic MUST NOT be delivered through
`OnReceived`.

## 6. Connection lifecycle

```mermaid
stateDiagram-v2
    [*] --> Constructed
    Constructed --> Initialized: Init succeeds
    Initialized --> Connecting: Start succeeds
    Connecting --> Connected: handshake accepted / OnConnected
    Connecting --> Stopped: establishment fails / OnStopped
    Connected --> Disconnecting: local, remote, or async failure
    Disconnecting --> Disconnected: OnDisconnected
    Disconnected --> Stopped: client OnStopped
    Stopped --> [*]
```

The client callback sequences are:

```text
successful connection: OnConnected -> OnDisconnected -> OnStopped
failed establishment:  OnStopped
```

For each successfully connected server session, `OnDisconnected` MUST occur
exactly once. Server handlers do not have an `OnStopped` callback.

During `OnConnected`, the endpoint MUST already be usable:

- `IsConnected` MUST be `true`;
- `RemoteEndPoint` MUST be non-null;
- `Send` and `Disconnect` are valid operations.

During and after `OnDisconnected`, `IsConnected` MUST be `false` and
`RemoteEndPoint` MUST be `null`. No further `OnReceived` callback may occur.
For a connected client, `OnDisconnected` and `OnStopped` MUST each occur
exactly once and in that order.

`Init` MUST succeed before `Start`. `Init` and `Start` are one-time
operations: RawReliableAck clients and servers are single-use and MUST NOT be
reinitialized or restarted after stopping. If `Start` returns `false`, the
transport is permanently invalid and no handler lifecycle callback is
guaranteed; the caller MUST handle that return directly.

There is no automatic reconnect or session resume. A disconnect is terminal;
application logic that wants reconnection MUST create a new client and a new
logical session.

## 7. Endpoint operations

### 7.1 Endpoint metadata

`IsConnected`, `RemoteEndPoint`, and `MessageMaxByteSize` MUST be safe to read
concurrently. `MessageMaxByteSize` is an inclusive maximum for the
application payload in a `UnionDataList`; it excludes transport framing and
control metadata. The client and server endpoints for one established
connection MUST report the same limit. Empty regular and handshake payloads
are valid.

### 7.2 `Send`

`Send(UnionDataList)` is thread-safe. Concurrent successful sends on one
endpoint are ordered by the transport's linearization order for those calls.
`Send` MUST NOT wait for network delivery or peer handling; it returns after
validation and outbound admission.

`SendResult.Ok` means only that the transport accepted the message for
delivery. It is **not** a peer receipt and does not mean that `OnReceived` has
run. RawReliableAck provides no application-visible message acknowledgement,
retry, receipt, or delivery-confirmation callback. Applications requiring a
receipt MUST define one above this transport.

For each direction independently:

1. An accepted regular message MUST be delivered no more than once.
2. Delivered messages MUST preserve the order of accepted sends.
3. If termination interrupts delivery, the peer MAY receive only a contiguous
   prefix of the accepted-send order. It MUST NOT receive a later accepted
   message after an earlier accepted message was lost.

The ordering guarantee is directional only. It does not define a total order
between messages sent concurrently in opposite directions.

Every `Send` invocation transfers ownership of its argument to the transport,
regardless of its `SendResult`. After calling `Send`, application code MUST
NOT read, mutate, retain, release, or retry using that buffer. The transport
may release or modify it internally.

```csharp
SendResult result = endpoint.Send(message); // Ownership transfers unconditionally.

if (result == SendResult.BufferOverflow)
{
    // Schedule application-defined backpressure handling. A retry must use a
    // newly created buffer containing the same logical message.
    ScheduleRetryWithNewBuffer();
}
else if (result != SendResult.Ok)
{
    RecordSynchronousSendFailure(result);
}
```

Every synchronous non-`Ok` result (`MessageTooBig`, `InvalidMessage`,
`InvalidAddress`, `NotConnected`, `BufferOverflow`, or `Error`) MUST leave
the logical connection unchanged. In particular, `BufferOverflow` is
non-fatal, and a later send MAY succeed. RawReliableAck offers no
queue-drained or writable notification; applications MUST choose their own
retry and backpressure policy.

If an asynchronous failure occurs after `Send` returns `Ok`, the transport
MUST destroy the logical connection and raise `OnDisconnected`. Delivery of
messages still outstanding at that point is unknown.

### 7.3 `Disconnect`

`Disconnect(reason)` is thread-safe and non-blocking. `true` means that this
call initiated logical teardown; completion is reported asynchronously through
lifecycle callbacks. `false` means that the endpoint is already disconnecting
or disconnected, and the original reason and teardown remain unchanged.

Once `Disconnect` returns `true`, the transport MAY discard already accepted
but undelivered outbound messages. The caller's reason MUST be supplied to
the local `OnDisconnected` and, for a client, local `OnStopped` as the exact
same `StopReason` instance. The remote peer's reason is implementation-defined
and MUST NOT be assumed to equal the local reason.

`Send` and `Disconnect` may race. Either operation may linearize first:
`Send` may return `Ok` and subsequently be discarded by teardown, or it may
return `NotConnected` if teardown wins.

Application logic MAY call `Send` and `Disconnect` from `OnConnected` or
`OnReceived`. `Send` in `OnDisconnected` MUST return `NotConnected` and still
consume its buffer according to the ownership rules. If `Disconnect` is called
from a callback, `OnDisconnected` MUST be deferred until that callback
returns; lifecycle callbacks MUST NOT be reentrant.

## 8. Callback concurrency and resource ownership

For one logical connection, all callbacks (`OnConnected`, `OnReceived`,
`OnDisconnected`, and client `OnStopped`) MUST be serialized and
non-reentrant. Callbacks for different server sessions MAY execute
concurrently. Application state shared across sessions MUST therefore be
thread-safe.

The callback thread or scheduler is implementation-defined. Handlers MUST NOT
rely on thread affinity. Handlers, including `TryAck`, MUST return promptly:
blocking a callback can stall processing and contribute to backpressure or
transport timeout.

The following ownership rules are normative:

| Callback or operation | Owner after the call | Required action |
| --- | --- | --- |
| `Send(buffer)` | Transport | Caller MUST never use or release `buffer` again. |
| `FillAckData(buffer)` | Transport | Handler populates only; MUST NOT retain or release. |
| `FillAckResponse(buffer)` | Transport | Handler populates only; MUST NOT retain or release. |
| `TryAck(ackData)` | Acknowledger | MUST release exactly once after validation. |
| client `OnConnected(..., ackResponse)` | Client handler | MUST release exactly once. |
| `OnReceived(receivedBuffer)` | Receiving handler | MUST release exactly once. |

Every owner in this table MUST eventually release its transferred or owned
reference exactly once after finishing with it. An implementation MAY take
additional internal references, but it MUST balance those references without
releasing the owner's reference more than once.

```csharp
public void OnReceived(UnionDataList receivedBuffer)
{
    try
    {
        ProcessMessage(receivedBuffer);
    }
    finally
    {
        receivedBuffer.Release();
    }
}

public void OnConnected(
    IRawReliableAckClientSideEndpoint endpoint,
    UnionDataList ackResponse)
{
    try
    {
        ApplyServerAdmissionData(ackResponse);
        _endpoint = endpoint;
    }
    finally
    {
        ackResponse.Release();
    }
}
```

The release obligation applies even when application processing fails. A
handler that needs data after a callback MUST retain or copy it according to
the `UnionDataList` resource contract, then release its callback-owned
reference exactly once.

## 9. Failure and transport shutdown

An exception thrown by application logic in `FillAckData`, `OnConnected`, or
`OnReceived` MUST terminate the affected local logical connection with an
`ExceptionFail` containing that exact exception instance. Every affected local
teardown callback MUST receive that reason. The remote peer's reason remains
implementation-defined. An exception in `TryAck` or `FillAckResponse` fails
establishment as described in Section 5. An exception in `OnDisconnected` or
client `OnStopped` MUST NOT create duplicate lifecycle callbacks. Applications
MUST use `try`/`finally` to release callback-owned buffers before allowing an
exception to escape.

When `Stop(reason)` is called on a connected client, normal teardown MUST
occur: `OnDisconnected(reason)` followed by `OnStopped(reason)`, both with the
exact supplied `StopReason` instance. If a started client is stopped or
otherwise terminates before `OnConnected`, it MUST receive exactly one
`OnStopped(reason)` with that exact instance and no `OnDisconnected`.

When `Stop(reason)` is called on a server, it MUST stop accepting new clients
and logically disconnect every active session with that exact reason instance.
The
transport-level `onStopped` callback supplied to `Start` MUST run only after
all affected handler teardown callbacks have completed.

The following behavior is explicitly implementation-defined:

- handshake, idle, and keep-alive timeouts;
- failure-detection mechanism and timing;
- client-visible signaling of server rejection;
- callback execution context;
- remote stop reason;
- outbound queue capacity, scheduling, and drain rate; and
- physical-connection behavior after logical disconnection.

## 10. Security considerations

RawReliableAck treats ACK data, ACK responses, and regular messages as opaque
application data. It provides **no** guarantee of:

- peer authentication or authorization;
- confidentiality;
- integrity or tamper detection;
- replay protection; or
- a stable identity derived from `RemoteEndPoint`.

Applications MUST use a protected and authenticated underlying transport or
implement the required security protocol above RawReliableAck. Admission
logic in `TryAck` SHOULD validate all information required to create a trusted
session.

## 11. Application-author conformance checklist

Application logic conforms to this specification only if it:

- [ ] calls `Init` successfully before `Start`, and handles `Start == false`;
- [ ] treats transport instances and client sessions as single-use;
- [ ] authenticates and authorizes connections in `TryAck`;
- [ ] returns a fresh handler from every accepted `TryAck`;
- [ ] releases every `TryAck`, `OnConnected` ACK-response, and `OnReceived`
      buffer exactly once;
- [ ] never accesses a buffer after passing it to `Send`;
- [ ] treats `SendResult.Ok` as queue admission rather than a delivery receipt;
- [ ] retries `BufferOverflow` only with a newly created buffer and an
      application-defined backpressure policy;
- [ ] keeps callbacks prompt and protects state shared by sessions;
- [ ] expects no callback thread affinity, no automatic reconnect, and no
      security protection from this contract; and
- [ ] implements application-level receipts when delivery confirmation is
      required.

## 12. Transport-implementer conformance checklist

An implementation conforms to this specification only if it:

- [ ] performs the ACK admission flow and serializes `TryAck` calls;
- [ ] creates exactly one fresh server handler per accepted session;
- [ ] invokes server `OnConnected` only after its ACK response is accepted for
      outbound delivery;
- [ ] delivers regular messages at most once, in accepted-send order, with
      only a contiguous prefix deliverable after termination;
- [ ] serializes callbacks and prevents lifecycle reentrancy per connection;
- [ ] allows concurrent `Send` calls and defines their order by linearization;
- [ ] transfers and releases buffers according to the ownership table;
- [ ] keeps every synchronous non-`Ok` send result non-fatal;
- [ ] terminates a connection after asynchronous failure or handler exception;
- [ ] preserves local disconnect reasons and performs the specified client and
      server teardown sequences;
- [ ] makes endpoint metadata safe for concurrent reads; and
- [ ] documents all implementation-defined timeout, rejection, queue, and
      execution-context behavior without weakening the normative contract.
