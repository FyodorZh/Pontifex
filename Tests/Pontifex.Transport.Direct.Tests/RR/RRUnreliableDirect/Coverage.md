# RRUnreliable Direct Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | RR unreliable Direct group |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/RR/RRUnreliableDirect/` |
| Inheritance parent | none |
| Dependency | The carrier classes are internal; the suite relies on the Direct assembly's `InternalsVisibleTo` entry. |
| Contract | [RRUnreliable](../../../../Pontifex/Abstractions/Transports/RR/Unreliable/Specification.md), [Direct RRUnreliable](../../../../Transports/Pontifex.Transport.Direct/RR/Unreliable/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `RequestAndResponse_RoundTripInProcess` | Request/response correlation | A request reaches the serialized server handler and its response reaches the bound client handler | The channel and the dispatch queue deliver asynchronously, so the test waits on signals. |
| `ResponseHandle_IsSingleUse` | Single-use response handle | The first response closes the handle; the second returns `Error` | The handle closes before the carrier send. |
| `RejectedRequest_ProducesNoCallbackAndExpiresSilently` | Silent rejection | A rejected request provides no callback and is swept by a later request | The sweep runs while the later request is added to the pending table. |
| `CancelledRequest_ProducesNoCallback` | Local cancellation | `Cancel` removes the pending entry without a callback | The clock does not advance; the later request only proves the old entry is gone. |
| `ExpiredRequest_ProducesNoCallback` | Request TTL | An expired pending entry is swept without a callback | Uses the manual clock and a later request as the sweep trigger. |
| `InvalidPayload_ReturnsInvalidMessage` | Send-result precedence | A null payload returns `InvalidMessage` and leaves the handler reusable | The core releases no null buffer. |
| `OversizedPayload_ReturnsMessageTooBigAndReleasesTheFrame` | Payload limit | A payload above `1048576` bytes returns `MessageTooBig` and is released | The limit is checked on the payload before the request-id header is added. |
| `ExactlyMaximumPayload_RoundTripsInBothDirections` | Inclusive payload limit | An exactly-`1048576`-byte request and response round-trip | Confirms the inbound check is payload-only after the header is removed. |
| `ServerInit_IsOneTimeAndRejectsNull` | Server `Init` | A null handler throws; only the first `Init` succeeds | The server does not start carrier activity in `Init`. |
| `ClientStart_IsOneTime` | One-time start | The first client start succeeds; the second returns false and the client remains running | Client transports have no `Init`. |
| `ServerInitAfterStart_IsRejectedAndTheServerStaysUsable` | Init after start | `Init` after a successful start returns false and does not disturb the running server | A later request still reaches the first handler. |
| `ServerStartWithoutInit_ReturnsFalseAndInvalidates` | Start precondition | Starting an uninitialized server returns false and invalidates it | No `onStopped` runs for a failed start. |
| `DuplicateServerId_SecondStartFailsAndFirstServerStaysUsable` | Process-wide id registry | A duplicate id fails the second start and leaves the first server serving | The registry is process-wide, so the id is unique per test. |
| `Stop_ClearsThePendingWindowWithoutCallbacks` | Stop clears pending work | Stopping the client unbinds the pending request without a callback | A later `Request` returns `NotConnected`. |
| `NoServer_RequestIsAcceptedAndExpiresThenALaterServerServes` | No-peer policy | Without a server a request returns `Ok`; a later server serves the next request | The client stays valid and reconnects lazily. |
| `RequestBeforeOnStarted_IsPresentedExactlyOnceAfterTheGateOpens` | Start gate | No callback begins before `OnStarted`; held and concurrent requests are presented once and in send order | The callback may run before `Start` returns to the caller; the test records the observation and accepts the declared deviation. |
| `PreStartPendingList_ReleasesThe101stFrame` | Bounded pre-start pending list | The first `100` requests are held; the `101`st is released | The bound is the constant `100`, not the queue capacity. |
| `RejectingTheLiveChannel_PurgesTheRegistryAndAllowsReconnect` | Channel rejection | A rejected channel leaves no registry entry; the next request reconnects, and a restarted server serves a later request | The reject hook is the same path the carrier uses. |
| `DisposingTheLiveChannel_IsALossAndTheNextRequestReconnects` | Channel loss | A disposed channel clears the client carrier, purges the registry entry, and a later request reconnects | No transport failure occurs for the unreliable family. |
| `ServerHandler_IsNeverConcurrent` | Serialized server handler | Two clients reach one server handler with a maximum concurrency of one | The dispatch queue provides serialization; the test blocks the first callback. |
| `TrackingFactories_CreateFreshSystemsAfterAReconnect` | Fresh delivery systems | Each connect creates two new systems; the loss releases the previous pair | Systems are tracked by identity; the channel owns installed systems. |
| `DuplicateResponse_YieldsExactlyOneCallback` | Duplicate suppression | A duplicated response produces one client callback | The scripted system clones the second copy. |
| `DroppedRequest_ExpiresSilently` | Carrier loss | A dropped request never reaches the server and expires with no callback | The second request triggers the sweep after the manual clock advances. |
| `ThrowingDeliverySystem_ReturnsErrorAndReleasesTheFrame` | Delivery exception | A throwing system yields `Error`, releases the frame, and leaves the transport usable | The transport is not invalidated for an unreliable send failure. |
| `FullDispatchQueue_ReleasesTheFrameWithoutBlocking` | Dispatch capacity | A frame posted to a full dispatch queue is released without blocking the sender | The queue capacity is configured to `2` for the test. |
| `FramesQueuedAtStop_AreReleased` | Stop releases queued frames | Stopping the server releases frames still waiting in the dispatch queue | The running callback is released after the assertion; queue disposal is non-blocking. |
| `FrameDeliveredAfterStop_IsReleasedExactlyOnce` | Stop releases late frames | A frame delivered through the channel server handler after the stop snapshot is released once and never reaches `OnRequested` | The handler is captured from the channel and invoked directly after `Stop`, so no timing race is involved. |
| `ResponseTtl_StartsAtReceiptBeforeTheDispatchQueueRuns` | Response TTL timestamp | A request that waited in the dispatch queue is presented with an already-closed handle after the clock advances | Proves the TTL starts at receipt, not at dequeue; the presentation deviation is declared in §7. |
| `PendingFrameAtStop_IsReleasedAndStartReportsFalse` | Stop releases pre-start frames | A pending pre-`OnStarted` frame is released and the interrupted start returns false | The stop is invoked reentrantly from the start gate; this is a white-box lifecycle test. |
| `CarrierWithoutPeer_ReturnsTheNoPeerPolicyAndReportsLiveSessionOnce` | No-peer policy and live session | `acceptWithoutPeer: false` returns `Error`; the live-session callback fires once on the first successful send | A direct carrier test, because the unreliable transport always accepts without a peer. |
| `SystemInstalledOnARejectedChannel_IsReleased` | Install/reject ownership | Systems installed on a channel disposed before install are released | The test-owned registry entry disposes the channel inside `Connect`. |
| `ChannelDisposedFromADeliveryFactory_ReleasesEveryCreatedSystemAndTheFrame` | Late-install ownership | Systems created after a channel disposal are released exactly once and the request frame is released once | The factory disposes the channel, so the disposal cannot release the systems installed afterwards. |
| `TrySendToARemovedDestination_ReturnsErrorAndReleasesTheFrame` | Removed destination | A frame for a removed route returns `Error` and is released | The registry removal is driven through `ChannelManager.Disconnect`. |
| `ServerStop_ClearsTheRegisteredClientCount` | Server stop | Stopping the carrier disposes registered channels and clears the count | The registration entry is removed before the count assertion. |
| `SendAfterChannelLoss_ReturnsTheNoPeerPolicyNotNotConnected` | No-peer policy | A carrier send after its server stopped returns `Ok`, never `NotConnected` | The carrier's `acceptWithoutPeer` policy hides the missing peer. |
| `CreateClient_WithoutParameters_UsesTheThreeSecondDefault` | Binding defaults | The client request TTL is three seconds and the byte limit is `1048576` | Uses the hand-written build context. |
| `CreateServer_WithoutParameters_UsesTheThreeSecondDefault` | Binding defaults | The server response TTL is three seconds and the byte limit is `1048576` | Uses the hand-written build context. |
| `Create_WithExplicitTtls_UsesThem` | Binding TTLs | Explicit whole-second TTLs reach the transport | Client and server are created separately. |
| `CreateServer_WithRequestTtl_Throws` | Role validation | A client-only TTL on the server is rejected | `ArgumentException` is the binding error shape. |
| `CreateClient_WithResponseTtl_Throws` | Role validation | A server-only TTL on the client is rejected | `InvalidOperationException` matches the UDP binding shape. |
| `CreateServer_WithDelivery_Throws` | Role validation | `delivery` on the server is rejected | `delivery` is client-only. |
| `CreateClient_WithUnknownDeliveryType_Throws` | Delivery validation | An unknown delivery type is rejected eagerly | No delivery system is constructed. |
| `CreateClient_WithFourStateWithoutParameters_ThrowsEagerly` | Loss-model validation | `fourState` without its parameter struct is a build error | Shares the netem loss-model check. |
| `CreateClient_WithGilbertElliotWithoutParameters_ThrowsEagerly` | Loss-model validation | `gilbertElliot` without its parameter struct is a build error | Shares the netem loss-model check. |
| `CreateClient_WithPerfectDelivery_ProducesAStartedTransport` | Binding construction | A perfect-delivery client starts and stops | No channel is created before the first send. |
| `BindingCreatedPair_RoundTripsARequest` | Integration | A client and server compiled through the real binding exchange a request and response | Category `Direct.RRUnreliable.Integration`; bounded signal waits. |
| `BindingCreatedClient_WithNetemReconnectsAfterAChannelLoss` | Integration and reconnect | A real netem client reconnects after its channel is disposed and serves a second request | Category `Direct.RRUnreliable.Integration`; real netem teardown is bounded by its five-second join. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Defensive `SendResult.NotConnected` translation from a live channel | Root — unreachable by a public seam: `Channel.SendToServer` reports `NotConnected` only after disposal, which the disposal-notification path already covers. The purge path is recorded as covered by the rejection and disposal tests. |
| Binding-created fresh systems per connect | Root — covered indirectly by `TrackingFactories_CreateFreshSystemsAfterAReconnect` at the carrier level. |
| Duplicate request presentation to the server handler | Root — the RRUnreliable type permits it; the Direct carrier has no duplicate suppression. |
| Wave/storm concurrency and allocation probes | Root — non-default facets belong in a separate load group. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timing and sleeps for TTL tests | The manual clock drives both TTLs; expiry is triggered by a later request. |
| Netem statistical loss and reorder | The VirtualDelivery suite owns the algorithms; this suite uses scripted systems for exact drops and duplicates. |
| Prompt netem teardown | `NetemDeliverySystem.Dispose` joins its dequeue thread for up to five seconds; the integration test accepts the bounded delay. |
| Callback scheduling before `onStopped` completes | Queue disposal is non-blocking; an already running `OnRequested` may finish after `onStopped` per the declared deviation. |
