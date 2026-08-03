# Big rename mapping (mechanical spec)

> **STATUS: APPLIED.** The Big Change described in this document has been executed
> and the solution builds and passes all tests with the new convention
> `{Raw|RR}{Reliable|Unreliable}{NoAck|Ack}`. The mapping below is retained as the
> historical record of the transformation.

Specification for the planned rename of the transport naming convention from
`{NoAck|Ack}{Raw|RR}{Reliable|Unreliable}` to `{Raw|RR}{Reliable|Unreliable}{NoAck|Ack}`
across namespaces, class names, interface names, enum members, directories and files.

Scheme-names are frozen (see `Docs/NamingConvention.md`): `udp`, `udp_rr`, `tcp`,
`direct`, `zip`, `log`, `reconnectable`, `convert` do NOT change.

## The rule

Every identifier that contains one of the eight type tokens gets that token replaced.
Replacement must be applied longest-first (`AckRawReliable` before `AckRaw`,
`NoAckRRUnreliable` before `NoAckRR`, ...), because the full tokens contain the
two-segment tokens as substrings.

| token | target |
|---|---|
| `AckRawReliable` | `RawReliableAck` |
| `NoAckRawReliable` | `RawReliableNoAck` |
| `AckRawUnreliable` | `RawUnreliableAck` |
| `NoAckRawUnreliable` | `RawUnreliableNoAck` |
| `AckRRReliable` | `RRReliableAck` |
| `NoAckRRReliable` | `RRReliableNoAck` |
| `AckRRUnreliable` | `RRUnreliableAck` |
| `NoAckRRUnreliable` | `RRUnreliableNoAck` |

Base-level (two-segment) tokens:

| token | target |
|---|---|
| `AckRaw` | `RawAck` |
| `NoAckRaw` | `RawNoAck` |
| `AckRR` | `RRAck` |
| `NoAckRR` | `RRNoAck` |

> Base-level abstractions are first duplicated into each leaf's `Base/` folder
> (see "Duplication of base abstractions"), then renamed via these tokens as part
> of the Big Change.

## Enum members

`Pontifex/Abstractions/TransportType.cs` (values 0..7 stay as-is):

| now | target |
|---|---|
| `AckRawReliable` | `RawReliableAck` |
| `NoAckRawReliable` | `RawReliableNoAck` |
| `AckRawUnreliable` | `RawUnreliableAck` |
| `NoAckRawUnreliable` | `RawUnreliableNoAck` |
| `AckRRReliable` | `RRReliableAck` |
| `NoAckRRReliable` | `RRReliableNoAck` |
| `AckRRUnreliable` | `RRUnreliableAck` |
| `NoAckRRUnreliable` | `RRUnreliableNoAck` |

## Namespaces and directories

### Type namespaces

The fully-specified type namespace triple, applied wherever it appears under `Pontifex.*`:

| now | target |
|---|---|
| `Ack.Raw.Reliable` | `Raw.Reliable.Ack` |
| `Ack.Raw.Unreliable` | `Raw.Unreliable.Ack` |
| `Ack.RR.Reliable` | `RR.Reliable.Ack` |
| `Ack.RR.Unreliable` | `RR.Unreliable.Ack` |
| `NoAck.Raw.Reliable` | `Raw.Reliable.NoAck` |
| `NoAck.Raw.Unreliable` | `Raw.Unreliable.NoAck` |
| `NoAck.RR.Reliable` | `RR.Reliable.NoAck` |
| `NoAck.RR.Unreliable` | `RR.Unreliable.NoAck` |

### Base layout

`Base` is a terminal node meaning "common to all transports under the enclosing
type segments". It carries no type segment below it and contributes no namespace
segment of its own (it is a folder-only marker). Base nodes exist at the transports
root (universal), at the two non-leaf type levels, and under every leaf:

```
Abstractions/Transports/
  Base/                                    <- universal, common to ALL transports
  Raw/                                     RR/
    Base/                                    Base/    <- common to all Raw*/RR* (Ack and NoAck)
    Reliable/                                Reliable/
      Base/                                    Base/
      Ack/                                     Ack/
        Base/  Client/  Server/                 Base/  Client/  Server/
      NoAck/                                   NoAck/
        Base/  Client/  Server/                 Base/  Client/  Server/
    Unreliable/                              Unreliable/
      Base/                                    Base/
      Ack/                                     Ack/
        Base/  Client/  Server/                 Base/  Client/  Server/
      NoAck/                                   NoAck/
        Base/  Client/  Server/                 Base/  Client/  Server/
```

- Universal base: `Transports/Base/` and `Implementations/Base/` -> namespace `Pontifex`.
- Level-1 base: `Raw/Base/` -> namespace `Pontifex.Raw`, `RR/Base/` -> `Pontifex.RR`.
- Level-2 base: `Raw/Reliable/Base/` -> `Pontifex.Raw.Reliable`,
  `Raw/Unreliable/Base/` -> `Pontifex.Raw.Unreliable`, etc.
- Leaves: `Raw/Reliable/Ack/` -> `Pontifex.Raw.Reliable.Ack`, etc.
- Leaf base: `Raw/Reliable/Ack/Base/` shares the leaf's namespace
  (`Pontifex.Raw.Reliable.Ack`); it holds the abstractions shared by that leaf's
  concrete transports (e.g. Tcp, Direct). Every leaf carries one.
- `Base` never carries a type segment below it: `Raw/Base/Ack` and `Raw/Ack/Base`
  remain impossible.

Directories follow the same layout, e.g.:
`Pontifex/Abstractions/Transports/Ack/Raw/Reliable/` -> `Raw/Reliable/Ack/`,
`Transports/Pontifex.Transport.Net/Tcp/Ack/Raw/Reliable/` -> `Raw/Reliable/Ack/`,
`Transports/Pontifex.Transport.Net/Udp/NoAck/RR/Unreliable/` -> `RR/Unreliable/NoAck/`.

### Duplication of base abstractions

The group-level bases (`{Ack|NoAck}/{Raw|RR}/Base`) represent commonality across
the `{Reliable|Unreliable}` siblings, which does not nest in the target tree. As a
staging step they are copied into each leaf's `Base/` folder (namespace updated to
the leaf namespace), then the original is removed. This gives every leaf a
self-contained base and lets the Big Change run as a pure rename; consolidation of
the resulting duplicates happens afterwards.

| source (removed) | duplicated to |
|---|---|
| `Abstractions/Transports/Ack/Raw/Base/` | `Ack/Raw/Reliable/Base/` + `Ack/Raw/Unreliable/Base/` |
| `Abstractions/Transports/Ack/RR/Base/` | `Ack/RR/Reliable/Base/` + `Ack/RR/Unreliable/Base/` |
| `Abstractions/Transports/NoAck/RR/Base/` | `NoAck/RR/Reliable/Base/` + `NoAck/RR/Unreliable/Base/` |
| `Abstractions/Transports/NoAck/Raw/Base/` | `NoAck/Raw/Reliable/Base/` + `NoAck/Raw/Unreliable/Base/` |
| `Implementations/NoAck/Raw/Base/` | `Implementations/NoAck/Raw/Reliable/Base/` + `Implementations/NoAck/Raw/Unreliable/Base/` |

Each copy changes its namespace to the target leaf namespace, e.g. the files in
`Ack/Raw/Reliable/Base/` -> `Pontifex.Ack.Raw.Reliable`. After the Big Change they
land as `Raw/Reliable/Ack/Base/`, `Raw/Unreliable/Ack/Base/`, `RR/Reliable/Ack/Base/`,
`RR/Unreliable/Ack/Base/`, `RR/Reliable/NoAck/Base/`, `RR/Unreliable/NoAck/Base/`,
`Raw/Reliable/NoAck/Base/`, `Raw/Unreliable/NoAck/Base/`, and
`Implementations/Raw/Reliable/NoAck/Base/` + `Implementations/Raw/Unreliable/NoAck/Base/`
— one base per leaf.

Not duplicated (already target-shaped): `Abstractions/Transports/Base/` and
`Implementations/Base/` (universal base).

## Identifiers

Each row is `now -> target`. All identifiers in the current codebase that embed
the convention:

### Concrete transports (Client / Server / Constructor)

| now | target |
|---|---|
| `AckRawReliableDirectClient` | `RawReliableAckDirectClient` |
| `AckRawReliableDirectServer` | `RawReliableAckDirectServer` |
| `AckRawReliableDirectConstructor` | `RawReliableAckDirectConstructor` |
| `AckRawReliableTcpClient` | `RawReliableAckTcpClient` |
| `AckRawReliableTcpServer` | `RawReliableAckTcpServer` |
| `AckRawReliableTcpConstructor` | `RawReliableAckTcpConstructor` |
| `NoAckRawUnreliableDirectClient` | `RawUnreliableNoAckDirectClient` |
| `NoAckRawUnreliableDirectServer` | `RawUnreliableNoAckDirectServer` |
| `NoAckRawUnreliableDirectConstructor` | `RawUnreliableNoAckDirectConstructor` |
| `NoAckRawUnreliableUdpClient` | `RawUnreliableNoAckUdpClient` |
| `NoAckRawUnreliableUdpServer` | `RawUnreliableNoAckUdpServer` |
| `NoAckRawUnreliableUdpConstructor` | `RawUnreliableNoAckUdpConstructor` |
| `NoAckRRUnreliableUdpClient` | `RRUnreliableNoAckUdpClient` |
| `NoAckRRUnreliableUdpServer` | `RRUnreliableNoAckUdpServer` |
| `NoAckRRUnreliableUdpConstructor` | `RRUnreliableNoAckUdpConstructor` |

### Implementation base classes (`Pontifex/Implementations/`)

| now | target |
|---|---|
| `AckRawReliableClient` | `RawReliableAckClient` |
| `AckRawReliableServer` | `RawReliableAckServer` |
| `NoAckRawUnreliableTransport` | `RawUnreliableNoAckTransport` |
| `NoAckRawUnreliableClientTransport` | `RawUnreliableNoAckClientTransport` |
| `NoAckRawUnreliableServerTransport` | `RawUnreliableNoAckServerTransport` |

### Abstraction interfaces (`Pontifex/Abstractions/Transports/`)

| now | target |
|---|---|
| `IAckRawReliableClient` | `IRawReliableAckClient` |
| `IAckRawReliableClientHandler` | `IRawReliableAckClientHandler` |
| `IAckRawReliableClientSideEndpoint` | `IRawReliableAckClientSideEndpoint` |
| `IAckRawReliableServer` | `IRawReliableAckServer` |
| `IAckRawReliableServerHandler` | `IRawReliableAckServerHandler` |
| `IAckRawReliableServerSideEndpoint` | `IRawReliableAckServerSideEndpoint` |
| `IAckRawReliableBaseEndpoint` | `IRawReliableAckBaseEndpoint` |
| `IAckRawUnreliableClient` | `IRawUnreliableAckClient` |
| `IAckRawUnreliableClientHandler` | `IRawUnreliableAckClientHandler` |
| `IAckRawUnreliableClientSideEndpoint` | `IRawUnreliableAckClientSideEndpoint` |
| `IAckRawUnreliableServer` | `IRawUnreliableAckServer` |
| `IAckRawUnreliableServerHandler` | `IRawUnreliableAckServerHandler` |
| `IAckRawUnreliableServerSideEndpoint` | `IRawUnreliableAckServerSideEndpoint` |
| `IAckRawUnreliableBaseEndpoint` | `IRawUnreliableAckBaseEndpoint` |
| `IAckRRReliableClient` | `IRRReliableAckClient` |
| `IAckRRReliableClientHandler` | `IRRReliableAckClientHandler` |
| `IAckRRReliableServer` | `IRRReliableAckServer` |
| `IAckRRReliableServerHandler` | `IRRReliableAckServerHandler` |
| `IAckRRUnreliableClient` | `IRRUnreliableAckClient` |
| `IAckRRUnreliableClientHandler` | `IRRUnreliableAckClientHandler` |
| `IAckRRUnreliableServer` | `IRRUnreliableAckServer` |
| `IAckRRUnreliableServerHandler` | `IRRUnreliableAckServerHandler` |
| `INoAckRRReliableClient` | `IRRReliableNoAckClient` |
| `INoAckRRReliableClientHandler` | `IRRReliableNoAckClientHandler` |
| `INoAckRRReliableClientSession` | `IRRReliableNoAckClientSession` |
| `INoAckRRReliableServer` | `IRRReliableNoAckServer` |
| `INoAckRRReliableServerEndpoint` | `IRRReliableNoAckServerEndpoint` |
| `INoAckRRReliableServerHandler` | `IRRReliableNoAckServerHandler` |
| `INoAckRRReliableCallbackOnClient` | `IRRReliableNoAckCallbackOnClient` |
| `INoAckRRReliableCallbackOnServer` | `IRRReliableNoAckCallbackOnServer` |
| `INoAckRRUnreliableClient` | `IRRUnreliableNoAckClient` |
| `INoAckRRUnreliableClientHandler` | `IRRUnreliableNoAckClientHandler` |
| `INoAckRRUnreliableServer` | `IRRUnreliableNoAckServer` |
| `INoAckRRUnreliableServerEndpoint` | `IRRUnreliableNoAckServerEndpoint` |
| `INoAckRRUnreliableServerHandler` | `IRRUnreliableNoAckServerHandler` |
| `INoAckRawReliableClient` | `IRawReliableNoAckClient` |
| `INoAckRawReliableClientSession` | `IRawReliableNoAckClientSession` |
| `INoAckRawReliableServer` | `IRawReliableNoAckServer` |
| `INoAckRawUnreliableClient` | `IRawUnreliableNoAckClient` |
| `INoAckRawUnreliableServer` | `IRawUnreliableNoAckServer` |
| `INoAckRawUnreliableConformanceControl` | `IRawUnreliableNoAckConformanceControl` |
| `INoAckRawUnreliableClientConformanceControl` | `IRawUnreliableNoAckClientConformanceControl` |
| `INoAckRawUnreliableServerConformanceControl` | `IRawUnreliableNoAckServerConformanceControl` |

### Base-level (two-segment) types

Duplicated into each leaf's `Base/` (see "Duplication of base abstractions") and
renamed via the two-segment tokens:

| now | target |
|---|---|
| `IAckRawClient` | `IRawAckClient` |
| `IAckRawClientHandler` | `IRawAckClientHandler` |
| `IAckRawServer` | `IRawAckServer` |
| `IAckRawServerHandler` | `IRawAckServerHandler` |
| `IAckRawBaseEndpoint` | `IRawAckBaseEndpoint` |
| `IAckRawBaseHandler` | `IRawAckBaseHandler` |
| `IRawServerAcknowledger` | `IRawServerAcknowledger` (no convention token) |
| `IAckRRClient` | `IRRAckClient` |
| `IAckRRClientHandler` | `IRRAckClientHandler` |
| `IAckRRServer` | `IRRAckServer` |
| `IAckRRServerHandler` | `IRRAckServerHandler` |
| `IRRServerAcknowledger` | `IRRServerAcknowledger` (no convention token) |
| `INoAckRRClient` | `IRRNoAckClient` |
| `INoAckRRServer` | `IRRNoAckServer` |
| `INoAckRRServerEndpoint` | `IRRNoAckServerEndpoint` |
| `INoAckRRServerHandler` | `IRRNoAckServerHandler` |
| `INoAckRawConformanceControl` | `IRawNoAckConformanceControl` |
| `NoAckRawTransport` | `RawNoAckTransport` |

### Wrappers and protocols

| now | target |
|---|---|
| `AckRawReliableClientLogger` | `RawReliableAckClientLogger` |
| `AckRawReliableServerLogger` | `RawReliableAckServerLogger` |
| `AckRawReliableLoggerConstructor` | `RawReliableAckLoggerConstructor` |
| `AckRawReliableWrapperLogic` | `RawReliableAckWrapperLogic` |
| `IAckRawReliableWrapperLogic` | `IRawReliableAckWrapperLogic` |
| `AckRawReliableWrapperClient` | `RawReliableAckWrapperClient` |
| `IAckRawReliableWrapperClientLogic` | `IRawReliableAckWrapperClientLogic` |
| `AckRawReliableWrapperServer` | `RawReliableAckWrapperServer` |
| `IAckRawReliableWrapperServerLogic` | `IRawReliableAckWrapperServerLogic` |
| `AckRawReliableZipClientLogic` | `RawReliableAckZipClientLogic` |
| `AckRawReliableZipServerLogic` | `RawReliableAckZipServerLogic` |
| `AckRawReliableZipConstructor` | `RawReliableAckZipConstructor` |
| `AckRawReliableReconnectableClient` | `RawReliableAckReconnectableClient` |
| `AckRawReliableReconnectableServer` | `RawReliableAckReconnectableServer` |
| `AckRawReliableReconnectableConstructor` | `RawReliableAckReconnectableConstructor` |
| `SynchronizedAckRawReliableClientHandler` | `SynchronizedRawReliableAckClientHandler` |
| `AckRawBaseEndpointWrapper` | `RawAckBaseEndpointWrapper` |
| `AckRawClientSideEndpointWrapper` | `RawAckClientSideEndpointWrapper` |
| `AckRawServerSideEndpointWrapper` | `RawAckServerSideEndpointWrapper` |

### Converters (`Pontifex/Converters/`)

| now | target |
|---|---|
| `NoAckRawUnreliableToAckRawReliableConverter` | `RawUnreliableNoAckToRawReliableAckConverter` |
| `NoAckRawUnreliableToAckRawReliableClient` | `RawUnreliableNoAckToRawReliableAckClient` |
| `NoAckRawUnreliableToAckRawReliableServer` | `RawUnreliableNoAckToRawReliableAckServer` |

### UI (`Pontifex/Elements/`, `Pontifex.TestUI/`)

| now | target |
|---|---|
| `AckRawReliableClientControl` | `RawReliableAckClientControl` |
| `IAckRawReliableClientControl` | `IRawReliableAckClientControl` |
| `AckRawReliableClientControlView` | `RawReliableAckClientControlView` |
| `AckRawReliableTcpClientDebugControlView` | `RawReliableAckTcpClientDebugControlView` |
| `IAckRawReliableTcpClientDebugControl` | `IRawReliableAckTcpClientDebugControl` |
| `AckRawReliableProtocol` | `RawReliableAckProtocol` |
| `AckRawReliableClientLogic` | `RawReliableAckClientLogic` |
| `AckRawReliableServerLogic` | `RawReliableAckServerLogic` |
| `AckRawReliableCommonLogic` | `RawReliableAckCommonLogic` |

### Tests (`Tests/Pontifex.Transport.Tests`, `Tests/Pontifex.Transports.Tests`)

| now | target |
|---|---|
| `AckRawReliableStacks` | `RawReliableAckStacks` |
| `DirectNoAckRawUnreliableConformanceAdapter` | `DirectRawUnreliableNoAckConformanceAdapter` |
| `DirectNoAckRawUnreliableConformanceAdapterTests` | `DirectRawUnreliableNoAckConformanceAdapterTests` |
| `DirectNoAckRawUnreliableConformanceTests` | `DirectRawUnreliableNoAckConformanceTests` |
| `UdpNoAckRawUnreliableConformanceAdapter` | `UdpRawUnreliableNoAckConformanceAdapter` |
| `UdpNoAckRawUnreliableConformanceAdapterTests` | `UdpRawUnreliableNoAckConformanceAdapterTests` |
| `UdpNoAckRawUnreliableConformanceTests` | `UdpRawUnreliableNoAckConformanceTests` |
| `INoAckRawUnreliableConformanceAdapter` | `IRawUnreliableNoAckConformanceAdapter` |
| `INoAckRawUnreliableConformanceFixture` | `IRawUnreliableNoAckConformanceFixture` |
| `NoAckRawUnreliableConformanceFixtureOptions` | `RawUnreliableNoAckConformanceFixtureOptions` |
| `NoAckRawUnreliableConformanceTests` | `RawUnreliableNoAckConformanceTests` |
| `NoAckRawUnreliableConformanceControl` | `RawUnreliableNoAckConformanceControl` |
| `NoAckRawUnreliableClientConformanceControl` | `RawUnreliableNoAckClientConformanceControl` |
| `NoAckRawUnreliableServerConformanceControl` | `RawUnreliableNoAckServerConformanceControl` |
| `NoAckRRReliableFailReason` | `RRReliableNoAckFailReason` |

## Non-identifier strings to update

- Enum-name strings inside `Description`/URI parsers, e.g. `new StringElement("AckRawReliable")`,
  `new StringElement("NoAckRawUnreliable")`, `new StringElement("NoAckRRUnreliable")`
  (identical to the enum member, so covered by the same text replace).
- Test marker: `message.PutFirst("NoAckRawUnreliable")` in
  `Tests/Pontifex.Transports.Tests/NoAck/Raw/Unreliable/NoAckRawUnreliableConformanceTests.cs`.
- Docs: `Tests/Pontifex.Transport.Tests/HowToExtend.md` and this spec's `Docs/`.
- Log/exception strings that interpolate enum values (`.ToString()`) change automatically.

## Frozen surfaces

- Scheme names (`Name`, `*Info.TransportName`): `udp`, `udp_rr`, `tcp`, `direct`,
  `zip`, `log`, `reconnectable`, `convert` — NOT renamed.
- Wire markers (protocol payloads), e.g. `AckTcp`, `AckTcp-OK`, `Direct-Ack-OK` — NOT renamed.
- Numeric enum values 0..7 — unchanged.

## Execution procedure (Big Change, one atomic change)

1. Duplicate the base abstractions (see "Duplication of base abstractions"): copy
   each group base into the two leaf `Base/` folders, update namespaces, repoint the
   `using Pontifex.{Ack|NoAck}.{Raw|RR};` statements (~33 files, each to its single
   family), remove the originals, build.
2. Apply the namespace/directory table via `git mv` bottom-up (deepest dirs first).
3. Apply the identifier table with a single text replace per token, longest-first
   (12 token replacements: 8 full tokens then 4 base tokens, e.g. `AckRawReliable`
   before `AckRaw`, otherwise substrings get corrupted). These cover all listed
   identifiers and enum-name strings.
4. Fix any remaining compile errors, rebuild.
5. Run `dotnet test` (all suites).
6. Update `Docs/NamingConvention.md` to the new convention.
