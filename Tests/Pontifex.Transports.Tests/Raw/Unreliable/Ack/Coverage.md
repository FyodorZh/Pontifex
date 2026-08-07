# RawUnreliableAck Conformance Coverage

`RawUnreliableAckConformanceTests` runs the shared
[RawUnreliable Conformance Coverage](../Coverage.md) suite plus the
Ack-specific triggering-message factory tests below.

## Covered

| Specification area | Coverage |
| --- | --- |
| Ack triggering message | The server factory receives the triggering message and can inspect it; the same logical message is delivered to OnReceived after a successful OnStarted, and the factory can accept or decline each message by content. |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Ack factory triggering-message ownership (no retain/mutate/release) | Requires the planned custom `IMemoryRental` test implementation. |
