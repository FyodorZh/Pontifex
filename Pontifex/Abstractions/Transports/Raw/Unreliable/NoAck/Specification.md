# RawUnreliableNoAck Transport Specification

## 1. Scope

RawUnreliableNoAck is the RawUnreliable variant whose server handler factory
receives only the inbound source route and never the triggering message. This
document defines that variant and adopts the RawUnreliable Specification as
its normative common core.

## 2. Adoption of the RawUnreliable Specification

This specification adopts the [RawUnreliable Specification](../Specification.md) (hereafter
"Common") as normative. Every requirement in Common applies to
RawUnreliableNoAck except as explicitly modified in this document. Where
Common refers to a shared type, RawUnreliableNoAck uses that shared type
unchanged. Where Common refers to the "variant server handler factory",
RawUnreliableNoAck uses the factory defined in this document.

Normative language uses the key words defined in Common §2.

## 3. Public contract

The client, endpoint, and handler contracts are defined by Common §3 through
`IRawUnreliableClient`, `IRawUnreliableEndpoint`, and
`IRawUnreliableHandler`. The server contract is:

```csharp
public interface IRawUnreliableNoAckServer : IRawUnreliableTransport
{
    bool Init(Func<IEndPoint, IRawUnreliableHandler?> handlerFactory);
}
```

The NoAck server factory receives only the source route. It **MUST NOT**
receive or observe the triggering message; the triggering message remains
owned by the transport and, after a successful `handler.OnStarted`, is
delivered through `handler.OnReceived` as specified in Common §7.3.

## 4. Initialization

Common §6 applies. The NoAck server binds
`Func<IEndPoint, IRawUnreliableHandler?>` as its variant handler factory
(Common §6.1).

## 5. Server source-route creation

Common §7.3 applies with the factory invocation defined here: the server
invokes the factory as `handlerFactory(source)`, supplying only the source
route. It never passes the triggering message.

## 6. Connectionless model and message path

Common §8 applies. The message-path sequence is identical to Common §8 with
the factory step `handlerFactory(source)`; the bracketed factory form in the
Common sequence diagram resolves to that single-argument call for
RawUnreliableNoAck.

## 7. Conformance controls

Common §12 applies. The variant transport control type is
`IRawUnreliableNoAckTransportConformanceControl`, extending
`IRawUnreliableTransportConformanceControl`; the variant endpoint control type
is `IRawUnreliableNoAckEndpointConformanceControl`, extending
`IRawUnreliableEndpointConformanceControl`.

### 7.1 Contract transition

The prior RawUnreliableNoAck revision was a breaking raw-unreliable contract
transition. It removed the RawUnreliableNoAck transport receive events and
transport-level `TrySend` methods in favor of endpoint-owned `UnreliableSend`.
It also changed the shared `IRawUnreliableHandler` contract by adding
`OnStopped` and changed `IRawUnreliableEndpoint.Stop` to return `bool` and
accept an optional reason.

A subsequent consolidation moved the shared client `Init` contract and the
shared conformance-control gate members to the RawUnreliable level. The NoAck
variant interfaces now inherit them from `IRawUnreliableClient`,
`IRawUnreliableTransportConformanceControl`, and
`IRawUnreliableEndpointConformanceControl`, which the acknowledgement variant
shares. This change is additive for consumers: the NoAck contract exposes the
same members through its variant interfaces as before.

Conformance adapters and suites using the former transport-level send, receive,
or reliability controls MUST be rewritten for the transport and endpoint
controls defined in Common §12 and named in this document. Any component that
inherits from, exposes, adapts, or otherwise consumes these shared
raw-unreliable abstractions MUST be updated to the current handler and
endpoint contract. A contract that merely has a similar name, including an
acknowledgement variant, is not implicitly changed unless it exposes or
inherits these shared types.

## 8. Security considerations and conformance checklists

The security considerations and both checklists of Common §14 apply to
RawUnreliableNoAck without change.
