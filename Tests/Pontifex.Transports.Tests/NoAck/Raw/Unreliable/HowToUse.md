# How to use the NoAckRawUnreliable conformance test suite

## For a new transport implementation

### 1. Implement the adapter

Create a class that implements `INoAckRawUnreliableConformanceTestAdapter`:

```csharp
using Pontifex.NoAck.Raw.Unreliable.Tests;

public sealed class MyTransportAdapter
    : INoAckRawUnreliableConformanceTestAdapter
{
    public string ImplementationName => "MyTransport v1";

    public INoAckRawUnreliableConformanceScope CreateScope()
        => new MyScope();
}
```

`CreateScope` returns a scope that owns transport instances and payload factories:

```csharp
public sealed class MyScope : INoAckRawUnreliableConformanceScope
{
    public INoAckRawUnreliableClient CreateClient(bool instrumented) { /* ... */ }
    public INoAckRawUnreliableServer CreateServer(bool instrumented) { /* ... */ }
    public UnionDataList CreateSmallValidMessage(ITransport transport) { /* ... */ }
    public UnionDataList CreateExactLimitMessage(ITransport transport) { /* ... */ }
    public UnionDataList CreateOneByteOverLimitMessage(ITransport transport) { /* ... */ }
    public IEndPoint CreateForeignServerDestination() { /* ... */ }
    public IEnumerable<INoAckRawUnreliableAdditionalNonOkCase>
        CreateAdditionalNonOkCases() => Enumerable.Empty<INoAckRawUnreliableAdditionalNonOkCase>();
    public void Dispose() { /* cleanup */ }
}
```

When `instrumented: true`, the transport must expose
`INoAckRawUnreliableConformanceControl` through `ITransport.GetControls`.

### 2. Register the adapter

In a `[SetUpFixture]` or module initializer:

```csharp
[SetUpFixture]
public sealed class MyTransportRegistration
{
    [OneTimeSetUp]
    public void Register()
    {
        ConformanceAdapterSource.Register(new MyTransportAdapter());
    }
}
```

### 3. Run the tests

```bash
dotnet test Tests/Pontifex.Transports.Tests
```

The 38 test cases appear grouped by fixture class. A passing suite earns
**Carrier-Independent Core Conformant** status. If the control is missing,
no tests run (Baseline Only).

## Interface & method contracts

You must implement two interfaces. The third (`INoAckRawUnreliableAdditionalNonOkCase`) is optional.

### `INoAckRawUnreliableConformanceTestAdapter`

| Member | Requirement |
|---|---|
| `ImplementationName` | Return a stable, human-readable name (e.g. `"MyTransport v1"`). Used only for display. |
| `CreateScope()` | Return a new `INoAckRawUnreliableConformanceScope`. Called once per test fixture instance. |

### `INoAckRawUnreliableConformanceScope`

Concrete transports are created through the scope so that the suite can control their lifetime independently.

| Member | Requirement |
|---|---|
| `CreateClient(bool instrumented)` | Create and return a new `INoAckRawUnreliableClient` instance. The transport must be unstarted (calling `IsStarted` returns `false`). When `instrumented` is `true`, the transport must also expose `INoAckRawUnreliableConformanceControl` via `GetControls`. |
| `CreateServer(bool instrumented)` | Same as `CreateClient`, but returns an `INoAckRawUnreliableServer`. |
| `CreateSmallValidMessage(ITransport transport)` | Return a `UnionDataList` that is valid and small enough to be sent successfully on any transport. The `transport` parameter is the client or server that will send the message — use it to query limits if needed. Must return at least one element. |
| `CreateExactLimitMessage(ITransport transport)` | Return a `UnionDataList` that is valid and whose serialised size equals the transport's maximum send size exactly. Use `transport` to determine the limit. Must return at least one element. |
| `CreateOneByteOverLimitMessage(ITransport transport)` | Return a `UnionDataList` that is one byte larger than the transport's maximum send size, so that calling `TrySend` on it returns `MessageTooBig`. Must return at least one element. |
| `CreateForeignServerDestination()` | Return an `IEndPoint` that belongs to a *different* server (real or fake) so that sending to it produces `InvalidAddress`. Must not be `null`. |
| `CreateAdditionalNonOkCases()` | Return additional test cases that exercise other non-`Ok` results. Return `Enumerable.Empty<…>()` if none. |
| `Dispose()` | Clean up all transports, payloads, and endpoints created by this scope. |

### `INoAckRawUnreliableAdditionalNonOkCase` (optional)

Returned by `CreateAdditionalNonOkCases`. Each instance describes one send attempt and the expected result.

| Member | Requirement |
|---|---|
| `Name` | Display name for the test case. |
| `ExpectedResult` | The `SendResult` that `Invoke()` is expected to return. |
| `Transport` | The transport to invoke `TrySend` on. |
| `Invoke()` | Execute the send and return the actual result. The test framework compares it against `ExpectedResult`. |

## Test discovery

No manual enumeration needed. `ConformanceAdapterSource` feeds all registered
adapters into `[TestFixtureSource]`. Every test class runs once per adapter.
