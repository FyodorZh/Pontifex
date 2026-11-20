# Valid audit findings left unfixed

- **Source:** `test-audit.md` (15 suite reports, 477 table findings).
- **Date:** 2026-11-20.
- **Scope:** valid findings that are still open. Invalid/stale/opinion-only claims, every finding already fixed in the preceding pass, and the 20 easy-risk items fixed in the current pass are excluded.
- **Final count:** 12 entries, grouped by suite in the same order as `test-audit.md`.
- **Numeration:** suite headings and problem ids are preserved from the original plan; gaps are completed items that were removed.

---

## 1. Endpoints

### 3. Sibling handshake harness logic is duplicated
- **Suite / section:** `Tests/Pontifex.Tests/Endpoints` — formal correctness (Medium, HARNESS-06/EVOL-12).
- **Finding:** `IsConnectedProbe`/`IsConnectedDuringDisconnected`/`ThrowOnConnected`/`ConnectedException` plumbing and the explicit `OnDisconnected` override are copied into both handshake client and server recording handlers.
- **Evidence:** `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/Harness/RecordingRawReliableHandshakeClientHandler.cs:20-50`; `.../Server/RawReliableHandshakeServerEndpoint/Harness/RecordingRawReliableHandshakeServerHandler.cs:20-51`.
- **Why left unfixed:** Extracting a shared recording base into `RawReliable/Harness` is a harness restructuring, not a behavioural fix, and would touch both handshake layers plus their coverage notes.
- **Suggested next step / owner:** Add a shared `RecordingRawReliableHandshakeHandler` base in `Endpoints/RawReliable/Harness`, derive both handlers from it, and keep client/server-only members local. Owner: Endpoints RawReliable layer.

---

## 2. Transports

### 4. Factory serialization is tested only for one source route
- **Suite / section:** `Tests/Pontifex.Tests/Transports` — missing test cases (Medium).
- **Finding:** Both specs require global factory serialization, including calls for unequal source routes. The suite tests only concurrent triggers for the same source, and the README narrows the claim to “for one source”.
- **Evidence:** `Pontifex/Abstractions/Transports/Raw/Unreliable/Specification.md:485-491`; `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md:146-148`; `Tests/Pontifex.Tests/Transports/Raw/Unreliable/Server/RawUnreliableServerTransportTests.cs:414-500`; `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableAdmissionServerTransport/RawReliableAdmissionServerTransportTests.cs:188-267`.
- **Why left unfixed:** Needs a gated two-source harness for both server layers and a README/spec wording update; the existing one-source test does not exercise the global lock.
- **Suggested next step / owner:** Add one gated two-source test per server layer using two distinct `IEndPoint` values and assert `maxActiveFactoryCalls == 1`; update README claims to “globally”. Owner: Transports reliable/unreliable server layers.

### 6. Session terminal notification ordering before the transport callback is unproven
- **Suite / section:** `Tests/Pontifex.Tests/Transports` — missing test cases (Medium).
- **Finding:** The reliable spec says each session handler’s terminal notification is scheduled before the transport’s own `onStopped` callback. The stop test drains sessions after `Stop` and never observes ordering.
- **Evidence:** `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md:197-201`; `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableServerTransportTests.cs:749-772` (current line area: `Stop_WhenStarted_StopsEveryRegisteredSession...`).
- **Why left unfixed:** The session double exposes only `DrainAll`; an ordering assertion needs a “stop requested” observable set at `Stop` time and read inside the transport callback.
- **Suggested next step / owner:** Expose `StopRequestedCount`/`LastTryBeGentle` set at request time in `TestRawReliableServerSession`, then assert it from the transport `onStopped` callback. Record the gap as planned until the seam exists. Owner: RawReliableServerTransport layer.

---

## 3. DeliveryManager

### 7. Facade `BufferOverflow` → `FailedToDeliver` translation is untested
- **Suite / section:** `Tests/Pontifex.Tests/DeliveryManager` — missing test cases (Medium).
- **Finding:** `DeliveryManager.ProcessOutgoing` maps a dispatcher `BufferOverflow` to `FailedToDeliver`, but no facade test fills the dispatcher and asserts the event or the double-release-safe ownership around it.
- **Evidence:** `Pontifex/Utils/DeliveryManager/DeliveryManager.cs:184-191`; `Tests/Pontifex.Tests/DeliveryManager/Tests/DeliveryManagerTests.cs` has no overflow-to-event test; `Coverage.md` defers only the dispatcher capacity case.
- **Why left unfixed:** Reaching the 5,000-pending production capacity in the default suite is slow; a deterministic test needs a capacity seam or a small dispatcher-specific fixture.
- **Suggested next step / owner:** Add a facade test with an injectable/pending-capacity seam that forces `BufferOverflow`, asserts one `FailedToDeliver`, and verifies the tracked pools report no double release. Owner: DeliveryManager layer.

---

## 9. Semantics

### 19. Several shape-matrix cells are missing
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Semantics` — missing test cases (Medium).
- **Finding:** Documented 4-shape × kind branches are not all exercised: decimal parameter receiving struct, integer/decimal struct field receiving list or struct, string/decimal array element with struct shape, and throwing `StringType` validator inside an array.
- **Evidence:** `Pontifex.TransportFormula/parsing.md:1068-1151,1220-1302`; `Tests/Pontifex.TransportFormula.Tests/Semantics/Tests/ParameterValidatorTests.cs:295-314`; `.../StructValidatorTests.cs:251-268,343-370`.
- **Why left unfixed:** Enumerating the full matrix is a substantial test expansion; the highest-value cells are now recorded as planned.
- **Suggested next step / owner:** Add at minimum decimal-parameter-struct and numeric-field-list/struct cells, then the array-element struct cases. Owner: Semantics layer.

---

## 11. LowLevelTcpSocket

### 23. Harness and supporting-type fixtures still report as subject coverage
- **Suite / section:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket` — formal correctness (High/Medium, HARNESS-11/TS-02).
- **Finding:** Fixtures for test-local helpers (`ApplicationByteLedger`, `ApplicationLivenessWatchdog`, `StrictSingleWriterQueue`, a fixture’s own `Dispose`) and for supporting production types (`SystemSocket`, `ReusablePartialView.Create`) carry the suite root category.
- **Evidence:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket/Tests/Composition/ApplicationByteLedgerTests.cs:12-13`; `.../ApplicationLivenessWatchdogTests.cs:12-13`; `.../ProducerCompositionTests.cs:141-158,185-234`; `.../Construction/SystemSocketTests.cs:9-63`; `.../Receiving/PartialReceiveViewTests.cs:238-272`.
- **Why left unfixed:** Moving them to `Harness/Tests/` or rewriting them through `LowLevelTcpSocket` is a structural change across several fixtures and coverage sections.
- **Suggested next step / owner:** Move harness-helper tests under `Harness/Tests/` with `[LowLevelTcpSocketHarnessSuite]`; declare supporting-type coverage explicitly in the README/Coverage or split those into their own suites. Owner: LowLevelTcpSocket suite.

---

## 12. Api

### 25. No negotiation-clock seam; production timers are armed by every test
- **Suite / section:** `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api` — formal correctness (Medium, TEST-04/05).
- **Finding:** `ClientSideApi`/`ServerSideApiInstance` construct `NegotiationSession` with the production clock, so each test arms a 10 s deadline and a 2 s rejection linger. No test completes the linger; it is cancelled only by disposal.
- **Evidence:** `Api/Pontifex.Api/Transport/Transport/ClientSideApi.cs:66-75`; `Api/Pontifex.Api/Transport/Transport/ServerSideApiInstance.cs:60-68`; `Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs:238`; `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api/Coverage.md:19,22`.
- **Why left unfixed:** Requires a production API change (an `INegotiationClock` parameter) or a manual-clock harness wired into the Api types; the README now states the real-timer exception instead.
- **Suggested next step / owner:** Add the clock seam used by the sibling wrapper clients and drive it with `ManualNegotiationClock`; then assert deadline/linger cancellation deterministically. Owner: Api transport + Production negotiation layer.

### 26. Per-session `TryCreate` freshness is not testable with the current harness
- **Suite / section:** `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api` — missing test cases (Medium).
- **Finding:** The contract requires a fresh `ServerSideApiInstance` per admitted session, but the harness factory returns one shared instance and no test connects twice.
- **Evidence:** `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md:149-150`; `Api/Pontifex.Api/Transport/Transport/ServerSideApi.cs:14-15,28-31`; `Tests/Pontifex.Transport.Tests/Raw/Reliable/Api/Harness/ApiNegotiationHarness.cs:409-411`.
- **Why left unfixed:** Needs a harness factory change (`Func<ServerSideApiInstance<TApi>>`) and a two-connect test; only the concurrency overlap is genuinely blocked, the sequential freshness is not.
- **Suggested next step / owner:** Make the pair take a factory delegate, add a two-connect test asserting distinct handlers and independent `ApiStarted` counts, and split the deferral row. Owner: Api layer.

---

## 13. Protocol

### 27. The server-side negotiation deadline is untested
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol` — missing test cases (High).
- **Finding:** The server handshake deadline (default 10 s) must fire `TimeOut` and send `"negotiation timeout"`. Only the client deadline is covered.
- **Evidence:** `Pontifex/Implementations/Raw/Reliable/Negotiation/NegotiationSession.cs:610-613`; `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md:133`; `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol/Tests/RawReliableWrapperTests.cs:99-123` covers the client only.
- **Why left unfixed:** Needs a server session connected without a client request and a clock advance; the harness supports it but the fixture has no helper to start only the server side and capture the reject envelope.
- **Suggested next step / owner:** Connect a `WrapperPair` server session, deliver no request, advance `pair.Clock` by 10 s, assert the reject envelope, handler stop, and no `OnConnected`. Owner: Protocol/RawReliableWrapper layer.

---

## 14. Zip

### 29. Fresh per-session compressor is not proven through the production binding
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip` — missing test cases (High).
- **Finding:** The binding wires `() => new RawReliableZipServerLogic(...)`, but the two-session test builds the wrapper pair by hand. A regression to shared logic in `RawReliableZipBinding.ZipServer` would leave the suite green.
- **Evidence:** `Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipBinding.cs:30-39`; `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip/Harness/ZipPair.cs:154-156`; `Tests/.../Zip/Tests/RawReliableZipTests.cs:85-144`.
- **Why left unfixed:** Needs a binding-driven session harness (compile `zip(...)` through `CreateRegistration`, then start two sessions); the metadata surface is now covered instead.
- **Suggested next step / owner:** Add a test that compiles `zip(...)` through `RawReliableZipBinding.CreateRegistration()` and drives two sequential sessions, asserting compression survives session 2. Owner: Zip layer.

### 30. Decompression failure after admission is not tested
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip` — missing test cases (Medium).
- **Finding:** The only non-happy-path data branch is unexercised: an inbound payload that is not a single compressed element (or corrupt bytes) should terminate the session with `TextFail`/`ExceptionFail`.
- **Evidence:** `Protocols/Pontifex.Protocol.Zip/ZLibUtils.cs:133-137`; `.../RawReliableWrapperServer.cs:375-389`; `.../RawReliableWrapperClient.cs:381-394`; planned row at `Tests/.../Zip/Coverage.md:34`.
- **Why left unfixed:** Needs a post-admission raw uncompressed payload delivered through the fake endpoint and assertions on the event triple and reason; the fixture currently has no raw-client delivery helper for that point.
- **Suggested next step / owner:** Deliver a two-element uncompressed payload after admission, assert `Events == ["connected","disconnected","stopped"]` and a `TextFail` reason, and add the coverage row. Owner: Zip layer.

---

## 15. Reconnectable

### 32. Unknown or stale reattach session ids are not rejected in a test
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable` — missing test cases (Medium).
- **Finding:** The server must reject a well-formed reattach with an unknown id or stale generation rather than admitting it or crashing. Only the live-id/wrong-secret case is tested.
- **Evidence:** `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableServer.cs:363-370`; `.../Common/SessionMap.cs:58-72`; `Tests/.../Reconnectable/Tests/RawReliableReconnectableTests.cs:87-120`.
- **Why left unfixed:** Needs the endpoint `Transform` seam to rewrite an id/generation in the reattach tail and a second connection attempt; the harness has the transform but no id-rewrite helper.
- **Suggested next step / owner:** Add `UnknownSessionId_IsRejectedWithoutFactoryCall` and a stale-generation variant, asserting `HandshakeRejected` and that the existing handler is untouched. Owner: Reconnectable layer.
