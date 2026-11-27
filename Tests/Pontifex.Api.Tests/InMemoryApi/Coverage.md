# InMemoryApi Layer Coverage

- **Layer:** InMemoryApi (root layer)
- **Subject:** in-memory API declaration and pipe group: `RRDecl<TRequest, TResponse>`,
  `C2SMessageDecl<TMessage>`, `S2CMessageDecl<TMessage>`, and `InMemoryPipeSystem`
- **Directory:** `Tests/Pontifex.Api.Tests/InMemoryApi`
- **Inheritance parent:** — (root, Test Suit)
- **Contract:** [ApiRoot.cs](../../../Api/Pontifex.Api/Logic/Api/ApiRoot.cs),
  [IApiRoot.cs](../../../Api/Pontifex.Api/Logic/Api/IApiRoot.cs),
  [RRDecl.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/RequestResponse/RRDecl.cs),
  [IRequester.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/RequestResponse/IRequester.cs),
  [IResponder.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/RequestResponse/IResponder.cs),
  [C2SMessageDecl.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/MessageOnly/Typed/C2SMessageDecl.cs),
  [S2CMessageDecl.cs](../../../Api/Pontifex.Api/Logic/Declaration/Unidirectional/MessageOnly/Typed/S2CMessageDecl.cs),
  [InMemoryPipeSystem.cs](../../../Api/Pontifex.Api/Logic/PipeSystem/InMemoryPipeSystem.cs)

## Covered tests

| Test | Contract clause / behaviour | What it proves | Non-extractable notes |
|---|---|---|---|
| `GracefulShutdown_FromClient_StopsBothEndsWithExpectedReasons` | `ApiRoot.GracefulShutdown`: a client shutdown sends a disconnect and stops both roots with local and remote intention reasons. | The initiating client raises `Disconnecting` and `UserIntention`; the server raises `GracefulRemoteIntention` without raising its own `Disconnecting`. | `GracefulShutdown(null)` skips the timer. In-memory delivery and both stop callbacks complete synchronously, so no wait or scheduling race is involved. |
| `GracefulShutdown_FromServer_StopsBothEndsWithExpectedReasons` | `ApiRoot.GracefulShutdown`: a server shutdown sends a kick and stops both roots with local and remote intention reasons. | The initiating server raises `Disconnecting` and `UserIntention`; the client raises `GracefulRemoteIntention` without raising its own `Disconnecting`. | The assertion checks the mirrored reason mapping as well as which endpoint reports that it initiated shutdown. |
| `Api_RequestResponseAndMessageCalls_ReturnExpectedResultsAndShutdownPropagates` | `RRDecl` request/response success and failure callbacks; typed client-to-server and server-to-client messages; graceful shutdown propagation. | A valid division reaches the success callback with the computed response; division by zero reaches the failure callback with the processor error; the message round trip doubles the value; server shutdown reaches both ends with the expected reasons. | Request callbacks are captured by value because production reuses response-info objects. The in-memory model pipe invokes receivers synchronously; payload serializers are deliberately unsupported. |

## Planned

| Missing test | Owner | Reason |
|---|---|---|
| Requests submitted after a declaration or root has stopped. | InMemoryApi layer | The stopped-state `NotConnected` and failure-callback path is not covered by the current happy-path and graceful-stop tests. |
| Pending requests when the owning declaration stops. | InMemoryApi layer | `RRDecl.Stop` fails pending requests; this failure path is not asserted. |
| Repeated shutdown and duplicate lifecycle-event suppression. | InMemoryApi layer | The current tests assert one shutdown transition and the expected stop-reason mapping, not idempotence or duplicate callback suppression. |

## Deferred by design

| Gap | Reason |
|---|---|
| Graceful-shutdown timeout and other timer-driven paths. | The default suite is deterministic and uses a null timeout. Timeout behaviour requires a separate timing seam or categorised timing suite. |
| Serialization, raw pipes, and wire-format compatibility. | The suite targets the in-memory typed model-pipe path; payload serializers intentionally throw if called. |
| API negotiation and transport-side client/server handlers. | Those contracts belong to the transport-backed API suites, not the in-memory declaration group. |
| Real transports, sockets, networks, and integration timing. | The suite uses only `InMemoryPipeSystem`. |
| Concurrency, allocation, load, and stress behaviour. | The default suite is single-threaded and deterministic. |

## Notes

- Every fixture carries `[Category("Pontifex.Api.InMemoryApi")]`.
- Tests use no sleeps, real clocks, sockets, files, randomness, timers, or
  uncontrolled background tasks.
- `InMemoryPipeSystem` invokes typed receivers synchronously. Tests therefore
  assert callback results and lifecycle events immediately after the operation.
- `EmptyApi` and `BigApi` are hand-written test implementations of `ApiRoot`;
  their payload types implement `IDataStruct` only to satisfy declaration
  constraints. The in-memory model pipe does not call `Serialize`.
