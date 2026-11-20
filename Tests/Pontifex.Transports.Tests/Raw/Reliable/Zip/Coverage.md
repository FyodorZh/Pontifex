# RawReliableZip Coverage

Layer: `RawReliableZip`
Subject: `Pontifex.Raw.Reliable.Zip.RawReliableZipBinding`,
`Pontifex.Raw.Reliable.Zip.RawReliableZipClientLogic`, and
`Pontifex.Raw.Reliable.Zip.RawReliableZipServerLogic`
Directory: `Tests/Pontifex.Transports.Tests/Raw/Reliable/Zip`

Contract references:
[RawReliableZipBinding.cs](../../../../../Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipBinding.cs),
[RawReliableZipClientLogic.cs](../../../../../Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipClientLogic.cs),
[RawReliableZipServerLogic.cs](../../../../../Protocols/Pontifex.Protocol.Zip/Raw/Reliable/RawReliableZipServerLogic.cs),
[CompressorLogic.cs](../../../../../Protocols/Pontifex.Protocol.Zip/CompressorLogic.cs),
[ZLibUtils.cs](../../../../../Protocols/Pontifex.Protocol.Zip/ZLibUtils.cs),
[RawReliableWrapperClient.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperClient.cs),
[RawReliableWrapperServer.cs](../../../../../Protocols/Pontifex.Protocol/Raw/Reliable/RawReliableWrapperServer.cs).

## Covered

| Test | Contract behaviour | What it proves | Non-extractable notes |
| --- | --- | --- | --- |
| `AcceptedNegotiation_RoundTripsDataInBothDirections` | The zip layer negotiates `[1, "zip", Request]` / `[1, "zip", Accepted]` and then forwards compressed application traffic in both directions. | The server factory is called once, both application handlers connect once, and payloads sent by either side arrive intact on the other side. | The fake inner endpoint reports `MessageMaxByteSize = 4096`; the wrapper proxies forward the inner value. Compression is transparent: the application handlers only ever observe the original dataset. |
| `CompressiblePayload_IsSentSmallerThanTheUncompressedDataset` | `ProcessSentData` replaces the application dataset with its compressed byte stream before the inner transport sends it; `ProcessReceivedData` restores it. | For a 2000-character repeated payload, the inner transport observes a single byte-array element whose size is strictly less than the serialized uncompressed dataset, and the server handler receives the original text. | Compression is proven on the captured inner send buffer, not only on the receiving side. The fake endpoint clones outbound payloads before delivery, so the assertion does not depend on post-send ownership. |
| `ReconnectAfterFirstSessionDisconnects_StillCompressesAndDecompresses` | The zip server obtains a fresh logic instance per accepted session; a session's `OnDisconnected` releases only that session's compressor/decompressor. | Two sequential clients on one running server: the first session round-trips a compressible payload and disconnects, then a second session is admitted, still compresses its outbound payload to a smaller single byte-array element, and still decompresses the peer's dataset. | Guards the shared-logic failure mode: the zip logic releases its compressor/decompressor in `OnDisconnected`, so one shared instance would be null from the second session on and every later session would be torn down with `TextFail`. The harness therefore passes a logic factory to `RawReliableWrapperServer`, exactly like the production binding. |
| `RawClientWithoutZipNegotiation_IsRejectedAndSkipsTheFactory` | A wrapper server admits only a first payload that is a valid `"zip"` negotiation request. | An application payload from a bare reliable client reaches no user factory and creates no session; the server sends `[1, "zip", Rejected, "expected a negotiation request"]`; the rejection linger is two seconds. | The "raw" client is the fake reliable client with an application handler and no wrapper negotiation. The linger is asserted through the manual clock, so no real timer runs. |
| `VersionMismatch_StopsClientWithHandshakeRejectedAndSkipsFactory` | A request whose version differs from the zip layer version is rejected. | The client observes exactly one `OnStopped` with `HandshakeRejected` and no `OnConnected`; the server factory is never called; the reject envelope carries `"version mismatch"`; the linger is two seconds. | The client reports the peer rejection status, so its public reason is `HandshakeRejected` rather than the server-internal `ProtocolMismatch`. |
| `NameMismatch_StopsClientWithHandshakeRejectedAndSkipsFactory` | The session validates the layer name before the zip logic sees the request. | A client that negotiates name `"layer"` against the `"zip"` server is rejected with `"name mismatch"`, stops with `HandshakeRejected`, and never reaches the factory. | Proves the zip server may rely on `CheckNegotiationData` accepting every structure-valid tail: the name gate lives in the shared session, and the zip logic adds no name check of its own. |

## Planned

| Missing test | Owning layer |
| --- | --- |
| `zip(nested)` formula compilation, scheme, `TransportType.RawReliable`, nested `Exactly(RawReliable)`, and `compression_level` default/range. | Loose file `FactoryBindingsTests` (outside this suite); recorded here because it covers `RawReliableZipBinding`. |
| Corrupt/invalid compressed bytes from the peer stopping the session through the wrapper `TextFail`/`ExceptionFail` path. | `RawReliableZip`. |
| Incompressible payload expanding on the wire and still round-tripping. | `RawReliableZip`. |
| Compression levels `0` and `9` producing different wire sizes for the same payload. | `RawReliableZip`. |
| End-to-end `zip(tcp-noack(...))` over a real carrier. | Carrier integration suites (`Pontifex.Transport.Net.Tests`/`Pontifex.Transport.Direct.Tests`). |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Real timers, deadline accuracy, and wall-clock behaviour | The wrapper exposes an internal time seam; the default suite is deterministic and uses a manual clock. |
| Real TCP/Direct carriers, sockets, and wire framing | Covered by the carrier integration suites; this suite drives fake reliable transports. |
| The reusable negotiation driver, admission gate, terminal lifecycle, and rejection linger mechanics | Owned by the `RawReliableWrapper` suite; this layer covers only the zip dataset and data transformation. |
| Exact compressed byte layout | An `Ionic.Zlib` implementation detail; the contract only promises a smaller-or-equal byte stream that decompresses to the original dataset (`CompressorLogic` notes compression can expand incompressible data). |
| Observing the `CompressorLogic.Release` call itself through pool reuse | `CompressorLogic` exposes no observable pool seam; the covered reconnect test asserts the contract-visible consequence instead — a later session still compresses and decompresses after an earlier session released its compressor. |
| Logging and diagnostics text | Requires an observable log sink; the `ILogger` is used only as a dependency. |
| Buffer reference-count internals | Tests assert observable ownership outcomes through the public contract, not private counters. |
| Security properties (authentication, confidentiality) | Out of scope for the zip layer. |
| Performance and allocation counts | Not part of this contract. |
