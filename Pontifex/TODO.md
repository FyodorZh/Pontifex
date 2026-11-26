# Pontifex TODO List

- [x] Merge RRUnreliableNoAck into RRUnreliable and delete the RRUnreliableAck family
- [x] Merge RawUnreliableNoAck into RawUnreliable and delete the RawUnreliableAck family
- [x] Implement RawUnreliableAck conformance tests
  - [x] Check (update) RawUnreliableNoAck tests coverage
- [x] Implement RawUnreliableNoAck direct implementation
  - [x] Consolidate RawUnreliable direct implementation
- [x] Make sure that we don't schedule messages for UDP transport twice.
- [x] Check RawUnreliableNoAck
  - For correctness
  - Points of improvement
- [ ] Implement global AGENTS.md
  - State no Tasks
  - State desire for allocation free code
  - State TODO.md management rules
- [x] Implement RawUnreliableAck: 'bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory);'
- [x] Convert RawUnreliable to the RawReliable architecture (endpoint-owned lifecycle and per-endpoint
  serial queues, transport as factory/registry/carrier owner, shared server send budget, relocated
  conformance gates, legacy dispatcher layer deleted)
  - [ ] Optional: a message-count cap for empty (zero-byte) messages, which the byte-only client-local
    outbound budget does not bound
  - [ ] Revisit D8 (the transport-base shape) with option E if a third RawUnreliable contract variant
    appears
- [ ] Refactor RRUnreliable to request/response semantics
  - [x] Stage 1: formal refactoring (request/response API, request ids, TTLs, carrier-agnostic core)
  - [x] Extract shared lifecycle into `RRUnreliableTransport` / `RRUnreliableTransport<THandler>`
    (carrier template, handler base, `Init`, dispatch gate)
  - [x] Stage 2a: `Specification.md` written
  - [x] Stage 2b: align the implementation with the specification (failed-start callbacks,
    `Response`/`OnRequested` exception policy, client source filtering, inbound size limit)
  - [ ] Stage 2c: conformance adapter and tests
  - [x] Accepted: `OnRequested` may begin across the stop transition, possibly after `onStopped`;
    documented in the unreliable specification, to be closed by a callback dispatcher
- [ ] Propagate a decode bound to byte-stream carriers (TCP handshake, Protocol, DeliveryManager):
  the `UnionData.maxArraySize` default of `int.MaxValue` still allows a declared multi-GB array
  allocation before the source runs out
- [x] Extract common RR transport abstractions (`IRRTransport`, `IRRReliableTransport`,
  `IRRUnreliableTransport`, `IRRClientTransport`, `IRRServerTransport`, `IRRRequestHandler`,
  `IRRResponse`, `RRRequestHandler`, `RRResponse`)
- [x] Extract the shared RR lifecycle base `RRTransport` (carrier start and stop, payload
  validation, server-global ids, frame-size formula) and derive `RRReliableTransport` and
  `RRUnreliableTransport` from it
- [x] Allow an RRUnreliable server to answer a request with a declared message sequence (1..256
  messages, zero-based ids, deduplicated client delivery) and raise the UDP payload default to 540
- [x] Write `RR/Specification.md` (common base) and reduce both family specifications to
  deltas over it
- [x] Unify the RR per-request model: per-request client handlers with `RequestId`/`Cancel` and a
  server response handle for both families
- [x] Correlation: 16-bit request id with a random per-lifetime base and unchecked increment
- [ ] Replace RRReliableAck and RRReliableNoAck with the RRReliable abstraction
  - [x] Stage 1: abstraction only (transport interfaces, per-request handlers, completion types,
    four-type matrix)
  - [x] Stage 2a: `Specification.md` written; TTL getters added
  - [ ] Stage 2b: unified implementations and conformance tests
