# How to Extend the Pontifex Test System

## Architecture Overview

```
TransportStacks/                     ← You curate stacks here (data)
       │
       ├── AckRawReliableStacks     ──→  [TestFixtureSource]
       │       │
       │       ├── PingTests                          ← Test plan
       │       └── InvariantCheckerTests              ← Test plan
       │
       ├── NoAckRawReliableStacks    ──→  [TestFixtureSource]
       │       │
       │       └── PingTests                          ← Test plan
       │
       └── [future] ... (6 more abstractions, same pattern)
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

    Builder.RegisterTransport(new AckRawDirectConstructor());
    Builder.RegisterTransport(new AckRawTcpConstructor());
    Builder.RegisterTransport(new NoAckRawReliableDirectConstructor());
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

**Directory convention:** `TestPlans/{TransportTypeName}/{TestPlanName}/{TestName}Tests.cs`

**Example:** `TestPlans/NoAckRawReliable/Ping/PingTests.cs`:

```csharp
using Actuarius.Memory;
using Pontifex.NoAck.Raw;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.NoAckRawReliable.Tests.Ping;

[TestFixtureSource(typeof(NoAckRawReliableStacks))]
public class PingTests
{
    private readonly ITransportStack _stack;

    public PingTests(ITransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task Ping_100_Times()
    {
        var memory = TransportRegistry.Memory;
        var factory = _stack.GetTransportFactory();

        var server = (INoAckRawReliableServer)factory.BuildServer();
        var client = (INoAckRawReliableClient)factory.BuildClient();

        // wire OnReceived handlers, send 100 messages, verify responses
        // ...
    }
}
```

For raw transport test plans, you handle protocol at the `OnReceived` level using `INoAckRawReliableClient`/`INoAckRawReliableServer` (or the corresponding interfaces for other abstractions). No API layer is involved.

### 2B. API-Based Test Plan (tests through ApiRoot)

Extends an existing API protocol or creates a new one, then uses `ApiTestHarness`.

**Example:** `TestPlans/AckRawReliable/TestProtocols/InvariantChecker/InvariantCheckerTests.cs`:

```csharp
using Pontifex.Ack.Raw;
using Pontifex.Api;
using Pontifex.StopReasons;
using Pontifex.Tests;

namespace Pontifex.AckRawReliable.Tests.InvariantChecker;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class InvariantCheckerTests
{
    private readonly ITransportStack _stack;

    public InvariantCheckerTests(ITransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task ConnectDisconnect()
    {
        var memory = TransportRegistry.Memory;
        var logger = TransportRegistry.GetLogger(failIfError: true);
        var factory = _stack.GetTransportFactory();

        var clientTransport = (IAckRawReliableClient)factory.BuildClient();
        var serverTransport = (IAckRawReliableServer)factory.BuildServer();

        var clientApi = new InvariantCheckerApiClient();
        var serverApi = new InvariantCheckerApiServer();

        var clientHandler = new ClientSideApi(clientApi, memory, logger);
        var serverInstance = new TestServerSideApiInstance<InvariantCheckerApiServer>(
            serverApi, memory, logger);
        var serverFactory = new ServerSideApiFactory<InvariantCheckerApiServer>(
            _ => serverInstance);

        clientTransport.Init(clientHandler);
        serverTransport.Init(serverFactory);

        // start, wait for connect, graceful shutdown, verify StopReason types
    }
}
```

**Note:** `ApiTestHarness<TClientApi, TServerApi>` is available as a convenience wrapper that handles initialization, connection waiting, and teardown. It requires a `bool failIfError` parameter. Use it when the test plan does not need custom handler wiring:

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
using Pontifex.Tests;

namespace Pontifex.AckRawReliable.Tests.Stream;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class StreamTests
{
    private readonly ITransportStack _stack;

    public StreamTests(ITransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task Stream_transfers_all_chunks()
    {
        var harness = new ApiTestHarness<StreamApiClient, StreamApiServer>(_stack, failIfError: true);
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

## 4. Rule: XML Doc Comments on Every Test Method

Every `[Test]` method must have a short but detailed `<summary>` XML doc comment describing what the test case validates. This ensures test intent is clear without reading the implementation.

```csharp
/// <summary>
/// Sends 100 concurrent ping requests and verifies each response carries the correct sequence number.
/// </summary>
[Test]
public async Task Ping_100_Times()
{
    // ...
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
Tests/Pontifex.Tests/
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
│   ├── AckRawReliableStacks.cs
│   └── NoAckRawReliableStacks.cs
├── TestPlans/                               # All test plans
│   ├── AckRawReliable/
│   │   └── TestProtocols/                   # API-based test plans for AckRawReliable
│   │       ├── Ping/
│   │       │   ├── PingApi.cs               # Protocol declarations (structs, ApiRoot, client/server)
│   │       │   └── PingTests.cs             # Tests using this protocol
│   │       └── InvariantChecker/
│   │           ├── InvariantCheckerApi.cs    # Stub ApiRoot for connect/disconnect testing
│   │           └── InvariantCheckerTests.cs  # Connect/disconnect tests
│   └── NoAckRawReliable/
│       └── Ping/
│           └── PingTests.cs                 # Raw exchange test (no API layer)
```

---

## 7. Transport URI Format Reference

All URIs follow the pattern `transport://<scheme>|<params>`. The `DescriptionFactory` parses these recursively for nested wrappers.

| Transport | URI Example | Notes |
|-----------|-------------|-------|
| Direct | `transport://direct\|server-name` | Server name is arbitrary; client uses same name to connect |
| TCP | `transport://tcp\|127.0.0.1:9000/60` | `/60` = disconnect timeout in seconds |
| Direct NoAck Raw Reliable | `transport://direct-noack-raw-reliable\|srv-{guid}` | Unacknowledged reliable direct transport |
| Convert | `transport://convert\|NoAckRawReliable:udp\|127.0.0.1:9000` | Builds inner transport, converts via ConvertersGraph |
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
