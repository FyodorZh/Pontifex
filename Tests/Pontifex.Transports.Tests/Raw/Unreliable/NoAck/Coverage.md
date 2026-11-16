# RawUnreliableNoAck Conformance Coverage

`RawUnreliableNoAckConformanceTests` runs exactly the shared
[RawUnreliable Conformance Coverage](../Coverage.md) suite. It has no
variant-specific tests and no variant-specific deferred items.

## Notes

- The NoAck factory never receives the triggering message. If the factory returns null or throws, or the
  carrier can no longer serve the route, the trigger is released silently; declined triggers are not logged.
- Carrier drops, per-endpoint callback serialization, and the client-local/server-shared budget scope are
  the same as in the shared coverage notes.
