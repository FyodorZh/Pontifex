# Pontifex — Session Notes: RawReliableAck TCP Endpoints (LowLevelTcpSocket based)

> Continuation document for planning the two new endpoint classes. Captures all research,
> decisions, elaborations, and open questions from the previous session. The previous session
> ended in **plan mode** (no code written). Current focus: **client endpoint only**; server
> endpoint **postponed**.

---

## 1. Goal

Write two new classes:

- `RawReliableAckTcpClientEndpoint` — **active now**
- `RawReliableAckTcpServerEndpoint` — **postponed**

They must inherit from:

- `RawReliableAckClientEndpoint`
- `RawReliableAckServerEndpoint`

Internally they must be based on the `ILowLevelTcpSocket` implementation (`LowLevelTcpSocket`).

Original premise was "receive an already-connected `System.Net.Sockets.Socket`". This was
**corrected** (see decision 7): the **client creates the socket itself** in `BeginConnect()`;
the **server** keeps the "receive an already-connected socket" model (postponed).

---

## 2. Locked decisions (from Q&A)

| # | Question | Decision |
|---|----------|----------|
| 1 | Location / visibility | `internal` classes in namespace `Pontifex.Raw.Reliable.Ack` |
| 2 | Naming collision with legacy `RawReliableAckTcpEndpoint` | Add new names; **keep legacy client as-is** (two clients coexist) |
| 3 | Consumers | Standalone primitives; the caller wires them manually |
| 4 | Constructor parameters | **Expose all parameters** |
| 5 | Socket options | Leave socket options to the caller (socket arrives pre-connected) — **conflicts with client-created socket, see C1**; keep all defaults, make them parameters |
| 6 | Socket ownership | Endpoint takes ownership of the socket (`SystemSocket`/`LowLevelTcpSocket` dispose it) |
| 7 | Start / socket creation | **Client creates socket from scratch in `BeginConnect()`** (connect to `remoteEndPoint`). Server: still receives an already-connected socket (postponed). Re-confirm if this seems wrong |
| 8 | Socket verification | Verification required (server-relevant; client creates its own socket) |
| 9 | SPSC queue creation | Elaborated — see section 4 |
| 10 | Writer thread-affinity | Elaborated — see section 5 |
| 11 | Outbound framing | (a) Reuse `UnionDataListCompositor.Encode(data, pool, blockSize, dst)` |
| 12 | Inbound framing | (a) Reuse `UnionDataListCompositor.PushData(buffer.Array, buffer.Offset, buffer.Count)` |
| 13 | Inbound dispatch | Straight dispatch from the completion thread (matching legacy) |
| 14 | Graceful vs abort | `tryBeGentle == graceful disconnect`; use a **10 s** timeout |
| 15 | When to call `FinishDisconnect` | Elaborated — see section 6 |
| 16 | Completion → StopReason mapping | "Sims OK but not sure" — see section 7 |
| 17 | `SourceQuiesced` drain | Yes — drain/release queued-but-unsent buffers after `SourceQuiesced` |
| 18 | `onCallbackError` | Yes — log (and handle; details see C8) |
| 19 | Server handler pre-authorization | (no explicit answer; server postponed) |
| 20 | Watchdog ticks | Owner responsible (no internal timer) — server-relevant |
| 21 | `Now` default | **Rewrite default to `UtcNow`** (base classes default to `DateTime.Now`) |
| 22 | Test seams | Don't add test seams yet |
| 23 | `bufferCapacityBytes` vs queue capacity | Rely on base budget |
| 24 | `DoSendRaw` failures | Use `TextFail` (like legacy); `DoSendRaw` must not throw |

---

## 3. Codebase research (facts)

### 3.1 Solution / project structure & target frameworks

- Solution: `/Users/fyodor/Development/Home/Pontifex/Pontifex.sln`
- Core `Pontifex` — `Pontifex/Pontifex.csproj` — `netstandard2.1`, `LangVersion 9`, `Nullable enable`, `TreatWarningsAsErrors` (Debug+Release).
  - Package refs: `Actuarius.Memory 0.1.9-dev.0`, `Operarius 0.2.4-dev.0`, `Scriba 0.2.4-dev.0`, `System.Text.Json`.
  - `InternalsVisibleTo("Pontifex.Tests")`.
- `Pontifex.Transport.Net` — `Transports/Pontifex.Transport.Net/Pontifex.Transport.Net.csproj` — `netstandard2.1`, `LangVersion 9`, `Nullable enable`, `TreatWarningsAsErrors`.
  - `RootNamespace = Pontifex.Transports.Tcp` (but sources use `Pontifex.Raw.Reliable.Ack.Tcp`, `Pontifex.NetSockets.Tcp`).
  - ProjectReference to core `Pontifex`; `InternalsVisibleTo("Pontifex.Transport.Net.Tests")`.
  - **This is where the new classes must live** because `ILowLevelTcpSocket`/`LowLevelTcpSocket`/`SystemSocket`/`ReceivePools`/`ISocketAccessor` are `internal` here.
- Tests target `net10.0`. `Pontifex.Tests` (LangVersion 14), `Pontifex.Transport.Net.Tests` (latest).

### 3.2 Endpoint inheritance chain (core `Pontifex`)

```
RawEndpoint (abstract, IRawEndpoint)                                     Implementations/Raw/RawEndpoint.cs
 └─ RawReliableEndpoint (abstract, IRawReliableEndpoint)                 Implementations/Raw/Reliable/RawReliableEndpoint.cs
     ├─ RawReliableClientEndpoint (abstract)                             Implementations/Raw/Reliable/RawReliableClientEndpoint.cs
     │   └─ RawReliableAckClientEndpoint (abstract)                      Implementations/Raw/Reliable/Ack/RawReliableAckClientEndpoint.cs
     │       ├─ RawReliableAckTcpEndpoint (internal, legacy prod client) Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckClientTcpEndpoint.cs
     │       └─ TestRawReliableAckClientEndpoint (tests)
     └─ RawReliableServerEndpoint (abstract)                             Implementations/Raw/Reliable/RawReliableServerEndpoint.cs
         └─ RawReliableAckServerEndpoint (abstract)                      Implementations/Raw/Reliable/Ack/RawReliableAckServerEndpoint.cs
             └─ TestRawReliableAckServerEndpoint (tests)  (NO production subclass exists)
```

### 3.3 Key base-class contracts (what the new client must implement)

**`RawEndpoint`** (`Pontifex/Implementations/Raw/RawEndpoint.cs`)
- Protected: `_actionQueue` (`ISerialActionQueue`), `TransportName`, `Log` (`ILogger`), `Memory` (`IMemoryRental`).
- Lifecycle: `Start()` → `BeginStart()` (abstract) → `FinishStart(onFail)`; `Stop(reason, tryBeGentle)` → `BeginStop(...)` → `FinishStop()`.
- `ScheduleToSend(UnionDataList)` — validates size vs `MessageMaxByteSize`, checks `CanSendNow`, enforces `bufferCapacityBytes` byte budget, then schedules `_sendAction` → `DoSendRaw(msg)` **on the serial action queue** (single-threaded at a time, thread may migrate).
- `protected abstract void DoSendRaw(UnionDataList)` — must release the buffer exactly once, must not throw.
- `protected virtual ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)` — default `new SerialActionQueue(onError)`.

**`RawReliableEndpoint`** (`.../Raw/Reliable/RawReliableEndpoint.cs`)
- `protected enum MessageType : byte { InvalidMessage=0, PayloadMessage=1, DisconnectMessage=2, PingMessage=3, PongMessage=4, AckRequestMessage=5, AckResponseMessage=6 }`.
- `public bool IsLowLevelConnected`, `public bool IsConnected` (calls `GetIsConnected()`).
- `sealed override BeginStart()` → sets stage `Connecting`, calls `abstract BeginConnect()`.
- `protected void FinishConnect(Action ifFailed)` — completes start + transitions to `Connected` + `OnConnected()`.
- `sealed override BeginStop()` → if `Connected` sets `Disconnecting`, calls `abstract BeginDisconnect(reason, tryBeGentle)`.
- `protected void FinishDisconnect()` — transitions to `Disconnected`, calls `FinishStop()`.
- `public override Send(UnionDataList)` — prepends `PayloadMessage` byte, then `ScheduleToSend`.
- `protected void DisconnectGracefully()` — sends a `DisconnectMessage` frame then `Stop(UserIntention, gentle)`; **currently unused by any code** (see C3).
- `sealed override ProcessInboundMessage(UnionDataList message)` — pops first byte as `MessageType`, dispatches to `virtual ProcessInboundMessage(MessageType, message)`; on failure releases + `Stop(TextFail "Failed to parse incoming message")`.
- `protected virtual bool ProcessInboundMessage(MessageType, UnionDataList)` — `PayloadMessage` → base (handler), `DisconnectMessage` → release + `Stop(GracefulRemoteIntention)`, default → return false.

**`RawReliableClientEndpoint`** (`.../Reliable/RawReliableClientEndpoint.cs`)
- `protected virtual DateTime Now => DateTime.Now;` — **seam; decision 21 says override to `UtcNow`**.
- `protected virtual void StartKeepAlive()` — if `_pingOptions != null`, runs a `Task.Run` loop calling `KeepAliveTick()` every 100 ms.
- `protected bool KeepAliveTick()` — sends `PingMessage` when `PingPeriod` elapsed; `Stop(TimeOut)` when no pong within `DisconnectTimeout`.
- `protected override bool OnConnected()` — records pong time, calls `StartKeepAlive()`.
- `ProcessInboundMessage` — `PongMessage` refreshes timestamp.
- `protected struct PingOptions { readonly TimeSpan DisconnectTimeout; readonly TimeSpan PingPeriod; PingOptions(TimeSpan disconnectTimeout, TimeSpan pingPeriod); }`.
- Ctor validation: when `pingOptions != null`, `PingPeriod > 0` and `PingPeriod * 2 <= DisconnectTimeout`.

**`RawReliableAckClientEndpoint`** (`.../Reliable/Ack/RawReliableAckClientEndpoint.cs`)
- Ctor: `protected RawReliableAckClientEndpoint(string transportName, IEndPoint remoteEndPoint, IRawReliableAckClientHandler handler, int messageMaxByteSize, long bufferCapacityBytes, IMemoryRental memory, ILogger log, PingOptions? pingOptions)`.
- `GetIsConnected()` = `IsLowLevelConnected && _stage == LogicallyConnected`.
- `OnConnected()` → `base.OnConnected()` + `BeginAckConnect()` (sends `AckRequestMessage` after `handler.FillAckData`).
- `ProcessInboundMessage` — `AckResponseMessage` → `FinishAckConnect` (→ `handler.OnConnected(this, ackResponse)`); default → base.
- `BeginDisconnect(reason, tryBeGentle)` — **partial hook**: notifies `handler.OnDisconnected` when logically connected; **does NOT call `FinishDisconnect`** — the concrete transport must.
- `FailHard(reason)` — notifies handler if logically connected, then base.

**`RawReliableAckServerEndpoint`** (`.../Reliable/Ack/RawReliableAckServerEndpoint.cs`) — server, **postponed**.
- Ctor is `public` (not protected). Takes `disconnectTimeout` instead of `pingOptions`.
- `ProcessInboundMessage` — `AckRequestMessage` → `RefreshLastInboundMessage()` + `FinishAckConnect` (→ `SendAckResponse` + `handler.OnConnected(this)`).
- Same partial `BeginDisconnect` contract.

### 3.4 Handler interfaces

- `IRawHandler : IHandler` — `void OnReceived(UnionDataList)`, `void OnStopped(StopReason)`.
- `IRawReliableHandler : IRawHandler` — `void OnDisconnected(StopReason)`.
- `IRawReliableClientHandler : IRawReliableHandler` — marker.
- `IRawReliableAckClientHandler : IRawReliableClientHandler` — `void OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)`, `void FillAckData(UnionDataList ackData)`.
- `IRawReliableAckServerHandler : IRawReliableServerHandler` — `void OnConnected(IRawReliableEndpoint endPoint)`, `void FillAckResponse(UnionDataList ackData)`.

### 3.5 `ILowLevelTcpSocket` and `LowLevelTcpSocket`

Namespace `Pontifex.NetSockets.Tcp`; both **internal**. Files under
`Transports/Pontifex.Transport.Net/Common/LowLevelTcpSocket/`.

`ILowLevelTcpSocket : IDisposable, IAsyncDisposable`:
- `ITcpSocketSender Sender { get; }`
- `ITcpSocketReceiver Receiver { get; }`
- `Task<SocketCompletionResult> Completion { get; }`
- `void Close();`

`ITcpSocketSender`:
- `Task<SenderCompletionResult> Completion { get; }`
- `Task SourceQuiesced { get; }` (producer must seal publication and drain leftovers only after this resolves; logical `Completion` may resolve earlier)
- `void Pump();`
- `void Shutdown(bool isGraceful);`
- `void Shutdown(bool isGraceful, TimeSpan deadline);`
- `event Action? SourceDrained;` (edge-triggered "source observed temporarily empty")

`ITcpSocketReceiver`: `Task<ReceiverCompletionResult> Completion { get; }`, `void Start();`, `void Shutdown();`

Result structs/enums (all public in same file):
- `RemoteHalfClosePolicy { KeepSendOpen, GracefulSendShutdown, AbortConnection }`
- `SocketCompletionReason { Closed, Disposed, RemoteAborted, Faulted }`
- `SenderCompletionReason { GracefulShutdown, ImmediateShutdown, Aborted, Faulted }`
- `ReceiverCompletionReason { RemoteEof, LocalShutdown, Aborted, Faulted }`
- `SocketCompletionResult { Reason, Error?, SecondaryError?, Sender?, Receiver? }`
- `SenderCompletionResult { Reason, Error? }`, `ReceiverCompletionResult { Reason, Error? }`
- `SendCompletedWithoutProgressException`

`LowLevelTcpSocket` public ctor (`LowLevelTcpSocket.cs:170`):
```csharp
public LowLevelTcpSocket(
    ISocketAccessor socket,
    ISingleReaderWriterConcurrentQueue<IMultiRefByteArray> dataToSendSource,
    Action<IMultiRefByteArray> onReceived,
    IConcurrentPool<IMultiRefByteArray, int> byteArraysPool,
    RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.KeepSendOpen,
    Action<Exception>? onCallbackError = null,
    int receiveBufferSize = DefaultReceiveBufferSize,          // 16 KiB
    TimeSpan? gracefulShutdownDeadline = null)
```
Internal ctor also accepts `IDeadlineScheduler? deadlineScheduler`, `ITeardownExecutor? teardownExecutor`, `IDeadlineScheduler? disposalScheduler` (test seams — decision 22: don't use yet).

Other members: `void Close()`, `void Dispose()`, `ValueTask DisposeAsync()`, `ValueTask DisposeAsync(TimeSpan timeout)`, `internal void ForceAbortFinalize(Exception)`, `internal void ThrowIfDisposed()`, `internal void ReportAdditionalError(Exception)`, `internal static readonly TimeSpan MaxFiniteDeadline`, `internal static void ValidateDeadline(TimeSpan, string)`, `internal static int DefaultReceiveBufferSize = 16*1024`.

Behavior notes (from XML docs):
- Ownership of the supplied socket/accessor transfers and it is disposed during teardown.
- `onReceived` is invoked **inline on the socket completion thread**, takes ownership of the delivered buffer (consumer must release, even on throw), must be short/non-blocking.
- The transport does **not** bound queued/retained bytes; the producer must bound the outbound source.
- Sender `Put` source is single-reader/single-writer; transport is sole reader, producer is sole writer.
- Queued-but-unsent buffers remain the producer's and must be drained by it after `SourceQuiesced` resolves.

`Sender.Pump()` (`LowLevelTcpSocket.cs:1255`): calls `_owner.ThrowIfDisposed()` then pumps the send state machine; publishes `SourceDrained` if the source was drained. **Guard needed during/after teardown.**

### 3.6 `ISocketAccessor` / `SystemSocket`

- `internal interface ISocketAccessor : IDisposable` — `CreateEventArgs()`, `SendAsync(args)`, `ReceiveAsync(args)`, `Shutdown(SocketShutdown)`, `DisposeEventArgs(args)`.
- `internal sealed class SystemSocket : ISocketAccessor` — ctor takes `System.Net.Sockets.Socket`; validates it's a live, connected stream/TCP socket; **does not dispose on rejection**; ownership transfers on success; **exposes no Connect** (caller must supply an already-connected socket). Disposes the socket on `Dispose()`.
- Test double: `FakeSocketAccessor` in `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket/Harness/`.

### 3.7 Framing — `UnionDataListCompositor` (THE bridge)

`Pontifex/Utils/ModelCompositor/UnionDataListCompositor.cs`:
- Ctor: `UnionDataListCompositor(Action<UnionDataList?> processor, ICollectablePool collectablePool, IConcurrentPool<IMultiRefByteArray,int> bytesPool, int maxPacketSize = 1024*1024)`.
- `void PushData(byte[] bytes, int start, int count)` — decode: builds `UnionDataList` and calls `processor`; on failure calls `processor(null)`. **Not thread-safe** (but receives are serialized, so fine).
- `static IMultiRefByteArray? Encode(UnionDataList data, IConcurrentPool<IMultiRefByteArray,int> pool)` — single-buffer encode.
- `static bool Encode(UnionDataList data, IConcurrentPool<IMultiRefByteArray,int> pool, int blockSize, IConsumer<IMultiRefByteArray> dst)` — splits encoded bytes into `blockSize` blocks and pushes each via `dst.Put(...)`. **Use this** (decision 11a).
- Wire format: 4-byte length prefix (`UnionDataMemoryAlias.WriteTo4`) + `UnionDataList.SerializeTo` (ZigZag varint element count + each `UnionData`).
- `BufferCompositor` (`Pontifex/Utils/ModelCompositor/BufferCompositor.cs`) drives the decode: `PushData(byte[], start, count)`; assembles using a pool and calls back with an `IMultiRefByteArray`.

`UnionDataList` (`Pontifex/Utils/UnionData/UnionDataList/UnionDataList.cs`): `MultiRefCollectableResource<UnionDataList>`, `PutFirst/PutLast/PopFirst/TryPopFirst`, `PeekFirstType()`, `GetDataSize()`, `SerializeTo`, `Deserialize(ref source, pool)`, `Clone(pool)`, `Release()`. Extension helpers include `PutFirst(byte)`.

### 3.8 SPSC queue — CONFIRMED available

- Interface: `Actuarius.Collections.ISingleReaderWriterConcurrentQueue<TData>` (public) — `int Count`, `bool Put(TData)`, `bool TryPop(out TData)`. Also implements `IQueue<T>`, `IProducer<T>`, `IConsumer<T>`.
- Concrete **public** impl: `Actuarius.Collections.SingleReaderWriterConcurrentQueue<TData>` with ctor `(int initialCapacity = 10)`.
  - Decompiled shape: double buffer `_writeDst` / `_readSrc`; `Put` does `Interlocked.Exchange(ref _writeDst, null)` → `queue.Put(value)` → `Interlocked.Exchange(ref _writeDst, queue)`; `TryPop` swaps buffers when needed.
  - Implications: **not thread-affine** (no thread-local state) but **strictly non-concurrent** for writers (a second concurrent `Put` would observe `null`).
- `Actuarius.Collections.IConsumer<in TData>`: `bool Put(TData value)`. Since `SingleReaderWriterConcurrentQueue<T>` implements `IConsumer<T>`, the queue can be passed **directly** as the `dst` argument of `UnionDataListCompositor.Encode`.
- `ISingleReaderWriterConcurrentQueue<T>` is **not** used by any production code today (only test doubles `StrictSingleWriterQueue`, `ControllableQueue`).

### 3.9 Receive path — what `onReceived` delivers

From `LowLevelTcpSocket.cs` receiver (`Dispatch`, ~2622; pump ~2680-2735):
- If `bytes == buffer.Count`, the full rented buffer is handed over.
- Otherwise `received = ReusablePartialView.Create(buffer, 0, bytes)` — a **right-sized view** (its `Count == bytes`; `Offset` = source offset + relative).
- Ownership transfers to `onReceived`; the consumer must `Release()` it (even on throw).

So `UnionDataListCompositor.PushData(buffer.Array, buffer.Offset, buffer.Count)` is correct on the delivered view.

### 3.10 Legacy client for reference

`Transports/Pontifex.Transport.Net/Tcp/Raw/Reliable/Ack/RawReliableAckClientTcpEndpoint.cs`
(class `RawReliableAckTcpEndpoint : RawReliableAckClientEndpoint`, **internal**, **keep as-is**):
- Ctor: `(string transportName, IpEndPoint remoteEndPoint, IRawReliableAckClientHandler handler, IMemoryRental memory, ILogger log)`; passes `TcpInfo.DefaultMessageMaxSize`, `TcpInfo.DefaultBufferCapacity`, `new PingOptions(180s, 30s)`.
- `BeginConnect()`: `new Socket(addressFamily, Stream, Tcp)`, sets `ReceiveTimeout`/`SendTimeout` = 180s, `NoDelay = true`, `BeginConnect`; callback `EndConnect` → legacy `TcpReceiver` + `TcpSender` (driven by Operarius `SingleJobLogicDriver`), then `FinishConnect(...)`.
- `BeginDisconnect`: `base` then `_socketReceiver?.Stop()`, `_socketSender?.Stop()`.
- `DoSendRaw`: `_socketSender.Send(bufferToSend)`; on non-Ok → `Stop(TextFail)`.
- Uses the **legacy** stack (`TcpSender`/`TcpReceiver` + `UnionDataListCompositor`), NOT `LowLevelTcpSocket`.

Legacy `TcpSender` (`.../Utils/TcpSender.cs`): uses `UnionDataListCompositor.Encode(packet, _memoryRental.ByteArraysPool, _messagePartMaxSize, dst)` with `_messagePartMaxSize = socket.SendBufferSize - 4`. Uses `PacketType` enum (AckRequest/AckResponse/Regular/Ping/Disconnect).

Legacy `TcpReceiver` (`.../Utils/TcpReciever.cs`): `new UnionDataListCompositor(onReceived, memoryRental.CollectablePool, memoryRental.ByteArraysPool, messageMaxSize)` and `PushData(args.Buffer, args.Offset, bytesRead)`.

### 3.11 Supporting types

- `Pontifex.NetSockets.IpEndPoint : IEndPoint` — wraps a `System.Net.EndPoint` (property `EP`), has `Equals`/`GetHashCode`/`ToString`. Ctor: `IpEndPoint(EndPoint endPoint)`.
- `StopReason` classes (`Pontifex/Abstractions/StopReason/StopReasons.cs`): `Unknown`, `UserIntention`, `UnknownRemoteIntention`, `GracefulRemoteIntention`, `TimeOut`, `AckRejected`, `TextFail`, `UserFail`, `ExceptionFail`, `ChainFail`, `Induced`.
- `TcpInfo` (`.../Tcp/Raw/Reliable/Ack/TcpInfo.cs`): `TransportName="tcp"`, `DefaultMessageMaxSize=1 MiB`, `DefaultBufferCapacity=100 MiB`, `ServerConnectionsLimit=20000`, `DefaultDisconnectTimeout=180s`, `AckRequest="AckTcp"`, `AckOKResponse="AckTcp-OK"`.
- `ReceivePools` (`.../Common/LowLevelTcpSocket/ReceivePools.cs`): `Shared` (`MemoryRental.Shared.SharedByteArraysPool`), `Bounded(int)`, `NonRecycling` (`MemoryRental.Shared.ByteArraysPool`).

### 3.12 Test doubles (reference only)

- `Tests/Pontifex.Tests/Endpoints/RawReliable/RawReliableAck/Harness/TestRawReliableAckClientEndpoint.cs`
- `.../TestRawReliableAckServerEndpoint.cs`
- `Tests/Pontifex.Transport.Net.Tests/LowLevelTcpSocket/Harness/LowLevelSocketHarness.cs` (canonical `LowLevelTcpSocket` wiring; uses `FakeSocketAccessor` + `ControllableQueue` + `ReceiveSink`).
- `.../Harness/StrictSingleWriterQueue.cs`, `ControllableQueue.cs`.

---

## 4. Q9 elaborated — where the outbound SPSC source comes from

Facts: see 3.8. The queue is publicly constructible, so no adapter is needed.

Variants:
- (a) Endpoint constructs `new SingleReaderWriterConcurrentQueue<IMultiRefByteArray>(initialCapacity)` internally.
  - Pros: self-contained primitive; caller needn't know LowLevel internals; matches "standalone".
  - Cons: not injectable; tests can't observe/substitute; additional `initialCapacity` knob.
- (b) Ctor-inject `ISingleReaderWriterConcurrentQueue<IMultiRefByteArray>`.
  - Pros: injectable/testable; caller controls capacity.
  - Cons: leaks an `Actuarius.Collections`/LowLevel type into the endpoint API; caller must honor SPSC rules; conflicts with "no test seams yet".
- (c) Construct internally in the public ctor, plus an `internal` overload accepting the queue (future seam).
  - Pros: clean public API + future testability; Cons: slightly more code.

**Recommendation: (a) now, structured for (c) later.**
Open: default `initialCapacity`; and which receive pool (`ReceivePools.Shared` vs `Bounded(n)` vs endpoint `Memory`). Note `ReceivePools.*` use `MemoryRental.Shared`, which may differ from the endpoint's `Memory`. Legacy used `Memory.ByteArraysPool` for encode.

---

## 5. Q10 elaborated — single-writer / thread-affinity

The SPSC impl (3.8) has **no thread-local state**, so it is **not thread-affine**; it only requires **non-concurrent** writers. The serial action queue already guarantees one-at-a-time execution of `DoSendRaw`.

Conclusion:
- Option (a) "rely on the serial action queue's serialization" is **correct and sufficient** — no dedicated publisher thread needed.
- Only remaining choice: **who calls `Sender.Pump()`**. Simplest: at the end of `DoSendRaw`, after `Put`, call `Pump()` (same serial thread). `Pump()` calls `ThrowIfDisposed()` → must guard during/after teardown. Alternative: rely on `SourceDrained` edge-trigger.
- **Recommendation: Put + Pump per send, guarded.**

So Q10 collapses to: confirm "Put + Pump from `DoSendRaw`, no extra publisher thread", and design the post-terminal Pump guard.

---

## 6. Q15 elaborated — when to call `FinishDisconnect()`

- **A. Synchronous** — call `FinishDisconnect()` immediately after initiating shutdown.
  - Pros: no reentrancy; trivial; deterministic; `OnStopped` prompt.
  - Cons: reports Stopped while socket still flushing/closing; buffers may still drain; fuzzy ordering with socket disposal.
- **B. Await `LowLevelTcpSocket.Completion`** then `FinishDisconnect()`.
  - Pros: Stopped truly means "torn down"; socket disposed; clean.
  - Cons: reentrancy (Completion continuation also triggers `Stop()` on remote close → may re-enter `BeginDisconnect`; needs one-shot guards); an un-fired Completion hangs stop forever.
- **C. Bounded wait** — initiate shutdown, await `Completion` up to a deadline (decision 14: **10 s**), on timeout force `DisposeAsync(timeout)`/abort, then `FinishDisconnect()`.
  - Pros: robust, bounded; matches 10 s hint.
  - Cons: most code — timer/Task, exactly-once `FinishDisconnect`, coordinate with remote-close path.

**Recommendation: C**, with a single-shot guard so the local-stop path and the remote-completion path funnel through one teardown routine that calls `FinishDisconnect` exactly once. **Needs confirmation.**

---

## 7. Q16 elaborated — Completion → StopReason mapping (proposed, needs sign-off)

| Signal | Proposed StopReason | Notes |
|---|---|---|
| Our own `Stop()` path (deliberate shutdown) | keep caller's reason; ignore Completion | dispose result is a no-op |
| `ReceiverCompletionReason.RemoteEof` (no `DisconnectMessage` arrived) | `UnknownRemoteIntention` **or** `GracefulRemoteIntention`? | ambiguous: clean FIN vs. abrupt |
| `ReceiverCompletionReason.Aborted` / `SocketCompletionReason.RemoteAborted` | `UnknownRemoteIntention` | remote went away |
| `ReceiverCompletionReason.Faulted` / `SocketCompletionReason.Faulted` (has `Error`) | `ExceptionFail(TransportName, error)` | real socket error |
| `SenderCompletionReason.Faulted` | `ExceptionFail(TransportName, error)` | |
| `DisconnectMessage` frame received | `GracefulRemoteIntention` | already handled by base, not Completion |
| `LocalShutdown` / `Disposed` / `Closed` | no-op | we initiated |

**Main open decision: `RemoteEof` → `GracefulRemoteIntention` or `UnknownRemoteIntention`?**

---

## 8. Remaining open questions (client)

- **C1. Socket configuration.** Client creates the socket, so decision 5 ("leave options to caller") does not apply.
  Options: (a) hard-code `NoDelay = true` (legacy); (b) `Action<Socket>? configureSocket` hook; (c) both.
  **Recommendation: (c).**
- **C2. Connect model & timeout.** (a) `BeginConnect` + OS connect timeout, no socket timeouts (async I/O makes them moot); (b) `BeginConnect` + explicit connect deadline (timer → abort + `ExceptionFail`); (c) sync `Connect` in `Task.Run`.
  **Recommendation: (a)** unless a hard connect deadline is wanted.
- **C3. Protocol-level graceful disconnect.** `DisconnectGracefully()` exists (sends `DisconnectMessage` then gentle stop) but is unused; legacy just shuts TCP down. Should `BeginDisconnect(tryBeGentle=true)` first admit + flush a `DisconnectMessage`? **Recommendation: yes (send + flush on gentle; abort on non-gentle).**
- **C4. Parameter shape.** ~10 params: flat ctor (legacy style) vs options struct/record. Preference?
- **C5. `remoteEndPoint` type.** `IpEndPoint` (legacy) and also pass to base as `IEndPoint`? Confirm.
- **C6. Connect failure.** `Stop(new ExceptionFail(TransportName, ex, "ConnectCallback failed"), false)` and do **not** call `FinishConnect` (legacy style)? Confirm.
- **C7. Keep-alive.** Expose `PingOptions?` with legacy default `(180s, 30s)`, or default `null` (disabled)? Preference.
- **C8. `onCallbackError`.** Log (`Log.wtf`) and `Stop(ExceptionFail)`; should it escalate while not yet connected (log-only until started)? Confirm.
- **C9. `remoteHalfClosePolicy`** for the client: `KeepSendOpen` / `GracefulSendShutdown` / `AbortConnection`? (Client keep-alive likely wants `GracefulSendShutdown` or `KeepSendOpen`.)
- **C10. `receiveBufferSize` default.** `LowLevelTcpSocket.DefaultReceiveBufferSize` (16 KiB) or tie to `MessageMaxByteSize`? (16 KiB streams a 1 MiB message fine.)

Confirmed recommendations already accepted in principle: Q9 (a), Q10 (Put+Pump, no thread), Q15 (C), Q16 (pending RemoteEof choice).

---

## 9. Strawman client flow (for confirmation)

1. **Ctor**: validate params, store; no socket yet.
2. **`Start()` → `BeginConnect()`** on the action queue: create `Socket` (address family from `remoteEndPoint`), apply config, `BeginConnect`.
3. **Callback `EndConnect`**: `new SystemSocket(socket)` → `new SingleReaderWriterConcurrentQueue<IMultiRefByteArray>()` → `UnionDataListCompositor` (receive) → `new LowLevelTcpSocket(accessor, queue, onReceived, receivePool, policy, onCallbackError, receiveBufferSize, gracefulDeadline)` → `Receiver.Start()` → subscribe `Completion` continuation → `FinishConnect(onFail)`.
4. **`OnConnected()`** (base) sends the ack request automatically.
5. **`DoSendRaw(msg)`**: `UnionDataListCompositor.Encode(msg, encodePool, blockSize, queue)`; release `msg`; `Sender.Pump()`; on failure `Stop(TextFail)`.
6. **`onReceived(buf)`**: `compositor.PushData(buf.Array, buf.Offset, buf.Count)`; `buf.Release()` (view is right-sized — confirmed).
7. **`Completion` continuation** → map reason → `Stop(...)` unless already stopping.
8. **`BeginDisconnect(reason, tryBeGentle)`**: base; optionally flush `DisconnectMessage`; `Sender.Shutdown(tryBeGentle, 10s)` + `Receiver.Shutdown()`; bounded wait → `DisposeAsync` → drain queued buffers after `SourceQuiesced` → `FinishDisconnect()`.

---

## 10. Implementation constraints / gotchas to respect

- New classes must be `internal` in `Pontifex.Transport.Net`, namespace `Pontifex.Raw.Reliable.Ack`.
- `netstandard2.1` + `LangVersion 9` + `Nullable enable` + `TreatWarningsAsErrors` — no `record`s with newer syntax issues; keep to C# 9.
- `DoSendRaw` runs on the serial action queue; must not throw; must release the buffer exactly once.
- `onReceived` runs inline on the socket completion thread; short and non-blocking; must release the buffer exactly once (right-sized `ReusablePartialView`).
- `Pump()` throws if disposed → guard post-terminal.
- After `SourceQuiesced` resolves, drain/release queued-but-unsent buffers (decision 17).
- Override `Now` → `DateTime.UtcNow` (decision 21).
- Take socket ownership; `LowLevelTcpSocket` disposes the accessor/socket during teardown.
- Do not add test seams yet (decision 22).
- Rely on base `bufferCapacityBytes` budget (decision 23).
- Keep the legacy `RawReliableAckTcpEndpoint` unchanged (decision 2).

---

## 11. Next steps

1. Get answers to C1–C10 and confirm Q9/Q10/Q15/Q16 recommendations (esp. `RemoteEof` mapping).
2. Produce a concrete client implementation plan.
3. Implement `RawReliableAckTcpClientEndpoint`.
4. Later: server endpoint (`RawReliableAckTcpServerEndpoint`) taking an already-connected socket.
