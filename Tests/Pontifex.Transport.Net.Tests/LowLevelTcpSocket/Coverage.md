# LowLevelTcpSocket Coverage

## Layer identity

- **Layer:** `LowLevelTcpSocket`
- **Subject:** `Pontifex.NetSockets.Tcp.LowLevelTcpSocket`
- **Directory:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket`
- **Contract references:** [LowLevelTcpSocket.cs](../../../Transports/Pontifex.Transport.Net/Common/LowLevelTcpSocket/LowLevelTcpSocket.cs), [ILowLevelTcpSocket.cs](../../../Transports/Pontifex.Transport.Net/Common/LowLevelTcpSocket/ILowLevelTcpSocket.cs)

This is the single root layer of the suite. Coverage is recorded here for every test in the layer, including tests in `Tests/` groups and harness self-tests. Groups are not layers and do not have their own `Coverage.md`.

## Covered tests

### Construction

Validation paths run against scripted accessors; loopback acceptance uses `RealSocketFixture`. No real time or uncontrolled scheduling is involved.

#### `ConstructionRollbackTests` — Construction rollback

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SecondArgsCreationFailure_DisposesFirstArgs_LeavesAccessorOwnedByCaller` | Construction rollback | Second Args Creation Failure Disposes First Args Leaves Accessor Owned By Caller. | Scripted validation; no real time. |
| `FirstArgsCreationFailure_HasNoPartialOwnership` | Construction rollback | First Args Creation Failure Has No Partial Ownership. | Scripted validation; no real time. |

#### `DeadlineValidationTests` — Deadline validation

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `InvalidShutdownDeadline_LeavesSenderOpen` | Deadline validation | Invalid Shutdown Deadline Leaves Sender Open. | 2 parameterized cases. |
| `InvalidDisposeTimeout_DoesNotDispose` | Deadline validation | Invalid Dispose Timeout Does Not Dispose. | Scripted validation; no real time. |
| `InvalidConstructorDeadline_CreatesNoArgs` | Deadline validation | Invalid Constructor Deadline Creates No Args. | Scripted validation; no real time. |
| `InvalidDeadlineParameters_AreRejectedByEveryOverload` | Deadline validation | Invalid Deadline Parameters Are Rejected By Every Overload. | Scripted validation; no real time. |
| `ValidDeadlineBoundaryValues_AreAccepted` | Deadline validation | Valid Deadline Boundary Values Are Accepted. | Scripted validation; no real time. |
| `DeadlineValidator_ComparesByTicksExactly` | Deadline validation | Deadline Validator Compares By Ticks Exactly. | Scripted validation; no real time. |

#### `LowLevelTcpSocketConstructionTests` — Constructor validation and endpoint exposure

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `NullSocket_Throws` | Constructor validation and endpoint exposure | Null Socket Throws. | Scripted validation; no real time. |
| `NullSource_Throws` | Constructor validation and endpoint exposure | Null Source Throws. | Scripted validation; no real time. |
| `NullOnReceived_Throws` | Constructor validation and endpoint exposure | Null On Received Throws. | Scripted validation; no real time. |
| `NullPool_Throws` | Constructor validation and endpoint exposure | Null Pool Throws. | Scripted validation; no real time. |
| `InvalidPolicy_Throws` | Constructor validation and endpoint exposure | Invalid Policy Throws. | Scripted validation; no real time. |
| `NonPositiveReceiveBufferSize_Throws` | Constructor validation and endpoint exposure | Non Positive Receive Buffer Size Throws. | Scripted validation; no real time. |
| `Endpoints_AreExposed` | Constructor validation and endpoint exposure | Endpoints Are Exposed. | Scripted validation; no real time. |

#### `SystemSocketTests` — System socket acceptance

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Rejects_NonTcpSocket` | System socket acceptance | Rejects Non Tcp Socket. | Scripted validation; no real time. |
| `Rejects_UnconnectedTcpSocket` | System socket acceptance | Rejects Unconnected Tcp Socket. | Scripted validation; no real time. |
| `Rejects_DisposedSocket` | System socket acceptance | Rejects Disposed Socket. | Scripted validation; no real time. |
| `Rejects_NullSocket` | System socket acceptance | Rejects Null Socket. | Scripted validation; no real time. |
| `Accepts_ConnectedLoopbackSocket` | System socket acceptance | Accepts Connected Loopback Socket. | Scripted validation; no real time. |

### Sending

The source, accessor, and deadline scheduler are scripted; no sleeping or real timers are used except where a probe/harness smoke explicitly says otherwise.

#### `SendFailureTests` — Send failure atomicity

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `FailedCompletion_PreventsRepostBeforePublication` | Send failure atomicity | Failed Completion Prevents Repost Before Publication. | Scripted source/accessor; no real time. |
| `ZeroProgressCompletion_PreventsRepostBeforePublication` | Send failure atomicity | Zero Progress Completion Prevents Repost Before Publication. | Scripted source/accessor; no real time. |
| `ExhaustedWouldBlockRetries_PreventRepostBeforePublication` | Send failure atomicity | Exhausted Would Block Retries Prevent Repost Before Publication. | Scripted source/accessor; no real time. |
| `SynchronousSendThrow_FaultsSender` | Send failure atomicity | Synchronous Send Throw Faults Sender. | Scripted source/accessor; no real time. |
| `ZeroByteSendFault_CarriesOperationContext` | Send failure atomicity | Zero Byte Send Fault Carries Operation Context. | Scripted source/accessor; no real time. |
| `NullSendItem_FaultsWithClearError` | Send failure atomicity | Null Send Item Faults With Clear Error. | Scripted source/accessor; no real time. |

#### `SenderPumpTests` — Sender pump and in-flight state

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `EmptySourcePump_RaisesSourceDrainedOnceAndSendsNothing` | Sender pump and in-flight state | Empty Source Pump Raises Source Drained Once And Sends Nothing. | Scripted source/accessor; no real time. |
| `SourceDrained_NotRaisedAgainUntilAPopSucceeds` | Sender pump and in-flight state | Source Drained Not Raised Again Until A Pop Succeeds. | Scripted source/accessor; no real time. |
| `Buffer_RetainedWhilePendingAndReleasedOnceWhenFullySent` | Sender pump and in-flight state | Buffer Retained While Pending And Released Once When Fully Sent. | Scripted source/accessor; no real time. |
| `PartialSend_RePostsRemainingRange` | Sender pump and in-flight state | Partial Send Re Posts Remaining Range. | Scripted source/accessor; no real time. |
| `Buffers_AreSentInFifoOrder` | Sender pump and in-flight state | Buffers Are Sent In Fifo Order. | Scripted source/accessor; no real time. |
| `PartialSuccess_RepostsRemainingRange` | Sender pump and in-flight state | Partial Success Reposts Remaining Range. | Scripted source/accessor; no real time. |
| `ConcurrentPumps_Coalesce` | Sender pump and in-flight state | Concurrent Pumps Coalesce. | Scripted source/accessor; no real time. |
| `CompletionOnAnotherThreadBeforePostingReturns_DoesNotStrandPending` | Sender pump and in-flight state | Completion On Another Thread Before Posting Returns Does Not Strand Pending. | Scripted source/accessor; no real time. |
| `Abort_IsNotStalledByBlockingSourcePop` | Sender pump and in-flight state | Abort Is Not Stalled By Blocking Source Pop. | Scripted source/accessor; no real time. |

#### `SenderShutdownDeadlineTests` — Sender shutdown deadline scheduling

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ObsoleteDispatchedTimer_DoesNotAbortReplacementDeadline` | Sender shutdown deadline scheduling | Obsolete Dispatched Timer Does Not Abort Replacement Deadline. | Scripted source/accessor; no real time. |
| `ExplicitInfiniteDeadline_CancelsFiniteDeadline` | Sender shutdown deadline scheduling | Explicit Infinite Deadline Cancels Finite Deadline. | Scripted source/accessor; no real time. |
| `ParameterlessGracefulShutdown_UsesConstructorDefault` | Sender shutdown deadline scheduling | Parameterless Graceful Shutdown Uses Constructor Default. | Scripted source/accessor; no real time. |
| `RepeatedParameterlessShutdown_DoesNotExtendDeadline` | Sender shutdown deadline scheduling | Repeated Parameterless Shutdown Does Not Extend Deadline. | Scripted source/accessor; no real time. |
| `RemoteEof_UsesConstructorDefault_AndDoesNotRestartEstablishedDeadline` | Sender shutdown deadline scheduling | Remote Eof Uses Constructor Default And Does Not Restart Established Deadline. | Scripted source/accessor; no real time. |
| `ImmediateEscalation_PreservesActiveDefault` | Sender shutdown deadline scheduling | Immediate Escalation Preserves Active Default. | Scripted source/accessor; no real time. |
| `TerminalSender_DoesNotRearm` | Sender shutdown deadline scheduling | Terminal Sender Does Not Rearm. | Scripted source/accessor; no real time. |
| `RealTimerSmoke_ExpiryForcesAbort` | Sender shutdown deadline scheduling | Real Timer Smoke Expiry Forces Abort. | Scripted source/accessor; no real time. |

#### `SenderShutdownTests` — Sender shutdown semantics

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `GracefulEmptySource_ShutsDownSendDirection` | Sender shutdown semantics | Graceful Empty Source Shuts Down Send Direction. | Scripted source/accessor; no real time. |
| `Graceful_DrainsQueuedBufferThenFin` | Sender shutdown semantics | Graceful Drains Queued Buffer Then Fin. | Scripted source/accessor; no real time. |
| `ImmediateEmptySource_ShutsDownSendDirection` | Sender shutdown semantics | Immediate Empty Source Shuts Down Send Direction. | Scripted source/accessor; no real time. |
| `ShutdownAfterTerminal_IsIdempotent` | Sender shutdown semantics | Shutdown After Terminal Is Idempotent. | Scripted source/accessor; no real time. |
| `EscalatingGracefulToImmediate_CompletesAsImmediate` | Sender shutdown semantics | Escalating Graceful To Immediate Completes As Immediate. | Scripted source/accessor; no real time. |
| `GracefulShutdown_DoesNotLosePreviouslyPublishedData` | Sender shutdown semantics | Graceful Shutdown Does Not Lose Previously Published Data. | Scripted source/accessor; no real time. |
| `TryShutdown_NotConnectedIsBenign` | Sender shutdown semantics | Try Shutdown Not Connected Is Benign. | Scripted source/accessor; no real time. |
| `TryShutdown_ShutdownIsBenign` | Sender shutdown semantics | Try Shutdown Shutdown Is Benign. | Scripted source/accessor; no real time. |
| `TryShutdown_ObjectDisposedIsBenign` | Sender shutdown semantics | Try Shutdown Object Disposed Is Benign. | Scripted source/accessor; no real time. |
| `TryShutdown_OperationAbortedIsBenign` | Sender shutdown semantics | Try Shutdown Operation Aborted Is Benign. | Scripted source/accessor; no real time. |
| `TryShutdown_ConnectionAbortedIsBenign` | Sender shutdown semantics | Try Shutdown Connection Aborted Is Benign. | Scripted source/accessor; no real time. |

#### `SynchronousSendTests` — Synchronous send completion

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SynchronousZeroProgress_FaultsWithOperationContext` | Synchronous send completion | Synchronous Zero Progress Faults With Operation Context. | Scripted source/accessor; no real time. |
| `SynchronousWouldBlock_ExhaustsWithinOnePump` | Synchronous send completion | Synchronous Would Block Exhausts Within One Pump. | Scripted source/accessor; no real time. |

#### `WholeDrainDeadlineTests` — Whole-drain deadline persistence

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ExpiryBetweenChunks_PreventsNextPost` | Whole-drain deadline persistence | Expiry Between Chunks Prevents Next Post. | Scripted source/accessor; no real time. |
| `ExpiryDuringInitialSourcePop_CompletesLogicalShutdown` | Whole-drain deadline persistence | Expiry During Initial Source Pop Completes Logical Shutdown. | 2 parameterized cases. |
| `RemoteEofGracefulPolicy_UsesSameWholeDrainExpiry` | Whole-drain deadline persistence | Remote Eof Graceful Policy Uses Same Whole Drain Expiry. | Scripted source/accessor; no real time. |
| `ExpiryDuringPartialSendContinuation_PreventsFurtherRangePost` | Whole-drain deadline persistence | Expiry During Partial Send Continuation Prevents Further Range Post. | Scripted source/accessor; no real time. |
| `CompletionBeforeDeadline_InvalidatesExpiry_WithoutAbortingReceiver` | Whole-drain deadline persistence | Completion Before Deadline Invalidates Expiry Without Aborting Receiver. | Scripted source/accessor; no real time. |
| `ObsoleteGeneration_IsIgnored` | Whole-drain deadline persistence | Obsolete Generation Is Ignored. | Scripted source/accessor; no real time. |
| `NativePendingBuffer_HeldUntilLateCallback` | Whole-drain deadline persistence | Native Pending Buffer Held Until Late Callback. | Scripted source/accessor; no real time. |
| `LatePoppedBuffer_ReturnsExactlyOnce` | Whole-drain deadline persistence | Late Popped Buffer Returns Exactly Once. | Scripted source/accessor; no real time. |
| `ImmediateShutdownDeadline_ForceFinalizesPendingSend` | Whole-drain deadline persistence | Immediate Shutdown Deadline Force Finalizes Pending Send. | Scripted source/accessor; no real time. |

### Receiving

The socket accessor and pool are scripted. Consumer-owned buffers and partial views are released by the test or a tracking harness.

#### `PartialReceiveViewTests` — Partial receive view lifetime

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ReleasedPartialViews_ReusedAtSteadyState_NoPerReceiveViewAllocation` | Partial receive view lifetime | Released Partial Views Reused At Steady State No Per Receive View Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `RetainedViews_AreIndependentAcrossReceives` | Partial receive view lifetime | Retained Views Are Independent Across Receives. | Scripted accessor/pool; consumer-owned release. |
| `View_ReturnedOnlyAfterFinalRelease` | Partial receive view lifetime | View Returned Only After Final Release. | Scripted accessor/pool; consumer-owned release. |
| `CallbackThrowAndForceClose_PreserveConsumerOwnership` | Partial receive view lifetime | Callback Throw And Force Close Preserve Consumer Ownership. | Scripted accessor/pool; consumer-owned release. |
| `FullBufferReceive_HandsTheOwnerDirectly` | Partial receive view lifetime | Full Buffer Receive Hands The Owner Directly. | Scripted accessor/pool; consumer-owned release. |
| `PartialView_RangeGuards_AreMeaningful` | Partial receive view lifetime | Partial View Range Guards Are Meaningful. | Scripted accessor/pool; consumer-owned release. |
| `ReleasedView_ReportsUnderflowWithoutReviving` | Partial receive view lifetime | Released View Reports Underflow Without Reviving. | Scripted accessor/pool; consumer-owned release. |

#### `ReceiveFailureTests` — Receive failure isolation

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ReceiveError_FaultsAndReleasesBuffer` | Receive failure isolation | Receive Error Faults And Releases Buffer. | Scripted accessor/pool; consumer-owned release. |
| `ZeroLengthReceiveBuffer_DoesNotProduceRemoteEof` | Receive failure isolation | Zero Length Receive Buffer Does Not Produce Remote Eof. | Scripted accessor/pool; consumer-owned release. |
| `OnReceivedThrow_IsReportedAndFaults` | Receive failure isolation | On Received Throw Is Reported And Faults. | Scripted accessor/pool; consumer-owned release. |
| `ReceiveSpanConstructionFailure_StillReleasesBuffer` | Receive failure isolation | Receive Span Construction Failure Still Releases Buffer. | Scripted accessor/pool; consumer-owned release. |

#### `ReceivePoolTests` — Receive pool rental

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `OptInPool_WarmedAcquireRelease_HasNoOwnerMetadataAllocation` | Receive pool rental | Opt In Pool Warmed Acquire Release Has No Owner Metadata Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `FullAndPartialTransportReceives_UseZeroSteadyStateMetadataAllocation` | Receive pool rental | Full And Partial Transport Receives Use Zero Steady State Metadata Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `OwnerRetainedByConsumer_CannotBeReused` | Receive pool rental | Owner Retained By Consumer Cannot Be Reused. | Scripted accessor/pool; consumer-owned release. |
| `PoolMissAndCacheLimit_AreSafe` | Receive pool rental | Pool Miss And Cache Limit Are Safe. | Scripted accessor/pool; consumer-owned release. |
| `RecyclingPool_RepeatedReceives_ExactlyOnceRelease_NoAliasing` | Receive pool rental | Recycling Pool Repeated Receives Exactly Once Release No Aliasing. | Scripted accessor/pool; consumer-owned release. |
| `LateNativeCompletion_ReturnsRecycledOwnerExactlyOnce` | Receive pool rental | Late Native Completion Returns Recycled Owner Exactly Once. | Scripted accessor/pool; consumer-owned release. |
| `ConcurrentFinalRelease_RecyclingPool_IsSafe` | Receive pool rental | Concurrent Final Release Recycling Pool Is Safe. | Scripted accessor/pool; consumer-owned release. |
| `AcquireFailure_FaultsReceiver_AndCleanupBalances` | Receive pool rental | Acquire Failure Faults Receiver And Cleanup Balances. | Scripted accessor/pool; consumer-owned release. |
| `ThrowingBufferReturn_IsCapturedAndCompletionStillPublishes` | Receive pool rental | Throwing Buffer Return Is Captured And Completion Still Publishes. | Scripted accessor/pool; consumer-owned release. |
| `GuardedDefaultPool_StillDiagnosesBalance` | Receive pool rental | Guarded Default Pool Still Diagnoses Balance. | Scripted accessor/pool; consumer-owned release. |

#### `ReceiverTests` — Receive dispatch and re-arm

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Start_PostsReceiveOnPoolBuffer` | Receive dispatch and re-arm | Start Posts Receive On Pool Buffer. | Scripted accessor/pool; consumer-owned release. |
| `ReceiveBufferSize_IsConfigurable` | Receive dispatch and re-arm | Receive Buffer Size Is Configurable. | Scripted accessor/pool; consumer-owned release. |
| `ReceivedBytes_AreDispatchedAndReceiveReArmed` | Receive dispatch and re-arm | Received Bytes Are Dispatched And Receive Re Armed. | Scripted accessor/pool; consumer-owned release. |
| `DeliveredSpan_UsesPoolBufferOffset` | Receive dispatch and re-arm | Delivered Span Uses Pool Buffer Offset. | Scripted accessor/pool; consumer-owned release. |
| `ZeroByteCompletion_IsRemoteEof` | Receive dispatch and re-arm | Zero Byte Completion Is Remote Eof. | Scripted accessor/pool; consumer-owned release. |
| `FullBufferReceive_IsDispatchedZeroCopy` | Receive dispatch and re-arm | Full Buffer Receive Is Dispatched Zero Copy. | Scripted accessor/pool; consumer-owned release. |
| `PartialBufferReceive_UsesASpanView` | Receive dispatch and re-arm | Partial Buffer Receive Uses A Span View. | Scripted accessor/pool; consumer-owned release. |
| `WouldBlock_ThenSuccessRetries` | Receive dispatch and re-arm | Would Block Then Success Retries. | Scripted accessor/pool; consumer-owned release. |
| `LocalShutdown_CompletesAsLocalShutdown` | Receive dispatch and re-arm | Local Shutdown Completes As Local Shutdown. | Scripted accessor/pool; consumer-owned release. |
| `Start_AfterTerminalIsNoOp` | Receive dispatch and re-arm | Start After Terminal Is No Op. | Scripted accessor/pool; consumer-owned release. |

#### `SynchronousReceiveTests` — Synchronous receive completion

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SynchronousSuccess_IsDispatchedAndReArmed` | Synchronous receive completion | Synchronous Success Is Dispatched And Re Armed. | Scripted accessor/pool; consumer-owned release. |
| `SynchronousZeroByteReceive_IsRemoteEof` | Synchronous receive completion | Synchronous Zero Byte Receive Is Remote Eof. | Scripted accessor/pool; consumer-owned release. |
| `WouldBlockExhaustion_FaultsAndBalancesEachAttempt` | Synchronous receive completion | Would Block Exhaustion Faults And Balances Each Attempt. | Scripted accessor/pool; consumer-owned release. |
| `CompletionOnAnotherThreadBeforePostingReturns_DoesNotStrandReceive` | Synchronous receive completion | Completion On Another Thread Before Posting Returns Does Not Strand Receive. | Scripted accessor/pool; consumer-owned release. |
| `ShutdownFromReceiveCallback_DoesNotDeadlockAndPublishes` | Synchronous receive completion | Shutdown From Receive Callback Does Not Deadlock And Publishes. | Scripted accessor/pool; consumer-owned release. |

### Draining

The source reader and notification path are driven explicitly. No handler starts after terminal state except an already-admitted handler.

#### `DrainHandlerAdmissionTests` — SourceDrained handler admission

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `FirstHandlerTerminates_SkipsSecondHandler` | SourceDrained handler admission | First Handler Terminates Skips Second Handler. | Deterministic reader; no post-terminal admission. |
| `AbortBeforeAdmission_SuppressesHandler` | SourceDrained handler admission | Abort Before Admission Suppresses Handler. | Deterministic reader; no post-terminal admission. |
| `AlreadyAdmittedHandler_MayCompleteAfterAbort` | SourceDrained handler admission | Already Admitted Handler May Complete After Abort. | Deterministic reader; no post-terminal admission. |
| `ThrowingFirstHandler_LaterHandlersStillRun` | SourceDrained handler admission | Throwing First Handler Later Handlers Still Run. | Deterministic reader; no post-terminal admission. |
| `DisposeFromHandler_DoesNotDeadlock` | SourceDrained handler admission | Dispose From Handler Does Not Deadlock. | Deterministic reader; no post-terminal admission. |
| `HandlerRepump_IsNonRecursive` | SourceDrained handler admission | Handler Repump Is Non Recursive. | Deterministic reader; no post-terminal admission. |

#### `DrainNotificationAllocationTests` — Drain notification allocation

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `NoSubscriberDrain_HasZeroSteadyStateMetadataAllocation` | Drain notification allocation | No Subscriber Drain Has Zero Steady State Metadata Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `StableSingleSubscriberDrain_HasZeroSteadyStateMetadataAllocation` | Drain notification allocation | Stable Single Subscriber Drain Has Zero Steady State Metadata Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `StableMultipleSubscriberDrain_HasZeroSteadyStateMetadataAllocation` | Drain notification allocation | Stable Multiple Subscriber Drain Has Zero Steady State Metadata Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `ThrowingSubscriber_OtherHandlersStillRun_ErrorRouted` | Drain notification allocation | Throwing Subscriber Other Handlers Still Run Error Routed. | Deterministic reader; no post-terminal admission. |
| `HandlerTriggeredShutdown_LaterHandlersAreNotAdmitted` | Drain notification allocation | Handler Triggered Shutdown Later Handlers Are Not Admitted. | Deterministic reader; no post-terminal admission. |
| `SubscriptionAddedDuringCallback_HasSnapshotSemantics` | Drain notification allocation | Subscription Added During Callback Has Snapshot Semantics. | Deterministic reader; no post-terminal admission. |
| `DuplicateSubscribeAndRemove_MatchDotNetEventSemantics` | Drain notification allocation | Duplicate Subscribe And Remove Match Dot Net Event Semantics. | Deterministic reader; no post-terminal admission. |

#### `DrainNotificationHandoffTests` — Drain notification handoff

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `RequestDuringOwnerExit_IsDeliveredWithoutThirdRequest` | Drain notification handoff | Request During Owner Exit Is Delivered Without Third Request. | Deterministic reader; no post-terminal admission. |
| `ConcurrentRequests_HaveOneNotifierAndAtMostOnePending` | Drain notification handoff | Concurrent Requests Have One Notifier And At Most One Pending. | Deterministic reader; no post-terminal admission. |
| `TerminationDuringHandoff_DropsOnlyIneligibleWork` | Drain notification handoff | Termination During Handoff Drops Only Ineligible Work. | Deterministic reader; no post-terminal admission. |
| `ReEligibility_StillRequiresSuccessfulPop` | Drain notification handoff | Re Eligibility Still Requires Successful Pop. | Deterministic reader; no post-terminal admission. |

#### `SourceDrainedTests` — SourceDrained edge semantics

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `EdgeTriggered_OncePerDrain` | SourceDrained edge semantics | Edge Triggered Once Per Drain. | Deterministic reader; no post-terminal admission. |
| `ReEligible_AfterSuccessfulPopAndDrain` | SourceDrained edge semantics | Re Eligible After Successful Pop And Drain. | Deterministic reader; no post-terminal admission. |
| `HandlerException_RoutedToCallbackError` | SourceDrained edge semantics | Handler Exception Routed To Callback Error. | Deterministic reader; no post-terminal admission. |
| `SourceDrainedHandler_RePumpingIsNotRecursive` | SourceDrained edge semantics | Source Drained Handler Re Pumping Is Not Recursive. | Deterministic reader; no post-terminal admission. |
| `NestedDrains_CoalesceIntoOnePendingNotification` | SourceDrained edge semantics | Nested Drains Coalesce Into One Pending Notification. | Deterministic reader; no post-terminal admission. |
| `ThrowingSourceDrainedHandler_IsReportedAndDoesNotDisrupt` | SourceDrained edge semantics | Throwing Source Drained Handler Is Reported And Does Not Disrupt. | Deterministic reader; no post-terminal admission. |
| `ThrowingDiagnosticCallback_IsSwallowed` | SourceDrained edge semantics | Throwing Diagnostic Callback Is Swallowed. | Deterministic reader; no post-terminal admission. |

#### `SourceQuiescedTests` — SourceQuiesced reader-quiescence

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SourceQuiesced_WaitsForBlockedPop_WhileCompletionPublishes` | SourceQuiesced reader-quiescence | Source Quiesced Waits For Blocked Pop While Completion Publishes. | Deterministic reader; no post-terminal admission. |
| `ProducerDrainAfterSourceQuiesced_HasNoReaderOverlap` | SourceQuiesced reader-quiescence | Producer Drain After Source Quiesced Has No Reader Overlap. | Deterministic reader; no post-terminal admission. |
| `TerminalBeforePopReservation_PreventsNewSourceRead` | SourceQuiesced reader-quiescence | Terminal Before Pop Reservation Prevents New Source Read. | Deterministic reader; no post-terminal admission. |
| `AbortDuringCompletedBufferRelease_PreventsNewSourceRead` | SourceQuiesced reader-quiescence | Abort During Completed Buffer Release Prevents New Source Read. | Deterministic reader; no post-terminal admission. |
| `NeverStartedSender_QuiescesAfterAbort` | SourceQuiesced reader-quiescence | Never Started Sender Quiesces After Abort. | Deterministic reader; no post-terminal admission. |
| `NormalGracefulDrain_QuiescesAfterSourceEmptied` | SourceQuiesced reader-quiescence | Normal Graceful Drain Quiesces After Source Emptied. | Deterministic reader; no post-terminal admission. |
| `ImmediateShutdown_LeavesQueuedItemsProducerOwned` | SourceQuiesced reader-quiescence | Immediate Shutdown Leaves Queued Items Producer Owned. | Deterministic reader; no post-terminal admission. |
| `ThrowingSourcePop_StillQuiesces` | SourceQuiesced reader-quiescence | Throwing Source Pop Still Quiesces. | Deterministic reader; no post-terminal admission. |
| `RepeatedAbort_QuiescesOnce` | SourceQuiesced reader-quiescence | Repeated Abort Quiesces Once. | Deterministic reader; no post-terminal admission. |

### Completion and error precedence

Completion quiescence includes `onReceived` callbacks but excludes `SourceDrained` handlers. First-terminal-wins is load-bearing.

#### `CompletionTests` — Completion aggregation and quiescence

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `BothHalvesGraceful_ProducesClosed` | Completion aggregation and quiescence | Both Halves Graceful Produces Closed. | Quiescence excludes SourceDrained handlers. |
| `Result_ExposesDirectionalDetails` | Completion aggregation and quiescence | Result Exposes Directional Details. | Quiescence excludes SourceDrained handlers. |
| `Result_ExposesRemoteEofOnReceiver` | Completion aggregation and quiescence | Result Exposes Remote Eof On Receiver. | Quiescence excludes SourceDrained handlers. |
| `LocalDispose_ProducesDisposed` | Completion aggregation and quiescence | Local Dispose Produces Disposed. | Quiescence excludes SourceDrained handlers. |
| `SendFault_ProducesFaultedWithError` | Completion aggregation and quiescence | Send Fault Produces Faulted With Error. | Quiescence excludes SourceDrained handlers. |
| `Completion_IsIdempotentAcrossRepeatedDispose` | Completion aggregation and quiescence | Completion Is Idempotent Across Repeated Dispose. | Quiescence excludes SourceDrained handlers. |
| `DisposeAsync_CompletesOnNormalPath` | Completion aggregation and quiescence | Dispose Async Completes On Normal Path. | Quiescence excludes SourceDrained handlers. |
| `FirstTerminalWins_AbortNotReplacedByLaterFault` | Completion aggregation and quiescence | First Terminal Wins Abort Not Replaced By Later Fault. | Quiescence excludes SourceDrained handlers. |
| `Completion_WaitsForInFlightReceiveCallback` | Completion aggregation and quiescence | Completion Waits For In Flight Receive Callback. | Quiescence excludes SourceDrained handlers. |
| `Completion_WaitsForPendingReceiveDispatch` | Completion aggregation and quiescence | Completion Waits For Pending Receive Dispatch. | Quiescence excludes SourceDrained handlers. |

#### `ErrorPrecedenceTests` — Error precedence and secondary errors

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SendFault_RemainsPrimary_WhenReleaseThrows` | Error precedence and secondary errors | Send Fault Remains Primary When Release Throws. | Quiescence excludes SourceDrained handlers. |
| `ReceiveFault_RemainsPrimary_WhenReleaseThrows` | Error precedence and secondary errors | Receive Fault Remains Primary When Release Throws. | Quiescence excludes SourceDrained handlers. |
| `DisposeFirst_RemainsDisposed_WithLaterCleanupFault` | Error precedence and secondary errors | Dispose First Remains Disposed With Later Cleanup Fault. | Quiescence excludes SourceDrained handlers. |
| `EquivalentSocketErrors_ConsumeOneSecondarySlot` | Error precedence and secondary errors | Equivalent Socket Errors Consume One Secondary Slot. | Quiescence excludes SourceDrained handlers. |
| `MultipleCleanupStagesFail_CleanupStillRunsAndPublishes` | Error precedence and secondary errors | Multiple Cleanup Stages Fail Cleanup Still Runs And Publishes. | Quiescence excludes SourceDrained handlers. |
| `DuplicateSocketError_DoesNotConsumeSecondaryErrorSlot` | Error precedence and secondary errors | Duplicate Socket Error Does Not Consume Secondary Error Slot. | Quiescence excludes SourceDrained handlers. |
| `ReReportingTheSameErrorInstance_DoesNotConsumeSecondarySlot` | Error precedence and secondary errors | Re Reporting The Same Error Instance Does Not Consume Secondary Slot. | Quiescence excludes SourceDrained handlers. |
| `AbortShutdownFailure_IsSurfacedAsSecondaryError` | Error precedence and secondary errors | Abort Shutdown Failure Is Surfaced As Secondary Error. | Quiescence excludes SourceDrained handlers. |

### Teardown and disposal

Bounded paths use the manual teardown executor and deterministic scheduler; every blocker is released in `finally` so eventual cleanup is verified.

#### `AccessorDisposalTests` — Accessor disposal and classification

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `GracefulClose_DisposalFailure_IsInAggregateResult` | Accessor disposal and classification | Graceful Close Disposal Failure Is In Aggregate Result. | Manual executor/scheduler; blockers released in finally. |
| `LocalDispose_DisposalFailure_IsSecondary` | Accessor disposal and classification | Local Dispose Disposal Failure Is Secondary. | Manual executor/scheduler; blockers released in finally. |
| `PrimaryIoFault_IsNotReplacedByDisposeFailure` | Accessor disposal and classification | Primary Io Fault Is Not Replaced By Dispose Failure. | Manual executor/scheduler; blockers released in finally. |
| `DisposalIsAttemptedOnce_AcrossRepeatedTeardown` | Accessor disposal and classification | Disposal Is Attempted Once Across Repeated Teardown. | Manual executor/scheduler; blockers released in finally. |
| `AlreadyDisposedAccessor_TeardownIsTolerated` | Accessor disposal and classification | Already Disposed Accessor Teardown Is Tolerated. | Manual executor/scheduler; blockers released in finally. |

#### `BoundedDisposalDeadlineTests` — Bounded-disposal deadline sharing

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `AlreadyCompletedBoundedDispose_DoesNotScheduleTimer` | Bounded-disposal deadline sharing | Already Completed Bounded Dispose Does Not Schedule Timer. | Manual executor/scheduler; blockers released in finally. |
| `AlreadyCompletedBoundedDispose_HasZeroSteadyStateMetadataAllocation` | Bounded-disposal deadline sharing | Already Completed Bounded Dispose Has Zero Steady State Metadata Allocation. | Release-only allocation probe; measurements are environment-specific. |
| `SuccessfulEarlyCleanup_CancelsOutstandingTimeoutWork` | Bounded-disposal deadline sharing | Successful Early Cleanup Cancels Outstanding Timeout Work. | Manual executor/scheduler; blockers released in finally. |
| `ConcurrentDisposeWaiters_ShareConnectionDeadlineState` | Bounded-disposal deadline sharing | Concurrent Dispose Waiters Share Connection Deadline State. | Manual executor/scheduler; blockers released in finally. |
| `LongerLaterWaiter_DoesNotExtendEarlierBound` | Bounded-disposal deadline sharing | Longer Later Waiter Does Not Extend Earlier Bound. | Manual executor/scheduler; blockers released in finally. |
| `InvalidTimeouts_RejectBeforeAnySideEffect` | Bounded-disposal deadline sharing | Invalid Timeouts Reject Before Any Side Effect. | Manual executor/scheduler; blockers released in finally. |
| `ExactInfinity_SchedulesNoTimer` | Bounded-disposal deadline sharing | Exact Infinity Schedules No Timer. | Manual executor/scheduler; blockers released in finally. |
| `ExpiredForcedResult_RemainsImmutable` | Bounded-disposal deadline sharing | Expired Forced Result Remains Immutable. | Manual executor/scheduler; blockers released in finally. |
| `BoundedDisposeAsync_ForceFinalizesWithPendingSend` | Bounded-disposal deadline sharing | Bounded Dispose Async Force Finalizes With Pending Send. | Manual executor/scheduler; blockers released in finally. |
| `BoundedDisposeAsync_ForceFinalizesWithPendingReceive` | Bounded-disposal deadline sharing | Bounded Dispose Async Force Finalizes With Pending Receive. | Manual executor/scheduler; blockers released in finally. |

#### `BoundedDisposalTests` — Bounded logical disposal

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `FiniteDisposeReturnsAwaitable_WhileReleaseIsBlocked` | Bounded logical disposal | Finite Dispose Returns Awaitable While Release Is Blocked. | Manual executor/scheduler; blockers released in finally. |
| `BlockedAccessorDisposal_DoesNotBlockDeadlinePublication` | Bounded logical disposal | Blocked Accessor Disposal Does Not Block Deadline Publication. | Manual executor/scheduler; blockers released in finally. |
| `BlockedShutdownBoth_DoesNotBlockDeadlinePublication` | Bounded logical disposal | Blocked Shutdown Both Does Not Block Deadline Publication. | Manual executor/scheduler; blockers released in finally. |
| `ForcedResultIsImmutable_LateCleanupErrorIsDiagnosedOnce` | Bounded logical disposal | Forced Result Is Immutable Late Cleanup Error Is Diagnosed Once. | Manual executor/scheduler; blockers released in finally. |
| `InfiniteDispose_WaitsForReceiveDispatchAndCleanup` | Bounded logical disposal | Infinite Dispose Waits For Receive Dispatch And Cleanup. | Manual executor/scheduler; blockers released in finally. |
| `FiniteSuccessfulDisposal_ReportsFullyCapturedErrors` | Bounded logical disposal | Finite Successful Disposal Reports Fully Captured Errors. | Manual executor/scheduler; blockers released in finally. |
| `RepeatedConcurrentRequests_ReserveOneNativeTeardown` | Bounded logical disposal | Repeated Concurrent Requests Reserve One Native Teardown. | Manual executor/scheduler; blockers released in finally. |
| `LateSourcePop_KeepsSourceQuiescedIndependent` | Bounded logical disposal | Late Source Pop Keeps Source Quiesced Independent. | Manual executor/scheduler; blockers released in finally. |
| `RealTimerSmoke_BoundedDisposePublishesWhileCleanupIsBlocked` | Bounded logical disposal | Real Timer Smoke Bounded Dispose Publishes While Cleanup Is Blocked. | Manual executor/scheduler; blockers released in finally. |

#### `DirectionalShutdownFaultTests` — Directional shutdown fault surfacing

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `EndOfSendShutdownFailure_FaultsSender` | Directional shutdown fault surfacing | End Of Send Shutdown Failure Faults Sender. | Manual executor/scheduler; blockers released in finally. |
| `ReceiveShutdownFailure_FaultsReceiver` | Directional shutdown fault surfacing | Receive Shutdown Failure Faults Receiver. | Manual executor/scheduler; blockers released in finally. |

#### `DisposedStateTests` — Disposed endpoint behavior

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SenderPumpAfterDispose_ThrowsObjectDisposed` | Disposed endpoint behavior | Sender Pump After Dispose Throws Object Disposed. | Manual executor/scheduler; blockers released in finally. |
| `SenderShutdownAfterDispose_ThrowsObjectDisposed` | Disposed endpoint behavior | Sender Shutdown After Dispose Throws Object Disposed. | Manual executor/scheduler; blockers released in finally. |
| `ReceiverStartAfterDispose_ThrowsObjectDisposed` | Disposed endpoint behavior | Receiver Start After Dispose Throws Object Disposed. | Manual executor/scheduler; blockers released in finally. |
| `ReceiverShutdownAfterDispose_ThrowsObjectDisposed` | Disposed endpoint behavior | Receiver Shutdown After Dispose Throws Object Disposed. | Manual executor/scheduler; blockers released in finally. |
| `ShutdownWithInvalidDeadlineAfterDispose_ThrowsObjectDisposedFirst` | Disposed endpoint behavior | Shutdown With Invalid Deadline After Dispose Throws Object Disposed First. | Manual executor/scheduler; blockers released in finally. |

#### `ForcedPublicationTests` — Late reclamation after forced publication

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ForcedSendCompletion_LateNativeCallback_ReclaimsOnce` | Late reclamation after forced publication | Forced Send Completion Late Native Callback Reclaims Once. | Manual executor/scheduler; blockers released in finally. |
| `ForcedReceiveCompletion_LateNativeCallback_ReclaimsOnce` | Late reclamation after forced publication | Forced Receive Completion Late Native Callback Reclaims Once. | Manual executor/scheduler; blockers released in finally. |
| `ForcedCompletion_DuringPoolAcquire_DisposesArgsAfterAcquireReturns` | Late reclamation after forced publication | Forced Completion During Pool Acquire Disposes Args After Acquire Returns. | Manual executor/scheduler; blockers released in finally. |
| `ForcedCompletion_DuringReceivedCallback_DoesNotReleaseConsumerBuffer` | Late reclamation after forced publication | Forced Completion During Received Callback Does Not Release Consumer Buffer. | Manual executor/scheduler; blockers released in finally. |
| `LateNativeCallback_WithGuardedPool_ReclaimsExactlyOnce` | Late reclamation after forced publication | Late Native Callback With Guarded Pool Reclaims Exactly Once. | Manual executor/scheduler; blockers released in finally. |
| `NativeCallbackBeforeForce_CleansUpOnce` | Late reclamation after forced publication | Native Callback Before Force Cleans Up Once. | Manual executor/scheduler; blockers released in finally. |
| `RepeatedForce_CleansUpOnceAfterLateCallback` | Late reclamation after forced publication | Repeated Force Cleans Up Once After Late Callback. | Manual executor/scheduler; blockers released in finally. |
| `ForceConcurrentWithLateCompletion_NoDuplicateCleanup` | Late reclamation after forced publication | Force Concurrent With Late Completion No Duplicate Cleanup. | Manual executor/scheduler; blockers released in finally. |

#### `TeardownConcurrencyTests` — Teardown concurrency isolation

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `BlockedSendReturn_DoesNotBlockAbortStateAdmission` | Teardown concurrency isolation | Blocked Send Return Does Not Block Abort State Admission. | Manual executor/scheduler; blockers released in finally. |
| `BlockedReceiveReturn_DoesNotBlockAbortStateAdmission` | Teardown concurrency isolation | Blocked Receive Return Does Not Block Abort State Admission. | Manual executor/scheduler; blockers released in finally. |
| `BlockedArgsDisposal_DoesNotHoldDirectionalGate` | Teardown concurrency isolation | Blocked Args Disposal Does Not Hold Directional Gate. | Manual executor/scheduler; blockers released in finally. |
| `BlockedDirectionalShutdown_DoesNotHoldIoGate` | Teardown concurrency isolation | Blocked Directional Shutdown Does Not Hold Io Gate. | 2 parameterized cases. |
| `NormalCompletion_WaitsForReservedBufferRelease` | Teardown concurrency isolation | Normal Completion Waits For Reserved Buffer Release. | Manual executor/scheduler; blockers released in finally. |
| `ConcurrentAbortAndLateCompletion_ReleaseAndDisposeExactlyOnce` | Teardown concurrency isolation | Concurrent Abort And Late Completion Release And Dispose Exactly Once. | Manual executor/scheduler; blockers released in finally. |
| `ConcurrentAbortAndLateReceiveCompletion_GuardedPool_ReturnsOnce` | Teardown concurrency isolation | Concurrent Abort And Late Receive Completion Guarded Pool Returns Once. | Manual executor/scheduler; blockers released in finally. |
| `ReentrantCleanupCallback_CannotDeadlock` | Teardown concurrency isolation | Reentrant Cleanup Callback Cannot Deadlock. | Manual executor/scheduler; blockers released in finally. |
| `AbortDuringGracefulDrain_Completes` | Teardown concurrency isolation | Abort During Graceful Drain Completes. | Manual executor/scheduler; blockers released in finally. |

### Ownership

Exactly-once release is load-bearing. A released reference must never be released again, including after a forced logical result.

#### `EventArgsStorageTests` — Event-args storage retention

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `DrainedIdleSender_DoesNotKeepReleasedArrayInArgs` | Event-args storage retention | Drained Idle Sender Does Not Keep Released Array In Args. | Exactly-once release is load-bearing. |
| `DrainedIdleSender_ReleasedArrayBecomesCollectible` | Event-args storage retention | Drained Idle Sender Released Array Becomes Collectible. | Exactly-once release is load-bearing. |
| `TerminalCleanup_DetachesReleasedStorage` | Event-args storage retention | Terminal Cleanup Detaches Released Storage. | Exactly-once release is load-bearing. |
| `PartialSend_KeepsRemainingBufferInstalled` | Event-args storage retention | Partial Send Keeps Remaining Buffer Installed. | Exactly-once release is load-bearing. |
| `NativePendingOperations_KeepArgsStorageThroughForcePublication` | Event-args storage retention | Native Pending Operations Keep Args Storage Through Force Publication. | Exactly-once release is load-bearing. |
| `ConsumerRetainedReceive_StaysValidAfterTransportDetachment` | Event-args storage retention | Consumer Retained Receive Stays Valid After Transport Detachment. | Exactly-once release is load-bearing. |

#### `OwnershipTests` — Buffer ownership and release

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ReceiveBuffer_ReleasedExactlyOnceOnSuccess` | Buffer ownership and release | Receive Buffer Released Exactly Once On Success. | Exactly-once release is load-bearing. |
| `GuardedSendBuffer_Balanced` | Buffer ownership and release | Guarded Send Buffer Balanced. | Exactly-once release is load-bearing. |
| `Transport_DoesNotReleaseBufferWhenConsumerThrows` | Buffer ownership and release | Transport Does Not Release Buffer When Consumer Throws. | Exactly-once release is load-bearing. |
| `ConsumerThatReleasesBeforeThrowing_DoesNotLeak` | Buffer ownership and release | Consumer That Releases Before Throwing Does Not Leak. | Exactly-once release is load-bearing. |
| `ThrowingReleaseOfInFlightBuffer_StillPublishesCompletion` | Buffer ownership and release | Throwing Release Of In Flight Buffer Still Publishes Completion. | Exactly-once release is load-bearing. |
| `Abort_DoesNotReleaseQueuedBuffers` | Buffer ownership and release | Abort Does Not Release Queued Buffers. | Exactly-once release is load-bearing. |

### Policies

Policy tests pin completion reasons; the loopback variants validate the resulting FIN/abort behaviour on a real local socket.

#### `RemoteHalfClosePolicyTests` — Remote half-close policy

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `AbortConnection_AbortsAndClosesOnRemoteEof` | Remote half-close policy | Abort Connection Aborts And Closes On Remote Eof. | Loopback only; not a network guarantee. |
| `KeepSendOpen_LeavesSenderOpenAndCompletionPendingOnRemoteEof` | Remote half-close policy | Keep Send Open Leaves Sender Open And Completion Pending On Remote Eof. | Loopback only; not a network guarantee. |
| `GracefulSendShutdown_ShutsDownSenderOnRemoteEof` | Remote half-close policy | Graceful Send Shutdown Shuts Down Sender On Remote Eof. | Loopback only; not a network guarantee. |
| `GracefulSendShutdown_ForceFinalizesOnDeadline` | Remote half-close policy | Graceful Send Shutdown Force Finalizes On Deadline. | Loopback only; not a network guarantee. |
| `RemoteDrivenAbort_IsNotReportedAsLocalDispose` | Remote half-close policy | Remote Driven Abort Is Not Reported As Local Dispose. | Loopback only; not a network guarantee. |

### Application composition and loopback integration

Producer/consumer examples are test-local. Loopback tests use real local TCP but remain bounded and deterministic.

#### `ApplicationByteLedgerTests` — Application byte-budget accounting

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `WeightedByteAdmission_RejectsOversizedItem_WithoutTakingOwnership` | Application byte-budget accounting | Weighted Byte Admission Rejects Oversized Item Without Taking Ownership. | Test-local composition; bounded loopback. |
| `ConsumerHeldBytes_RemainAccountedAfterSocketCompletion` | Application byte-budget accounting | Consumer Held Bytes Remain Accounted After Socket Completion. | Test-local composition; bounded loopback. |
| `QueuedPendingHeldAndDeferredCleanup_AreSeparate` | Application byte-budget accounting | Queued Pending Held And Deferred Cleanup Are Separate. | Test-local composition; bounded loopback. |

#### `ApplicationLivenessWatchdogTests` — External liveness monitoring

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `LivenessComposition_CanAbortStalledNormalIo` | External liveness monitoring | Liveness Composition Can Abort Stalled Normal Io. | Test-local composition; bounded loopback. |
| `LivenessDisabled_LeavesNormalIoIntentionallyPending` | External liveness monitoring | Liveness Disabled Leaves Normal Io Intentionally Pending. | Test-local composition; bounded loopback. |

#### `LoopbackHalfClosePolicyTests` — Remote-EOF half-close outcome on loopback

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `GracefulSendShutdown_OnRemoteEof_FlushesQueuedDataThenFin` | Remote-EOF half-close outcome on loopback | Graceful Send Shutdown On Remote Eof Flushes Queued Data Then Fin. | Test-local composition; bounded loopback. |
| `AbortConnection_OnRemoteEof_ReportsRemoteAbortedAndCloses` | Remote-EOF half-close outcome on loopback | Abort Connection On Remote Eof Reports Remote Aborted And Closes. | Test-local composition; bounded loopback. |

#### `LoopbackSocketTests` — Real loopback full-duplex integration

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `FullDuplex_SeveralLoopbackConnections_PreserveBytePatterns` | Real loopback full-duplex integration | Full Duplex Several Loopback Connections Preserve Byte Patterns. | Test-local composition; bounded loopback. |
| `PendingReceiveDisposed_EventuallyBalancesGuardedPool` | Real loopback full-duplex integration | Pending Receive Disposed Eventually Balances Guarded Pool. | Test-local composition; bounded loopback. |
| `StalledPeer_ObservedPendingSend_DeadlineBoundsTeardown` | Real loopback full-duplex integration | Stalled Peer Observed Pending Send Deadline Bounds Teardown. | Test-local composition; bounded loopback. |
| `PeerEof_CompletesReceiverWithRemoteEof` | Real loopback full-duplex integration | Peer Eof Completes Receiver With Remote Eof. | Test-local composition; bounded loopback. |
| `PeerReset_CompletesWithPortableOutcome` | Real loopback full-duplex integration | Peer Reset Completes With Portable Outcome. | Test-local composition; bounded loopback. |
| `FinalPublicationRace_HandedOffBufferIsSentByGracefulDrain` | Real loopback full-duplex integration | Final Publication Race Handed Off Buffer Is Sent By Graceful Drain. | Test-local composition; bounded loopback. |

#### `ProducerCompositionTests` — Producer seal, shutdown, and drain composition

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ProducerHandoffHelper_WaitsForSourceQuiesced` | Producer seal, shutdown, and drain composition | Producer Handoff Helper Waits For Source Quiesced. | Test-local composition; bounded loopback. |
| `FixtureTeardown_CompletesOutstandingFakeOperations` | Producer seal, shutdown, and drain composition | Fixture Teardown Completes Outstanding Fake Operations. | Test-local composition; bounded loopback. |
| `NativePendingProbe_RetainsBorrowedBufferUntilLateCompletion` | Producer seal, shutdown, and drain composition | Native Pending Probe Retains Borrowed Buffer Until Late Completion. | Test-local composition; bounded loopback. |
| `StrictSingleWriter_RejectsConcurrentProducerPublication` | Producer seal, shutdown, and drain composition | Strict Single Writer Rejects Concurrent Producer Publication. | Test-local composition; bounded loopback. |

### Allocation and load probes

These are calibrated, Release-only measurements. Assertions are sanity/balance checks; exact numbers are environment-specific, not universal budgets.

#### `AllocationBaselineTests` — Allocation baselines

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SharedByteArrayPool_RentRelease_AllocationBaseline` | Allocation baselines | Shared Byte Array Pool Rent Release Allocation Baseline. | Release-only allocation probe; measurements are environment-specific. |
| `PartialReceiveSpan_AllocationBaseline` | Allocation baselines | Partial Receive Span Allocation Baseline. | Release-only allocation probe; measurements are environment-specific. |
| `DrainNotification_NoSubscriber_AllocationBaseline` | Allocation baselines | Drain Notification No Subscriber Allocation Baseline. | Release-only allocation probe; measurements are environment-specific. |
| `DrainNotification_SingleSubscriber_AllocationBaseline` | Allocation baselines | Drain Notification Single Subscriber Allocation Baseline. | Release-only allocation probe; measurements are environment-specific. |
| `BoundedDispose_CompletedSocket_AllocationBaseline` | Allocation baselines | Bounded Dispose Completed Socket Allocation Baseline. | Release-only allocation probe; measurements are environment-specific. |

#### `LoadProbeTests` — Load probes

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `SteadyStateSmallMessage_ThroughputAndLatency` | Load probes | Steady State Small Message Throughput And Latency. | Release-only load probe; assertions are sanity/balance checks only. |
| `CompetingConnections_ThroughputAndLatency` | Load probes | Competing Connections Throughput And Latency. | Release-only load probe; assertions are sanity/balance checks only. |
| `ShutdownStorm_ConnectionChurn_AllocationAndTiming` | Load probes | Shutdown Storm Connection Churn Allocation And Timing. | Release-only load probe; assertions are sanity/balance checks only. |
| `IdleAfterLargeSend_ReleasedPayloadsAreCollectible` | Load probes | Idle After Large Send Released Payloads Are Collectible. | Release-only load probe; assertions are sanity/balance checks only. |

#### `TransportProfilingProbesTests` — Profiling matrix

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `ColdConnectionChurn_VsLongLived_AllocationPerConnection` | Profiling matrix | Cold Connection Churn Vs Long Lived Allocation Per Connection. | Release-only load probe; assertions are sanity/balance checks only. |
| `AmbientContext_SyncTransport_AndRuntimeCaptureCost` | Profiling matrix | Ambient Context Sync Transport And Runtime Capture Cost. | Release-only load probe; assertions are sanity/balance checks only. |
| `AmbientContext_RealSocketAsync_ByContextSize` | Profiling matrix | Ambient Context Real Socket Async By Context Size. | Release-only load probe; assertions are sanity/balance checks only. |
| `ManyPendingReceives_StorageBudgetAndPoolReturns` | Profiling matrix | Many Pending Receives Storage Budget And Pool Returns. | Release-only load probe; assertions are sanity/balance checks only. |
| `DelayedConsumerRetention_CachePressure_AndMisses` | Profiling matrix | Delayed Consumer Retention Cache Pressure And Misses. | Release-only load probe; assertions are sanity/balance checks only. |
| `ShutdownStorm_TimerEntriesAndCancellation` | Profiling matrix | Shutdown Storm Timer Entries And Cancellation. | Release-only load probe; assertions are sanity/balance checks only. |
| `CompetingConnections_NoFairnessBudget_ContentionAndLatency` | Profiling matrix | Competing Connections No Fairness Budget Contention And Latency. | Release-only load probe; assertions are sanity/balance checks only. |

### Harness self-tests

Harness self-tests validate the test infrastructure itself. They are optional, carry only `LowLevelTcpSocket.Harness`, and are not subject-contract coverage.

#### `AllocationMeasurerTests` — Allocation measurement calibration

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `AllocationMeasurer_KnownAllocationLoop_ReportsPositiveDelta` | Allocation measurement calibration | Allocation Measurer Known Allocation Loop Reports Positive Delta. | Optional harness self-test; .Harness category only. |
| `AllocationMeasurer_EmptyPreallocatedLoop_IsNearZero` | Allocation measurement calibration | Allocation Measurer Empty Preallocated Loop Is Near Zero. | Optional harness self-test; .Harness category only. |
| `AllocationMeasurer_RejectsThreadMigration` | Allocation measurement calibration | Allocation Measurer Rejects Thread Migration. | Optional harness self-test; .Harness category only. |

#### `ControllableQueueTests` — Controllable SPSC source

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `StrictSource_RejectsOverlappingReaders` | Controllable SPSC source | Strict Source Rejects Overlapping Readers. | Optional harness self-test; .Harness category only. |
| `StrictSource_RejectsOverlappingWriters` | Controllable SPSC source | Strict Source Rejects Overlapping Writers. | Optional harness self-test; .Harness category only. |
| `StrictSource_AllowsSingleReaderAndSingleWriter` | Controllable SPSC source | Strict Source Allows Single Reader And Single Writer. | Optional harness self-test; .Harness category only. |

#### `FakeSocketAccessorTests` — Scripted socket accessor contract

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `PendingArgs_CannotBePostedAgain` | Scripted socket accessor contract | Pending Args Cannot Be Posted Again. | Optional harness self-test; .Harness category only. |
| `CompletedArgs_CanBePostedAgain` | Scripted socket accessor contract | Completed Args Can Be Posted Again. | Optional harness self-test; .Harness category only. |
| `DoubleCompletion_IsReportedInStrictMode` | Scripted socket accessor contract | Double Completion Is Reported In Strict Mode. | Optional harness self-test; .Harness category only. |
| `SynchronousResult_DoesNotRaiseCompleted` | Scripted socket accessor contract | Synchronous Result Does Not Raise Completed. | Optional harness self-test; .Harness category only. |
| `AsynchronousCompletion_RaisesExactlyOneCallback` | Scripted socket accessor contract | Asynchronous Completion Raises Exactly One Callback. | Optional harness self-test; .Harness category only. |
| `CompletionOnAnotherThreadBeforePostingReturns_IsObservedOnce` | Scripted socket accessor contract | Completion On Another Thread Before Posting Returns Is Observed Once. | Optional harness self-test; .Harness category only. |
| `Dispose_DoesNotAutoCompletePendingOperations` | Scripted socket accessor contract | Dispose Does Not Auto Complete Pending Operations. | Optional harness self-test; .Harness category only. |
| `BoundedTeardown_CompletesOutstandingOperations` | Scripted socket accessor contract | Bounded Teardown Completes Outstanding Operations. | Optional harness self-test; .Harness category only. |

#### `HarnessSanityTests` — Harness wiring sanity

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Send_TransfersBufferAndReleasesItOnceWhenDrained` | Harness wiring sanity | Send Transfers Buffer And Releases It Once When Drained. | Optional harness self-test; .Harness category only. |
| `Receive_DeliversBytesToTheSinkAndReArms` | Harness wiring sanity | Receive Delivers Bytes To The Sink And Re Arms. | Optional harness self-test; .Harness category only. |

#### `NoCopyReceiveConsumerTests` — No-copy receive consumer

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `NoCopyConsumer_DoesNotAllocatePerDelivery` | No-copy receive consumer | No Copy Consumer Does Not Allocate Per Delivery. | Optional harness self-test; .Harness category only. |
| `CounterConsumer_ReleasesInFinally_WhenValidationThrows` | No-copy receive consumer | Counter Consumer Releases In Finally When Validation Throws. | Optional harness self-test; .Harness category only. |
| `CounterConsumer_DoesNotReleaseWhenConfiguredToRetain` | No-copy receive consumer | Counter Consumer Does Not Release When Configured To Retain. | Optional harness self-test; .Harness category only. |

#### `TestSocketAsyncEventArgsTests` — Manual SocketAsyncEventArgs completion

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Complete_SetsTransferAndRaisesCompleted` | Manual SocketAsyncEventArgs completion | Complete Sets Transfer And Raises Completed. | Optional harness self-test; .Harness category only. |
| `CompleteError_SetsErrorAndZeroTransferred` | Manual SocketAsyncEventArgs completion | Complete Error Sets Error And Zero Transferred. | Optional harness self-test; .Harness category only. |

#### `TrackingByteArrayTests` — Tracking buffer ref-count diagnostics

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `DoubleRelease_IsDetected_EvenWhenFinalReleaseCountIsOne` | Tracking buffer ref-count diagnostics | Double Release Is Detected Even When Final Release Count Is One. | Optional harness self-test; .Harness category only. |
| `BalancedAcquireRelease_RecordsSingleFinalReleaseAndZeroRefs` | Tracking buffer ref-count diagnostics | Balanced Acquire Release Records Single Final Release And Zero Refs. | Optional harness self-test; .Harness category only. |

## Planned coverage

No known missing tests or missing layers are currently recorded. New gaps discovered during review must be added here with the owning layer and a short reason.

## Deferred by design

| Gap | Reason |
| --- | --- |
| Real network conditions beyond deterministic loopback TCP and scripted socket failures | The suite is a deterministic contract suite, not a network-environment matrix. |
| Universal allocation numbers and wall-clock performance SLOs | Allocation and load probes are calibration/sanity checks; exact figures depend on runtime, JIT, and hardware. |
| Fairness, wall-clock latency budgets, and scheduler guarantees | The transport intentionally has no fairness or idle deadline; liveness and fairness belong to the application. |
| Production implementation changes | The suite asserts the current contract and does not own production code. |
