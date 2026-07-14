# Pontifex Virtual Delivery — Netem

Network emulation delivery system implementing the algorithms from Linux `sch_netem.c`.
Wraps `IDeliverySystem` to introduce configurable latency, jitter, loss, duplication, reordering,
and rate limiting into a message stream.

## Namespace

```csharp
using Pontifex.VirtualDelivery.Netem;
```

## Quick Start

```csharp
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Transports.Direct.Delivery;
using Pontifex.VirtualDelivery.Netem;

// 50ms delay, 10ms jitter, 5% random loss
var config = new NetemConfig
{
    LatencyNs        = 50_000_000,   // 50ms base delay
    JitterNs         = 10_000_000,   // ±10ms uniform jitter
    LossProbability  = (uint)(0.05 * uint.MaxValue),  // 5%
    Correlation      = new CorrelationParams(0, 0, 0, 0)
};

var delivery = new NetemDeliverySystem(config, collectablePool, bytesPool);
delivery.Delivered += msg => ProcessMessage(msg);

delivery.Deliver(message);  // message emerges later via Delivered

delivery.Dispose();
```

Constructor parameters:
- `NetemConfig` — impairment settings
- `ICollectablePool` — pool for `UnionDataList` instantiation (required for message cloning when duplication is enabled)
- `IPool<IMultiRefByteArray, int>` — byte array pool (required for serialization during cloning)

## Configuration Reference

### `NetemConfig`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LatencyNs` | `long` | `0` | Base delay added to every packet (nanoseconds) |
| `JitterNs` | `long` | `0` | Uniform jitter range ±value (nanoseconds) |
| `LossProbability` | `uint` | `0` | Random loss probability (0 = none, ~0u = all) |
| `DuplicateProbability` | `uint` | `0` | Duplication probability (0 = none, ~0u = always) |
| `ReorderProbability` | `uint` | `0` | Reorder probability for gap candidates |
| `Gap` | `uint` | `0` | Every Nth packet is a reorder candidate (0 = disabled) |
| `RateBytesPerSec` | `ulong` | `0` | Rate limit in bytes/sec (0 = unlimited) |
| `PacketOverhead` | `int` | `0` | Extra bytes per packet for rate calculation |
| `CellSize` | `uint` | `0` | ATM-style cell size for rate calculation (0 = disabled) |
| `CellOverhead` | `int` | `0` | Extra bytes per cell |
| `QueueLimit` | `int` | `int.MaxValue` | Max queued messages before dropping |
| `DelayDistribution` | `DistributionTable?` | `null` | Empirical delay distribution table |
| `LossModel` | `LossModelKind` | `Random` | Loss model: `Random`, `FourState`, `GilbertElliot` |
| `FourState` | `FourStateParams?` | `null` | Parameters for 4-state GI Markov model |
| `GilbertElliot` | `GilbertElliotParams?` | `null` | Parameters for Gilbert-Elliot model |
| `Correlation` | `CorrelationParams` | — | Correlation coefficients for each random process |
| `Slot` | `SlotConfig` | — | Slot-based transmission scheduling |
| `SlotDistribution` | `DistributionTable?` | `null` | Distribution table for slot spacing |

### `CorrelationParams`

Correlation coefficient (rho) for each stochastic process. Range: `0` (uncorrelated) to `~0u` (highly correlated).
Higher values make random outcomes cluster (bursts of loss, bursts of jitter, etc.).

```csharp
new CorrelationParams(
    delayRho:     0,   // jitter correlation
    lossRho:      0,   // loss correlation
    duplicateRho: 0,   // duplication correlation
    reorderRho:   0    // reorder correlation
);
```

### `DistributionTable`

Loads an empirical CDF as a `short[]` table (values scaled by 8192).
Used for non-uniform jitter distributions (normal, Pareto, experimental).

```csharp
var dist = new DistributionTable(new short[] { /* CDF values */ });
config.DelayDistribution = dist;
```

When `DelayDistribution` is `null`, jitter uses a uniform distribution.

### `FourStateParams`

4-state GI (General and Intuitive) Markov loss model. All values scaled to `uint.MaxValue`.

| Param | Meaning |
|-------|---------|
| `P13` | Isolated loss within gap |
| `P31` | Recovery from isolated loss |
| `P32` | Entry into burst loss |
| `P14` | Start of burst loss |
| `P23` | End of burst → isolated loss |

### `GilbertElliotParams`

2-state Gilbert-Elliot model. All values scaled to `uint.MaxValue`.

| Param | Meaning |
|-------|---------|
| `P` | GOOD → BAD transition probability |
| `R` | BAD → GOOD transition probability |
| `H` | Loss threshold in BAD state (lost if rand > H) |
| `K1` | Loss probability in GOOD state (lost if rand < K1) |

### `SlotConfig`

Creates periodic transmission windows for bursty traffic patterns.

| Property | Type | Description |
|----------|------|-------------|
| `MinDelayNs` | `long` | Minimum slot spacing (uniform mode) |
| `MaxDelayNs` | `long` | Maximum slot spacing (uniform mode) |
| `DistDelayNs` | `long` | Mean delay for distribution-based slots |
| `DistJitterNs` | `long` | Jitter for distribution-based slots |
| `MaxPackets` | `int` | Max packets per slot (0 = unlimited) |
| `MaxBytes` | `int` | Max bytes per slot (0 = unlimited) |

When `SlotConfig.IsEnabled` is `false` (all zeros), slots are disabled.

## Recipes

### Simple delay with jitter

```csharp
var config = new NetemConfig
{
    LatencyNs   = 100_000_000,  // 100ms
    JitterNs    = 20_000_000,   // ±20ms
    Correlation = new CorrelationParams(delayRho: 0, lossRho: 0, duplicateRho: 0, reorderRho: 0)
};
```

### Packet loss with reordering

```csharp
var config = new NetemConfig
{
    LatencyNs          = 30_000_000,
    LossProbability    = (uint)(0.1 * uint.MaxValue),   // 10% loss
    ReorderProbability = (uint)(0.25 * uint.MaxValue),  // 25% of candidates
    Gap                = 4,                              // every 4th packet
    Correlation        = new CorrelationParams(0, 0, 0, 0)
};
```

### Rate limit 1 Mbit/s with duplication

```csharp
var config = new NetemConfig
{
    RateBytesPerSec       = 125_000,                       // 1 Mbit/s
    DuplicateProbability  = (uint)(0.02 * uint.MaxValue),  // 2% duplicate
    Correlation           = new CorrelationParams(0, 0, 0, 0)
};
```

### Correlated burst loss (Gilbert-Elliot)

```csharp
var config = new NetemConfig
{
    LossModel = LossModelKind.GilbertElliot,
    GilbertElliot = new GilbertElliotParams(
        p:  (uint)(0.001 * uint.MaxValue),   // rarely enter bad state
        r:  (uint)(0.010 * uint.MaxValue),   // slowly recover
        h:  (uint)(0.500 * uint.MaxValue),   // 50% loss in bad state
        k1: (uint)(0.001 * uint.MaxValue)),  // 0.1% loss in good state
    Correlation = new CorrelationParams(0, 0, 0, 0)
};
```

### Full impairment pipeline

```csharp
var config = new NetemConfig
{
    LatencyNs           = 50_000_000,
    JitterNs            = 10_000_000,
    LossProbability     = (uint)(0.05 * uint.MaxValue),
    DuplicateProbability = (uint)(0.01 * uint.MaxValue),
    ReorderProbability  = (uint)(0.15 * uint.MaxValue),
    Gap                 = 5,
    RateBytesPerSec     = 1_000_000,  // 8 Mbit/s
    QueueLimit          = 1000,
    Correlation = new CorrelationParams(
        delayRho:     (uint)(0.25 * uint.MaxValue),
        lossRho:      (uint)(0.50 * uint.MaxValue),
        duplicateRho: 0,
        reorderRho:   0)
};
```

## Lifecycle

- **Constructor** starts a background dequeue thread. Messages begin flowing immediately.
- **`Deliver()`** is thread-safe. All pipeline processing runs under an internal lock.
- **`Delivered`** fires serially — one message at a time, never overlapping. The handler runs
  on the background dequeue thread; avoid blocking it.
- **`Dispose()`** cancels the dequeue loop (waits up to 5 seconds), drains and releases
  all queued messages. After disposal, `Deliver()` throws `ObjectDisposedException`.

## Pipeline Order

Each `Deliver()` call processes the message through these stages in order:

1. **Duplication** — CRNG check; if triggered, clone is created (serialize/deserialize)
2. **Loss** — selected model decides whether to drop
3. **Queue limit** — drop if already at capacity
4. **Reorder** — gap counter + CRNG decides immediate delivery vs. delayed
5. **Delay** — tabledist/jitter + rate pacing (leaky bucket chain)
6. **Time-ordered FIFO** — message waits until `time_to_send` passes
7. **Slot gating** — message held until transmission slot opens (dequeue path)
8. **Delivered** — event fires on background thread

Dropped messages are released back to the collectable pool and never reach `Delivered`.
