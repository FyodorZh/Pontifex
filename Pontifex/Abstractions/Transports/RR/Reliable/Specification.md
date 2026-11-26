# RRReliable Transport Specification

## 1. Scope

`RRReliable` is the reliable type of the RR transport model. This document
includes the [RR common specification](../Specification.md) by reference. Its
requirements are normative here, and this document defines only what the
reliable type adds.

The client sends a request bound to a per-request handler. The server handler
accepts the request by responding to it, or rejects it explicitly, and answers it
with zero or more responses followed by one explicit completion. The client may
cancel the request.

This specification defines:

- the type interfaces;
- the delivery, ordering, and duplicate-suppression model;
- request acceptance, rejection, and completion;
- response-stream states and results;
- cancellation and the type send results;
- the terminal notification raised when the transport stops.

### 1.1 Applicability

| Section or topic | Included | Reason |
| --- | --- | --- |
| Client association | yes | Inherited from the shared contract; the route is the client `ServerEndPoint`. |
| Server association and routing | yes | Inherited from the shared contract; the server binds one handler. |
| Establishment and negotiation | no | The model has no handshake. |
| Rejection and timeout | no | Rejection is a per-request operation, defined in section 6.6. |
| Admission and handler binding | no | The server does not admit peers. |
| Request acceptance | yes | The server decides each request through its response handle. |
| Identifiers and correlation | yes | Inherited from the shared contract. |
| Pending window and TTLs | yes | Inherited start points; expiry raises a completion, defined in section 6.5. |
| Responses and completion | yes | Zero or more ordered responses followed by exactly one completion. |
| Cancellation | yes | Cancellation produces `Finish(Canceled)` and a best-effort server notice. |

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
| **response stream** | The ordered sequence of responses accepted for one request, followed by one completion. |
| **completion** | The single `Finish` notification delivered to the client request handler. |
| **finish** | The server operation `IRRReliableResponse.Finish()` that completes a request normally. |
| **reject** | The server operation `IRRReliableResponse.Reject()` that refuses a request. |
| **terminal state** | The state of a request after its completion, its rejection, its cancellation, or its failure. |

## 4. Public contract

```csharp
public interface IRRReliableClientTransport : IRRClientTransport
{
    SendResult Request(UnionDataList request, IRRReliableRequestHandler handler);
}

public interface IRRReliableTransport : IRRTransport
{
}

public interface IRRReliableRequestHandler : IRRRequestHandler
{
    void Response(UnionDataList response);
    void Finish(RRReliableCompletionType type);
}

public interface IRRReliableServerTransport : IRRServerTransport
{
    bool Init(IRRReliableServerHandler handler);
}

public interface IRRReliableServerHandler : IHandler
{
    void OnRequested(IEndPoint client, UnionDataList request,
        IRRReliableResponse response);
}

public interface IRRReliableResponse : IRRResponse
{
    SendResult Finish();
    SendResult Reject();
}

public enum RRReliableCompletionType
{
    Normal,
    Canceled,
    Rejected,
    Failed
}

public interface ITimerFactory
{
    IDisposable Schedule(TimeSpan delay, Action callback);
}
```

`IRRClientTransport`, `IRRServerTransport`, `IRRRequestHandler`, `IRRResponse`,
`RRRequestHandler`, and `RRResponse` come from the shared contract.

`Pontifex.Utils.ITimerFactory` is the abstraction the type uses to schedule its TTL
callbacks. It appears in protected constructors of the public abstract core
types, so a carrier implementation supplies the clock-free timer seam. The core
types default to `SystemTimerFactory.Instance` when no factory is passed. The
contract is documented on the interface: scheduling is one-shot,
`Timeout.InfiniteTimeSpan` arms a callback that never fires, any other negative
delay is rejected, and a callback exception is swallowed by the factory.

The client binds no transport-level handler and has no `Init`; it is ready to
start after construction. The server binds one `IRRReliableServerHandler`
through `Init`. Neither handler has `Started` or `Stopped` callbacks. Readiness
is `Start` returning `true`; the stop signals are `onStopped`, the terminal
completion, and closed response handles.

The type requires no correlation header; an implementation **MAY** add an
implementation-defined header under section 12. `MessageMaxByteSize` is the
serialized payload only: it excludes transport framing and that
implementation-defined header.

## 5. Model

The unit of work is one request with a complete response stream. The client
handler receives zero or more `Response` calls followed by exactly one `Finish`.
The server decides the outcome only through the response handle.

### 5.1 Responsibility split

| Topic | Guaranteed by RRReliable | Application responsibility | Implementation-defined |
| --- | --- | --- | --- |
| Delivery | Exactly-once presentation per request, ordered responses, one completion per accepted request. | Handle every completion type; treat `Ok` as local acceptance. | Retries, reordering, and duplicate suppression internally. |
| Sending | `Request` and `Response` return a `SendResult`; `Ok` is local acceptance. | Choose retry and backpressure policy; use a new payload for a retry. | Queue capacity and scheduling. |
| Handlers | Callbacks for one request handler are serialized and non-reentrant; different request handlers may run concurrently. | Decide every response handle exactly once; release payloads. | Callback thread. |
| Routing | Responses are accepted only from `ServerEndPoint`. | Treat the route as unauthenticated. | Addressing. |
| Failure | A stopped or failed transport finishes pending requests with `Failed`. | Handle `Finish(Failed)` as a terminal outcome. | Failure detection and timing. |
| Security | No authentication, authorization, confidentiality, integrity, or replay protection. | Protect the response handle and add the required protection. | Protection outside this contract. |
| Lifecycle | Server `Init` is one-time and pre-start; `Start` is one-time. | Call `Init` once before `Start`. | Scheduler and timing. |

## 6. Establishment and requests

### 6.1 Client association

Inherited from [the shared contract](../Specification.md), section 6.1 (Client
association). `ServerEndPoint` is immutable and is the only accepted response
route.

### 6.2 Server association and routing

Inherited from [the shared contract](../Specification.md), section 6.2 (Server
association and routing). The server binds one `IRRReliableServerHandler`.

### 6.3 Request acceptance

`OnRequested` is invoked once for every request the transport presents. The
single server handler's callbacks are serialized and non-reentrant, matching
the shared contract section 11; the transport provides no global serialization
across transports.

The handler owns the request reference and **MUST** release it exactly once.

The response handle starts undecided. The handler decides the outcome only
through the handle:

| First successful call | Resulting state | Client-visible effect |
| --- | --- | --- |
| `Response` | accepted response stream | responses may be delivered |
| `Finish` | accepted and completed | `Finish(Normal)` after the accepted responses |
| `Reject` | rejected | `Finish(Rejected)` when the notification arrives, otherwise `Finish(Failed)` at `RequestTtl` |

`Response` and `Finish` may be called inside `OnRequested` or later. Returning
without deciding leaves the handle undecided and live until it is answered or
`ResponseTtl` expires. An accepted or rejected handle keeps that state.

A throwing `OnRequested` **MUST** be caught and logged and **MUST NOT** stop or
invalidate the transport. If the handle was still undecided, the transport
**MUST** reject it. If it was already accepted or rejected, the throw changes
nothing and the request keeps its state.

### 6.4 Identifiers and correlation

Inherited from [the shared contract](../Specification.md), section 6.4
(Identifiers and correlation). This type requires no correlation header; the
client-local id is local bookkeeping and is not required on the wire. An
implementation **MAY** add an implementation-defined header under section 12.

### 6.5 Pending window and TTLs

Inherited start points from [the shared contract](../Specification.md), section
6.5 (Pending window and TTLs). This type defines the observable effects.

When `ResponseTtl` expires, the handle is closed, every later `Response`,
`Finish`, or `Reject` returns `Error`, and the transport **MUST** complete the
client request with `Finish(Failed)` unless another terminal notification was
already accepted. Expiry triggers no application callback.

The server transport **MAY** drop a cancelled, failed, or expired client
connection or route; if it does, later handle calls return `Error`.

A request whose `RequestTtl` expires receives exactly one `Finish(Failed)`.

### 6.6 Responses and completion

The handle stays usable after `OnRequested` returns and may be used
asynchronously, including from inside `OnRequested`. Different requests may use
their handles concurrently. Calls for one handle are serialized by the transport
and may be made from any thread.

`Response`, `Finish`, and `Reject` follow these rules:

- **`Response`** sends one response. Zero or more responses are allowed before
  `Finish`. The first successful call accepts the request. Its result precedence
  is:
  1. the transport is stopping or stopped, or the handle is closed → `Error`;
  2. the request was rejected → throw `InvalidOperationException`;
  3. `InvalidMessage` — the response is null or cannot be serialized;
  4. `MessageTooBig` — the serialized payload exceeds `MessageMaxByteSize`;
  5. the carrier result, including `Ok`, `BufferOverflow`, `Error`, or another
     implementation-defined synchronous failure.
- **`Finish`** completes the request normally and accepts it if it is still
  undecided. The first call that completes it returns `Ok`; every later call
  returns `Error`. `Finish` participates in the ordered stream: the client's
  `Finish(Normal)` follows every response that returned `Ok` before it.
- **`Reject`** rejects the request if it is still undecided. It returns `Ok` for
  the first successful call and for a repeated call, and `Error` when the
  transport is stopping or stopped. After a successful `Reject`, every
  `Response` and `Finish` call throws `InvalidOperationException`; if the
  transport later stops, all calls return `Error` instead. Calling `Reject`
  after `Response` or `Finish` succeeded throws.

A response accepted with `Ok` participates in the request's ordered delivery:
the transport attempts it until the request completes or leaves the live set.
`Ok` is local acceptance; it does not prove that the client received the
response.

Every request accepted by `Request` receives exactly one `Finish`:

| Trigger | `RRReliableCompletionType` |
| --- | --- |
| The server's `Finish()` completed normally and its completion reached the client | `Normal` |
| The server's `Reject()` succeeded and the rejection reached the client | `Rejected` |
| The client cancelled the request through `handler.Cancel()` | `Canceled` |
| A transport stopped or failed, the carrier lost the request, the request TTL expired, or a rejection notification did not reach the client | `Failed` |
| The server response TTL expired | `Failed` |

`Finish(Normal)` **MUST** follow every response of the request that was accepted
before the server's `Finish`. No `Response` follows any `Finish`.

`Finish(Rejected)` is delivered when the rejection reaches the client, even when
the server sent no responses.

`Finish(Canceled)` is delivered exactly once by `Cancel()`. The server does not
receive a cancellation callback. `Finish(Failed)` covers every other terminal
path.

`Finish` is guaranteed; its type is the outcome the client actually observed, not
necessarily the server's decision. Several terminal causes may race. The first
cause the client transport commits wins; every later cause is suppressed and
produces no additional `Finish`.

### 6.7 Cancellation

For a pending request, `handler.Cancel()` **MUST**:

1. suppress every later `Response` for that request;
2. deliver exactly one `Finish(Canceled)`;
3. make a best effort to inform the server so it stops producing responses.

If `Cancel` is called outside a callback of that handler, the notification
**MUST** be scheduled before the call returns. If `Cancel` is called from inside
a callback of the same handler, the terminal `Finish` **MUST** be delivered after
the running callback returns, not reentrantly.

No server-side cancellation callback exists. If the transport can inform the
server, later `Response` and `Finish` calls on that request **MUST** return
`Error`. If it cannot, the server observes the loss on its next response attempt
or at its response TTL.

The handler remains bound until the `Finish` callback returns.

## 7. Lifecycle and failure

Initialization, start, and stop are inherited from
[the shared contract](../Specification.md), sections 7.1 to 7.4. This type adds
one terminal rule and one ordering rule.

A stopped transport finishes its pending requests with `Failed`. Requests and
responses from a previous transport lifetime are not part of the new instance's
pending set.

The terminal `Finish` notification for each pending request **MUST** be scheduled
before the transport `onStopped` callback begins.

### 7.1 Callback matrix

| Situation | Handler callbacks |
| --- | --- |
| Transport `Start` returns false | none |
| `Start` returns true, request accepted | `OnRequested` once on the server; `Response` and `Finish` on the client |
| Request rejected | one `Finish(Rejected)` or `Finish(Failed)` |
| Request cancelled | one `Finish(Canceled)` |
| Request TTL or response TTL expired | one `Finish(Failed)` |
| Transport stops or fails | one `Finish(Failed)` per pending request, scheduled before `onStopped` begins; it may complete after |

## 8. Message model and limits

Inherited from [the shared contract](../Specification.md), section 8 (Message
model and limits). This type excludes transport framing and any
implementation-defined header under section 12; it requires no correlation
header.

## 9. Send results

An implementation **MUST** select a `Request` result in this order: the shared
handler checks of section 6.3, which throw rather than return; transport state;
message validity; message size; outbound capacity; other synchronous errors.

| Result | Required meaning |
| --- | --- |
| `Ok` | The request was accepted; exactly one `Finish` will follow. |
| `InvalidMessage` | The request is null or cannot be serialized. |
| `MessageTooBig` | The payload exceeds `MessageMaxByteSize`. |
| `InvalidAddress` | The client route is fixed at construction, so this transport never returns it. |
| `NotConnected` | The client is not running. |
| `BufferOverflow` | The implementation cannot accept the request because local capacity is full. |
| `Error` | Another synchronous transport error occurred. |
| `Unknown` | Reserved enumeration value. `Request` never returns it. |

`IRRReliableResponse` uses `Ok`, `InvalidMessage`, `MessageTooBig`,
`BufferOverflow`, and `Error` as defined in section 6.6, plus the
`InvalidOperationException` cases.

A non-`Ok` `SendResult` from `Request` delivers no callbacks. A synchronous
non-`Ok` result is non-fatal and carries no retry signal.

## 10. Delivery guarantees

`RRReliable` presents an exactly-once, ordered stream per request to the
application, while allowing implementations to retry, reorder, and duplicate
frames internally.

For each request accepted with `SendResult.Ok`:

1. The transport **MUST** attempt delivery until the request is delivered,
   rejected, cancelled, or failed.
2. `OnRequested` **MUST** be invoked at most once for that request. If it is
   invoked, it is invoked exactly once.
3. The transport **MUST** attempt to deliver every response accepted with `Ok`
   to the request handler exactly once, in the order those `Response` calls
   returned `Ok`, unless the request completes first.
4. The transport **MUST** deliver exactly one `Finish`, after every response that
   precedes its completion.
5. The transport **MUST** suppress duplicates. The application **MUST NOT**
   observe the same request or response twice, even when the carrier
   retransmits.

The transport **MUST NOT** claim exactly-once delivery across a persistence
boundary it cannot support. Implementations that persist work across restarts
**MUST** document and preserve their duplicate-suppression scope.

No ordering is guaranteed between different requests. Requests from one client,
requests from different clients, and responses for different requests may be
interleaved, delayed, or handled concurrently by different request handlers. The
single server handler's `OnRequested` callbacks remain serialized and
non-reentrant.

`Ok` from `Request`, `Response`, `Finish`, or `Reject` is local acceptance only.

## 11. Concurrency and buffer ownership

Inherited from [the shared contract](../Specification.md), section 11
(Concurrency and buffer ownership). This type adds:

- calls for one response handle are serialized by the transport;
- a response accepted with `Ok` remains part of the request stream until the
  request completes or leaves the live set;
- a non-`Ok` handle result carries no retry signal, and the application chooses
  its own retry policy with a new payload buffer.

## 12. Implementation-defined choices

Inherited areas apply. This type leaves open:

| Area | Left open | Invariant that remains binding |
| --- | --- | --- |
| Wire correlation | How a request and its responses are correlated on the wire. | The shared correlation and staleness rules of section 6.4. |
| Persistence | Whether pending requests survive a restart. | The duplicate-suppression scope is documented and preserved. |
| Completion transport | How a rejection or completion notification travels. | Exactly one `Finish` reaches the client. |
| Capacity | Queue size and retry scheduling. | `BufferOverflow` is reported, not hidden. |

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

- [ ] Provide one-time server `Init` and one-time `Start`.
- [ ] Provide no handler `Started` or `Stopped` callbacks.
- [ ] Preserve exactly-once request presentation, ordered responses, and one
      `Finish` per accepted request.
- [ ] Treat a rejection notification as best effort and report the observed
      outcome in `Finish`.
- [ ] Suppress duplicates within the documented persistence scope.
- [ ] Reject a handler that does not derive from `RRRequestHandler` with
      `ArgumentException`.
- [ ] Throw `ArgumentNullException` for a null handler and
      `InvalidOperationException` for an already-bound handler before consuming
      the payload.
- [ ] Start client-local ids from a fresh random value on every start and
      increment them with 16-bit wraparound.
- [ ] Publish `RequestId` before the first callback and before carrier
      visibility.
- [ ] Accept responses only from `ServerEndPoint`.
- [ ] Serialize callbacks per request handler and per response handle.
- [ ] Serialize the single server handler's callbacks.
- [ ] Enforce `RequestTtl` from the moment `Request` returns `Ok`.
- [ ] Enforce `ResponseTtl` from the moment the request is received.
- [ ] Finish every pending request with `Failed` on stop or failure, scheduled
      before `onStopped` begins; it may complete after.
- [ ] Close a handle after completion or expiry, and throw
      `InvalidOperationException` after `Reject`.
- [ ] Enforce `MessageMaxByteSize` on sent and received payloads.
- [ ] Drop and log malformed or oversized inbound data without stopping.
- [ ] Transfer and release buffers according to the shared contract and section
      11.
- [ ] Catch and log throwing handlers and keep unrelated requests running.
- [ ] Document every implementation-defined choice of section 12.

## 16. Application-author checklist

- [ ] Bind a distinct request handler to each pending request.
- [ ] Derive every request handler from `RRRequestHandler`.
- [ ] Never reuse a handler while its request is pending; reuse only after
      `Finish` returns.
- [ ] Expect zero or more `Response` calls followed by exactly one `Finish`.
- [ ] Treat `Ok` from `Request`, `Response`, `Finish`, or `Reject` as local
      acceptance only.
- [ ] Handle every completion type; a rejection may surface as `Failed`.
- [ ] Decide every response handle exactly once: `Response` or `Finish` to
      accept, `Reject` to reject.
- [ ] Never call `Response` or `Finish` after `Reject`; they throw.
- [ ] Never call `Response` after `Finish`.
- [ ] Use `handler.Cancel()` to cancel; expect no callbacks after `Finish`.
- [ ] Release every delivered payload exactly once.
- [ ] Keep request and server handlers prompt, non-blocking, and exception-safe.
- [ ] Choose TTLs that accommodate the carrier's latency.
- [ ] Observe `onStopped`; do not restart a stopped or invalid transport.
