# NoAckRawUnreliable Carrier-Independent Core Conformance Test Plan

## 1. Purpose and scope

This document defines the transport-agnostic NUnit test suite for the
[NoAckRawUnreliable Carrier-Independent Core Conformance Profile](../../../../../Pontifex/Abstractions/Transports/NoAck/Raw/Unreliable/Tests/TestSpecification.md).
It must run against every implementation-specific conformance adapter.

The suite certifies local API behavior only. It does not certify packet
delivery, callback delivery behavior, inbound validation, logging, routing, or
buffer ownership. Those requirements remain part of the full transport
specification but are intentionally outside the local profile.

Each numbered test below is a required local-certification test unless marked
**Adapter extension**. A missing required control is a **Baseline Only** result,
not a skipped or passing local-certification result.

## 2. Normative sources

The suite derives its assertions from these documents:

- [NoAckRawUnreliable Specification](../../../../../Pontifex/Abstractions/Transports/NoAck/Raw/Unreliable/Specification.md)
- [NoAckRawUnreliable Carrier-Independent Core Conformance Profile](../../../../../Pontifex/Abstractions/Transports/NoAck/Raw/Unreliable/Tests/TestSpecification.md)
- [Carrier-independent core conformance control](../../../../../Pontifex/Abstractions/Transports/NoAck/Raw/Unreliable/Tests/INoAckRawUnreliableConformanceControl.cs)

If this plan conflicts with the Carrier-Independent Core Conformance Profile, the profile
prevails. If the profile conflicts with the full transport specification, the
full transport specification remains the normative protocol contract and the
profile defines only what this suite can certify.

## 3. Common test architecture

### 3.1 Adapter registration

Every implementation under test supplies an implementation-specific adapter to
the common test fixture. Test discovery or a fixture source enumerates one
adapter per implementation. The common suite must not reference implementation
types, address formats, carrier configuration, serializers, or internal
transport types.

An adapter identifies its implementation with a stable display name used in
test-case names and reports. The name is descriptive only; it has no conformance
meaning.

### 3.2 Adapter contract

The following contract is the minimum required to implement this plan. It may
be represented by interfaces in the test project or by equivalent test-fixture
infrastructure, but its semantics must be preserved.

```csharp
public interface INoAckRawUnreliableConformanceTestAdapter
{
    string ImplementationName { get; }

    INoAckRawUnreliableConformanceScope CreateScope();
}

public interface INoAckRawUnreliableConformanceScope : IDisposable
{
    INoAckRawUnreliableClient CreateClient(bool instrumented);
    INoAckRawUnreliableServer CreateServer(bool instrumented);

    UnionDataList CreateSmallValidMessage(ITransport transport);
    UnionDataList CreateExactLimitMessage(ITransport transport);
    UnionDataList CreateOneByteOverLimitMessage(ITransport transport);

    IEndPoint CreateForeignServerDestination();

    IEnumerable<INoAckRawUnreliableAdditionalNonOkCase>
        CreateAdditionalNonOkCases();
}

public interface INoAckRawUnreliableAdditionalNonOkCase : IDisposable
{
    string Name { get; }
    SendResult ExpectedResult { get; }
    ITransport Transport { get; }

    // Transport is already successfully started when this is invoked.
    SendResult Invoke();
}
```

The adapter and scope have these requirements:

- `CreateClient` and `CreateServer` return fresh, valid, unstarted transports.
- `instrumented: true` returns an instance exposing the required local API
  conformance control. `instrumented: false` may omit that control.
- Each created transport uses unique, isolated ordinary physical-carrier
  configuration. The adapter must not replace, relay, intercept, or inject
  packet traffic.
- The scope owns implementation-specific cleanup not available through
  `ITransport`. Its `Dispose` method must release external resources after the
  test has released all checkpoint gates and completed public lifecycle calls.
- Every payload factory method returns a fresh, valid `UnionDataList`. The
  caller owns it until it passes the message to `TrySend`; after that call, the
  caller must not read, mutate, retain, or release it.
- `CreateExactLimitMessage` creates a message whose serialized
  `UnionDataList` size is exactly the supplied transport's
  `MessageMaxByteSize`, excluding carrier framing.
- `CreateOneByteOverLimitMessage` creates a valid message whose serialized
  `UnionDataList` size is exactly one byte greater than that limit.
- `CreateForeignServerDestination` returns an endpoint that was not issued by
  the test server and does not compare equal to any endpoint the test server
  could issue.
- `CreateAdditionalNonOkCases` returns a fresh case for every naturally
  reachable synchronous non-`Ok` condition not already covered by this plan.
  It returns an empty sequence when there are no such conditions.

An additional non-`Ok` case must use a real public `TrySend` call. It must not
alter a transport result, call an internal sending method, inject an exception,
or use a replacement carrier. Its `Transport` must be valid, running, and
owned by that case when `Invoke` is called. `ExpectedResult` must be non-`Ok`.

The adapter must not expose a data-plane test control, logger recorder, or
memory instrumentation to this suite.

### 3.3 Required control discovery

For every test that requests an instrumented transport, retrieve controls only
through `ITransport.GetControls`.

1. Create an empty `List<IControl>`.
2. Call `GetControls`, filtering for
   `INoAckRawUnreliableConformanceControl`.
3. Require exactly one matching control.
4. Treat zero or multiple matching controls as an adapter-contract failure.

The common suite must not cast the transport to an implementation-specific
control provider. It must not require controls on ordinary instances.

### 3.4 Common fixtures

Use these fixtures in every test.

| Fixture | Required behavior |
| --- | --- |
| **Stop recorder** | A thread-safe callback passed to `Start`. It records every `StopReason`, increments a count, and completes a `TaskCompletionSource` on its first invocation. It never throws. The task source uses asynchronous continuations. |
| **Checkpoint lease** | A helper that obtains a gate, arms it with `Arm(1)`, awaits `Reached`, and always calls `Reset` during cleanup. It must reset the gate even when an assertion fails. |
| **Concurrent reader** | A task that repeatedly reads `IsValid`, `IsStarted`, and `MessageMaxByteSize`, recording thrown exceptions. It stops only when the test signals completion. |
| **Fresh payload** | A payload returned by one adapter factory invocation. It is never reused for another `TrySend` call. |
| **Foreign destination** | An endpoint from `CreateForeignServerDestination`. It is used only for server invalid-address tests. |

Tests must not attach `OnReceived` handlers. No test in this plan depends on
packet delivery or application receive callbacks.

### 3.5 Asynchrony, watchdogs, and cleanup

Tests use `CheckPoint.Arm`, `CheckPoint.Reset`, task completion sources, and
task joins for synchronization. A configurable watchdog timeout may fail a test
that deadlocks or never completes an expected public lifecycle operation. A
watchdog must not establish carrier delivery, nondelivery, throughput, or a
scheduler-specific ordering.

Each test must perform cleanup in `finally`:

1. Reset every armed checkpoint gate.
2. Await or join every task started by the test.
3. Call `Stop` on each still-valid started transport.
4. Dispose additional non-`Ok` cases.
5. Dispose the adapter scope.

An invalid transport must not be restarted during cleanup. An already stopped
valid transport may receive another `Stop` call, which is permitted by the
profile.

## 4. Test cases

### Construction and start

1. **Client initial state is valid and unstarted.**
   **Profile:** 5.1. **Setup and steps:** Create an ordinary client. Read
   `IsValid` and `IsStarted` before any lifecycle call.
   **Assertions:** `IsValid` is `true`; `IsStarted` is `false`.
   **Cleanup:** No start is required; dispose the scope.

2. **Server initial state is valid and unstarted.**
   **Profile:** 5.1. **Setup and steps:** Repeat test 1 for an ordinary server.
   **Assertions:** `IsValid` is `true`; `IsStarted` is `false`.
   **Cleanup:** No start is required; dispose the scope.

3. **Concurrent client starts have exactly one winner.**
   **Profile:** 5.1. **Setup and steps:** Create an instrumented client. Release
   at least eight caller tasks simultaneously; each calls `Start` with its own
   stop recorder. Wait for all calls to return.
   **Assertions:** Exactly one result is `true`; every other result is `false`;
   the client is valid and started; only the winning recorder may later receive
   a stop callback.
   **Cleanup:** Stop the client and await exactly one callback on the winning
   recorder.

4. **Concurrent server starts have exactly one winner.**
   **Profile:** 5.1. **Setup and steps:** Repeat test 3 for an instrumented
   server.
   **Assertions:** The same assertions as test 3 apply.
   **Cleanup:** Stop the server and await its winning recorder.

5. **Later start after a successful client start is rejected.**
   **Profile:** 5.1. **Setup and steps:** Start an instrumented client
   successfully, then call `Start` again with a distinct recorder.
   **Assertions:** The second call returns `false`; the client remains valid and
   started; the second recorder receives no callback.
   **Cleanup:** Stop the client and await the first recorder once.

6. **Later start after a successful server start is rejected.**
   **Profile:** 5.1. **Setup and steps:** Repeat test 5 for an instrumented
   server.
   **Assertions:** The same assertions as test 5 apply.
   **Cleanup:** Stop the server and await the first recorder once.

### Failed start

7. **Forced client start failure is terminal and silent.**
   **Profile:** 5.2. **Setup and steps:** Create an instrumented client, obtain
   its required control, call `FailNextStart`, then call `Start` with a stop
   recorder.
   **Assertions:** `Start` returns `false`; `IsValid` and `IsStarted` are both
   `false`; the recorder count is zero; a later `Start` returns `false`; the
   instance never becomes started.
   **Cleanup:** Do not restart or stop the invalid client; dispose the scope.

8. **Forced server start failure is terminal and silent.**
   **Profile:** 5.2. **Setup and steps:** Repeat test 7 for an instrumented
   server.
   **Assertions:** The same assertions as test 7 apply.
   **Cleanup:** Dispose the scope.

9. **Start-failure arming is one-shot.**
   **Profile:** 4.2, 5.2. **Setup and steps:** Create an instrumented client,
   arm `FailNextStart`, and call `Start`.
   **Assertions:** The call consumes the fault and follows test 7. The test must
   not attempt a second start on the invalid instance. This test exists to
   verify the adapter/control contract, not to require restart behavior.
   **Cleanup:** Dispose the scope.

### Stop and stopped lifecycle

10. **Client stop before start is a no-op that preserves startability.**
    **Profile:** 5.3. **Setup and steps:** Create an instrumented client with no
    start callback registered. Call `Stop`, then call `Start` with a recorder.
    **Assertions:** The pre-start stop returns `true`; no callback occurs before
    start; the later start returns `true`; the client is valid and started.
    **Cleanup:** Stop the client and await the recorder exactly once.

11. **Server stop before start is a no-op that preserves startability.**
    **Profile:** 5.3. **Setup and steps:** Repeat test 10 for an instrumented
    server.
    **Assertions:** The same assertions as test 10 apply.
    **Cleanup:** Stop the server and await the recorder exactly once.

12. **Client stop invokes the winning callback exactly once and is terminal.**
    **Profile:** 5.3. **Setup and steps:** Start an instrumented client with a
    recorder. Call `Stop`, await the recorder, call `Stop` again, then call
    `Start` with a second recorder.
    **Assertions:** Both stop calls return `true`; the first recorder count is
    exactly one; the second start returns `false`; the second recorder count is
    zero; `IsValid` remains `true`; `IsStarted` is `false`.
    **Cleanup:** Dispose the scope.

13. **Server stop invokes the winning callback exactly once and is terminal.**
    **Profile:** 5.3. **Setup and steps:** Repeat test 12 for an instrumented
    server.
    **Assertions:** The same assertions as test 12 apply.
    **Cleanup:** Dispose the scope.

14. **Repeated stop cannot duplicate a paused client stopped callback.**
    **Profile:** 4.2, 5.3. **Setup and steps:** Start an instrumented client
    with a recorder. Arm `BeforeStoppedCallback`; call `Stop` on a task; await
    the gate's `Reached` result. Issue at least four additional `Stop` calls on
    separate tasks. Reset the gate, then await all stop tasks and the recorder.
    **Assertions:** Every stop call returns `true`; the recorder count is one;
    `IsValid` is `true`; `IsStarted` is `false`.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

15. **Repeated stop cannot duplicate a paused server stopped callback.**
    **Profile:** 4.2, 5.3. **Setup and steps:** Repeat test 14 for an
    instrumented server.
    **Assertions:** The same assertions as test 14 apply.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

### Forced unrecoverable failure

16. **Forced client failure stops, invalidates, and notifies once.**
    **Profile:** 5.4. **Setup and steps:** Start an instrumented client with a
    recorder. Call `InjectUnrecoverableFailure` and await the recorder.
    **Assertions:** `IsStarted` is `false`; `IsValid` is `false`; recorder count
    is one; every later `Stop` call returns `false`; every later `Start` call
    returns `false`.
    **Cleanup:** Do not restart or stop the invalid client; dispose the scope.

17. **Forced server failure stops, invalidates, and notifies once.**
    **Profile:** 5.4. **Setup and steps:** Repeat test 16 for an instrumented
    server.
    **Assertions:** The same assertions as test 16 apply.
    **Cleanup:** Dispose the scope.

18. **Repeated stop cannot duplicate a paused fatal-failure callback.**
    **Profile:** 4.2, 5.4. **Setup and steps:** Start an instrumented client
    with a recorder. Arm `BeforeStoppedCallback`; invoke
    `InjectUnrecoverableFailure` on a task; await `Reached`; issue repeated
    `Stop` calls on other tasks; reset the gate; await all tasks and the
    recorder.
    **Assertions:** The recorder count is one; the client is invalid and not
    started; every repeated stop returns `false` after the failure transition.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

### Status properties

19. **Client status reads remain safe while stop is in progress.**
    **Profile:** 5.5. **Setup and steps:** Start an instrumented client. Arm
    `BeforeStopStateTransition`; start a stop task and await `Reached`; start
    multiple concurrent readers; reset the gate; await stop and reader tasks.
    **Assertions:** No reader throws or deadlocks; final state is valid and not
    started.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

20. **Server status reads remain safe while stop is in progress.**
    **Profile:** 5.5. **Setup and steps:** Repeat test 19 for an instrumented
    server.
    **Assertions:** The same assertions as test 19 apply.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

21. **Client status reads remain safe while fatal failure is initiated.**
    **Profile:** 5.5. **Setup and steps:** Start an instrumented client, run
    concurrent readers, then invoke `InjectUnrecoverableFailure` concurrently.
    Stop readers only after the failure call and callback complete.
    **Assertions:** No reader throws or deadlocks; final state is invalid and
    not started.
    **Cleanup:** Dispose the scope.

22. **Server status reads remain safe while fatal failure is initiated.**
    **Profile:** 5.5. **Setup and steps:** Repeat test 21 for an instrumented
    server.
    **Assertions:** The same assertions as test 21 apply.
    **Cleanup:** Dispose the scope.

23. **Message maximum is immutable for client and server lifetime.**
    **Profile:** 5.5. **Setup and steps:** In separate client and server runs,
    capture `MessageMaxByteSize` before start; read it repeatedly while running,
    stopping, and, in a separate instance, after forced failure.
    **Assertions:** Every observed value equals the initial value; no read
    throws.
    **Cleanup:** Stop valid instances and dispose every scope.

### Local synchronous send validation

24. **Client exact-limit and oversized messages are classified correctly.**
    **Profile:** 5.6. **Setup and steps:** Start an instrumented client. Send a
    fresh exact-limit message, then a fresh one-byte-over-limit message.
    **Assertions:** The exact-limit result is not `MessageTooBig`; the oversized
    result is `MessageTooBig`; after each call the client remains valid and
    started.
    **Cleanup:** Stop the client and await its recorder.

25. **Client send before start and after stop returns NotConnected.**
    **Profile:** 5.7. **Setup and steps:** Use three fresh small valid messages.
    Send before start. Start the client, then stop and await its recorder. Send
    once after stop. A separate test covers the while-stopping interval.
    **Assertions:** Both observed results are `NotConnected`.
    **Cleanup:** Dispose the scope.

26. **Server send before start and after stop returns NotConnected.**
    **Profile:** 5.7. **Setup and steps:** Use a fresh foreign destination and
    fresh small valid messages. Send before start. Start and stop the server,
    then send again after stop.
    **Assertions:** Both observed results are `NotConnected`, regardless of the
    foreign destination.
    **Cleanup:** Dispose the scope.

27. **Client null message is rejected non-fatally.**
    **Profile:** 5.7. **Setup and steps:** Start an instrumented client and call
    `TrySend(null!)`.
    **Assertions:** The result is `InvalidMessage`; the client remains valid
    and started; its stop recorder has no callback.
    **Cleanup:** Stop the client and await its recorder once.

28. **Server null message is rejected non-fatally.**
    **Profile:** 5.7. **Setup and steps:** Start an instrumented server and call
    `TrySend(foreignDestination, null!)`.
    **Assertions:** The result is `InvalidMessage`; the server remains valid and
    started; its stop recorder has no callback.
    **Cleanup:** Stop the server and await its recorder once.

29. **Server foreign destination is rejected non-fatally.**
    **Profile:** 5.7. **Setup and steps:** Start an instrumented server. Create
    a fresh small valid message and a foreign destination, then call `TrySend`.
    **Assertions:** The result is `InvalidAddress`; the server remains valid and
    started; its stop recorder has no callback.
    **Cleanup:** Stop the server and await its recorder once.

30. **Every adapter-declared additional non-Ok result is non-fatal.**
    **Profile:** 5.7. **Adapter extension.** **Setup and steps:** Parameterize
    over `CreateAdditionalNonOkCases`. For each fresh case, capture the
    transport's initial `IsValid` and `IsStarted` values, invoke the case once,
    then read the values again.
    **Assertions:** The returned result equals `ExpectedResult` and is not
    `Ok`; the transport remains valid and started. The case must not report a
    condition already covered by tests 24 through 29.
    **Cleanup:** Call `case.Transport.Stop()` when it remains valid and started,
    then dispose the case and the scope.

### Send-versus-stop ordering

31. **Client send loses when stop wins the state race.**
    **Profile:** 5.8. **Setup and steps:** Start an instrumented client with a
    recorder. Arm `BeforeTrySendStateDecision`; invoke `TrySend` with a fresh
    small valid message on a task; await `Reached`; call `Stop` and await its
    recorder; reset the send gate; await the send task.
    **Assertions:** The send result is `NotConnected`; final state is valid and
    not started.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

32. **Server send loses when stop wins the state race.**
    **Profile:** 5.8. **Setup and steps:** Start an instrumented server with a
    recorder. Arm `BeforeTrySendStateDecision`; invoke `TrySend` with a fresh
    small valid message and foreign destination on a task; await `Reached`; call
    `Stop`; reset the send gate; await the send task.
    **Assertions:** The send result is `NotConnected`; final state is valid and
    not started. The test does not assert `InvalidAddress`, because stopping won
    the defined state-ordering race.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

33. **Client send does not lose solely because a later stop begins.**
    **Profile:** 5.8. **Setup and steps:** Start an instrumented client with a
    recorder. Arm `BeforeStopStateTransition`; call `Stop` on a task; await
    `Reached`; synchronously call `TrySend` with a fresh small valid message;
    reset the stop gate; await stop and recorder completion.
    **Assertions:** The send result is not `NotConnected`. The result may be
    `Ok`, `BufferOverflow`, `Error`, or another independently applicable
    synchronous result. Final state is valid and not started.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

34. **Client send while stopping returns NotConnected.**
    **Profile:** 5.7. **Setup and steps:** Start an instrumented client with a
    recorder. Arm `BeforeStoppedCallback`; call `Stop` on a task; await
    `Reached`; synchronously call `TrySend` with a fresh small valid message;
    reset the callback gate; await stop and recorder completion.
    **Assertions:** The send result is `NotConnected`; final state is valid and
    not started.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

35. **Server send while stopping returns NotConnected.**
    **Profile:** 5.7. **Setup and steps:** Repeat test 34 for an instrumented
    server using a fresh small valid message and foreign destination.
    **Assertions:** The send result is `NotConnected`; final state is valid and
    not started.
    **Cleanup:** Reset the gate in `finally`, then dispose the scope.

### Local operation control contract

36. **Checkpoint lookup is stable and validates its argument.**
    **Profile:** 4.2. **Setup and steps:** Create an instrumented client and
    obtain its local operation control. Obtain each defined checkpoint twice.
    Call `GetGate` once with an undefined enum value.
    **Assertions:** Repeated lookup of one checkpoint returns the same gate
    instance; different checkpoints return different gate instances; lookup of
    the undefined value throws `ArgumentOutOfRangeException`.
    **Cleanup:** Reset every obtained gate and dispose the scope.

37. **FailNextStart validates its one-shot pre-start state.**
    **Profile:** 4.2. **Setup and steps:** On a fresh instrumented client, call
    `FailNextStart` twice, then call `Start`. In a separate fresh client scope,
    start successfully and then call `FailNextStart`.
    **Assertions:** The second pre-start arm throws `InvalidOperationException`;
    the first arm remains effective and the following start fails according to
    test 7; arming after successful start throws `InvalidOperationException`.
    **Cleanup:** Stop the successfully started second client. Do not restart the
    invalid first client. Dispose both scopes.

38. **InjectUnrecoverableFailure validates its running one-shot state.**
    **Profile:** 4.2. **Setup and steps:** On a fresh unstarted instrumented
    client, call `InjectUnrecoverableFailure`. In a separate client scope, start
    successfully, inject failure, await its recorder, then inject failure again.
    In a third scope, start a client, arm `BeforeStoppedCallback`, begin `Stop`,
    await `Reached`, and call `InjectUnrecoverableFailure` before releasing the
    gate.
    **Assertions:** Injection before start, after a prior injected failure, and
    after stopping has begun each throw `InvalidOperationException`. The first
    valid injection follows test 16 and is not affected by the rejected second
    call.
    **Cleanup:** Reset the third scope's gate, await stop completion, stop any
    still-valid running client, and dispose all scopes.

## 5. Non-certifying smoke observations

An implementation may add physical-carrier smoke tests outside the local
certification fixture. They may start an adapter-created client/server pair and
observe actual delivery, callback invocation, endpoint routing, or physical
carrier failure.

Such tests must obey all of these rules:

- They are reported as **Smoke Observation**, never as Carrier-Independent Core Conformant.
- A timeout may identify a hung test but must not prove packet loss or lack of
  delivery.
- A received packet may reveal a defect, but absence of receipt proves nothing.
- Their failure must not be reported as a failure of a local-profile requirement
  unless the failure independently violates a local assertion.

## 6. Requirements deliberately not implemented by this suite

The following requirements have no local-certification test until future
instrumentation profiles exist:

| Full-specification area | Required future capability |
| --- | --- |
| Delivery count, ordering, boundaries, and content | Deterministic data-plane control. |
| Source filtering and server reply routing | Controlled inbound source identities. |
| Receive callback ordering, serialization, and exception recovery | Deterministic inbound delivery and callback barriers. |
| Invalid inbound-frame discard | Raw frame injection. |
| Logging invalid inbound frames | Recording logger. |
| Send and receive ownership | Recording memory instrumentation. |
| Queued-work discard on stop or failure | Data-plane and memory instrumentation. |

## 7. Traceability summary

| Local profile section | Tests |
| --- | --- |
| 4.1 and 4.2 adapter/control contract | Fixture setup, discovery, and tests 36-38 |
| 5.1 initial state and start | 1-6 |
| 5.2 failed start | 7-9 |
| 5.3 stop semantics | 10-15 |
| 5.4 forced unrecoverable failure | 16-18 |
| 5.5 status properties | 19-23 |
| 5.6 local size validation | 24 |
| 5.7 synchronous send rejection | 25-30, 34-35 |
| 5.8 send-versus-stop ordering | 31-33 |
| Section 7 smoke observations | Separate non-certifying fixture only |
