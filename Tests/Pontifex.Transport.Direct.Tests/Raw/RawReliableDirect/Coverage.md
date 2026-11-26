# RawReliable Direct Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | Raw reliable Direct group |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/Raw/RawReliableDirect/` |
| Inheritance parent | none |
| Dependency | The carrier classes are internal; the suite relies on the Direct assembly's `InternalsVisibleTo` entry and on the shared loose `TestSupport/TestBuildContext.cs`. Handshake deadlines use the production `Pontifex.Utils.ManualTimerFactory`. |
| Contract | [RawReliable](../../../../Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md), [Direct RawReliable](../../../../Transports/Pontifex.Transport.Direct/Raw/Reliable/Specification.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `HandshakeMaximum_AcceptsIntMaxValueWhenMessageMaximumAllowsIt` | §5 parameter range | The handshake maximum accepts `int.MaxValue` when the message maximum also allows it | Both values are applied through the binding and read from the server. |
| `HandshakeMaximum_RejectsIntMaxValuePlusOne` | §5 parameter range | One byte above `int.MaxValue` is rejected | The rejection is a construction failure, not a deferred send failure. |
| `HandshakeMaximum_AboveTheDefaultMessageMaximum_IsRejected` | §5 consistency rule | A handshake maximum above the default message maximum is rejected | The check runs at binding creation. |
| `HandshakeMaximum_AboveAConfiguredMessageMaximum_IsRejected` | §5 consistency rule | A handshake maximum above an explicitly configured message maximum is rejected | The configured message maximum is the comparison source. |
| `Create_AppliesTheMessageBufferAndTimeoutParameters` | §5 parameters | The message maximum, buffer capacity, and handshake timeout reach the client transport | The timeout is converted from whole seconds. |
| `Create_WithoutTheNewParameters_UsesTheDocumentedDefaults` | §5 defaults | The documented defaults are 1 MiB message maximum, 100 MiB buffer, and a 10-second timeout | Client and server defaults are asserted together. |
| `HandshakeTimeout_Zero_IsRejectedByTheDeclaredRange` | §5 positive range | A zero handshake timeout is rejected by the binding | The binding declares the same positive range as the constructors. |
| `PublicClientConstructor_RejectsANonPositiveOrInfiniteDeadline` | §5 constructor guard | The public client constructor rejects zero and infinite deadlines | The internal test constructor may still disable the deadline. |
| `PublicServerConstructor_RejectsANonPositiveOrInfiniteDeadline` | §5 constructor guard | The public server constructor rejects zero and infinite deadlines | Companion to the client constructor. |
| `Successful_handshake_notifies_both_handlers` | §4 handshake, §6 lifecycle | Both handlers receive one connect notification and both deadlines are disposed | Integration group; the client uses the manual handshake timer and a bounded connect wait. |
| `Client_handshake_timeout_fires_before_a_response` | §4 client deadline | Firing the client deadline stops the client with `TimeOut` before any response | Integration group; a test-owned registry entry holds the handshake open. |
| `Server_handshake_timeout_fires_before_a_request` | §4 server deadline | Firing the server deadline closes the channel without invoking the factory | Integration group; the test waits for the server to schedule its timer first. |
| `Handshake_frames_are_not_delivered_to_the_handlers` | §4 framing | Request and response frames never reach the application handlers | Integration group; proves the type byte is consumed by the carrier. |
| `Messages_are_delivered_in_both_directions_in_order` | §6 delivery | Ten messages in each direction arrive exactly once and in order | Integration group; bounded waits cover the channel hand-off. |
| `Version_mismatch_stops_the_client_with_ProtocolMismatch` | §4 validation and mapping | A wrong server version stops the client with `ProtocolMismatch` and no connect | Integration group; the synchronous reject wins the race against channel disposal. |
| `Name_mismatch_stops_the_client_with_ProtocolMismatch_and_skips_the_factory` | §4 validation order | A wrong handshake name stops the client with `ProtocolMismatch` before the factory runs | Integration group; the factory-call count proves the order. |
| `Rejected_client_stops_with_HandshakeRejected` | §4 rejection mapping | A factory rejection stops the client with `HandshakeRejected` while the server stays up | Integration group; rejection is an ordinary connection outcome. |
| `Message_max_size_is_negotiated_as_the_minimum` | §4 negotiation | Both endpoints apply the smaller of the requested and local message maxima | Integration group; both directions reject an oversized payload after negotiation and the connection survives. |
| `Rejected_client_closes_with_zero_linger` | §6 zero linger | The reject frame is delivered synchronously and the channel closes with no linger delay | Integration group; the 100 ms upper bound is a real-time guard, not a timing contract. |
| `Direct_client_uses_and_disposes_its_handshake_timer` | §4 and §6 deadline ownership | A completed handshake schedules a deadline and disposes it | Integration group; the manual factory exposes both the scheduled and pending counts. |
| `Client_disconnect_notifies_the_server_session` | §6 teardown | A client disconnect reaches the server session as `UnknownRemoteIntention` and the server stays up | Integration group; the handler's stop reason preserves the caller's instance. |
| `Server_stop_notifies_connected_clients` | §6 server stop | A server stop notifies connected clients with `UnknownRemoteIntention` | Integration group; the client handler observes disconnect and stop. |
| `Client_start_without_server_stops_without_connecting` | §6 missing server | A client with no registered server stops without connecting and stays valid | Integration group; a missing server is not a failed `Start`. |
| `Oversized_message_is_rejected_and_the_connection_survives` | §6 message limit | A payload above the limit returns `MessageTooBig` and later traffic still flows | Integration group; the connection is still connected after the rejection. |
| `Client_transport_stop_notifies_the_handler_and_the_server_session` | §6 teardown | A client transport stop preserves the supplied reason to the handler and disconnects the server session | Integration group; the server stays running. |
| `Many_clients_connect_concurrently` | §6 registry and session routing | Sixty-four concurrent clients each create one session and each session receives its message | Integration group; the test uses parallel starts and bounded waits. |
| `ServerHandler_ReceivesOnDisconnectedThenOnStopped_OnNormalStop` | §6 server session lifecycle | A normal server session stop delivers `connected`, `disconnected`, then `stopped` in order | Integration group; the handler records the event sequence by lock. |
| `Payload_at_exact_message_max_is_accepted_in_both_directions` | §6 message limit | A payload at exactly the message maximum is accepted; one extra byte is rejected | Integration group; the test constructs exact-size lists through the transport memory. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Load, allocation, and sustained-concurrency probes | Root — non-default facets; they need a separate declared load group. |
| Binding registration, canonicalization, and formula defaults for `direct(...)` | `Tests/Pontifex.Transports.Tests/FactoryBindingsTests.cs` — factory-level integration coverage outside this suite. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real-time handshake expiry | The suite injects the production `Pontifex.Utils.ManualTimerFactory`; the system timer is never waited on. |
| Pre-connect payload buffering, malformed frames, and unknown frame types | The abstract endpoint suites under `Tests/Pontifex.Tests/Endpoints/RawReliable` own those endpoint contracts. |
| Reconnect after a stop or a rejection | The `RawReliable` contract treats both as terminal connection outcomes; reconnect is not part of this carrier. |
| Cross-process, socket, and byte-wire behaviour | The Direct carrier is in-process and carries complete `UnionDataList` values. |
