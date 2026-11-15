# RawUnreliableServerEndpoint Layer Coverage

- **Layer:** RawUnreliableServerEndpoint
- **Subject:** `Pontifex.Raw.Unreliable.RawUnreliableServerEndpoint`
- **Directory:** `Tests/Pontifex.Tests/Endpoints/RawUnreliable/Server`
- **Inheritance parent:** `Pontifex.Raw.Unreliable.RawUnreliableEndpoint`
- **Contract:** [RawUnreliableServerEndpoint.cs](../../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/RawUnreliableServerEndpoint.cs),
  [IRawUnreliableServerEndpointImp.cs](../../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/IRawUnreliableServerEndpointImp.cs),
  [SendBudget.cs](../../../../../Pontifex/Implementations/Endpoints/Raw/Unreliable/SendBudget.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `Constructor_WithNullTrigger_Throws` | The constructor requires a pending trigger. | A null trigger is rejected with `ArgumentNullException`. | The base endpoint is constructed first; the guard runs in this layer. |
| `Constructor_WithNullSendBudget_Throws` | The constructor requires a shared budget. | A null budget is rejected with `ArgumentNullException`. | The trigger is not adopted before the budget check, so the test still owns and releases it. |
| `Start_WithBufferedMessages_DeliversTriggerFirst` | The trigger is delivered before every message buffered before the start. | The trigger reaches the handler first, followed by the two buffers delivered before the start in delivery order. | The inbox is drained in FIFO order. Prepend-vs-append at constructor time is contract-equivalent because the trigger is inserted before any `Deliver` can run, so this test pins the observable delivery order, not the insertion mechanism. |
| `Start_WhenInboxCapsAreZero_StillBuffersAndDeliversTrigger` | The trigger is not subject to the inbox caps. | With zero message and zero byte capacity the trigger is still buffered and delivered. | The trigger occupies one inbox slot and its payload counts toward inbox byte usage, but its admission bypasses the caps. |
| `Stop_BeforeStart_ReleasesTriggerWithoutDelivery` | The trigger is released on a terminal path before start. | The trigger buffer is released, the handler receives nothing, and `Stopped` fires once. | The release runs through the inherited `Stopped` subscription, exactly like a buffered delivery. |
| `Send_WhenSharedBudgetAccepts_ReturnsOk` | The shared budget admits a message within its limits. | `Send` returns `Ok`. | The inherited local budget is configured larger than the shared cap in these tests, so the shared budget binds first. |
| `Send_WhenSharedMessageBudgetRejects_ReturnsBufferOverflowAndReleases` | The shared message cap rejects a message the local budget would accept. | The second send returns `BufferOverflow`, its buffer is released, and only the first message is transmitted after the drain. | A rejected admission reserves nothing, so no negative delta pair is needed. |
| `Send_WhenSharedByteBudgetRejects_ReturnsBufferOverflow` | The shared byte cap rejects on the byte boundary. | Two messages fill the budget and the third returns `BufferOverflow` and is released. | The budget is sized to twice the exact message size, read from `GetDataSize`. |
| `Send_AfterDrain_ReleasesSharedBudget` | The shared budget is released as pending messages drain. | A message-cap-1 budget accepts another send after the first message is transmitted. | Proves the negative deltas reach the shared budget through `OnPendingSendChanged`. |
| `Send_WhenLocalBudgetOverflows_RollsSharedBudgetBack` | A local rejection must not leave a shared reservation behind. | The inherited `BufferOverflow` path emits a matching `+size/+1` and `-size/-1`, so `CanAccept` still accepts the exact message size. | The byte cap equals one message size, so a surviving reservation would make `CanAccept` return false. The local capacity is 1 to force the local rejection. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| Concrete server endpoints (`RawUnreliableDirectServerEndpoint`, `RawUnreliableUdpServerEndpoint`). | Child layers of this Abstract Test Suit | Those types do not exist yet; they arrive with the carrier conversion. |
| Cross-session budget sharing through two endpoints of one transport. | This layer | The transport that owns and shares the budget does not exist yet; a manually shared `SendBudget` is the current stand-in. |
| Budget behaviour under concurrent senders. | Deferred suite layer | The default suite is deterministic; concurrency belongs to the transport conformance suite. |

## Deferred by design

| Gap | Reason |
|---|---|
| Route publication and factory invocation. | Transport responsibilities, covered by the transport suite. |
| Carrier-level sends and the send-commit gates. | Belong to the concrete `DoSendRaw` introduced with the carriers. |

## Notes

- Every test creates its own `SendBudget`, so budget state never leaks between tests; `SendBudget` has no
  reset member by design and its only observables are `CanAccept` and the deltas applied by the endpoint.
- Prepend-vs-append at constructor time is contract-equivalent: the trigger is inserted while the inbox is
  empty and before any `Deliver` can run, so the functional guarantees this layer pins are the delivery order
  (trigger before everything buffered before the start) and the cap exemption (the zero-cap test), not the
  insertion mechanism itself.
- The trigger is drained by the ancestor's flush loop, so its per-message `AfterReceivedGate` behaviour is
  proven in the ancestor layer rather than repeated here.
- The layer reuses the ancestor harness: `TestRawUnreliableEndpoint`'s queue, recording, and lifecycle
  patterns, `RecordingRawUnreliableHandler`, and `TestBuffers`. Only the server-specific double lives here.
- The trigger is created before the endpoint and is owned by the endpoint after construction; on the one path
  where construction throws before adoption the test releases it.
