# RawUnreliable Direct Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | Raw unreliable Direct group |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/Raw/RawUnreliableDirect/` |
| Inheritance parent | none |
| Dependency | The carrier classes are internal; the suite relies on the Direct assembly's `InternalsVisibleTo` entry and on the shared loose `TestSupport/TestBuildContext.cs` and `TestSupport/BlockingDeliverySystem.cs`. |
| Contract | [RawUnreliable](../../../../Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md), [Direct RawUnreliable](../../../../Transports/Pontifex.Transport.Direct/Raw/Unreliable/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `InfoConstants_PinThePlanStartingCaps` | §6 limits | The info constants declare 100 queued messages and 104,857,600 bytes | The absolute values are calibration anchors, not derived values. |
| `DirectClientAndServer_DeriveTheInfoBudgetCaps` | §6 limits | Both transports expose the byte and message budgets derived from the info constants | The test reads the internal exposed hooks; it does not start a transport. |
| `ClientStop_ReleasesConfiguredSystemsThatWereNeverInstalled` | §6 delivery-system ownership | A configured pair that the endpoint never installed on a channel is released exactly once when the client stops | The stop notification is signalled synchronously; the bounded wait is only a guard. |
| `ClientSend_ToAServerThatRejectsTheChannel_ReleasesTheConfiguredSystems` | §6 delivery-system ownership | A channel rejected during connect releases the systems installed on it | The server registry is test-owned and unregisters in `finally`. |
| `SetDeliverySystem_AfterStart_IsRejected` | §6 configuration timing | Delivery configuration is accepted only before start | The client is stopped in `finally` so the rejection does not leak a running transport. |
| `ServerStop_ActiveHandler_ReceivesTheSuppliedReasonInstance` | §6 server teardown | An active handler observes the exact `StopReason` instance supplied to the server | The later channel-disposal notification is a no-op; the reason is compared by identity. |
| `OversizedInbound_ForNewRoute_DoesNotInvokeFactoryAndKeepsTransportAlive` | §6 inbound limit | An oversized first message is released before routing, never invokes the factory, and leaves the transport alive | The server is driven through a raw `Channel` so the oversized message reaches the transport directly. |
| `OversizedInbound_ForExistingRoute_DoesNotReachHandlerAndKeepsSessionAlive` | §6 inbound limit | An oversized message on an existing route is released without reaching the handler or invalidating the endpoint | The factory-call count proves the existing route is reused. |
| `DeclinedEndpoint_NotifiesFactoryCreatedHandlerOnceOutsideAdmissionLock` | §6 role and §7 failure | A factory-created handler whose endpoint is declined receives one `TextFail` outside the admission lock | The handler stops the server from inside `OnStopped`, proving reentrancy is supported. |
| `ThrowingEndpointFactory_NotifiesFactoryCreatedHandlerOnceOutsideAdmissionLock` | §6 role and §7 failure | A throwing endpoint factory notifies the handler once with `ExceptionFail` and keeps the transport usable | A later message creates a fresh route, proving the failure is isolated. |
| `ClientCommit_DisposedChannel_DiscardsWithoutHittingCommitGate` | §6 commit queue | A commit aimed at a disposed channel releases the message and bypasses the commit gate | The commit gate is armed with two required hits, so a single hit proves the bypass. |
| `ClientStop_RemovesTheDisposedChannelFromTheServerRegistry` | §6 teardown | Stopping the client disposes its channel and removes the registry entry | Integration group; uses a bounded wall-clock wait because the registry removal follows the asynchronous stop path. |
| `ServerStop_RemovesEveryRegisteredChannel` | §6 teardown | Stopping the server disposes every registered channel and empties the registry | Integration group; companion to the client-direction test. |
| `Stop_WhileDrainBlockedAtReceiveGate_DeliversNothingAfterStop` | §6 and §7 stop/drain | A stop latched while the startup drain is blocked releases every message that has not begun delivery | Integration group; the start gate and receive gate force the interleaving without a scheduling race. |
| `OversizedInboundMessage_IsDroppedWithoutStoppingTheClient` | §6 inbound limit | An oversized client-bound message is released without reaching the handler and without stopping the client | Integration group; the test waits for the channel to be acquired, then sends directly through it. |
| `TransportWithoutStart_ConstructsNoDeliverySystem` | §5.1 and §6 ownership | A pre-start stop constructs no delivery system | The factory counter distinguishes construction from installation. |
| `NetemFactory_StopBeforeStart_ConstructsNoSystem` | §5.1 | A configured netem factory stays inert until the endpoint materializes it | Companion to the perfect-factory test. |
| `Binding_NetemClient_StopBeforeStart_ConstructsNoSystem` | §5.1 | The binding builds one factory per direction but constructs no system before start | The binding is created through the hand-written build context. |
| `Binding_FactoryBuilderThrows_DoesNotConstructAnySystem` | §5.1 | A throwing first factory builder aborts binding creation without constructing a system | The second builder is never called. |
| `Binding_SecondFactoryBuilderThrows_DoesNotConstructAnySystem` | §5.1 | A throwing second factory builder aborts binding creation without constructing a system | Both builders are called once; the first factory is not materialized. |
| `SetDeliverySystem_StopBeforeStart_ReleasesPairAndLaterStartUsesFreshPerfect` | §6 ownership | A pre-start explicit pair is released and replaced by fresh perfect systems for the later start | The released systems are not reused; their clear counts stay at one. |
| `SetDeliverySystem_Twice_ReleasesTheFirstPairOnce` | §6 ownership | Replacing an explicit pair releases only the replaced pair | The second pair stays unowned and un-cleared until stop. |
| `SetDeliverySystem_SameInstanceBothDirections_ReleasesOnceOnPreStartStop` | §6 ownership | A shared instance installed in both directions is released once | Deduplication is by reference. |
| `TryMakeReliable_ReleasesExplicitPairAndUsesPerfectSystems` | §6 reliable debug | Entering client debug mode releases the explicit pair and installs perfect systems | The debug mode is client-only and selected before start. |
| `SetDeliverySystem_AfterTryMakeReliable_Throws` | §6 configuration timing | Explicit configuration is refused after reliable debug mode is selected | The guard is the same ordering rule as after start. |
| `TryMakeReliable_AfterStart_ReturnsFalse` | §6 reliable debug | Debug mode cannot be selected after the client starts | The client stays usable; the test stops it in `finally`. |
| `TryMakeReliable_BeforeStartLock_AppliesToEveryEndpoint` | §6 reliable debug | Debug mode selected while `Start` is paused applies to the endpoint the start creates | Integration group; the test-owned start gate makes the ordering deterministic. |
| `EndpointFactoryThrow_ReleasesExplicitPairExactlyOnce` | §6 start failure | A throwing endpoint factory releases both materialized systems once and invalidates the client | Proves the retry state is reset rather than reused. |
| `StartBeforeInit_ReleasesExplicitPairExactlyOnce` | §6 start failure | A start before init releases the explicit pair once | The failed start does not leave an owning transport. |
| `ManagerCallbackThrow_EndpointSurvivesAndRetriesWithFreshSystems` | §7 failure isolation | A manager callback exception leaves the endpoint alive and a later server serves the next message | Integration group; the registry entry is test-owned and removed in `finally`. |
| `InstallThrow_ReleasesOnlyTheUninstalledSystemAndRetries` | §6 ownership | An install failure releases only the direction the channel never took and retries with fresh systems | The channel owns the direction it accepted. |
| `NormalStartAndStop_ReleasesEachDirectionExactlyOnce` | §6 ownership | A successful start and stop release each direction once | Integration group; the channel acquisition is awaited before stopping. |
| `Server_ReportsReliableDebugAsUnsupported` | §7.1 residual limitation | The server reports reliable debug mode as unsupported | The Direct server owns no delivery systems, so it cannot make replies reliable. |
| `UnboundedCallbackQueue_AcceptsBeyondAnyFixedCapacity` | §8 callback scheduler | The debug commit queue accepts 10,000 messages without a fixed cap | Exercises the production serial callback queue used by the carrier; no endpoint is started. |
| `BoundedCallbackQueue_RejectsAtItsCapacity` | §8 callback scheduler and §7.1 | The bounded reply queue rejects once its fixed capacity is reached | Confirms the server reply queue remains bounded even when client debug mode is active. |
| `ClientCommitWindow_RejectsThe101stMessageWithBufferOverflow` | §6 commit window | The 101st admitted message is rejected with `BufferOverflow` and released | Integration group; a blocking delivery system holds the worker so the window is filled deterministically. |
| `ClientCommitWindow_CountsEmptyMessages` | §6 commit window | Empty messages consume the same 100-message window | Integration group; mirrors the non-empty case. |
| `ServerCommitWindow_UsesItsOwnCounterAndRejectsThe101stMessage` | §6 commit window | A server session endpoint has its own 100-message window | Integration group; the server endpoint is captured from the handler. |
| `PostThenThrow_ReleasesTheReservationOnceAndStopsTheEndpoint` | §6 commit queue | A throw after posting releases the reservation once and stops the endpoint | Integration group; the test seam forces the post-then-throw path. |
| `ClientSendReturnBarrier_DefersTheServerCallbackUntilSendReturns` | §6 callback barrier | A client send does not begin the server callback before `Send` returns | Integration group; the sender is parked at the return barrier, so the ordering is exact. |
| `ServerSendReturnBarrier_DefersTheClientCallbackUntilSendReturns` | §6 callback barrier | A server reply does not begin the client callback before `Send` returns | Integration group; companion to the client-direction barrier. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Malformed or hostile carrier input beyond the oversized-message limit | Root — the in-process channel passes typed `UnionDataList` values and offers no byte-level framing seam; a hostile-carrier control would be required. |
| Statistical netem loss, duplication, reorder, and rate behaviour | VirtualDelivery suite. |
| Load, allocation, and sustained-concurrency probes | Root — non-default facets; they need a separate declared load group. |
| Contract-level lifecycle, callback, and route conformance | `Tests/Pontifex.Transports.Tests/Raw/Unreliable` (shared conformance suite). |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real-time timing and uncontrolled background work | The default fixtures are deterministic; integration fixtures use bounded waits or explicit gates and are excluded from the default run. |
| Server reliable-debug mode | The Direct server owns no delivery systems; the specification declares the capability client-only. |
| Cross-process, socket, and wire-format behaviour | The Direct carrier is in-process only and has no byte serialization. |
| Duplicate-request presentation to the server handler | The `RawUnreliable` contract permits duplicates; the Direct carrier has no duplicate suppression. |
