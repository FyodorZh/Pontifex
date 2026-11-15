# RawUnreliableAck Conformance Coverage

`RawUnreliableAckConformanceTests` runs the shared
[RawUnreliable Conformance Coverage](../Coverage.md) suite plus the
Ack-specific triggering-message factory tests below.

## Covered

| Specification area | Coverage |
| --- | --- |
| Ack triggering message | The server factory receives the triggering message and can inspect it; the same logical message is delivered to OnReceived after a successful OnStarted, and the factory can accept or decline each message by content. |

## Notes

- Declining a source route (null factory result, a throwing factory, or a carrier that can no longer serve
  the route) releases the triggering message silently. Only factory and handler exceptions are logged; a
  declined trigger is not a logged drop.
- Server callbacks are serialized per endpoint. The Ack variant adds no cross-endpoint ordering or
  serialization; the shared suite asserts only per-handler serialization.

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Ack factory triggering-message ownership (no retain/mutate/release) | Requires the planned custom `IMemoryRental` test implementation. |
