# RRReliable Direct Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | RR reliable Direct group |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/RR/RRReliableDirect/` |
| Inheritance parent | none |
| Dependency | The carrier classes are internal; the suite relies on the Direct assembly's `InternalsVisibleTo` entry and the loose `RR/TestSupport/ManualTimerFactory.cs`. |
| Contract | [RRReliable](../../../../Pontifex/Abstractions/Transports/RR/Reliable/Specification.md), [Direct RRReliable](../../../../Transports/Pontifex.Transport.Direct/RR/Reliable/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `MultipleOrderedResponsesAndNormalCompletion_ReachTheClientInOrder` | Ordered response stream and normal completion | Two responses and one `Finish(Normal)` arrive in send order | The channel delivers asynchronously, so the test waits on bounded signals. |
| `Reject_ProducesOneRejectedCompletionAndNoResponse` | Rejection completion | A rejected request yields exactly one `Finish(Rejected)` and no response | The rejection frame travels through the same channel. |
| `Cancel_ProducesOneCanceledCompletion` | Client cancellation | `Cancel` yields one `Finish(Canceled)` and suppresses responses | The best-effort cancel frame closes the server handle. |
| `RequestTtl_ProducesOneFailedCompletion` | Request TTL | Firing the manual request timer yields one `Finish(Failed)` | Manual timer, no real time. |
| `ResponseTtl_ProducesOneFailedCompletionAndDisposesTheRequestTimer` | Response TTL | Firing the server response timer sends `Finish(Failed)` and the client disposes its request timer | The server and client use separate manual factories so each side's timer is identified. |
| `NoServer_RequestReturnsErrorWithoutCallbacksAndALaterServerServes` | No-peer policy | A request without a server returns `Error`, delivers no callback, arms no timer, and leaves the client valid; a later server serves the next request | The carrier releases the frame and never reports `NotConnected`. |
| `SynchronousCancelInsideAResponseCallback_ProducesOneCanceledCompletion` | Cancellation inside a callback | A synchronous `Cancel` from inside `Response` completes without deadlock and yields one `Finish(Canceled)` | The terminal notification is deferred until the callback returns. |
| `HandlerReuseAfterFinish_IsAccepted` | Handler reuse | A handler may be bound again after its first `Finish` returns | The handler keeps its last request id after unbinding. |
| `PayloadBoundaries_EnforceTheInclusiveMaximum` | Payload limit | An exactly-maximum payload round-trips; the next byte returns `MessageTooBig` and releases the payload | The limit is payload-only; the reliable header is outside it. |
| `TwoConcurrentRequests_SecondAnsweredFirst_KeepSeparateCompletionsAndIds` | Concurrent correlation | Two requests from one client receive their own responses and completions even when answered out of order, with distinct server ids | Server handles are captured from `OnRequested` and answered by the test. |
| `TwoClients_AreIndependent` | Per-client sessions | Two clients reach one server and each receives its own response and completion | The server records distinct source routes. |
| `ClientStart_IsOneTime` | One-time start | The first client start succeeds; the second returns false | Client transports have no `Init`. |
| `ServerInit_IsOneTimeAndRejectsNull` | Server `Init` | A null handler throws; only the first `Init` succeeds | The server does not start carrier activity in `Init`. |
| `ServerStartWithoutInit_ReturnsFalseAndInvalidates` | Start precondition | Starting an uninitialized server returns false and invalidates it | `StartCarrier` never runs. |
| `ServerInitAfterStart_IsRejectedAndTheServerStaysUsable` | Init after start | `Init` after a successful start returns false and does not disturb the running server | A later request still reaches the first handler. |
| `DuplicateServerId_SecondStartFailsAndFirstServerStaysUsable` | Process-wide id registry | A duplicate id fails the second start and leaves the first server serving | The registry is process-wide, so each test uses a fresh id. |
| `NormalStop_SchedulesOneFailedCompletionAndLeavesTheTransportValid` | Stop scheduling and validity | Stop delivers one `Finish(Failed)` and leaves `IsValid` true | The terminal notification is scheduled by `ClearPendingRequests`; the test waits on the callback. |
| `NormalStopThenChannelDisposal_LeavesIsValidTrueAndDoesNotFailAgain` | Disposal after stop | Disposing a captured channel after `Stop` neither invalidates the transport nor repeats the stop callback | The stop already unsubscribed and disposed the carrier channel. |
| `ChannelDisposalAfterALiveSession_FailsOnceAndFinishesPendingRequests` | Channel-loss failure | A live channel disposal reports `TextFail`, invalidates the transport, delivers one `Finish(Failed)`, and closes the server session | `Dispose` notifies the server subscription synchronously, so the retained handle is already closed when the event returns. |
| `ChannelDisposalBeforeAnySuccessfulSend_IsNotAFailureAndALaterServerServes` | Pre-session loss | A channel disposed during connect returns `Error` without failing the transport or scheduling a callback; a later server serves the next request | A test-owned `ChannelManager` entry disposes the created channel immediately. |
| `CreateClient_WithoutParameters_UsesTheThreeSecondDefault` | Binding defaults | The client request TTL is three seconds and the byte limit is the shared Direct maximum | The Direct friend assembly asserts `RRDirectInfo.MessageMaxByteSize`. |
| `CreateServer_WithoutParameters_UsesTheThreeSecondDefault` | Binding defaults | The server response TTL is three seconds and the byte limit is the shared Direct maximum | The server has no request TTL. |
| `Create_WithExplicitTtls_UsesThem` | Binding TTLs | Explicit whole-second TTLs reach the transport | Client and server are created separately. |
| `CreateServer_WithRequestTtl_Throws` | Role validation | A client-only TTL on the server is rejected | `ArgumentException` matches the RRUnreliable Direct binding. |
| `CreateClient_WithResponseTtl_Throws` | Role validation | A server-only TTL on the client is rejected | `InvalidOperationException` matches the RRUnreliable Direct binding. |
| `CreateRegistration_DeclaresTheReliableScheme` | Registration metadata | The registration declares `direct-rr-reliable` and `TransportType.RRReliable` | The binding registers no `delivery` parameter. |
| `BindingCreatedPair_RoundTripsARequestAndCompletion` | Integration | A client and server compiled through the real binding exchange a request, response, and completion | Category `Direct.RRReliable.Integration`; bounded signal waits; the binding uses the system timer, but the request completes well before the TTL. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Defensive `SendResult.NotConnected` translation from a live channel | Root — unreachable by a public seam: `Channel.SendToServer` reports `NotConnected` only after disposal, which the disposal-notification test already covers. |
| Post-start dispatch-queue saturation through the Direct carrier | Root — the core server layer already proves the bounded queue; the Direct suite adds only the pre-start path. |
| Exhausting the 65,536-request duplicate window through the Direct carrier | Root — the core server layer proves the bitmap cap; an end-to-end run would need 65,536 requests. |
| Late `OnRequested` finishing after `onStopped` | Root — the core server layer proves the non-blocking disposal; the Direct suite cannot make the race deterministic without a production seam that the plan does not add. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real-time TTL expiry | The manual timer factory fires both TTLs deterministically; the integration test uses the system timer only for a fast round trip. |
| Virtual delivery and netem | The reliable binding exposes no delivery parameter; the channel always uses perfect delivery. |
| Carrier retransmission and duplicate frames | The in-process channel is ordered and lossless; the core suites cover scripted duplicates and reordering. |
| A second transport lifetime after a channel loss | The carrier fails after a live-session loss; reconnect is explicitly out of scope. |
