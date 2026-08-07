# Pontifex TODO List

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