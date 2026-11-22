# RRUnreliable Transport Specification

## 1. Scope

`RRUnreliable` is the unreliable type of the RR transport model. This document
includes the [RR common specification](../Specification.md) by reference. Its
requirements are normative here, and this document defines only what the
unreliable type adds.

It matches each client request with at most one server response.

The client sends a `UnionDataList` request bound to a per-request handler and
receives at most one response through that handler. The server receives the
request with a response handle and the source `IEndPoint`, and may answer it
once. The client may cancel a pending request locally.

This specification defines:

- the type interfaces;
- request delivery and response correlation;
- the client pending window;
- the server response handle and its single-use rule;
- the observable effects of cancellation and TTL expiry;
- the type send results.

`RRUnreliable` provides no response guarantee. A request may receive no
response. A server response may be lost, duplicated, or reordered. A server may
reject a request silently; the client observes only the pending-window expiry.

### 1.1 Applicability

| Section or topic | Included | Reason |
| --- | --- | --- |
| Client association | yes | Inherited from the shared contract. |
| Server association and routing | yes | Inherited from the shared contract; the server binds one handler. |
| Establishment and negotiation | no | The model has no handshake. |
| Rejection and timeout | no | Rejection is a silent per-request decision, defined in section 6.3. |
| Admission and handler binding | no | The server does not admit peers. |
| Request acceptance | yes | `OnRequested` accepts or rejects each request through its return value. |
| Identifiers and correlation | yes | The request id travels on the wire; the server-global id is local bookkeeping. |
| Pending window and TTLs | yes | The pending window is the core of this type. |
| Responses and completion | yes | At most one response and no completion callback. |
| Cancellation | yes | Cancellation is client-local and silent. |

### 1.2 References

- [`../Specification.md`](../Specification.md) — the shared RR contract:
  interfaces, binding, identifiers, TTLs, message model, ownership, security.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Terms

Terms of the shared contract apply as defined there. This specification adds:

| Term | Meaning |
| --- | --- |
| **pending window** | The set of client request handlers bound to requests that have not been answered, cancelled, expired, or failed. |
| **single-use handle** | A response handle that closes on the first call that passes validation. |
| **silent rejection** | A server decision not to answer a request, delivered to the client only as pending-window expiry. |

## 4. Public contract

```csharp
public interface IRRUnreliableClientTransport : IRRClientTransport
{
    SendResult Request(UnionDataList request,
        IRRUnreliableRequestHandler handler);
}

public interface IRRUnreliableTransport : IRRTransport
{
}

public interface IRRUnreliableRequestHandler : IRRRequestHandler
{
    void Response(UnionDataList response);
}

public interface IRRUnreliableServerTransport : IRRServerTransport
{
    bool Init(IRRUnreliableServerHandler handler);
}

public interface IRRUnreliableServerHandler : IHandler
{
    bool OnRequested(IEndPoint client, UnionDataList request,
        IRRUnreliableResponse response);
}

public interface IRRUnreliableResponse : IRRResponse
{
}
```

`IRRClientTransport`, `IRRServerTransport`, `IRRRequestHandler`, `IRRResponse`,
`RRRequestHandler`, and `RRResponse` come from the shared contract.

The client binds no transport-level handler and has no `Init`; it is ready to
start after construction. Each `Request` binds its own
`IRRUnreliableRequestHandler`. The server binds one
`IRRUnreliableServerHandler` through `Init`. Neither handler has lifecycle
callbacks; readiness is `Start` returning `true`.

A response handle is supplied by the transport; applications **MUST NOT**
implement `IRRUnreliableResponse`.

A request frame carries a request-id header. `MessageMaxByteSize` excludes
transport framing and that header.

## 5. Model

The unit of work is one request answered by at most one response. There is no
completion notification and no response stream. The client owns a bounded pending
window; the server owns a single-use handle per request.

### 5.1 Responsibility split

| Topic | Guaranteed by RRUnreliable | Application responsibility | Implementation-defined |
| --- | --- | --- | --- |
| Delivery | Complete messages; requests and responses may be lost, duplicated, reordered, or delayed. | Treat every accepted request and response as possibly lost. | Carrier and queue behavior. |
| Sending | `Ok` means local acceptance; the handler is bound and holds its request id. | Handle every `SendResult`; retry with a new handle and a new payload. | Queue capacity and scheduling. |
| Handlers | Per-request callbacks are serialized; the single server handler is serialized. | Return promptly, release payloads, protect shared state. | Callback thread. |
| Routing | Responses are accepted only from `ServerEndPoint` and only for a live id. | Treat the route as unauthenticated. | Addressing. |
| Failure | A stopped or failed transport clears the pending window without a callback. | Observe `onStopped`; expect no completion callback. | Failure detection and timing. |
| Security | No authentication, authorization, confidentiality, integrity, or replay protection. | Protect the response handle and add the required protection. | Protection outside this contract. |
| Lifecycle | Server `Init` is one-time and pre-start; `Start` is one-time. | Call `Init` once before `Start`. | Scheduler and timing. |

## 6. Establishment and requests

### 6.1 Client association

Inherited from [the shared contract](../Specification.md), section 6.1 (Client
association). Responses are accepted only from `ServerEndPoint`.

### 6.2 Server association and routing

Inherited from [the shared contract](../Specification.md), section 6.2 (Server
association and routing). The server binds one `IRRUnreliableServerHandler`.

### 6.3 Request acceptance

`OnRequested` is invoked on a running server. Callbacks for the single server
handler are serialized and non-reentrant.

The handler owns the request reference and **MUST** release it exactly once,
including when it rejects the request or throws.

The response handle starts open and carries the server-global id. Returning
`true` accepts the request and keeps the handle open. Returning `false` rejects
it: the transport **MUST** close the handle, and a later `Response` call on it
**MUST** return `Error`. Rejection is silent to the client; the client's pending
request simply expires.

If `OnRequested` throws, the server **MUST** catch and log the exception, treat
the request as rejected, close the handle, and continue running. It **MUST NOT**
stop or invalidate the transport solely because `OnRequested` threw. A response
already accepted with `Ok` before the throw may still be delivered.

The handle stays usable after `OnRequested` returns and may be used
asynchronously. Different requests receive different handles.

### 6.4 Identifiers and correlation

Inherited id domains from [the shared contract](../Specification.md), section
6.4 (Identifiers and correlation). This type defines the wire use.

A request frame carries the client-local request id. A response frame carries
the client-local request id that the server received, so the client can select
the bound handler. The server-global id is server-side bookkeeping and is not
required to appear on the wire.

### 6.5 Pending window and TTLs

`RequestTtl` and `ResponseTtl` start at the points defined by the
[shared contract](../Specification.md), section 6.5. This type defines their
observable effects.

The client pending window contains every bound request handler until one of the
following wins:

1. the first valid response for the request is taken for delivery;
2. the request is cancelled through `Cancel()`;
3. the request TTL expires;
4. the transport stops or fails, which clears the pending window.

Expiry is silent: no callback is raised, the handler is unbound, and the pending
id is no longer eligible. A response for an id that is unknown, expired, already
answered, cancelled, or sent by another route **MUST** be released and dropped.

An accepted request therefore receives **zero or one** `Response` call. A
duplicate response for a request whose first response was already taken is
dropped. When several pending-window causes race, the first cause that removes
the request wins; every later cause produces no additional callback.

The response TTL closes the server handle. When it expires, the handle is closed
and a later `Response` returns `Error`; no callback is raised. If the server
never responds, the client pending request eventually expires independently.
Neither side receives a timeout callback.

### 6.6 Responses and completion

`Request` returns a result from section 9.

On `Ok`, `handler.RequestId` is the 16-bit client-local request id of the
request. The 64-bit `long` id belongs to `IRRResponse.RequestId` and identifies
the request on the server side.

The id enters the pending window before the frame is submitted to the carrier.
The request may still be lost; `Ok` is not a peer or response guarantee.

If the carrier call fails after the id entered the pending window, the id
**MUST** be removed, the handler **MUST** be unbound, and the call **MUST NOT**
return `Ok`.

`Response` **MAY** begin before `Request` returns. The handler already holds the
id, so it must tolerate a response that arrives before the caller has observed
the return value. The handler may already be unbound when `Request` returns.

`IRRUnreliableRequestHandler.Response(UnionDataList response)` is invoked at
most once per accepted request, after `Start` returned `true`. While it runs,
`handler.RequestId` still identifies the request.

Callbacks for one request handler are serialized and non-reentrant. Callbacks
for different requests may run concurrently; an implementation may serialize all
client callbacks. A `Request` or `Stop` call from a response callback **MUST
NOT** cause a nested `Response` invocation on the same handler.

After the callback returns, the transport unbinds the handler. The handler
**MUST NOT** expect another callback for that request.

If `Response` throws, the transport **MUST** catch and log the exception,
suppress further processing of only that delivery, and continue running. It
**MUST NOT** stop or invalidate the transport solely because `Response` threw.

There is no completion notification. The absence of a response is observed only
as pending-window expiry.

`SendResult Response(UnionDataList response)` on the server handle sends the
single response for the request. Its result precedence is:

1. the handle is closed or the transport is not running → `Error`;
2. `InvalidMessage` — the response is null or cannot be serialized;
3. `MessageTooBig` — the serialized payload exceeds `MessageMaxByteSize`;
4. the carrier result, including `Ok`, `BufferOverflow`, `Error`, or another
   implementation-defined synchronous failure.

A handle is single-use. The first call that passes validation closes the handle
before the send is attempted, so a later `Response` call returns `Error` even
when the carrier send fails. `Ok` is local acceptance only; it is not a delivery
guarantee.

Calls for one handle are serialized by the transport and may be made from any
thread.

### 6.7 Cancellation

`Cancel()` removes a bound request from the pending window and unbinds the
handler. No callback is raised; a response that arrives later is released and
dropped. Cancellation is local: the server is not informed and may still produce
one response that the client discards.

## 7. Lifecycle and failure

Initialization, start, and stop are inherited from
[the shared contract](../Specification.md), sections 7.1 to 7.4. This type adds
the pending-window rule.

A stopped or failed transport **MUST** clear the pending window without raising
a completion callback. A request removed this way is not replayed.

The transport-level `onStopped` callback **MUST** be invoked exactly once after
the terminal transition.

### 7.1 Callback matrix

| Situation | Handler callbacks |
| --- | --- |
| Transport `Start` returns false | none |
| `Start` returns true, request accepted | `OnRequested` on the server; at most one `Response` on the client |
| Request rejected by returning `false` | none; the client expires silently |
| Request cancelled | none |
| Request TTL expired | none |
| Response TTL expired | none |
| Transport stops or fails | none for pending requests; `onStopped` runs |

## 8. Message model and limits

Inherited from [the shared contract](../Specification.md), section 8 (Message
model and limits). This type excludes transport framing and the request-id
header from `MessageMaxByteSize`.

## 9. Send results

An implementation **MUST** select a `Request` result in this order: the shared
handler checks, which throw rather than return; transport state; message
validity; message size; outbound capacity; other synchronous errors.

| Result | Required meaning |
| --- | --- |
| `Ok` | The request was accepted; the handler is bound and holds its client-local request id. |
| `InvalidMessage` | The request is null or cannot be serialized. |
| `MessageTooBig` | The serialized payload exceeds `MessageMaxByteSize`. |
| `InvalidAddress` | The client route is fixed at construction, so this transport never returns it. |
| `NotConnected` | The client transport is not running. |
| `BufferOverflow` | The implementation cannot accept the request because local outbound capacity is full. |
| `Error` | Another synchronous sending failure occurred. |
| `Unknown` | Reserved enumeration value. `Request` never returns it. |

`Response` results are defined in section 6.6. Every synchronous non-`Ok` result
is non-fatal and **MUST NOT** by itself stop or invalidate the transport.

`BufferOverflow` and the other non-`Ok` results carry no retry signal.
Applications choose their own retry and backpressure policy, and must obtain a
new handle and a new payload for a new attempt; a handle is single-use.

## 10. Delivery guarantees

`RRUnreliable` preserves complete messages but gives no delivery guarantee.
Requests and responses may be lost, duplicated, reordered, or delayed.

For each accepted request:

- the request may never reach the server;
- the server may invoke `OnRequested` zero, one, or multiple times when the
  carrier duplicates the request;
- the client delivers at most one `Response`;
- the response may never reach the client;
- no ordering is guaranteed between different requests.

The client delivers the first valid response for a pending request and drops
every later or unknown response.

An implementation **MUST NOT** invoke `Response` for a response that was accepted
before the corresponding `Request` call bound the handler. The callback for a
response **MAY** begin on any scheduler after the response frame is accepted
locally.

```mermaid
sequenceDiagram
    participant C as Request handler
    participant CE as Client transport
    participant ST as Server transport
    participant S as Server handler
    participant R as Response handle

    C->>CE: Request(payload, handler) -> Ok
    Note over CE,ST: request may be lost, duplicated, or reordered
    CE-->>ST: zero or more request frames
    ST->>S: OnRequested(client, request, response)
    alt accepted and answered
        S->>R: Response(payload)
        R-->>CE: response frame carrying the request id
        CE->>C: Response(payload)
    else rejected
        S-->>ST: false
        Note over R: handle closed
        Note over CE: pending request expires silently
    end
```

There is no acknowledgement, retry, confirmation, response receipt, or
completion callback.

## 11. Concurrency and buffer ownership

Inherited from [the shared contract](../Specification.md), section 11
(Concurrency and buffer ownership). This type adds:

- callbacks for one request handler are serialized and non-reentrant;
- calls for one response handle are serialized by the transport;
- the server's single handler callbacks are serialized.

## 12. Implementation-defined choices

Inherited areas apply. This type leaves open:

| Area | Left open | Invariant that remains binding |
| --- | --- | --- |
| Wire correlation | How the request id and any transport header are encoded. | Responses select the bound handler by the client-local id. |
| TTL values | The configured `RequestTtl` and `ResponseTtl`. | Both are positive durations exposed by the transport. |
| Pending-window storage | The data structure and capacity of the window. | At most one response is delivered per request. |
| Capacity | Queue size and scheduling. | `BufferOverflow` is reported, not hidden. |

An implementation-defined choice **MUST NOT** weaken any requirement of this
specification, and the implementation **MUST** document it.

## 13. Security boundary

Inherited from [the shared contract](../Specification.md), section 13 (Security
boundary). A response handle is a capability for exactly one request; the
application **MUST NOT** expose it to code that is not authorized to answer that
request.

## 14. Conformance controls

No conformance controls are defined.

## 15. Transport-implementer checklist

- [ ] Bind one server handler with a one-time, pre-start `Init`.
- [ ] Keep server callbacks serialized and non-reentrant.
- [ ] Keep each request handler's callbacks serialized and non-reentrant.
- [ ] Assign server-global ids unique for the transport lifetime, starting at
      `1`.
- [ ] Start client-local ids from a fresh random value on every start and
      increment them with 16-bit wraparound.
- [ ] Throw `ArgumentNullException` for a null handler before consuming the
      payload.
- [ ] Throw `InvalidOperationException` for an already-bound handler before
      consuming the payload.
- [ ] Bind the handler before submitting the request frame and publish its
      `RequestId`.
- [ ] Deliver at most one response per request handler.
- [ ] Drop unknown, expired, duplicate, cancelled, and foreign-route responses.
- [ ] Unbind the handler after its response callback returns, or when the
      request is cancelled, expires, or is abandoned.
- [ ] Close the response handle on success, rejection, expiry, or stop; a closed
      handle returns `Error`.
- [ ] Enforce both TTLs without fabricating callbacks for expiry.
- [ ] Enforce `MessageMaxByteSize` on sent and received payloads.
- [ ] Drop and log malformed or oversized inbound frames without stopping.
- [ ] Transfer and release buffers according to the shared contract.
- [ ] Treat `OnRequested` and `Response` exceptions as specified and keep
      unrelated activity running.
- [ ] Preserve stop and `onStopped` ordering and the exact supplied reason.
- [ ] Document every implementation-defined choice of section 12.

## 16. Application-author checklist

- [ ] Call server `Init` exactly once before `Start`.
- [ ] Derive every request handler from `RRRequestHandler`.
- [ ] Use a distinct handler for each concurrently pending request; reuse only
      after the request completed, was cancelled, or expired.
- [ ] Treat every accepted request as potentially lost and every accepted
      response as potentially lost.
- [ ] Never expect more than one `Response` per accepted request.
- [ ] Treat `Ok` from `Request` or `Response` as local acceptance only.
- [ ] Never access or release a payload after passing it to `Request` or
      `Response`.
- [ ] Release each delivered payload exactly once, even when processing fails.
- [ ] Keep handlers prompt, non-blocking, and exception-safe.
- [ ] Treat rejection as silent; rely on TTLs rather than a rejection callback.
- [ ] Do not answer a request after the response TTL exposed by
      `IRRUnreliableServerTransport.ResponseTtl`.
- [ ] Observe `onStopped`; do not restart a stopped or invalid transport.
