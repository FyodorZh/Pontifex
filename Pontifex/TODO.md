# Pontifex TODO List

- [ ] Implement RawUnreliableAck conformance tests
  - [ ] Check (update) RawUnreliableNoAck tests coverage
- [ ] Implement RawUnreliableNoAck direct implementation
  - [ ] Consolidate RawUnreliable direct implementation
- [ ] Make sure that we don't schedule messages for UDP transport twice.
- [ ] Check RawUnreliableNoAck
  - For correctness
  - Points of improvement
- [ ] Implement global AGENTS.md
  - State no Tasks
  - State desire for allocation free code
  - State TODO.md management rules
- [x] Implement RawUnreliableAck: 'bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory);'