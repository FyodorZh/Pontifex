# Valid audit findings left unfixed

- **Source:** `test-audit.md` (15 suite reports, 477 table findings).
- **Date:** 2026-11-20.
- **Scope:** valid findings deliberately not fixed in the preceding validation pass. Invalid/stale/opinion-only claims and every finding already fixed are excluded.
- **Final count:** 32 entries, grouped by suite in the same order as `test-audit.md`.

These are the remaining items after the fixes applied in the working tree (41 tests added or strengthened, 7 duplicate tests removed, documentation corrected, and one `DeliveryReporter` production defect fixed). Evidence paths are relative to the repository root unless stated otherwise.

---

## 1. Endpoints

### 1. Negotiated `MessageMaxByteSize` is not proven after the handshake
- **Suite / section:** `Tests/Pontifex.Tests/Endpoints` — missing test cases (Medium).
- **Finding:** The handshake client must apply `min(local, server)` and keep `MessageMaxByteSize` read-only afterwards. No endpoint test asserts the value after `CompleteHandshake`, nor that it survives stop/disconnect.
- **Evidence:** `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md:105-111`; `Pontifex/Implementations/Endpoints/Raw/Reliable/RawReliableHandshakeClientEndpoint.cs:369-372`; `Tests/Pontifex.Tests/Endpoints/RawReliable/Client/RawReliableHandshakeClientEndpoint/RawReliableHandshakeClientEndpointTests.cs` has no `MessageMaxByteSize` assertion.
- **Why left unfixed:** Needs a small harness accessor for the negotiated maximum and a clear assertion point after `CompleteHandshake`; lower priority than the failure branches added in this pass.
- **Suggested next step / owner:** Add `HandshakeResponse_NegotiatesSmallerMaximum_AndKeepsItAfterStop` to the handshake client fixture, then repeat for the server layer. Owner: Endpoints handshake client/server layers.

### 2. `Send` with a non-null dead/released buffer is not tested
- **Suite / section:** `Tests/Pontifex.Tests/Endpoints` — missing test cases (Medium).
- **Finding:** The contract says `Send` returns `InvalidMessage` when the buffer is null **or** not alive, and ownership stays with the caller. Only the null half is covered.
- **Evidence:** `Pontifex/Abstractions/Transports/Raw/IRawEndpoint.cs:40-44`; `Pontifex/Implementations/Endpoints/Raw/RawEndpoint.cs:457-460`; `Tests/Pontifex.Tests/Endpoints/RawEndpointTests.cs:203-209` covers only `null`.
- **Why left unfixed:** Requires constructing a released `UnionDataList` and asserting the ownership outcome; the root fixture lacks a small helper for “released but still referenced” input.
- **Suggested next step / owner:** Add `Send_ReleasedBuffer_ReturnsInvalidMessageAndKeepsOwnership` in the root layer, then the RawReliable and RawUnreliable server overrides. Owner: Endpoints root/RawReliable/RawUnreliable layers.

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

### 5. A null or throwing `TryCreate` must not be cached and must be re-invoked
- **Suite / section:** `Tests/Pontifex.Tests/Transports` — missing test cases (Medium).
- **Finding:** Reliable spec rules 4–5 require a rejected `TryCreate` to remain eligible for a later attempt from the same remote. The admission layer tests one throwing call and never retries; no null-result retry test exists.
- **Evidence:** `Pontifex/Abstractions/Transports/Raw/Reliable/Specification.md:149-156`; `Tests/Pontifex.Tests/Transports/Raw/Reliable/Server/RawReliableAdmissionServerTransport/RawReliableAdmissionServerTransportTests.cs:154-172`.
- **Why left unfixed:** Needs a scripted factory that returns null or throws on the first call and succeeds on the second, plus a second admitted session to prove re-eligibility.
- **Suggested next step / owner:** Add `TryCreateHandler_AfterNull_ReinvokesFactory` and `TryCreateHandler_AfterThrow_ReinvokesFactory`, asserting `TryCreateCalls == 2` and a non-null second handler. Owner: RawReliableAdmissionServerTransport layer.

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

### 8. A late ACK after retry exhaustion must not fire `Delivered`
- **Suite / section:** `Tests/Pontifex.Tests/DeliveryManager` — missing test cases (Low).
- **Finding:** After `FailedToDeliver`, the dispatcher removes both maps; a later `ConfirmDelivered` should no-op. No test acknowledges after the failure transition.
- **Evidence:** `Pontifex/Utils/DeliveryManager/DeliveryDispatcher.cs:150-160,164-185`; `Tests/Pontifex.Tests/DeliveryManager/Tests/DeliveryDispatcherTests.cs:148-166`.
- **Why left unfixed:** Small but strictly a lifecycle edge; the current failure test already proves the forward transition, and the suite has no mixed failure/late-ACK helper.
- **Suggested next step / owner:** Extend `TryToDeliver_Failure_FiresFailedToDeliver` with `ConfirmDelivered(Info(1))` and assert `Delivered` never fires. Update `Coverage.md` in the same change. Owner: DeliveryManager layer.

---

## 4. Bootstrap

### 9. `Create` is not proven to be invoked only by compilation
- **Suite / section:** `Tests/Pontifex.Factory.Tests/Bootstrap` — missing test cases (Medium).
- **Finding:** `parsing.md` §6.9 says the direct compiler is the only caller of `Create`; parser, resolver, and printer must not call it. No test counts invocations across `Resolve`/`Canonicalize`/`Compile`.
- **Evidence:** `Pontifex.TransportFormula/parsing.md` §6.9; `Tests/Pontifex.Factory.Tests/Bootstrap/Harness/BootstrapTestData.cs` `CoreRegistration` passes a lambda without a counter.
- **Why left unfixed:** Requires a counting harness registration; the current tests assert results, not invocation counts.
- **Suggested next step / owner:** Add an invocation counter to the harness registration and assert `0` after `Resolve`/`Canonicalize`, `1` after `Compile`. Owner: Bootstrap layer (with Compilation cross-reference).

### 10. A rejected registration does not prove the prior registration survived
- **Suite / section:** `Tests/Pontifex.Factory.Tests/Bootstrap` — missing test cases (Medium).
- **Finding:** The coverage note claims “a rejected registration never reaches the runtime registry”, but the duplicate-registration test asserts only the exception and never resolves afterwards.
- **Evidence:** `Pontifex.Factory/Bootstrap/TransportFactoryBuilder.cs:35-41`; `Tests/Pontifex.Factory.Tests/Bootstrap/Tests/TransportFactoryBuilderTests.cs:507-530` (current area: duplicate canonical scheme test).
- **Why left unfixed:** Needs an additional build-and-compile step after the expected throw; low risk but not covered by the existing fixture.
- **Suggested next step / owner:** After the duplicate throws, `Build()` and compile the first scheme, asserting the original transport resolves and no runtime entry drifted. Owner: Bootstrap layer.

---

## 5. Compilation

### 11. Build-context accessor absent/wrong-kind branches are untested
- **Suite / section:** `Tests/Pontifex.Factory.Tests/Compilation` — missing test cases (Medium).
- **Finding:** `TryGetTransport`, `TryGetTransportList`, and `TryGetStruct` have absent-value and wrong-kind branches required by `parsing.md` §9.1/§13.6. Only success/laziness/empty-list paths are covered.
- **Evidence:** `Pontifex.Factory/Compilation/Internal/TransportBuildContext.cs:102-111,124-149,152-171`; `Tests/Pontifex.Factory.Tests/Compilation/Tests/TransportCompilerTests.cs` exercises these only on success (around lines 270, 328, 339-427, 1170).
- **Why left unfixed:** Requires binding a non-transport value to each parameter kind and asserting the exact `E_CMP_PARAM_KIND_MISMATCH` message, including the kind vocabulary (`transport`, `transport list`, `struct`).
- **Suggested next step / owner:** Add six cases (absent and wrong kind for each accessor) and one row per case in `Coverage.md`. Owner: Compilation layer.

### 12. `TransportCompiler` null-dependency constructor guards are untested
- **Suite / section:** `Tests/Pontifex.Factory.Tests/Compilation` — missing test cases (Low).
- **Finding:** The compiler constructor rejects null registry/converters/logger/memory, but no test constructs it with a null dependency.
- **Evidence:** `Pontifex.Factory/Compilation/TransportCompiler.cs:31-38`; `Tests/Pontifex.Factory.Tests/Compilation/Harness/CompilationTestData.cs:43-56` always supplies all four.
- **Why left unfixed:** Defensive argument checks with low contractual value; the README already treats bootstrap wiring as out of scope but not these guards.
- **Suggested next step / owner:** Add four `Assert.Throws<ArgumentNullException>` tests via `CompilationTestData.NewCompiler` overloads, or list them under Deferred-by-design. Owner: Compilation layer.

---

## 6. Converters

### 13. The client handshake request dataset is never decoded
- **Suite / section:** `Tests/Pontifex.Factory.Tests/Converters` — missing test cases (High).
- **Finding:** The accepted test waits for any delivery-manager user message on the peer; it never parses the request, so a wrong version/name/capacity in the client request would still pass.
- **Evidence:** `Tests/Pontifex.Factory.Tests/Converters/Tests/RawReliableConverterTests.cs:115`; `Pontifex/Converters/RawUnreliableNoAck/RawReliable/ConverterHandshake.cs:115` provides `TryParseRequest`.
- **Why left unfixed:** Needs a peer-side capture of the first payload and a decode assertion; the scripted peer currently only signals “received”.
- **Suggested next step / owner:** Decode the peer’s first payload with `ConverterHandshake.TryParseRequest` and assert version, name `"converter"`, and `DeliveryMax(innerMax)`. Owner: Converters layer.

### 14. Client reject mapping covers only one of four statuses
- **Suite / section:** `Tests/Pontifex.Factory.Tests/Converters` — missing test cases (High).
- **Finding:** `MapReject` maps version/name mismatch to `ProtocolMismatch` and rejected/capacity to `HandshakeRejected`, plus timeout to `TimeOut`; only the server-name-mismatch case is tested.
- **Evidence:** `Pontifex/Converters/RawUnreliableNoAck/RawReliable/RawUnreliableNoAckToRawReliableClient.cs:408-420`; `Tests/Pontifex.Factory.Tests/Converters/Tests/RawReliableConverterTests.cs:132-167` covers only `NameMismatch`.
- **Why left unfixed:** Needs a `TestCase`-driven scripted peer for the remaining statuses and exact stop-reason types; the current helper builds only the name-mismatch reject.
- **Suggested next step / owner:** Parameterize over `HandshakeStatus` and assert the stop-reason type and text handling for each mapping. Owner: Converters layer.

---

## 7. Parsing

### 15. `as` as a named-argument or struct-field name is untested
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Parsing` — missing test cases (Low).
- **Finding:** The grammar allows `as` in parameter/struct-field name positions. Tests cover it as a scheme, scalar value, cast type, and keyword, but not `alpha(as=1)` or `alpha({as=1})`.
- **Evidence:** `Pontifex.TransportFormula/grammar.md:150-155`; `Tests/Pontifex.TransportFormula.Tests/Parsing/Tests/ParserTests.cs` has no such case.
- **Why left unfixed:** Low contractual value; the parser’s contextual-keyword handling is already covered on four other axes and the gap is recorded as planned.
- **Suggested next step / owner:** Add one named-argument and one struct-field success case. Owner: Parsing layer.

### 16. `TokenCursor.Match`/`MatchWord` are declared subject surface with no call sites
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Parsing` — missing test cases (Low).
- **Finding:** `Coverage.md` lists `TokenCursor` as an indirectly exercised subject, but `Match` and `MatchWord` have no call site outside the class and are never executed.
- **Evidence:** `Pontifex.TransportFormula/Parsing/Internal/TokenCursor.cs:36-57`; `Pontifex.TransportFormula/Parsing/Internal/FormulaParser.cs` uses only `Current`, `Peek`, `Consume`, `IsCurrentAsKeyword`.
- **Why left unfixed:** Either remove dead members or add unit coverage; both are low-value churn and the parser tests are strong.
- **Suggested next step / owner:** Decide whether these are intended API; remove them if not, otherwise add direct cursor tests and update the subject note. Owner: Parsing layer.

---

## 8. Canonicalization

### 17. `StringParam` printability is not asserted at validator level
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Canonicalization` — missing test cases (Low).
- **Finding:** `PrinterInvariantValidator` calls `EnsurePrintable` for plain string parameters, but CR coverage exists only for struct fields and array elements.
- **Evidence:** `Pontifex.TransportFormula/Canonicalization/Internal/PrinterInvariantValidator.cs:191`; struct/array CR cases at `Tests/Pontifex.TransportFormula.Tests/Canonicalization/Tests/PrinterInvariantValidatorTests.cs:691-710,1572-1589`.
- **Why left unfixed:** Low-value gap; the shared `EnsurePrintable` helper is already exercised through two other value kinds.
- **Suggested next step / owner:** Add `alpha(beta(x), timeout="a\rb")` → `E_PRN_INVALID_SCALAR U+000D`. Owner: Canonicalization layer.

### 18. Remaining accessor-kind and non-identifier name branches are untested
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Canonicalization` — missing test cases (Low).
- **Finding:** Field-level mismatch is proven only for integer-field and decimal-field accessors; string-field-over-integer, nested-struct-field-over-string, and non-identifier names are not combined with a violation.
- **Evidence:** `Pontifex.TransportFormula/Canonicalization/Internal/PrinterInvariantValidator.cs:266-272,322-330,636-645`; tested at `Tests/Pontifex.TransportFormula.Tests/Canonicalization/Tests/PrinterInvariantValidatorTests.cs:965-1079`.
- **Why left unfixed:** Two more accessor matrices plus name-shape cases; low contractual value compared with the nested-failure tests added in this pass.
- **Suggested next step / owner:** Add the two remaining accessor mismatch tests and one empty/leading-digit name case, then extend the kind-isolation rows. Owner: Canonicalization layer.

---

## 9. Semantics

### 19. Several shape-matrix cells are missing
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Semantics` — missing test cases (Medium).
- **Finding:** Documented 4-shape × kind branches are not all exercised: decimal parameter receiving struct, integer/decimal struct field receiving list or struct, string/decimal array element with struct shape, and throwing `StringType` validator inside an array.
- **Evidence:** `Pontifex.TransportFormula/parsing.md:1068-1151,1220-1302`; `Tests/Pontifex.TransportFormula.Tests/Semantics/Tests/ParameterValidatorTests.cs:295-314`; `.../StructValidatorTests.cs:251-268,343-370`.
- **Why left unfixed:** Enumerating the full matrix is a substantial test expansion; the highest-value cells are now recorded as planned.
- **Suggested next step / owner:** Add at minimum decimal-parameter-struct and numeric-field-list/struct cells, then the array-element struct cases. Owner: Semantics layer.

### 20. Inclusive count maxima are not accepted anywhere
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Semantics` — missing test cases (Low).
- **Finding:** Only `single` (`min == max == 1`) proves an inclusive upper bound; `gamma` and `counts` at their maximum counts are never accepted.
- **Evidence:** `Pontifex.TransportFormula/parsing.md:1123-1125,1270-1271`; `Tests/Pontifex.TransportFormula.Tests/Semantics/Tests/ParameterValidatorTests.cs:189-208`; `.../StructValidatorTests.cs:303-320`.
- **Why left unfixed:** Boundary-only gap; the over-limit side is covered and the suite already records maxima as planned.
- **Suggested next step / owner:** Add `gamma([a,a,a])` and `arrays({counts=[0,0,0]})` success cases. Owner: Semantics layer.

---

## 10. Registry

### 21. Rule 11 expected-member ordering is not distinguished
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Registry` — missing test cases (Medium).
- **Finding:** Validation must visit the constructor type and each expected member in declaration order. The only test has one parameter with one expected member, so neither cross-parameter nor intra-set order is proven.
- **Evidence:** `Pontifex.TransportFormula/parsing.md:802-804`; `Pontifex.TransportFormula/Registry/Internal/MetadataValidator.cs:266-287`; `Tests/Pontifex.TransportFormula.Tests/Registry/Tests/TransportRegistryBuilderTests.cs:950-964`.
- **Why left unfixed:** Needs two parameters whose expected sets each contain an absent type and one set with two absent members; straightforward but not yet written.
- **Suggested next step / owner:** Add one test for first-parameter precedence and one for first-member precedence, then correct the coverage note. Owner: Registry layer.

### 22. Registry null/invalid guard paths are untested
- **Suite / section:** `Tests/Pontifex.TransportFormula.Tests/Registry` — missing test cases (Medium, TEST-08).
- **Finding:** Defensive branches are unproven: null `TransportTypeInfo`, null registration/constructor, `GetCanonicalName` outside the catalog, and null scheme.
- **Evidence:** `Pontifex.TransportFormula/Registry/Internal/TransportRegistryBuilder.cs:23-26,39-51`; `.../TransportTypeCatalog.cs:73-83`; `.../TransportRegistry.cs:65-78`.
- **Why left unfixed:** These are defensive guards whose reachability from the public registration path is limited; the suite records them as planned rather than pinning possibly unreachable behaviour.
- **Suggested next step / owner:** Add `ArgumentNullException`/`InvalidOperationException` tests where the public contract permits them, and record the rest as deferred with the guarding contract. Owner: Registry layer.

---

## 11. LowLevelTcpSocket

### 23. Harness and supporting-type fixtures still report as subject coverage
- **Suite / section:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket` — formal correctness (High/Medium, HARNESS-11/TS-02).
- **Finding:** Fixtures for test-local helpers (`ApplicationByteLedger`, `ApplicationLivenessWatchdog`, `StrictSingleWriterQueue`, a fixture’s own `Dispose`) and for supporting production types (`SystemSocket`, `ReusablePartialView.Create`) carry the suite root category.
- **Evidence:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket/Tests/Composition/ApplicationByteLedgerTests.cs:12-13`; `.../ApplicationLivenessWatchdogTests.cs:12-13`; `.../ProducerCompositionTests.cs:141-158,185-234`; `.../Construction/SystemSocketTests.cs:9-63`; `.../Receiving/PartialReceiveViewTests.cs:238-272`.
- **Why left unfixed:** Moving them to `Harness/Tests/` or rewriting them through `LowLevelTcpSocket` is a structural change across several fixtures and coverage sections.
- **Suggested next step / owner:** Move harness-helper tests under `Harness/Tests/` with `[LowLevelTcpSocketHarnessSuite]`; declare supporting-type coverage explicitly in the README/Coverage or split those into their own suites. Owner: LowLevelTcpSocket suite.

### 24. Receiver-direction benign shutdown classification is untested
- **Suite / section:** `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket` — missing test cases (Medium).
- **Finding:** `TryShutdown` treats `ObjectDisposedException`, `OperationAborted`, `ConnectionAborted`, `NotConnected`, and `Shutdown` as benign during teardown. Only the send direction is covered; the receive path has only the non-benign fault case.
- **Evidence:** `Transports/Pontifex.Transport.Net/Common/LowLevelTcpSocket/LowLevelTcpSocket.cs:947-971,2440-2458`; `Tests/.../Sending/SenderShutdownTests.cs:106-164`; `Tests/.../Teardown/DirectionalShutdownFaultTests.cs:38-59`.
- **Why left unfixed:** Needs a receiver-side `TestCase` matrix mirroring the sender suite; the shared `TryShutdown` helper makes it likely correct but not proven.
- **Suggested next step / owner:** Add a receiver-direction benign-shutdown test matrix (`ObjectDisposed`, `OperationAborted`, `ConnectionAborted`, `NotConnected`, `Shutdown` ⇒ never `Faulted`). Owner: LowLevelTcpSocket suite.

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

### 28. `ProcessReceivedData` returning `false` does not have a termination test
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Protocol` — missing test cases (High).
- **Finding:** A `false` return from `IRawReliableWrapperLogic.ProcessReceivedData` must terminate the session (`TextFail` on the client, endpoint disconnect on the server). No test sets the harness switch.
- **Evidence:** `Protocols/Pontifex.Protocol/Raw/Reliable/IRawReliableWrapperLogic.cs:31-35`; `.../RawReliableWrapperClient.cs:379-389`; `.../RawReliableWrapperServer.cs:373-383`; `Tests/.../Protocol/Harness/RecordingWrapperLogic.cs:122,148-152`.
- **Why left unfixed:** Needs one client-side and one server-side test plus assertions on the terminal callbacks; the harness knob already exists but was not wired.
- **Suggested next step / owner:** Set `ProcessReceivedResult = false` in each side’s accepted session and assert the typed stop/disconnect. Owner: Protocol/RawReliableWrapper layer.

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

### 31. Duplicate-free replay after reattach is untested
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable` — missing test cases (Medium).
- **Finding:** The layer’s central promise is that replayed unconfirmed messages are suppressed by `DeliverySystem.OpResult.DuplicateMessage`. `QueuedMessages_SurviveReattach` drops the link before any tick, so the payload never enters the delivery system.
- **Evidence:** `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/Common/DeliverySystem.cs:131-154,180-184`; `.../Common/ReconnectableBaseLogic.cs:166-172,382`; `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable/Tests/RawReliableReconnectableTests.cs:154-175`.
- **Why left unfixed:** Needs a send → `TickAll()` → drop → reconnect → `TickAll()` sequence and an exactly-once received assertion; the harness supports it but the test is not written.
- **Suggested next step / owner:** Add `ReplayedMessageAfterReattach_IsDeliveredOnce` using the existing manual driver and assert server handler receives the value exactly once. Owner: Reconnectable layer.

### 32. Unknown or stale reattach session ids are not rejected in a test
- **Suite / section:** `Tests/Pontifex.Transports.Tests/Raw/Reliable/Reconnectable` — missing test cases (Medium).
- **Finding:** The server must reject a well-formed reattach with an unknown id or stale generation rather than admitting it or crashing. Only the live-id/wrong-secret case is tested.
- **Evidence:** `Protocols/Pontifex.Protocol.Reconnectable/Raw/Reliable/RawReliableReconnectableServer.cs:363-370`; `.../Common/SessionMap.cs:58-72`; `Tests/.../Reconnectable/Tests/RawReliableReconnectableTests.cs:87-120`.
- **Why left unfixed:** Needs the endpoint `Transform` seam to rewrite an id/generation in the reattach tail and a second connection attempt; the harness has the transform but no id-rewrite helper.
- **Suggested next step / owner:** Add `UnknownSessionId_IsRejectedWithoutFactoryCall` and a stale-generation variant, asserting `HandshakeRejected` and that the existing handler is untouched. Owner: Reconnectable layer.
