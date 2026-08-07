# RawUnreliableAck Transport Specification

## 1. Scope

RawUnreliableAck is the RawUnreliable variant whose server handler factory
receives the inbound source route and the triggering message, so a source
route can be accepted or declined per message. This document defines that
variant and adopts the RawUnreliable Specification as its normative common
core.

## 2. Adoption of the RawUnreliable Specification

This specification adopts the [RawUnreliable Specification](../Specification.md) (hereafter
"Common") as normative. Every requirement in Common applies to
RawUnreliableAck except as explicitly modified in this document. Where Common
refers to a shared type, RawUnreliableAck uses that shared type unchanged.
Where Common refers to the "variant server handler factory", RawUnreliableAck
uses the factory defined in this document.

Normative language uses the key words defined in Common §2.

## 3. Public contract

The client, endpoint, and handler contracts are defined by Common §3 through
`IRawUnreliableClient`, `IRawUnreliableEndpoint`, and
`IRawUnreliableHandler`. The server contract is:

```csharp
public interface IRawUnreliableAckServer : IRawUnreliableTransport
{
    bool Init(Func<IEndPoint, UnionDataList, IRawUnreliableHandler?> handlerFactory);
}
```

The Ack server factory receives the source route and the triggering message.
The triggering-message ownership, inspection, and delivery rules are defined
in §5.

## 4. Initialization

Common §6 applies. The Ack server binds
`Func<IEndPoint, UnionDataList, IRawUnreliableHandler?>` as its variant
handler factory (Common §6.1).

## 5. Server source-route creation

Common §7.3 applies with the factory invocation defined here: the server
invokes the factory as `handlerFactory(source, triggeringMessage)`, supplying
the source route and the triggering message.

The triggering message is owned by the transport for the entire source-route
selection and endpoint-startup sequence. The factory MAY inspect it to decide
acceptance, but it **MUST NOT** acquire an additional reference, retain it
beyond the call, mutate it, or release it. If the factory returns a handler
and `handler.OnStarted` returns successfully, the transport delivers the same
logical triggering message through `handler.OnReceived`. If the factory
returns null, throws, or `handler.OnStarted` throws, the transport releases
the triggering message and otherwise follows Common §7.3.

## 6. Connectionless model and message path

Common §8 applies. The message-path sequence is identical to Common §8 with
the factory step `handlerFactory(source, message)`; the bracketed factory
form in the Common sequence diagram resolves to that two-argument call for
RawUnreliableAck.

## 7. Conformance controls

Common §12 applies. The variant transport control type is
`IRawUnreliableAckTransportConformanceControl`, extending
`IRawUnreliableTransportConformanceControl`; the variant endpoint control type
is `IRawUnreliableAckEndpointConformanceControl`, extending
`IRawUnreliableEndpointConformanceControl`.

## 8. Security considerations and conformance checklists

The security considerations and both checklists of Common §14 apply to
RawUnreliableAck without change. In addition, an Ack server factory MUST treat
the triggering message as untrusted input: its acceptance decision must be
based only on authenticated and validated content, because the transport
provides no integrity or authentication.
