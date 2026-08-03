# Transport naming convention

Current convention: `{Raw|RR}{Reliable|Unreliable}{NoAck|Ack}`.
Applied to namespaces, class names, interface names, and directory structure
(e.g. `Pontifex.Raw.Reliable.Ack.Tcp`, `RawReliableAckTcpClient`).

The Big Change (rename from the former `{NoAck|Ack}{Raw|RR}{Reliable|Unreliable}`)
has been applied. The old -> new mapping used is recorded in
`Docs/BigRenameMapping.md`.

## Frozen surfaces
- Scheme-names do not change. The URI schemes exposed via
  `ITransportConstructor.Name` and the `*Info.TransportName` constants
  (`udp`, `udp_rr`, `tcp`, `direct`, `zip`, `log`, `reconnectable`, `convert`, ...)
  are excluded from the convention.
