# How to Extend the Pontifex Test System

## Architecture Overview

```
TransportStacks/                     ← You curate stacks here (data)
       │
       └── AckRawReliableStacks     ──→  [TestFixtureSource]
       │       │
       │       ├── ApiPingTests                      ← Test plan (API-based)
       │       ├── ApiConnectDisconnectTests         ← Test plan (API-based)
       │       └── ApiConnectReceiveKickTests        ← Test plan (API-based)
       │
       └── [future] ... (7 more abstractions, same pattern)
```

Five layers work together:

| Layer | File(s) | Role |
|-------|---------|------|
| **Stack** | `Core/Stacks/ITransportStack.cs`, `StaticTransportStack.cs`, `DynamicTransportStack.cs` | Interface + implementations describing one transport configuration |
| **Factory** | `Core/TransportFactory.cs` | Holds parsed `IDescription`, calls `TransportBuilder.BuildServer/Client` |
| **Catalog** | `TransportStacks/*.cs` | Per-type files. Each class implements `IEnumerable<ITransportStack>` and serves as both the catalog and the NUnit `[TestFixtureSource]`. |
| **Registry** | `Core/TransportRegistry.cs` | Shared `TransportBuilder` with all constructors registered. Static class with eager init. |
| **Harness** | `Core/TestHarness.cs` | Builds transports, wires `ClientSideApi`/`ServerSideApiFactory`, starts both, exposes API instances. |

The flow:

1. NUnit discovers a `[TestFixtureSource]` and calls the source class (e.g. `AckRawReliableStacks`)
2. For each `ITransportStack`, NUnit instantiates the test fixture
3. The fixture constructor stores the stack; each `[Test]` method creates a harness or uses `_stack.GetTransportFactory()`
4. `TransportFactory` uses `TransportRegistry.Builder` to build server + client transports from the stack's description
5. `TestHarness` wires API handlers, starts transports, and returns control to the test
6. The test exercises the API, then disposes the harness

---

## 1. Adding a New Transport Implementation or Wrapper

When a new transport (e.g. WebSocket) or wrapper (e.g. encryption) is created, three steps integrate it into the test system.

### Step A: Register the Constructor

In `Core/TransportRegistry.cs`, add the constructor to the static initializer:

```csharp
static TransportRegistry()
{
    Memory = MemoryRental.Shared;
    Builder = new TransportBuilder(ConvertersGraph.Default);

    Builder.RegisterTransport(new AckRawReliableDirectConstructor());
    Builder.RegisterTransport(new AckRawReliableTcpConstructor());
    Builder.RegisterTransport(new NoAckRawUnreliableDirectConstructor());
    Builder.RegisterTransport(new NoAckRawUdpConstructor());
    Builder.RegisterTransport(new MyNewWebSocketConstructor());  // ← ADD
}
```

The `TransportBuilder` now knows how to construct this transport from a URI.

### Step B: Add Stacks to the Catalog

Create a new file in `TransportStacks/` for the transport type, or add to an existing one.
Each file is a self-contained `IEnumerable<ITransportStack>` that NUnit uses directly as a `[TestFixtureSource]`.

**Example:** `TransportStacks/AckRawReliableStacks.cs`:

```csharp
using System.Collections;

namespace Pontifex.Tests;

public class AckRawReliableStacks : IEnumerable<ITransportStack>
{
    public IEnumerator<ITransportStack> GetEnumerator()
    {
        yield return new DynamicTransportStack("direct",
            () => $"transport://direct|srv-{Guid.NewGuid()}");

        yield return new DynamicTransportStack("tcp",
            () => $"transport://tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/60");

        // NEW:
        yield return new DynamicTransportStack("ws",
            () => $"transport://ws|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}");

        yield return new DynamicTransportStack("tcp+encrypt",
            () => $"transport://encrypt|tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/60");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

Two stack implementations are available:
- **`DynamicTransportStack`** — takes a `Func<string>` URI provider; the URI is parsed fresh each time `GetTransportFactory()` is called. Use this when the URI contains a runtime value (e.g. a dynamic port).
- **`StaticTransportStack`** — takes a fixed URI string; parsed once at construction. Use this for URIs that do not change.

If the new transport produces a different `TransportType` (e.g. `NoAckRawUnreliable`), create a new file `TransportStacks/NoAckRawUnreliableStacks.cs` with its own class.

### Result

All existing test plans for that abstraction automatically test the new stack. No test code changes needed.

---

## 2. Adding a New Test Plan

A "test plan" is a set of related tests for one transport abstraction. There are two kinds:

### 2A. Raw Transport Test Plan (tests ITransport directly)

Create a new fixture class and use `_stack.GetTransportFactory()` to build transports.

**Directory convention:** `TestPlans/{Ack|NoAck}/{Raw|RR}/{Reliable|Unreliable}/{TestPlanName}/{TestName}Tests.cs`

**Example:** `TestPlans/Ack/Raw/Reliable/HighLoadDataTransfer/HighLoadDataTransfer.cs`:

```csharp
using System.Collections.Concurrent;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Tests
{
    [TestFixtureSource(typeof(AckRawReliableStacks))]
    public class HighLoadDataTransfer
    {
        private const int MinN = 200;
        private const int MaxN = 2000;

        private static readonly IMultiRefReadOnlyByteArray AckRequest =
            new StaticReadOnlyByteArray("STRESS-LOGIC-ACK-REQUEST"u8.ToArray());

        [Test]
        public async Task Transfer_Load()
        {
            var memory = TransportRegistry.Memory;
            var factory = _stack.GetTransportFactory();

            var server = (IAckRawReliableServer)factory.BuildServer();
            var client = (IAckRawReliableClient)factory.BuildClient();

            // wire OnReceived handlers, send messages, verify responses
            // ...
        }
    }
}
```

For raw transport test plans, you handle protocol at the `OnReceived` level using `IAckRawReliableClient`/`IAckRawReliableServer` (or the corresponding interfaces for other abstractions). No API layer is involved.

### 2B. API-Based Test Plan (tests through ApiRoot)

Every API-based test plan follows the **canonical structure**:

1. API types (structs, `ApiRoot`, client/server classes) defined **inline** in the test file — no separate API file
2. A single `Run*` method with `(int clientCount, int concurrency)` parameters
3. Creates a shared server, then runs clients via `Parallel.ForEachAsync`
4. Each client connects, performs work, calls `GracefulShutdown`, asserts no `AnyFail` stop reasons
5. Three test cases: `Test_Single` (1, 1), `Test_Small` (`GetSmallTestSize()`), `Test_Big` (`GetBigTestSize()`)

**Example:** `TestPlans/Ack/Raw/Reliable/TestProtocols/ApiPing/PingTests.cs`:

```csharp
using System.Collections.Concurrent;
using Archivarius;
using Pontifex.Ack.Raw;
using Pontifex.Api;
using Pontifex.StopReasons;
using Pontifex.Tests;

namespace Pontifex.AckRawReliable.Tests.ApiPing;

// --- API definitions (inline, no separate file) ---

public struct PingRequest : IDataStruct
{
    public int Seq;
    public void Serialize(ISerializer serializer) => serializer.Add(ref Seq);
}

public struct PongResponse : IDataStruct
{
    public int Seq;
    public void Serialize(ISerializer serializer) => serializer.Add(ref Seq);
}

public class PingApi : ApiRoot
{
    public readonly RRDecl<PingRequest, PongResponse> Ping = new();
}

public class PingApiClient : PingApi
{
    public Task<PongResponse> SendPing(int seq)
        => Ping.RequestAsync(new PingRequest { Seq = seq });
}

public class PingApiServer : PingApi
{
    public PingApiServer()
    {
        Ping.SetProcessor(r => r.Response(new PongResponse { Seq = r.Data.Seq }));
    }
}

// --- Test fixture ---

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class PingTests
{
    private readonly ITransportStack _stack;

    public PingTests(ITransportStack stack) => _stack = stack;

    private async Task RunPing(int clientCount, int concurrency)
    {
        const int pingCount = 100;
        Console.WriteLine($"Run '{clientCount}' clients, '{pingCount}' pings each, using '{concurrency}' tasks");

        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(true);
        var factory = _stack.GetTransportFactory(true);

        var serverTransport = (IAckRawReliableServer)factory.BuildServer();
        var serverStoppedTcs = new TaskCompletionSource<StopReason>();

        var serverFactory = new ServerSideApiFactory<PingApiServer>(
            _ => new TestServerSideApiInstance<PingApiServer>(new PingApiServer(), memory, logger));

        Assert.That(serverTransport.Init(serverFactory), Is.True);
        Assert.That(serverTransport.Start(reason => serverStoppedTcs.TrySetResult(reason)), Is.True);

        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, clientCount),
            new ParallelOptions { MaxDegreeOfParallelism = concurrency },
            async (_, ct) =>
            {
                try
                {
                    var api = new PingApiClient();
                    var handler = new ClientSideApi(api, memory, logger);
                    var connectedTcs = new TaskCompletionSource();
                    var disconnectedTcs = new TaskCompletionSource<StopReason>();
                    var stoppedTcs = new TaskCompletionSource<StopReason>();

                    handler.Connected += _ => connectedTcs.TrySetResult();
                    api.Disconnected += reason => disconnectedTcs.TrySetResult(reason);

                    var transport = (IAckRawReliableClient)factory.BuildClient();
                    if (!transport.Init(handler)) { errors.Add("Init failed"); return; }
                    if (!transport.Start(reason => stoppedTcs.TrySetResult(reason))) { errors.Add("Start failed"); return; }

                    await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

                    for (var i = 0; i < pingCount; i++)
                    {
                        var response = await api.SendPing(i);
                        if (response.Seq != i) { errors.Add($"Seq mismatch at {i}"); return; }
                    }

                    api.GracefulShutdown(TimeSpan.FromMilliseconds(100));

                    var disconnectReason = await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                    if (disconnectReason is AnyFail) { errors.Add($"Error: {disconnectReason}"); return; }

                    await stoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors.Add($"{ex.GetType().Name}: {ex.Message}");
                }
            });

        Assert.That(errors, Is.Empty);
        serverTransport.Stop(new UserIntention("test", "complete"));
        var serverStopReason = await serverStoppedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(serverStopReason, Is.Not.InstanceOf(typeof(AnyFail)));
    }

    [Test]
    [Category("Small")]
    public async Task Test_Single() => await RunPing(1, 1);

    [Test]
    [Category("Small")]
    public async Task Test_Small()
    {
        var (count, concurrency) = _stack.GetSmallTestSize();
        await RunPing(count, concurrency);
    }

    [Test]
    [Category("Big")]
    public async Task Test_Big()
    {
        var (count, concurrency) = _stack.GetBigTestSize();
        await RunPing(count, concurrency);
    }
}
```

**Naming convention:** API-based test directories use the `Api` prefix (e.g. `ApiPing/`, `ApiConnectDisconnect/`, `ApiConnectReceiveKick/`). This distinguishes them from raw transport tests which do not use the API layer.

**Note:** `ApiTestHarness<TClientApi, TServerApi>` is available as a convenience wrapper for simple single-client scenarios:

```csharp
var harness = new ApiTestHarness<PingApiClient, PingApiServer>(_stack, failIfError: true);
await harness.StartAsync();
// use harness.ClientApi, harness.ServerApi
harness.Dispose();
```

---

## 3. Adding a New API-Based Test Plan (with a New Protocol)

When you need a new API protocol for testing (e.g. a streaming API, a complex stateful API):

### Step A: Define the Protocol

Create a file in `TestPlans/Ack/Raw/Reliable/TestProtocols/YourProtocolName/` together with its test file:

```csharp
using Archivarius;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;

namespace Pontifex.AckRawReliable.Tests.Stream;

// --- Message structs with proper Archivarius serialization ---

public struct StreamRequest : IDataStruct
{
    public int ChunkCount;
    public int ChunkSize;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref ChunkCount);
        serializer.Add(ref ChunkSize);
    }
}

public struct StreamChunk : IDataStruct
{
    public int Index;
    public byte[]? Payload;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref Index);
        serializer.Add(ref Payload);
    }
}

public struct StreamComplete : IDataStruct
{
    public int TotalBytes;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref TotalBytes);
    }
}

// --- ApiRoot ---

public class StreamApi : ApiRoot
{
    public readonly RRDecl<StreamRequest, StreamComplete> RequestStream = new();
    public readonly S2CMessageDecl<StreamChunk> OnChunk = new();
}

// --- Client ---

public class StreamApiClient : StreamApi
{
    public async Task<int> RequestStreamAsync(int chunkCount, int chunkSize)
    {
        var response = await RequestStream.RequestAsync(new StreamRequest
        {
            ChunkCount = chunkCount,
            ChunkSize = chunkSize
        });
        return response.TotalBytes;
    }
}

// --- Server ---

public class StreamApiServer : StreamApi
{
    public StreamApiServer()
    {
        RequestStream.SetProcessor(r =>
        {
            var totalBytes = 0;
            for (var i = 0; i < r.Data.ChunkCount; i++)
            {
                var payload = new byte[r.Data.ChunkSize];
                new Random().NextBytes(payload);
                totalBytes += payload.Length;

                OnChunk.Send(new StreamChunk { Index = i, Payload = payload });
            }

            r.Response(new StreamComplete { TotalBytes = totalBytes });
        });
    }
}
```

### Step B: Write the Test Plan

Every API-based test plan must follow the **canonical structure** (see §2B for the full reference):

- API types defined inline in the test file
- A single `Run*` method with `(int clientCount, int concurrency)` params
- Server built and started first
- `Parallel.ForEachAsync` for concurrent clients
- Each client: connects, performs work, calls `GracefulShutdown`, asserts no `AnyFail` reasons
- Three test methods: `Test_Single`, `Test_Small`, `Test_Big`
- `[Category("Small")]` on Single/Small, `[Category("Big")]` on Big

**Example:**

```csharp
[TestFixtureSource(typeof(AckRawReliableStacks))]
public class StreamTests
{
    private readonly ITransportStack _stack;

    public StreamTests(ITransportStack stack) => _stack = stack;

    private async Task RunStream(int clientCount, int concurrency)
    {
        // ... canonical Run* pattern (see §2B) ...
    }

    [Test]
    [Category("Small")]
    public async Task Test_Single() => await RunStream(1, 1);

    [Test]
    [Category("Small")]
    public async Task Test_Small()
    {
        var (count, concurrency) = _stack.GetSmallTestSize();
        await RunStream(count, concurrency);
    }

    [Test]
    [Category("Big")]
    public async Task Test_Big()
    {
        var (count, concurrency) = _stack.GetBigTestSize();
        await RunStream(count, concurrency);
    }
}
```

---

## 4. Rule: XML Doc Comments on Every Test Method

Every `[Test]` method must have a short but detailed `<summary>` XML doc comment describing what the test case validates. This ensures test intent is clear without reading the implementation.

```csharp
/// <summary>
/// A single client connects, sends 100 sequential ping requests,
/// and verifies each response carries the correct sequence number.
/// </summary>
[Test]
[Category("Small")]
public async Task Test_Single()
{
    await RunPing(1, 1);
}
```

A good XML doc should:
- State **what** is being tested (the scenario)
- State **what** the expected outcome is
- Be concise (2-4 lines is sufficient)

Test methods may also carry an NUnit `[Category]` attribute for filtering:

| Category | Meaning | Run command |
|----------|---------|-------------|
| `"Fast"` | Quick smoke-level tests (no long waits, no heavy parallelism) | `dotnet test --filter "TestCategory=Fast"` |
| _(none)_ | Default — no category means general/acceptance test | `dotnet test` (all) |

Categories compose with `AND`/`OR` in the filter expression, e.g. `--filter "TestCategory=Fast|TestCategory=Slow"`.

---

## 5. Key Classes Reference

### ITransportStack

Interface in `Core/Stacks/ITransportStack.cs`:

| Member | Type | Purpose |
|--------|------|---------|
| `Id` | `string` | Human-readable identifier; appears in test output |
| `GetTransportFactory()` | `TransportFactory` | Returns a factory that builds client/server transports |

Two implementations:
- **`StaticTransportStack(string id, string transportUri)`** — Parses the URI once at construction. Use for fixed URIs.
- **`DynamicTransportStack(string id, Func<string> uriProvider)`** — Parses the URI each time `GetTransportFactory()` is called. Use when the URI contains runtime values like dynamic ports.

### TransportFactory

`Core/TransportFactory.cs`. Created by a stack, wraps `IDescription` and exposes:
- `BuildServer()` → `ITransport`
- `BuildClient()` → `ITransport`

### TransportStacks

Per-type files in `TransportStacks/`. Each file contains a class implementing `IEnumerable<ITransportStack>` that serves as both the stack catalog for its transport type and the NUnit `[TestFixtureSource]`.

### TransportRegistry

Static class in `Core/TransportRegistry.cs`. Exposes:
- `TransportRegistry.Memory` — `IMemoryRental` singleton (`MemoryRental.Shared`)
- `TransportRegistry.Builder` — `TransportBuilder` with all constructors registered
- `TransportRegistry.DescriptionFactory` — for parsing URIs to descriptions
- `TransportRegistry.GetLogger(bool failIfError)` — creates a new `ILogger`; when `failIfError` is true, any error-severity message fails the test

### ApiTestHarness<TClientApi, TServerApi>

Creates a running client-server API pair over a transport stack. Usage:

```csharp
var harness = new ApiTestHarness<MyClientApi, MyServerApi>(stack, failIfError: true);
try
{
    await harness.StartAsync();  // builds transports, starts them, waits for API connection
    // use: harness.ClientApi, harness.ServerApi
}
finally
{
    harness.Dispose();  // stops both transports with UserIntention
}
```

Where `TClientApi : class, IApiRoot, new()` and `TServerApi : class, IApiRoot, new()`.

### DynamicPortAllocator

```csharp
int port = DynamicPortAllocator.GetRandomPort();  // returns a free ephemeral port
```

Use for TCP/UDP stacks that need real network sockets. Avoids port conflicts during parallel test execution.

---

## 6. File Organization Convention

```
Tests/Pontifex.Transport.Tests/
├── Core/                                    # Test infrastructure
│   ├── DynamicPortAllocator.cs              # Random port helper
│   ├── TestHarness.cs                       # ApiTestHarness + TestServerSideApiInstance
│   ├── TransportFactory.cs                  # Builds client/server transports from IDescription
│   ├── TransportRegistry.cs                 # Shared TransportBuilder, Memory, GetLogger()
│   └── Stacks/                              # Stack interface + implementations
│       ├── ITransportStack.cs               # Interface (Id + GetTransportFactory)
│       ├── StaticTransportStack.cs          # Fixed URI, parsed once
│       └── DynamicTransportStack.cs         # URI from provider function, parsed per call
├── TransportStacks/                         # Per-type stack catalogs (also NUnit sources)
│   └── AckRawReliableStacks.cs
├── TestPlans/                               # All test plans
│   └── Ack/Raw/Reliable/                    # AckRawReliable test plans
│       ├── TestProtocols/                   # API-based test plans (name prefix 'Api')
│       │   ├── ApiPing/PingTests.cs         # API + tests in one file (canonical pattern)
│       │   ├── ApiConnectDisconnect/ConnectDisconnectTests.cs
│       │   └── ApiConnectReceiveKick/ConnectReceiveKickTests.cs
│       ├── Connect_ServerGracefullDisconnect/Connect_ServerGracefullDisconnect.cs  # Raw handler test
│       └── HighLoadDataTransfer/HighLoadDataTransfer.cs                             # Raw handler test
```

---

## 7. Transport URI Format Reference

All URIs follow the pattern `transport://<scheme>|<params>`. The `DescriptionFactory` parses these recursively for nested wrappers.

| Transport | URI Example | Notes |
|-----------|-------------|-------|
| Direct | `transport://direct\|server-name` | Server name is arbitrary; client uses same name to connect |
| TCP | `transport://tcp\|127.0.0.1:9000/60` | `/60` = disconnect timeout in seconds |
| Convert | `transport://convert\|AckRawReliable:udp\|127.0.0.1:9000` | Builds an inner NoAckRawUnreliable (e.g. Udp) transport and converts it via ConvertersGraph |
| Zip wrapper | `transport://zip\|9:direct\|srv` | `9` = compression level (optional; default 9) |
| Log wrapper | `transport://log\|direct\|srv` | Wraps inner transport with logging |
| Reconnectable | `transport://reconnectable\|30:direct\|srv` | `30` = reconnect timeout in seconds |

Wrapper URIs nest recursively:
```
transport://log|zip|tcp|127.0.0.1:9000/60
                ↑    ↑    ↑
                log  zip  tcp (innermost)
```

---

## 8. Debugging Tips

- Use the `"direct"` stack for the fastest feedback loop — it has no network dependency.
- Network stacks (`"tcp"`) need a few seconds for connection; the harness has a 10-second timeout.
- If a test hangs, check that the server transport starts before the client (the harness already does this).
- `TransportRegistry.GetLogger(failIfError: false)` returns a logger that writes to console at `WARN` level. Pass `failIfError: true` to fail the test on any error-severity log message.
