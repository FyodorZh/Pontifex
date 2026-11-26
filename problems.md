# Direct Transport Problems

Audit of the Direct transport implementations under
`Transports/Pontifex.Transport.Direct`, including the shared base classes those
carriers run on, their implementation specifications, and the existing Direct
tests.

**Verification basis**

- All Direct sources were read: `Channel`, `ChannelManager`, virtual delivery /
  netem, Raw Unreliable, Raw Reliable, RR Unreliable, RR Reliable, bindings, and
  implementation specs.
- The shared implementations used by Direct were read: `AnyTransport`,
  `RawEndpoint`, `RawReliableEndpoint`, `RawUnreliableEndpoint`, Raw/RR transport
  bases, RR reliable server/session/response state, and the conformance controls.
- `dotnet test Tests/Pontifex.Transport.Direct.Tests` passed: 306/306.
- The Direct RawUnreliable conformance fixture passed: 51/51.
- The tests are green, but the defects below are code-level, race-level, or
  specification-level. They are not reports of missing tests.

**Severity scale**

- **High** — contract violation likely to be observed in normal use, or a
  concurrency/teardown defect capable of corrupting state.
- **Medium** — contract violation in a race, error path, or
  configuration-dependent path.
- **Low** — edge case, robustness gap, specification/doc defect, or test
  hygiene.

---

## Summary

| ID | Severity | Area | Problem |
| --- | --- | --- | --- |
| H-01 | High | Netem | Time base overflows, breaking all netem timing. |
| H-02 | High | RawUnreliable Direct | Server stop loses the supplied `StopReason` for active handlers. |
| H-03 | High | RRReliable (shared) | Server session maps are mutated without the session lock. |
| H-04 | High | RawReliable (shared) | Throwing handlers stop the endpoint/transport. |
| H-05 | High | RawReliable Direct (shared client base) | `Init` does not throw on null and invalidates on rejected calls. |
| H-06 | High | RawReliable Direct (shared client base) | A failed `Start` invokes `onStopped`. |
| M-01 | Medium | RawUnreliable Direct | Server does not enforce `MessageMaxByteSize` on inbound data. |
| M-02 | Medium | RawUnreliable Direct | Peer callbacks can begin before `Send` returns. |
| M-03 | Medium | RawUnreliable (shared) | `Stop` can return before the startup flush delivers messages. |
| M-04 | Medium | RawReliable (shared) | Malformed/unknown frames stop the endpoint instead of being dropped. |
| M-05 | Medium | RawReliable Direct | No handshake deadline in either role. |
| M-06 | Medium | RawReliable Direct | Payloads at the top of the declared size range are rejected. |
| M-07 | Medium | RawUnreliable (shared) | Factory-created handler is abandoned when endpoint creation fails. |
| M-08 | Medium | Channel | Teardown aborts if a delivery system throws during release. |
| M-09 | Medium | Channel | Send/dispose race leaks an owned message. |
| M-10 | Medium | RawUnreliable Direct | Eager netem systems leak threads before start. |
| M-11 | Medium | RRReliable (shared) | `Reject` after a successful `Finish` returns `Error` instead of throwing. |
| M-12 | Medium | RRReliable (shared client) | Timer-factory exception leaves a bound handler with no TTL. |
| M-13 | Medium | RawUnreliable Direct | Client `Connect` does not isolate manager/install exceptions. |
| M-14 | Medium | RawUnreliable Direct (shared) | `Stopped` event invocation is not per-subscriber fault-isolated. |
| L-01 | Low | RawUnreliable Direct | The 100-message outbound budget is not enforced by client admission. |
| L-02 | Low | Raw (shared) | Send-result precedence is wrong in several paths. |
| L-03 | Low | RawUnreliable Direct | Reliable-debug control has contract and race problems. |
| L-04 | Low | RawUnreliable Direct | Several conformance-control checkpoint details are wrong. |
| L-05 | Low | RawReliable Direct | Handshake reject/response frames are not size-checked. |
| L-06 | Low | RawReliable Direct | A payload can reach the handler before `OnConnected`. |
| L-07 | Low | RawReliable Direct | Binding advertises an unusable `handshake_max_byte_size` range. |
| L-08 | Low | RawUnreliable Direct | Binding leak when the second delivery build fails. |
| L-09 | Low | RawUnreliable Direct | Endpoint construction failure leaks handed-off delivery systems. |
| L-10 | Low | RRUnreliable Direct | Client returns `Ok` when its carrier is already stopped. |
| L-11 | Low | RRUnreliable Direct | Pre-start pending bound ignores the configured queue capacity. |
| L-12 | Low | RRUnreliable Direct spec | Deviation wording is inaccurate (queue timing, closed handle, 5 s). |
| L-13 | Low | RawUnreliable Direct spec | Specification claims that the implementation does not satisfy. |
| L-14 | Low | RawReliable Direct spec | “No deviation” is false; missing deadline is not declared. |
| L-15 | Low | RRReliable Direct spec | Response-TTL completion description is wrong. |
| L-16 | Low | RRUnreliable Direct spec | Lines 104–106 exceed 100 characters. |
| L-17 | Low | Netem | Negative configuration values are accepted. |
| L-18 | Low | Netem | Duplicates can exceed `QueueLimit`. |
| L-19 | Low | Netem | Release is not repeat-safe; `Dispose` is not thread-safe. |
| L-20 | Low | ChannelManager | `Connect` leaves a channel registered when the server callback throws. |
| L-21 | Low | Channel | The same delivery system installed in both directions is released twice. |
| L-22 | Low | Direct registry | `id` is shared across Direct protocols; cross-protocol traffic is mishandled. |
| L-23 | Low | Netem docs | `help.md` uses a non-existent namespace. |
| L-24 | Low | VirtualDelivery tests | Suite documentation does not match its contents. |
| L-25 | Low | Direct tests | Default suites poll wall-clock time instead of using a scheduling seam. |
| L-26 | Low | Netem tests | The one-hour-latency rationale is invalid on current runtimes. |

---

## High severity

### H-01 — Netem time base overflows, breaking all netem timing

**Where:** `Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/NetemDeliverySystem.cs`
(`CurrentTimeNs`, around lines 196–199) and
`VirtualDelivery/Netem/SlotScheduler.cs` (around lines 71–74).

**Description:** Both methods compute:

```csharp
Stopwatch.GetTimestamp() * 1_000_000_000L / Stopwatch.Frequency
```

The multiplication overflows `long` long before the division. On .NET on Unix,
`Stopwatch.Frequency` is `1_000_000_000`, so the product overflows after about
9.2 seconds of machine uptime. On Windows (frequency `10_000_000`) it overflows
after about 922 seconds. I verified the current runtime on this machine:
frequency was `1000000000`, and the formula returned `-55,280,639` ns.

**Impact:** Latency, jitter, rate pacing, and slot scheduling are broken for
every `delivery = netem` channel in Raw Unreliable Direct and RR Unreliable
Direct. Messages can be delivered immediately or wait for a wrapped clock;
rate limiting and slot windows are meaningless. The `NetemDeliverySystemTests`
comment that “the base latency is one hour” so a queued packet would still be
alive is false under this runtime.

**Specification:** The Direct RawUnreliable `delivery` contract describes
`latencyns` as “base added delay” and `ratebytespersec` as a rate limit; neither
is implemented correctly.

---

### H-02 — RawUnreliable Direct server loses the supplied `StopReason` for active handlers

**Where:** `Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs`
(`OnStopped`, around lines 463–498),
`Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectServerTransport.cs`
(`StopCarrier`), and
`Raw/Unreliable/RawUnreliableDirectServerEndpoint.cs`
(`OnChannelDisposed`).

**Description:** The server transport stops the carrier before stopping its
route endpoints. `RawUnreliableDirectServerTransport.StopCarrier` unregisters
the id and disposes every registered channel. Every server endpoint has
subscribed to `Channel.Disposed`, so it stops immediately with
`new UnknownRemoteIntention(TransportName)` while the channel is being disposed.
When the base transport later calls `StopRouteEndpoint(entry.Endpoint, reason)`,
the endpoint is already invalidated and the call is a no-op.

**Impact:** For every active server handler, `OnStopped` receives
`UnknownRemoteIntention` instead of the `StopReason` instance passed to
`transport.Stop(reason)`. The transport-level `onStopped` callback still gets the
supplied reason, so the handler and transport disagree. This violates abstract
RawUnreliable §7.2 (“that same instance is supplied to the local handler
teardown callbacks and to `onStopped`”) and §7.4.

**Specification:** `Raw/Unreliable/Specification.md` §7 says this
implementation “declares no deviation”. That is false for this path.

---

### H-03 — RRReliable server session maps are mutated without the session lock

**Where:** `Pontifex/Implementations/RR/Reliable/RRReliableServerSession.cs`
(`MarkCompleted`, around lines 103–126) and
`RRReliableResponse.cs` (`CloseCore`, `MarkClosed`, `CloseForDisposal`).

**Description:** `RRReliableServerSession.MarkCompleted` removes entries from
`_live`, updates `_completed`, and increments `_completedCount` without taking
`SyncRoot`. It is called from response completion (`CloseCore`), session close
(`MarkClosed`), disposal (`CloseForDisposal`), the response-TTL callback, and
cancel handling. `HandleRequest`, `HandleCancel`, and `Close` access the same
facts under `session.SyncRoot`.

**Impact:** A response completion on the dispatch worker, an asynchronous
handler thread, or the TTL timer can race a new request on a client channel
thread. `Dictionary` is not thread-safe: concurrent add/remove/clear can throw
or corrupt internal state. The completion bitmap read-modify-write can lose
bits, and `_completedCount++` is not atomic, so the 65,536 request cap can
misbehave. This affects Direct RRReliable; requests arrive on channel threads
while responses complete elsewhere.

**Specification:** The class remarks state that the maps are guarded by
`SyncRoot`; the implementation violates that invariant. Abstract RRReliable §10
requires exactly-once presentation and duplicate suppression; the cap is also
declared in the Direct specification.

---

### H-04 — RawReliable handlers that throw terminate the endpoint/transport

**Where:** `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`
(`_receiveAction`, around lines 164–184) and
`Raw/Reliable/RawReliableEndpoint.cs` (`NotifyConnectedCore`, around lines
194–208). Shared by Direct RawReliable client and server.

**Description:** `RawEndpoint._receiveAction` wraps `OnReceived` in a `try` but,
on exception, records an `ExceptionFail` and calls `FailHard`. So an application
handler that throws from `OnReceived` stops the endpoint. `NotifyConnectedCore`
similarly stops the endpoint when `OnConnected` throws. Raw Unreliable avoids
this through `HandlerGate`, and the RR cores catch their callbacks; Raw Reliable
does not.

**Impact:** A single handler exception tears down the session and, for a client,
the transport. Abstract RawReliable §11 says the transport **MUST NOT** stop a
session, an endpoint, or the transport solely because a handler threw. The
Direct RawReliable specification declares no deviation.

---

### H-05 — RawReliable client `Init` violates its contract

**Where:** `Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs`
(`Init`, around lines 101–127). Shared by `RawReliableDirectClientTransport`.

**Description:** The method does not check for a null handler. It calls
`SetupHandler(handler)` (the default returns the argument), then treats a null
result as “Transport is not set up” and calls `Fail`, which invalidates the
transport. It also calls `Fail` for every rejected call: a second `Init`, an
`Init` after `Start`, and an `Init` after a normal `Stop`. The result is `false`
in all cases, but the transport is invalidated.

**Impact:**

- `Init(null)` must throw `ArgumentNullException` without changing state; it
  instead returns `false` and invalidates.
- A later `Init` on a valid or running transport kills it.
- `Init` after a normal stop flips `IsValid` to `false`, violating the lifecycle
  rule that a normal stop leaves `IsValid == true`.

**Specification:** Abstract RawReliable §7.1 and §7.2.

---

### H-06 — RawReliable client invokes `onStopped` on a failed `Start`

**Where:** `Pontifex/Implementations/Raw/Reliable/RawReliableClientTransport.cs`
(`TryStart`, around lines 129–160) and
`Pontifex/Implementations/AnyTransport.cs` (`Start`/`Fail`).

**Description:** When `TryStart` is called with the transport not initialized,
it calls `Fail("TryStart", "Transport is not initialized")`. At that point
`AnyTransport.Start` has already set `_started = true`, so `AnyTransport.Fail`
invokes `_onStopped`. `BeginConnect` returning `false` follows the same path
(`_startFailed` suppresses only the handler callback, not the transport-level
`onStopped`).

**Impact:** Calling `Start` before `Init` returns `false` and invalidates the
transport as required, but also invokes the user’s `onStopped` callback.
Abstract RawReliable §7.2 says a failed initial `Start` **MUST NOT** invoke
`onStopped`. The RawUnreliable client has the same pattern on carrier-start and
endpoint-creation exception paths, though those paths are rarer.

---

## Medium severity

### M-01 — RawUnreliable Direct server does not enforce `MessageMaxByteSize` on inbound data

**Where:** `RawUnreliableDirectServerTransport.cs`
(`OnChannelCreated`, around lines 176–202), the base
`RawUnreliableServerTransport.OnCarrierInbound`, and
`RawUnreliableDirectServerEndpoint.cs`.

**Description:** The client endpoint checks inbound size in `OnChannelMessage`.
The server path never does. `OnChannelCreated` installs
`channel.ServerHandler = (clientEp, message) => OnCarrierInbound(clientEp, message)`;
neither that path nor `CreateEndpoint` nor `RawUnreliableServerEndpoint` checks
`message.GetDataSize()`. An oversized message invokes the handler factory, is
placed in the endpoint inbox, and is delivered to the handler. It is not logged.

**Impact:** Abstract RawUnreliable §8 requires the server not to invoke a
handler, invoke a factory, or create an endpoint for oversized data, and to
release and log it. The Direct RawUnreliable specification §6 and the
implementer checklist make the same claim for “both directions”. The
`BeforeHandlerFactoryGate` “not hit for oversized” rule is also violated.

---

### M-02 — RawUnreliable Direct peer callbacks can begin before `Send` returns

**Where:** `RawUnreliableDirectClientEndpoint.cs` (`DoSendRaw`, `CommitSend`)
and `RawUnreliableDirectServerEndpoint.cs` (`DoSendRaw`, `CommitSend`).

**Description:** `DoSendRaw` posts the buffer to a `SerializedCallbackQueue`
inside the endpoint’s send action. `Post` releases the worker semaphore
immediately; nothing holds the worker until the caller has returned from
`Send`. The commit worker can therefore call the peer’s channel handler (and
eventually `OnReceived`) while the caller is still unwinding through
`ScheduleToSend` and `Send`.

**Impact:** Abstract RawUnreliable §10 says the callback for a message accepted
by `Send` must not begin before that `Send` call returns. The Direct
specification §8 claims “a committed message reaches the channel after `Send`
returns”; the code does not guarantee that ordering. The race is small but real
on a multi-core machine.

---

### M-03 — RawUnreliable `Stop` can return before the startup flush delivers messages

**Where:** `Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableEndpoint.cs`
(`FlushInbox`, around lines 230–253, and `IRawUnreliableEndpoint.Stop`).

**Description:** `FlushInbox` checks only `_inboxState` and `_invalidated`.
`_invalidated` is set later by `BeginStop`, which is scheduled as a separate
action. A stop requested from `OnStarted`, or concurrently before the flush
action runs, takes the one-shot latch and returns; the flush action then raises
`OnStarted`, opens the inbox, and drains buffered messages, including the server
trigger.

**Impact:** `OnReceived` can begin after `Stop` returned, violating abstract
RawUnreliable §7.2 (“Once it returns, no new `OnReceived` invocation may begin
on any endpoint of that transport”) and §11. The server trigger is the most
visible case: a handler that calls `endpoint.Stop()` in `OnStarted` can still
receive the trigger afterwards.

---

### M-04 — RawReliable stops the session on malformed or unknown inbound frames

**Where:** `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`
(`ProcessInboundMessage`, around lines 540–548).

**Description:** If the message has no type byte, or the type byte is not a
recognized `MessageType`, the method releases the message and calls
`Stop(new TextFail(..., "Failed to parse incoming message"))`. An unexpected
handshake frame after the connection is established is treated the same way.

**Impact:** Abstract RawReliable §8 requires malformed inbound data to be
discarded and logged without stopping the transport. Direct RawReliable has no
filter that turns this into a drop. A single malformed in-process frame tears
down the session.

---

### M-05 — RawReliable Direct has no handshake deadline in either role

**Where:** `RawReliableDirectClientEndpoint.cs` (constructor passes
`Timeout.InfiniteTimeSpan`; `CreateHandshakeTimer` returns `null`) and
`RawReliableDirectServerTransport.cs` (base constructor passes
`Timeout.InfiniteTimeSpan`).

**Description:** The client never starts a handshake timer and the server never
has a handshake deadline. A channel that connects and never sends a handshake
request, or sends one and never receives a response, stays in the handshake
state forever. The server holds the channel entry and the reserved session /
handshake slots forever.

**Impact:** Abstract RawReliable §6.3 requires a positive, configurable
handshake deadline with a 10-second default. The Direct specification §6 says
“no handshake timer” and §7 declares no deviation. A non-compliant peer or a
server endpoint that cannot send its response can hang the client indefinitely.
A RawReliable server with a tiny outbound buffer can fail to send the handshake
response; its endpoint stops, but the client has no deadline to produce a
`TimeOut`.

---

### M-06 — RawReliable Direct rejects payloads at the top of its declared size range

**Where:** `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`
(`Send`, around lines 466–473) and
`Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`
(`ScheduleToSend`, around lines 455–467); Direct
`RawReliableDirectClientEndpoint.OnChannelMessage` and the server counterpart
also check the total before popping the type byte.

**Description:** `Send` prepends the `PayloadMessage` type byte and then
`ScheduleToSend` checks `GetDataSize() > MessageMaxByteSize`. The type byte (and
possible element-count growth) is transport framing, which the abstract says is
excluded from the limit. The receiver checks the total before removing the frame
type as well.

**Impact:** An application payload of exactly `MessageMaxByteSize` is rejected
as `MessageTooBig`; the effective payload maximum is a few bytes below the
declared value. This affects Direct RawReliable in both directions and violates
abstract RawReliable §8.

---

### M-07 — RawUnreliable factory-created handler is abandoned when endpoint creation fails

**Where:** `Pontifex/Implementations/Raw/Unreliable/RawUnreliableServerTransport.cs`
(`OnCarrierInbound`, around lines 382–411) and
`RawUnreliableDirectServerTransport.CreateEndpoint` (returns `null` when the
channel disappeared or the shared budget is unavailable).

**Description:** When the factory returns a non-null handler but
`CreateEndpoint` returns `null` or throws, the base sets `releaseTrigger = true`
and releases the trigger. It never calls `handler.OnStopped`, and no endpoint
exists to do it. Direct `CreateEndpoint` returns `null` during a concurrent
server stop or channel removal.

**Impact:** The application’s factory-created handler receives no terminal
callback and cannot release resources. Abstract RawUnreliable is silent for this
exact “handler created, endpoint declined” case, but RawReliable explicitly
notifies in the analogous path; the Direct RawUnreliable behavior is an
unannounced resource leak.

---

### M-08 — `Channel.Dispose` can abort teardown if a delivery system throws

**Where:** `Transports/Pontifex.Transport.Direct/Channel/Channel.cs`
(`Dispose`, around lines 181–219).

**Description:** `Dispose` calls `DeliverySystemLifetime.Release` for the
client and server systems without a `try`. `Release` calls `Clear` and then
`Dispose`; either can throw. A throw prevents the second system from being
released and prevents the `Disposed` event from being raised.

**Impact:** The Direct transports use channel disposal as their only
disconnection and teardown signal. A custom delivery system, or Netem `Clear`
after a prior `Dispose`, can leave the peer and transport running with a dead
channel. The same lack of fault isolation exists in `SetClientDeliverySystem`
and `SetServerDeliverySystem`.

---

### M-09 — Channel send/dispose race leaks an owned message

**Where:** `Transports/Pontifex.Transport.Direct/Channel/Channel.cs`
(`SendToClient`, `SendToServer`, `Dispose`).

**Description:** `SendToClient`/`SendToServer` check `IsDisposed`, then read
the delivery-system field and call `Deliver`. `Dispose` swaps each field to a
fresh `PerfectDeliverySystem` that is never subscribed to
`OnClientDeliveredMessage`/`OnServerDeliveredMessage`. A send that passes the
disposed check just before the swap reads the fresh system, whose `Deliver` has
no subscriber, so the message is neither delivered nor released, and the method
returns `Ok`.

**Impact:** This violates the complete ownership release required by the Direct
transport specifications. It is a narrow race but a real message leak. The
`ChannelManager` coverage lists a related planned test for delivery-system
replacement races; this disposal variant is distinct.

---

### M-10 — RawUnreliable Direct with `delivery = netem` can leak threads before start

**Where:** `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectBinding.cs`
(`Create`) and `RawUnreliableDirectClientTransport.SetDeliverySystem`.

**Description:** The binding builds two `NetemDeliverySystem`s during transport
construction and hands ownership to the transport. Each netem system starts a
dequeue thread immediately. `AnyTransport.Stop` before a successful `Start` is a
no-op and does not call `OnStopped`/`StopCarrier`, and the transport has no
`IDisposable`. If the transport is constructed and abandoned, or stopped before
start and never started, both threads and systems remain alive.

**Impact:** A normal lifecycle choice (`Stop` before `Start`) cannot release the
resources. RRUnreliable avoids this by using per-connect factories, so systems
are built only when a channel is actually created.

---

### M-11 — RRReliable `Reject` after a successful `Finish` returns `Error` instead of throwing

**Where:** `Pontifex/Implementations/RR/Reliable/RRReliableResponse.cs`
(`Reject`, around lines 222–267; `Finish`, around lines 182–219).

**Description:** `Finish` closes the handle with `ResponseState.Closed`.
`Reject` checks only `Rejected` and `Accepted`, then treats any closed handle as
`Error`. A `Reject` after a successful `Finish` therefore returns `Error`
instead of throwing `InvalidOperationException`.

Related behavior: `Finish` and `Reject` close the handle even when the carrier
send returns `Error` (lines 216 and 264). The contract describes the first
“successful” call and repeated `Reject` behavior; closing on a failed send
prevents a retry, and a later `Reject` can return `Ok` even though nothing was
sent.

**Impact:** Abstract RRReliable §6.6 explicitly says calling `Reject` after
`Response` or `Finish` succeeded throws. The implementation cannot distinguish a
handle closed by `Finish` from one closed by session teardown.

---

### M-12 — RRReliable client timer-factory exception leaves a bound handler with no TTL

**Where:** `Pontifex/Implementations/RR/Reliable/RRReliableClientTransport.cs`
(`Request`, around lines 264–282).

**Description:** After the request state is accepted and the frame has been
sent, `Request` calls `TimerFactory.Schedule` without a `try`. A throwing timer
factory (or one that returns null and triggers `TrySetTimer`’s null check)
escapes `Request` after acceptance. The handler remains bound and the request
has no request TTL.

**Impact:** The handler cannot be reused, and the request can remain live
indefinitely. The server-side equivalent, `RRReliableServerTransport.HandleRequest`,
does guard the schedule call and closes/releases on failure; the client does
not.

---

### M-13 — RawUnreliable Direct client `Connect` does not isolate manager/install exceptions

**Where:** `Transports/Pontifex.Transport.Direct/Raw/Unreliable/RawUnreliableDirectClientEndpoint.cs`
(`Connect`, around lines 166–195).

**Description:** `Connect` calls `ChannelManager.Instance.Connect` and
`Channel.SetClientDeliverySystem`/`SetServerDeliverySystem` with no `try`. A
server `OnChannelCreated` exception (see L-20) or a custom delivery-system
install exception escapes `DoSendRaw`. The endpoint action queue then treats it
as an unhandled exception and calls `FailHard`; the current message buffer may
not be released.

**Impact:** This violates the Direct RawUnreliable specification §7 claim that
a delivery-system exception is caught, logged, releases the message, and does
not stop the endpoint/transport.

---

### M-14 — `Stopped` event invocation is not per-subscriber fault-isolated

**Where:** `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs`
(`RaiseStopped`, around lines 281–296).

**Description:** `Stopped` is invoked as one multicast delegate inside a single
`try`/`catch`. If an earlier subscriber throws, later subscribers are skipped.
For RawUnreliable Direct, the derived endpoint’s `OnTerminalStopped` (disposes
the send queue, channel, and delivery systems) is subscribed before the
transport’s `OnAttachedEndpointStopped`; a throw there means the endpoint stops
but the owning client transport is never told. For server endpoints, the
transport’s route-removal subscription is skipped, leaving a dead route in
`_routes`.

**Impact:** Direct teardown relies on endpoint `Stopped` to stop the client
transport and to remove server routes. The base comment claims subscriber
failure isolation, but the isolation is only around the whole list, not each
subscriber. (The `Channel.Disposed` event does isolate each subscriber.)

---

## Low severity

### L-01 — RawUnreliable client does not enforce the declared 100-message outbound budget

**Where:** `RawUnreliableDirectClientTransport.cs` and the shared
`RawEndpoint.ScheduleToSend`.

**Description:** The client exposes `SendBudgetMessages = 100`, but `Send`
admission only enforces the 100 MB byte budget. `queueCapacity` is used only
for the per-endpoint commit queue, where a full queue drops the message after
`Send` already returned `Ok`. Empty messages bypass the byte budget entirely.

**Impact:** The Direct RawUnreliable specification’s queue/budget table and the
implementer checklist (“Enforce the 100-message and 104857600-byte budgets”)
are only half true for the client. The server endpoint does enforce both through
`SendBudget`.

---

### L-02 — Send-result precedence is wrong in several shared paths

**Where:** `RawEndpoint.ScheduleToSend`, `RawUnreliableServerEndpoint.Send`,
and `RRUnreliableClientTransport.Request`.

**Description:** The abstract transports require the result precedence:
transport state, message validity, message size, capacity, other errors.

- Raw endpoints check message size before endpoint state, so a stopped endpoint
  with an oversized message returns `MessageTooBig` instead of
  `Error`/`NotConnected`.
- `RawUnreliableServerEndpoint.Send` checks the shared budget before state, so
  a stopped endpoint with a full budget returns `BufferOverflow`.
- `RRUnreliableClientTransport.Request` checks null (`InvalidMessage`) before
  `IsStarted` (`NotConnected`).

**Impact:** The observable result is wrong when multiple failure conditions
apply. The tests do not combine these conditions.

---

### L-03 — RawUnreliable reliable-debug control has contract and race problems

**Where:** `RawUnreliableDirectServerTransport.TryMakeReliableForDebug`,
`RawUnreliableDirectClientTransport.TryMakeReliableForDebug`, and
`RawUnreliableTransportConformanceControl`.

**Description:** The server method returns `true` unconditionally, including
after `Start`, contrary to `IRawUnreliableDebugReliability` (“must be called
before the transport starts”). A server-only call reports success but cannot
make routes reliable; reliability is driven by the client’s delivery systems.
The client sets `_askedForReliableDelivery` after a separate
`HasStartBeenAttempted` check outside the transport lock, so it can race
`Start`. The reliable-debug pair still relies on the bounded commit queue; a
full queue drops an accepted message after `Ok`, so the control’s
“exactly once in FIFO” claim is not unconditional.

---

### L-04 — RawUnreliable conformance-control checkpoint details are wrong

**Where:** `Pontifex/Implementations/Raw/Unreliable/RawUnreliableEndpoint.cs`,
the Direct RawUnreliable endpoints, and the transport base.

**Description:**

- `BeforeHandlerStoppedGate` is not hit when `FailHard` is the path that
  delivers `OnStopped` (unrecoverable failure/injected failure).
- The Direct client `CommitSend` hits `BeforeSendCommitGate` for a message
  whose channel was disposed and which is then discarded; the server
  `CommitSend` checks disposed first.
- `BeforeHandlerFactoryGate` is hit for oversized inbound messages because the
  server never validates size (M-01).

**Impact:** The conformance-control tables describe tighter hit/not-hit
conditions than the implementation provides.

---

### L-05 — RawReliable handshake reject/response frames are not size-checked

**Where:** `RawReliableDirectServerTransport.SendRejection`,
`RawReliableHandshake.BuildResponse`, and the Direct client/server inbound
handshake checks.

**Description:** `SendRejection` builds and sends a reject without checking
`HandshakeMaxByteSize`; `BuildResponse` is not checked at the sending endpoint.
The client’s reject/response checks operate on the body after the type byte has
been popped, so they exclude the type byte. A long handshake name or diagnostic
text can produce an oversized handshake frame.

**Impact:** Abstract RawReliable §6.3 requires every handshake frame to stay
within `HandshakeMaxByteSize`. A large reject can exceed the client’s limit; the
client reports `TextFail` (“Handshake reject exceeds HandshakeMaxByteSize”)
instead of the mapped rejection.

---

### L-06 — RawReliable can expose a payload before `OnConnected`

**Where:** `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableEndpoint.cs`
and the Direct endpoints’ `OnChannelMessage`.

**Description:** Inbound payload delivery is gated on the base
`RawEndpoint` state (`Running`), which becomes true after the start completion
but before the handshake response completes and `OnConnected` is called. A peer
that sends a payload in that window can reach the handler.

**Impact:** Abstract RawReliable §6.5 says regular messages must not be exposed
before `OnConnected` completes. The compliant Direct client/server ordering
normally prevents this, but the transport does not enforce it for a
non-compliant or malicious in-process peer.

---

### L-07 — RawReliable binding advertises an unusable `handshake_max_byte_size` range

**Where:** `Transports/Pontifex.Transport.Direct/Raw/Reliable/RawReliableDirectBinding.cs`.

**Description:** The parameter is declared as `1 .. long.MaxValue`, but the
binding does `checked((int)handshakeMax)` (OverflowException above `int.Max`).
The transport constructors also reject a handshake maximum above the local
message maximum, and the binding does not expose `messageMaxSize`; the default
local maximum is 1 MB.

**Impact:** Values the registration declares as valid can fail with an
`OverflowException` or an argument-range exception during construction.

---

### L-08 — RawUnreliable binding leak when the second delivery build fails

**Where:** `RawUnreliableDirectBinding.Create`.

**Description:** The binding calls `UnreliableDeliveryBinding.Build` twice. If
the first build succeeds (possibly starting a netem thread) and the second
throws, the first system is not released.

**Impact:** A construction failure can leak a netem dequeue thread and its
resources.

---

### L-09 — RawUnreliable endpoint construction failure leaks handed-off delivery systems

**Where:** `RawUnreliableDirectClientTransport.CreateEndpoint`.

**Description:** `_handedOffClientDeliverySystem` and
`_handedOffServerDeliverySystem` are assigned before the endpoint is constructed.
If the endpoint constructor throws, `StopCarrier` believes the systems were
handed off and does not release them, and no endpoint exists to release them. The
same ownership hole exists for the fresh perfect systems created in
reliable-debug mode.

**Impact:** A rare constructor failure (thread creation, allocation, etc.)
leaks delivery systems and, for netem, a dequeue thread.

---

### L-10 — RRUnreliable Direct client returns `Ok` when its carrier is already stopped

**Where:** `Transports/Pontifex.Transport.Direct/RR/Unreliable/RRUnreliableDirectClient.cs`
(`SendFrame`).

**Description:** When `_carrier == null` (the state during `StopCarrier`),
`SendFrame` releases the frame and returns `Ok`. This is the no-peer policy,
but it is also applied when the carrier has been stopped.

**Impact:** A `Request` that races a transport stop can be accepted by the
core, added to the pending window after `ClearPendingRequests`, and then expire
silently. Returning `Error`/`NotConnected` would let the core unbind the
handler. The RRUnreliable client core also has no `_stopping` latch equivalent
to RRReliable, so a request that passes `IsStarted` just before the stop can
remain in the table until its TTL.

---

### L-11 — RRUnreliable pre-start pending bound ignores the configured queue capacity

**Where:** `Transports/Pontifex.Transport.Direct/RR/Unreliable/RRUnreliableDirectServer.cs`
(`OnInbound`, around line 183).

**Description:** The pending-list bound compares `_pending.Count` with the
static `RRDirectInfo.DispatchQueueCapacity` (100), not with `_queueCapacity`.
A server constructed with a smaller queue capacity can hold more pre-start
frames than the dispatch queue will accept and drops the surplus at
`OnStarted`; a larger capacity still caps the pre-start list at 100.

**Impact:** The constructor parameter and the actual pre-start bound disagree.
The Direct specification says 100, so the static value is spec-consistent but
the constructor parameter is misleading.

---

### L-12 — RRUnreliable specification deviations are misstated

**Where:** `Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md` §7.

**Description:**

- Deviation 1 says a request can wait “behind `OnStarted`”. It can also wait
  behind a running `OnRequested`, because the response TTL starts at frame
  receipt and the dispatch queue is also open after startup.
- Deviations 1 and 2 say the response handle is already closed at
  presentation/stop. `RRUnreliableResponse` is closed lazily on the next
  `Response` call; the observable result is `Error`, but the described state is
  not literal.
- Deviation 4 says netem teardown can take up to five seconds. The client
  channel has two netem systems, disposed sequentially, so the bound is closer
  to ten seconds.

**Impact:** The declared bounded deviations understate or misdescribe the
actual implementation behavior.

---

### L-13 — Direct RawUnreliable specification claims the implementation does not satisfy

**Where:** `Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md`.

**Description:**

- §7 “This implementation declares no deviation” is contradicted by H-02,
  M-02, M-03, and M-13.
- §8 says “A committed message reaches the channel after `Send` returns”; M-02
  shows this is not guaranteed.
- §8 says “`onStopped` is dispatched after endpoint teardown”. The
  endpoint transition can be queued while the transport invokes `onStopped`; the
  abstract only requires scheduling the handler terminal notification first.
- The message-limit text says oversized inbound data is released and logged,
  but the server inbound path has no check (M-01).

---

### L-14 — Direct RawReliable specification claims the implementation does not satisfy

**Where:** `Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md`.

**Description:** §7 “This implementation declares no deviation” is contradicted
by H-04, M-04, M-05, and M-06. §5 has no handshake-timeout parameter and §6
explicitly says no timer is created; the infinite deadline is a deviation from
abstract §6.3 and is not declared as one.

---

### L-15 — Direct RRReliable specification describes response-TTL completion incorrectly

**Where:** `Transports/Pontifex.Transport.Direct/RR/Reliable/Specification.md` §8.

**Description:** The completion-transport row says “the response TTL and
session close send nothing, and the client’s own stop or failure supplies
`Failed`.” The shared core’s `RRReliableResponse.SendFailed` sends a
`Finish(Failed)` frame when the server response TTL expires, and the test
`ResponseTtl_ProducesOneFailedCompletionAndDisposesTheRequestTimer` proves that
the client receives the server-generated terminal. Only session close sends
nothing.

**Impact:** The Direct specification contradicts both the implementation and
abstract RRReliable §6.5. The implementation is correct; the spec is wrong.

---

### L-16 — Direct RRUnreliable specification line-length violation

**Where:** `Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md`
lines 104–106.

**Description:** These non-table lines exceed 100 characters, contrary to the
specification form rule (“A line MUST NOT exceed 100 characters, except in a
table row”).

---

### L-17 — Netem configuration accepts negative values

**Where:** `Transports/Pontifex.Transport.Direct/Raw/Unreliable/UnreliableDeliveryBinding.cs`
(schema and parsing) and `VirtualDelivery/Netem/NetemConfig.cs`.

**Description:** The delivery schema allows `int.MinValue..int.MaxValue` for
`packetoverhead`, `celloverhead`, `queuelimit`, `slot.maxpackets`, and
`slot.maxbytes`. A negative queue limit makes `totalQueued >= limit` true for
every message, so every message is dropped. A negative packet overhead makes
`(ulong)len` in `RateLimiter.PacketTimeNs` enormous, producing nonsensical
pacing. A negative slot maximum is silently treated as unlimited.

`UnreliableDeliveryBinding.Validate` also returns early for `type = perfect`, so
a `lossmodel` value such as `"bogus"` is accepted even though it cannot be
parsed, contrary to the “reject an unparsable field” rule.

---

### L-18 — Netem duplicates can exceed `QueueLimit`

**Where:** `VirtualDelivery/Netem/NetemDeliverySystem.cs` (`Deliver`).

**Description:** `Deliver` checks `totalQueued >= _config.QueueLimit` once,
using the current queue size, then enqueues `count` copies. With duplication
enabled and one free slot, two copies are enqueued, so the configured maximum is
exceeded before a drop.

---

### L-19 — Netem release is not repeat-safe and `Dispose` is not thread-safe

**Where:** `VirtualDelivery/DeliverySystemLifetime.cs` and
`VirtualDelivery/Netem/NetemDeliverySystem.cs` (`Clear`, `Dispose`).

**Description:** `DeliverySystemLifetime.Release` is documented as safe for
repeated release. `NetemDeliverySystem.Clear` calls `_signal.Set()`; after
`Dispose` the `AutoResetEvent` is disposed, so a second release throws
`ObjectDisposedException` before `Dispose` is reached. `NetemDeliverySystem.Dispose`
checks a plain `bool _disposed` without synchronization, so concurrent
`Dispose` calls can both proceed and double-dispose the CTS and signal.

---

### L-20 — `ChannelManager.Connect` leaves a channel registered when the server callback throws

**Where:** `Transports/Pontifex.Transport.Direct/Channel/ChannelManager.cs`
(`Connect`, around lines 31–46).

**Description:** `Connect` adds the channel to the server entry and then calls
`InvokeOnChannelCreated(channel)`. If that callback throws, the exception
propagates; the channel remains in `_clients` and is not disposed. The
`ChannelManager` coverage records this as a planned test, but it is still a
production defect for any callback that can throw.

---

### L-21 — The same delivery system installed in both directions is released twice

**Where:** `Transports/Pontifex.Transport.Direct/Channel/Channel.cs`
(`Dispose`, `SetClientDeliverySystem`, `SetServerDeliverySystem`).

**Description:** `Channel.Dispose` independently exchanges and releases
`_clientDeliverySystem` and `_serverDeliverySystem`. If the same instance is
installed in both fields (for example, a custom factory that returns one shared
system), both fields reference it and it is cleared/disposed twice. Netem’s
second `Clear` throws `ObjectDisposedException`.

**Impact:** Custom delivery-system implementations can fail during teardown.
The built-in bindings create distinct instances, so the common path is not
affected.

---

### L-22 — Direct `id` is shared across protocols

**Where:** `ChannelManager` and all Direct bindings (`RawUnreliable`,
`RawReliable`, `RRUnreliable`, `RRReliable`).

**Description:** All Direct servers register only a `StringEndPoint(id)` in the
same process-wide `ChannelManager`. A client using one Direct scheme can connect
to a server using another scheme with the same id. A RawReliable handshake
request can then be delivered to a RawUnreliable server as an ordinary
application message, or a RawReliable client can wait forever for a handshake
response because RawReliable Direct has no handshake deadline.

**Impact:** The RR specifications say `id` is unique across all Direct
transports, but the Raw specifications do not state the cross-protocol scope or
the failure behavior. Misconfiguration produces misdelivery or a hang instead of
a clear rejection.

---

### L-23 — `help.md` uses a non-existent namespace

**Where:** `Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/help.md`
(Quick Start).

**Description:** The example uses
`using Pontifex.Transports.Direct.Delivery;`, but the types are in
`Pontifex.VirtualDelivery.Netem` and `Pontifex.VirtualDelivery`
(`PerfectDeliverySystem`). The example does not compile as written.

---

### L-24 — VirtualDelivery suite documentation does not match its contents

**Where:** `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/README.md` and
`Coverage.md`.

**Description:** The root subject and the “Out of scope” section omit
`NetemDeliverySystem` and `SlotScheduler`, yet the suite contains
`NetemDeliverySystemTests` and lists it in `Coverage.md`.

**Impact:** The suite documentation is internally inconsistent. This is a
documentation defect, not a coverage failure.

---

### L-25 — Direct tests poll wall-clock time instead of using a scheduling seam

**Where:** Many Direct test fixtures, for example
`Tests/Pontifex.Transport.Direct.Tests/Raw/RawUnreliableDirect/Tests/Integration/RawUnreliableDirectChannelOwnershipTests.cs`,
`RawUnreliableDirectInboundLimitTests.cs`, and the RR lifecycle/integration
fixtures.

**Description:** The tests use `WaitFor` loops with `Thread.Sleep(1)` and
`DateTime.UtcNow` deadlines, real threads, and real `SerializedCallbackQueue`
workers. `Tests/RULES.md` TEST-03 and TEST-05 require the default suite to be
deterministic and to complete asynchronous hooks explicitly rather than depend
on a scheduling race.

**Impact:** The tests can be flaky under load and do not use the available
serial-queue/clock seams consistently.

---

### L-26 — Netem test rationalizes drop-vs-delay with an invalid assumption

**Where:** `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/Tests/Netem/NetemDeliverySystemTests.cs`
(`SaturatedLossWithDuplication_DropsThePacket`).

**Description:** The comment says a queued packet would still be alive because
the base latency is one hour. The netem time base overflows on Unix (H-01), so
the latency path cannot be relied on to keep the packet queued. The test’s loss
assertion still passes, but its stated distinction between “dropped” and
“delayed” is not valid on the current runtime.

---

## Cross-cutting notes

- The Direct implementation specs are structurally complete, but the Raw
  Unreliable and Raw Reliable specs both say “declares no deviation” while the
  code violates abstract requirements (H-02, H-04, M-01 to M-06, M-13).
- The highest-impact items are not in the thin Direct binding files: they are in
  the shared endpoint/transport bases and in the netem timing code that the
  Direct carriers use. The Direct specs inherit those guarantees, so these are
  Direct-transport problems in practice.
- The RRReliable session-map race (H-03) is independent of the Direct carrier
  and can corrupt state with ordinary concurrent requests; it deserves
  priority even though the existing tests pass.
