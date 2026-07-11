# How to Extend the Pontifex Test System

## Architecture Overview

```
TransportStackCatalog         ← You curate stacks here (data)
       │
       ├── AckRawReliableStacks  ──→  [TestFixtureSource]
       │       │
       │       └── PingTests (Api)                          ← Test plan
       │       └── [future] ConnectionTests                 ← Test plan
       │       └── [future] ExchangeTests                   ← Test plan
       │
       ├── [future] NoAckRawUnreliableStacks                ← Another abstraction
       │
       └── [future] ... (7 more abstractions, same pattern)
```

Four layers work together:

| Layer | File(s) | Role |
|-------|---------|------|
| **Stack** | `Core/TransportStack.cs` | Immutable descriptor of one transport configuration (id + URI) |
| **Catalog** | `TransportStacks/*.cs` | Per-type files. Each class implements `IEnumerable<TransportStack>` and serves as both the catalog and the NUnit `[TestFixtureSource]`. |
| **Registry** | `Core/TransportRegistry.cs` | Shared `TransportBuilder` with all constructors registered. Static class with eager init. |
| **Harness** | `Core/TestHarness.cs` | Builds transports, wires `ClientSideApi`/`ServerSideApiFactory`, starts both, exposes API instances. |

The flow:

1. NUnit discovers a `[TestFixtureSource]` and calls the source class (e.g. `AckRawReliableStacks`)
2. For each `TransportStack`, NUnit instantiates the test fixture
3. The fixture constructor stores the stack; each `[Test]` method creates a `TestHarness`
4. `TestHarness` uses `TransportRegistry.Builder` to build server + client transports from the stack's URI
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
    Logger = new Logger(Array.Empty<ILogConsumer>());
    Memory = MemoryRental.Shared;
    Builder = new TransportBuilder(ConvertersGraph.Default);

    Builder.RegisterTransport(new AckRawDirectConstructor());
    Builder.RegisterTransport(new AckRawTcpConstructor());
    Builder.RegisterTransport(new MyNewWebSocketConstructor());  // ← ADD
}
```

The `TransportBuilder` now knows how to construct this transport from a URI.

### Step B: Add Stacks to the Catalog

Create a new file in `TransportStacks/` for the transport type, or add to an existing one.
Each file is a self-contained `IEnumerable<TransportStack>` that NUnit uses directly as a `[TestFixtureSource]`.

**Example:** `TransportStacks/AckRawReliableStacks.cs`:

```csharp
using System.Collections;

namespace Pontifex.Test;

public class AckRawReliableStacks : IEnumerable<TransportStack>
{
    public static readonly TransportStack Direct = new(
        id: "direct",
        transportUri: "transport://direct|test-srv"
    );

    public static readonly TransportStack Tcp = new(
        id: "tcp",
        transportUri: $"transport://tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/10"
    );

    // NEW:
    public static readonly TransportStack WebSocket = new(
        id: "ws",
        transportUri: $"transport://ws|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}"
    );

    public static readonly TransportStack TcpEncrypted = new(
        id: "tcp+encrypt",
        transportUri: $"transport://encrypt|tcp|127.0.0.1:{DynamicPortAllocator.GetRandomPort()}/10"
    );

    public IEnumerator<TransportStack> GetEnumerator()
    {
        yield return Direct;
        yield return Tcp;
        yield return WebSocket;       // ← ADD
        yield return TcpEncrypted;     // ← ADD
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

If the new transport produces a different `TransportType` (e.g. `NoAckRawUnreliable`), create a new file `TransportStacks/NoAckRawUnreliableStacks.cs` with its own class.

### Result

All existing test plans for that abstraction automatically test the new stack. No test code changes needed.

---

## 2. Adding a New Test Plan

A "test plan" is a set of related tests for one transport abstraction. There are two kinds:

### 2A. Raw Transport Test Plan (tests ITransport directly)

Create a new fixture class that extends a base fixture for the abstraction.

**Directory convention:** `TestPlans/{TransportTypeName}/{TestPlanName}/{TestName}Tests.cs`

**Example:** `TestPlans/AckRawReliable/Connection/ConnectionTests.cs`

```csharp
using Pontifex.Test;

namespace Pontifex.AckRawReliable.Tests.Connection;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class ConnectionTests
{
    private readonly TransportStack _stack;

    public ConnectionTests(TransportStack stack)
    {
        _stack = stack;
    }

    private (IAckRawReliableClient client, IAckRawReliableServer server) CreateTransports()
    {
        var desc = TransportRegistry.DescriptionFactory.FromUri(_stack.TransportUri);
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.Logger;

        var client = (IAckRawReliableClient)TransportRegistry.Builder.BuildClient(desc, memory, logger);
        var server = (IAckRawReliableServer)TransportRegistry.Builder.BuildServer(desc, memory, logger);
        return (client, server);
    }

    [Test]
    public async Task Client_connects_to_server()
    {
        var (client, server) = CreateTransports();

        var clientConnected = new TaskCompletionSource<bool>();
        var serverConnected = new TaskCompletionSource<bool>();

        var clientHandler = new TestClientHandler(onConnected: _ => clientConnected.SetResult(true));
        var serverAcknowledger = new TestServerAcknowledger(onConnected: _ => serverConnected.SetResult(true));

        client.Init(clientHandler);
        server.Init(serverAcknowledger);

        server.Start(_ => { });
        client.Start(_ => { });

        await Task.WhenAll(
            clientConnected.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            serverConnected.Task.WaitAsync(TimeSpan.FromSeconds(5))
        );

        Assert.That(clientConnected.Task.IsCompletedSuccessfully, Is.True);
        Assert.That(serverConnected.Task.IsCompletedSuccessfully, Is.True);

        client.Stop(new UserIntention("test", "done"));
        server.Stop(new UserIntention("test", "done"));
    }
}
```

**Note:** For raw transport test plans, you create handler implementations directly (implementing `IAckRawReliableClientHandler`, `IAckRawReliableServerHandler`, etc.) rather than using the API layer. Each abstraction has different handler interfaces — check the corresponding `Abstractions/Transports/` directory for the exact interfaces.

### 2B. API-Based Test Plan (tests through ApiRoot)

Extends an existing API protocol or creates a new one, then uses `ApiTestHarness`.

**Example:** Adding a disconnect test using the existing `PingApi`:

```csharp
using Pontifex.Test;
using Pontifex.AckRawReliable.Tests.Ping;
using Pontifex.StopReasons;

namespace Pontifex.AckRawReliable.Tests.Disconnect;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class DisconnectTests
{
    private readonly TransportStack _stack;

    public DisconnectTests(TransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task Graceful_shutdown_produces_correct_stop_reasons()
    {
        var harness = new ApiTestHarness<PingApiClient, PingApiServer>(_stack);
        try
        {
            await harness.StartAsync();

            StopReason? clientReason = null;
            StopReason? serverReason = null;

            harness.ClientApi.Disconnected += r => clientReason = r;
            if (harness.ServerApi != null)
                harness.ServerApi.Disconnected += r => serverReason = r;

            harness.ClientApi.GracefulShutdown(TimeSpan.FromMilliseconds(100));
            await Task.Delay(500);

            Assert.That(clientReason, Is.TypeOf<UserIntention>());
            Assert.That(serverReason, Is.TypeOf<GracefulRemoteIntention>());
        }
        finally
        {
            harness.Dispose();
        }
    }
}
```

---

## 3. Adding a New API-Based Test Plan (with a New Protocol)

When you need a new API protocol for testing (e.g. a streaming API, a complex stateful API):

### Step A: Define the Protocol

Create a file in `TestPlans/AckRawReliable/TestProtocols/YourProtocolName/` together with its test file:

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

```csharp
using Pontifex.Test;
using Pontifex.AckRawReliable.Tests.Stream;

namespace Pontifex.AckRawReliable.Tests.Stream;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class StreamTests
{
    private readonly TransportStack _stack;

    public StreamTests(TransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task Stream_transfers_all_chunks()
    {
        var harness = new ApiTestHarness<StreamApiClient, StreamApiServer>(_stack);
        try
        {
            await harness.StartAsync();

            var receivedChunks = new List<StreamChunk>();
            harness.ClientApi.OnChunk.SetProcessor(chunk => receivedChunks.Add(chunk));

            const int chunkCount = 10;
            const int chunkSize = 1024;

            var totalBytes = await harness.ClientApi.RequestStreamAsync(chunkCount, chunkSize);

            Assert.That(receivedChunks, Has.Count.EqualTo(chunkCount));
            Assert.That(totalBytes, Is.EqualTo(chunkCount * chunkSize));
        }
        finally
        {
            harness.Dispose();
        }
    }
}
```

---

## 4. Key Classes Reference

### TransportStack

Immutable value object. Create with `new TransportStack(id, transportUri)`:

| Parameter | Type | Example | Purpose |
|-----------|------|---------|---------|
| `id` | `string` | `"direct+zip"` | Human-readable identifier; appears in test output |
| `transportUri` | `string` | `"transport://zip\|direct\|test-srv"` | Full URI for TransportBuilder |

### TransportStacks

Per-type files in `TransportStacks/`. Each file contains a class implementing `IEnumerable<TransportStack>` that serves as both the stack catalog for its transport type and the NUnit `[TestFixtureSource]`. Static `readonly` fields on the class provide direct access to individual stacks.

### TransportRegistry

Static class. Exposes:
- `TransportRegistry.Logger` — `ILogger` singleton
- `TransportRegistry.Memory` — `IMemoryRental` singleton (`MemoryRental.Shared`)
- `TransportRegistry.Builder` — `TransportBuilder` with all constructors registered
- `TransportRegistry.DescriptionFactory` — for parsing URIs to descriptions

### ApiTestHarness<TClientApi, TServerApi>

Creates a running client-server API pair over a transport stack. Usage:

```csharp
var harness = new ApiTestHarness<MyClientApi, MyServerApi>(stack);
try
{
    await harness.StartAsync();           // builds transports, starts them, waits for API connection
    // use: harness.ClientApi, harness.ServerApi
}
finally
{
    harness.Dispose();                    // stops both transports
}
```

Where `TClientApi : class, IApiRoot, new()` and `TServerApi : class, IApiRoot, new()`.

### DynamicPortAllocator

```csharp
int port = DynamicPortAllocator.GetRandomPort();  // returns a free ephemeral port
```

Use for TCP/UDP stacks that need real network sockets. Avoids port conflicts during parallel test execution.

---

## 5. File Organization Convention

```
Tests/Pontifex.Tests/
├── Core/                                    # Test infrastructure
│   ├── TransportStack.cs                    # Immutable stack descriptor (id + uri)
│   ├── TransportRegistry.cs                 # Shared TransportBuilder + TestLogger
│   ├── DynamicPortAllocator.cs              # Random port helper
│   └── TestHarness.cs                       # ApiTestHarness + TestServerSideApiInstance
├── TransportStacks/                         # Per-type stack catalogs (also NUnit sources)
│   ├── AckRawReliableStacks.cs
│   └── NoAckRawReliableStacks.cs
├── TestPlans/                               # All test plans
│   ├── AckRawReliable/
│   │   └── TestProtocols/                   # API-based test plans for AckRawReliable
│   │       └── Ping/
│   │           ├── PingApi.cs               # Protocol declarations (structs, ApiRoot, client/server)
│   │           └── PingTests.cs             # Tests using this protocol
│   ├── NoAckRawReliable/
│   │   └── Ping/
│   │       └── PingTests.cs                 # Raw exchange test (no API layer)
│   ├── AckRawUnreliable/                    # [future]
│   ├── NoAckRawUnreliable/                  # [future]
│   ├── AckRRReliable/                       # [future]
│   ├── AckRRUnreliable/                     # [future]
│   ├── NoAckRRReliable/                     # [future]
│   └── NoAckRRUnreliable/                   # [future]
```

---

## 6. Transport URI Format Reference

All URIs follow the pattern `transport://<scheme>|<params>`. The `DescriptionFactory` parses these recursively for nested wrappers.

| Transport | URI Example | Notes |
|-----------|-------------|-------|
| Direct | `transport://direct\|server-name` | Server name is arbitrary; client uses same name to connect |
| TCP | `transport://tcp\|127.0.0.1:9000/10` | `/10` = disconnect timeout in seconds |
| Convert | `transport://convert\|NoAckRawReliable:udp\|127.0.0.1:9000` | Builds inner transport, converts via ConvertersGraph |
| Zip wrapper | `transport://zip\|9:direct\|srv` | `9` = compression level (optional; default 9) |
| Log wrapper | `transport://log\|direct\|srv` | Wraps inner transport with logging |
| Reconnectable | `transport://reconnectable\|30:direct\|srv` | `30` = reconnect timeout in seconds |

Wrapper URIs nest recursively:
```
transport://log|zip|tcp|127.0.0.1:9000/10
                ↑    ↑    ↑
                log  zip  tcp (innermost)
```

---

## 7. Debugging Tips

- Use `TransportStacks.Direct` for the fastest feedback loop — it has no network dependency.
- Network stacks (`Tcp`) need a few seconds for connection; the harness has a 10-second timeout.
- If a test hangs, check that the server transport starts before the client (the harness already does this).
- `TransportRegistry.Logger` uses an empty consumer list — it swallows logs. To see logs, replace with `new Logger(new ILogConsumer[] { new ConsoleConsumer(new DefaultFormatter()) })` if your Scriba version supports those types.
