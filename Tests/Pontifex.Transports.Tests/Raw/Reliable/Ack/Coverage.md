# RawReliableAck Conformance Coverage

`RawReliableAckConformanceTests` is the carrier-independent conformance suite for
the RawReliableAck transport contract. Each implementation supplies a conformance
adapter that creates linked server-client topologies. The suite exercises every
normative requirement in the [RawReliableAck Specification](../../../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Ack/Specification.md).

## Covered

### Init lifecycle
| Specification area | Coverage |
| --- | --- |
| Null arguments | Null client handler and null server acknowledger each throw `ArgumentNullException` without changing transport state. |
| One-time init | Concurrent calls produce exactly one winner; every racing or later call returns false. A failed init terminally invalidates the transport. |
| Init-after-start | Calling `Init` on a successfully started transport returns false. |
| Start-before-init | Calling `Start` before successful `Init` returns false and terminally invalidates the transport. |

### Transport lifecycle
| Specification area | Coverage |
| --- | --- |
| Null Start callback | Throws `ArgumentNullException` without changing lifecycle state. |
| One-time Start | Concurrent calls produce at most one winner; every later call returns false. |
| Failed Start | `FailNextStart` causes false return, invalidation, and no `onStopped`. |
| Normal Stop | Stops the transport, `IsValid` stays true, `IsStarted` becomes false, `onStopped` invoked exactly once. |
| Stop-before-Start | No-op, returns true, does not consume the one-time Start. |
| Start-after-Stop | A stopped transport refuses `Start` and remains invalid. |
| Unrecoverable failure | `InjectUnrecoverableFailure` invalidates the transport and invokes `onStopped` exactly once; stopped transport rejects further `Stop`. |
| Concurrent status reads | `IsValid`, `IsStarted`, `IsConnected`, `RemoteEndPoint`, and `MessageMaxByteSize` are safe for concurrent reads during stop. |

### Client lifecycle
| Specification area | Coverage |
| --- | --- |
| Successful connection | `OnConnected` → `OnDisconnected` → `OnStopped` occur exactly once in that order, with the same local disconnect reason. |
| Failed establishment | Exactly one `OnStopped` occurs; `OnConnected` and `OnDisconnected` never occur. |
| Endpoint in OnConnected | During `OnConnected`: `IsConnected` is true, `RemoteEndPoint` is non-null, `Send` and `Disconnect` are valid. |
| Disconnected endpoint | During and after `OnDisconnected`: `IsConnected` is false, `RemoteEndPoint` is null, `OnReceived` never occurs. |
| Client start without server | `Start` succeeds but no `OnConnected` occurs until a server is available and completes the handshake. |
| FillAckData exception | Terminates the connection with `ExceptionFail`; client receives `OnStopped` without `OnConnected`. |
| Oversized ACK data | Establishment fails; client receives `OnStopped` without `OnConnected`. |

### Server session lifecycle
| Specification area | Coverage |
| --- | --- |
| Successful acceptance | `OnConnected` → `OnDisconnected` occur exactly once per session in that order. |
| No OnStopped for server | Server handler never receives an `OnStopped` callback. |
| Handler freshness | Each accepted session receives a distinct handler instance; a handler never serves multiple sessions. |
| Session independence | Sessions are independent; stopping the server transport disconnects all sessions. |
| Session cleanup on reconnection | After a session disconnects, a new connection from the same source triggers a fresh `TryAck` and creates a distinct handler. Old handlers never receive additional callbacks after their `OnDisconnected`. |

### ACK handshake
| Specification area | Coverage |
| --- | --- |
| TryAck invocation | `TryAck` is called for each valid connection attempt; receives the client's ACK data. |
| TryAck serialization | Calls to `TryAck` on a single server never overlap concurrently. |
| TryAck rejection | Returning null rejects the connection; a later attempt from the same client invokes `TryAck` again. |
| TryAck acceptance | Returning a handler creates a session; the server invokes `FillAckResponse` before `OnConnected`. |
| TryAck exception | Caught and logged; the individual connection attempt fails without stopping the transport. |
| FillAckResponse exception | Establishment fails; the pre-connected server handler receives no lifecycle callback; client receives `OnStopped` without `OnConnected`. |
| FillAckResponse ownership | The handler populates a transport-owned buffer; must not retain or release it. |
| FillAckData ownership | The handler populates a transport-owned buffer; must not retain or release it. |
| Oversized ACK data/response | Handshake data exceeding `MessageMaxByteSize` causes establishment failure with the specified callback sequences. |
| AckResponse in OnConnected | The client receives the server's ACK response in `OnConnected`; it owns and must release the buffer. |
| Server OnConnected ordering | Server `OnConnected` is invoked only after the ACK response is accepted for outbound delivery. |
| ACK response lost before commit | If a failure occurs between `FillAckResponse` returning and the ACK response being committed for outbound delivery, the server handler receives no lifecycle callback and the client receives `OnStopped` without `OnConnected`. |

### Send and SendResult
| Specification area | Coverage |
| --- | --- |
| Send Ok | The transport accepts the message; ownership transfers unconditionally. |
| Send NotConnected | Returned when the endpoint is not connected; buffer is consumed according to ownership rules. |
| Send InvalidMessage | Returned for null; does not affect connection state. |
| Send MessageTooBig | Returned when serialized message exceeds `MessageMaxByteSize`; does not affect connection state. |
| Send BufferOverflow | Non-fatal; a later send with a newly-created buffer may succeed. |
| Send Error | Returned when the endpoint or transport is unavailable after normal stop. |
| Concurrent Send | Thread-safe; every accepted message is eventually delivered. |
| Send ownership | Every invocation transfers ownership of the buffer regardless of result. |
| Async failure after Ok | If an asynchronous failure occurs after Ok, the logical connection is destroyed and `OnDisconnected` raised. |
| Send/Disconnect race | Either linearizes first; `Send` may return `Ok` and be discarded by teardown, or `NotConnected` if teardown wins. |

### Delivery
| Specification area | Coverage |
| --- | --- |
| At-most-once | No accepted regular message is delivered more than once. |
| FIFO order, client-to-server | Delivered messages preserve accepted-send order (directional guarantee). |
| FIFO order, server-to-client | Delivered messages preserve accepted-send order (directional guarantee). |
| Contiguous prefix after termination | If termination interrupts delivery, the peer receives only a contiguous prefix of accepted sends (verified using `BeforeSendCommitHitCount` and gate arming to cut delivery at a known boundary). |
| Empty messages | Empty `UnionDataList` payloads are valid and delivered normally. |
| Complex content, both directions | Arbitrary content is preserved through send/delivery. |
| Exact-limit messages | Messages at exact `MessageMaxByteSize` are accepted and delivered. |
| Handshake isolation | ACK data, ACK responses, and transport-control traffic are never delivered through `OnReceived`. |
| Message boundaries | Each `OnReceived` delivers exactly one complete message; no fragmentation or merging. |
| Consistent MessageMaxByteSize | Client and server endpoints for one connection report identical `MessageMaxByteSize`. |
| Malformed inbound data | `InjectInboundData` supplies deliberately malformed or oversized data; the transport must discard, log, and disconnect the connection without stopping the transport. |
| Valid injected inbound data | `InjectInboundData` with a well-formed `UnionDataList` delivers it through the normal `OnReceived` path, subject to serialization and ordering guarantees. |

### Disconnect
| Specification area | Coverage |
| --- | --- |
| One-time Disconnect | Returns true once, then false on subsequent calls. |
| Reason propagation | The caller's reason reaches `OnDisconnected` and (for client) `OnStopped` as the exact same instance. |
| Client Disconnect stops transport | Disconnecting the single client endpoint stops the client transport. |
| Server Disconnect keeps server | Disconnecting a server session endpoint does not stop the server transport. |
| Disconnect from callback | `Disconnect` called from `OnConnected` or `OnReceived` defers `OnDisconnected` until the callback returns. |
| Null Disconnect reason | Supplies an `Unknown` reason to `OnDisconnected`. |
| Disconnect post-disconnect | Returns false; original reason and teardown are unchanged. |

### Callback serialization and safety
| Specification area | Coverage |
| --- | --- |
| Per-connection serialization | All callbacks for one connection are serialized and non-reentrant. |
| TryAck global serialization | Two `TryAck` invocations never overlap concurrently. |
| Cross-session concurrency | Callbacks for different server sessions may execute concurrently; shared state must be thread-safe. |
| Handler exception — OnConnected | Terminates the affected connection with `ExceptionFail` containing the exception instance. |
| Handler exception — OnReceived | Catches the exception, suppresses further processing of that delivery, and terminates the connection. |
| Handler exception — OnDisconnected / OnStopped | Caught and logged; no duplicate callbacks created; teardown continues. |
| Handler exception — FillAckData | Terminates the connection; client receives `OnStopped` without `OnConnected`. |
| Handler exception — TryAck | Caught and logged; individual connection attempt fails without stopping the transport. |
| Handler exception — FillAckResponse | Fails establishment; client receives `OnStopped` without `OnConnected`. |
| Stop from callback | `Stop` may be called from any handler callback without deadlock or reentrancy. |
| OnReceived not before OnConnected | `OnReceived` is never invoked before `OnConnected` returns successfully. |

### Transport shutdown
| Specification area | Coverage |
| --- | --- |
| Client Stop (connected) | `OnDisconnected(reason)` then `OnStopped(reason)` with exact supplied `StopReason` instance. |
| Client Stop (pre-connect) | Exactly one `OnStopped(reason)` without `OnDisconnected`. |
| Server Stop | Stops accepting new clients; every active session receives `OnDisconnected` before server-level `onStopped`. |
| Server Stop timing | All affected session `OnDisconnected` callbacks are scheduled before `onStopped` begins. |

### Conformance controls — transport
| Specification area | Coverage |
| --- | --- |
| BeforeAcknowledgerGate | Armed gate blocks `TryAck` invocation; hit exactly once per valid connection attempt on server. |
| BeforeAckResponseCommitGate | Armed gate blocks the ACK response from being accepted for outbound delivery after `FillAckResponse` returns; hit on server only. Enables testing mid-handshake establishment failure. |
| BeforeHandlerConnectedGate | Armed gate blocks `OnConnected` for both client and server; hit after handshake succeeds but before callback. |
| BeforeStopStateTransitionGate | Armed gate blocks the Running → Stopping state transition during `Stop`. |
| BeforeStoppedCallbackGate | Armed gate blocks the `onStopped` callback dispatch. |
| FailNextStart | Next `Start` returns false, invalidates transport, and does not invoke `onStopped`. |
| InjectUnrecoverableFailure | Stops and invalidates the transport, disconnects all sessions, invokes `onStopped` exactly once. |

### Conformance controls — endpoint
| Specification area | Coverage |
| --- | --- |
| BeforeEndpointDisconnectStateTransitionGate | Armed gate blocks the endpoint's transition to disconnected; `IsConnected` still reads true. |
| BeforeHandlerDisconnectedGate | Armed gate blocks `OnDisconnected` callback; endpoint is already disconnected. |
| BeforeHandlerStoppedGate | Armed gate blocks client `OnStopped` callback (not triggered for server sessions). |
| BeforeSendCommitGate | Armed gate blocks message from reaching the underlying IO commit attempt. |
| AfterSendCommitGate | Armed gate blocks after an endpoint message completes an IO commit attempt. |
| AfterReceivedGate | Armed gate blocks `OnReceived` invocation per impending delivery. |
| BeforeSendCommitHitCount | Monotonic counter of how many messages hit `BeforeSendCommitGate`; enables contiguous-prefix verification. |
| AfterSendCommitHitCount | Monotonic counter of how many messages hit `AfterSendCommitGate`; enables commit-boundary verification. |
| AfterReceivedHitCount | Monotonic counter of received-message deliveries; enables delivery-count verification. |
| InjectInboundData | Injects valid, malformed, or oversized data into the receive path; verifies delivery, discard, and disconnect behavior without carrier-level manipulation. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Buffer ownership verification (TryAck release, callback release, no-post-Send access) | Requires the planned custom `IMemoryRental` test implementation. |
| Logging verification (exceptions caught and logged by transport) | Requires custom observable `ILogSink`; partially addressable via `RawReliableAckConformanceFixtureOptions.Logger`. |
| BufferOverflow backpressure behavior | Queue capacity and drain rate are implementation-defined; the suite asserts only that `BufferOverflow` is non-fatal. |
| SendResult precedence ordering | No control over outbound queue fill-to-capacity without implementation-defined queue limits. |
| Carrier submission timing and scheduler affinity | Implementation-defined; not tested. |
| Client-visible server-rejection signaling | Implementation-defined; only the callback-sequence consequences of rejection are asserted. |
| Remote peer stop reason | Implementation-defined; the suite asserts local-reason propagation, not remote-reason identity. |
| Timeout and keep-alive policy | Explicitly implementation-defined; no test injects timeouts. |
| Wire format and carrier encoding | Explicitly implementation-defined; no wire-level conformance is tested. |
| Security properties (authentication, confidentiality, integrity) | Explicitly out of scope for this transport contract. |
| Endpoint equality and hash consistency | Not defined by this contract. |
| True loss, duplication, and reordering | The reliable transport contract specifies exactly-once delivery; the suite verifies at-most-once and ordering but relies on the implementation not to inject loss at the conformance layer. |
| OnConnected-window callback isolation | Verifying that `OnReceived` cannot begin while `OnConnected` is still executing (not just before it) requires a gate that blocks during callback execution rather than before it. |
| OnConnected-window carrier data buffering | Verifying that regular messages arriving during window are buffered (not dropped) and delivered after `OnConnected` requires access to carrier-level timing controls. |
