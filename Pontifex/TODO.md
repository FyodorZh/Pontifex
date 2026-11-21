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
