# RawReliableNoAck Transport Specification

## 1. Scope

RawReliableNoAck is a bidirectional, connection-oriented transport contract for
opaque `UnionDataList` messages. It is the sibling of RawReliableAck: it
provides the same reliable delivery and endpoint-owned architecture but has
**no application acknowledgement step**. A client is admitted, or rejected,
without exchanging any application-level acknowledgment payload.

This specification defines:

- client admission through a server-side handler factory;
- per-connection endpoint and callback lifecycles;
- ordered, duplicate-free delivery within a live logical connection;
- ownership rules for transport buffers; and
- responsibilities of application logic and transport implementations.

This specification is implementation-agnostic. It does **not** define wire
format, version negotiation, authentication, authorization, confidentiality,
integrity protection, timeout policy, keep-alive policy, or automatic
reconnection. Those concerns belong to the selected transport implementation
or to a protocol layered above RawReliableNoAck.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Adoption of the RawReliableAck specification

RawReliableNoAck adopts the RawReliableAck specification as its common core.
The endpoint model, endpoint operations, delivery guarantees, buffer
ownership, callback concurrency, and failure and transport-shutdown rules of
the RawReliableAck specification apply unchanged to RawReliableNoAck, with
these exceptions:

- admission replaces the ACK handshake with a server-side handler factory
  (Section 5);
- no acknowledgment data or acknowledgment response exists, so the ownership
  rows for `FillAckData`, `TryAck`, and `FillAckResponse` have no equivalent;
- the lifecycle rule table in Section 6 is authoritative for handler callbacks;
- this document overrides admission and handshake behavior only.

Where this document and the adopted RawReliableAck core disagree about
admission or handshake, this document wins. Everywhere else, the RawReliableAck
text is normative for RawReliableNoAck as well.

## 4. Public contract

```csharp
public interface IRawReliableNoAckClient
    : IRawReliableClientTransport<IRawReliableNoAckClientHandler>
{
}

public interface IRawReliableNoAckClientHandler : IRawReliableClientHandler
{
    void OnConnected(IRawReliableEndpoint endPoint);
}

public interface IRawReliableNoAckServer : IRawReliableTransport
{
    bool Init(IRawReliableNoAckServerHandlerFactory<IRawReliableNoAckServerHandler> factory);
}

public interface IRawReliableNoAckServerHandlerFactory<out THandler>
    where THandler : IRawReliableNoAckServerHandler
{
    THandler? TryCreate(IEndPoint remote);
}

public interface IRawReliableNoAckServerHandler : IRawReliableServerHandler
{
    void OnConnected(IRawReliableEndpoint endPoint);
}

public interface IRawReliableNoAckEndpoint : IRawReliableEndpoint
{
}
```

`IRawReliableClientTransport<TClientHandler>` supplies `Init(handler)`.
`IRawReliableTransport` extends `IRawTransport` and supplies
`MessageMaxByteSize`. `IRawTransport` extends `ITransport` and supplies
`Name`, `IsValid`, `IsStarted`, `Start(Action<StopReason>)`, `Stop(StopReason?)`,
`Log`, `Memory`, `MessageMaxByteSize`, and `GetControls`.

`IRawReliableHandler` extends `IRawHandler` and supplies
`OnDisconnected(StopReason)`. `IRawReliableClientHandler` and
`IRawReliableServerHandler` extend `IRawReliableHandler` and add no lifecycle
members of their own. `IRawHandler` supplies `OnReceived(UnionDataList)` and
`OnStopped(StopReason)`.

`IRawEndpoint` extends `IBaseEndpoint` and supplies `RemoteEndPoint`,
`MessageMaxByteSize`, `GetControls`, and the merged `Send(UnionDataList)`
method. Client and server endpoints for one established connection report the
same `MessageMaxByteSize`.

Clients and servers expose neither a transport-level receive event nor a
transport-level send operation. A handler receives an `IRawReliableEndpoint`
through `OnConnected` and uses that endpoint for both sending and logical
disconnection.

`IRawReliableNoAckEndpoint` is a marker for endpoint implementation contracts.
It adds no members; it lets an implementation type itself as a
no-acknowledgement endpoint without exposing a concrete endpoint class.

## 5. Admission

Admission has no handshake payload. The server asks the bound
`IRawReliableNoAckServerHandlerFactory<THandler>` whether to admit a
connection attempt and, if so, for its session handler.

1. The server **MUST** invoke `TryCreate(remote)` for each connection attempt.
2. Calls to `TryCreate` on a server **MUST NOT** overlap concurrently.
3. `TryCreate` **MUST** return a fresh server handler for each admitted
   session. A handler instance **MUST NOT** serve multiple sessions.
4. The returned handler owns no buffers: there is no acknowledgment payload to
   populate or release.
5. Returning `null` rejects the connection attempt. The server **MUST NOT**
   cache a null return: a later connection attempt from the same source is
   eligible to invoke `TryCreate` again.
6. An exception thrown by `TryCreate` is a rejection of that connection
   attempt. The server **MUST** catch and log the exception and **MUST NOT**
   stop or invalidate the transport solely because of it. A later connection
   attempt from the same source is eligible to invoke `TryCreate` again.

Regular messages **MUST NOT** be exposed to application handlers before their
side has completed `OnConnected`.

The way a rejection is observed by the client is implementation-defined, but
the client **MUST** observe it: the client **MUST** receive `OnStopped` without
`OnConnected` or `OnDisconnected` for a rejected connection attempt. Because
there is no acknowledgment packet, a carrier typically signals rejection by
closing the physical connection; a carrier **MAY** use any other mechanism that
terminates the establishment attempt.

For an admitted session, the server **MUST** create and start the session
endpoint and invoke its handler's `OnConnected` only after that endpoint is
usable. Server acceptance is local. Server `OnConnected` MAY run before the
client observes its own connection result.

## 6. Lifecycle

The handler callback rules are:

| Situation | Handler callbacks |
| --- | --- |
| Transport `Start` returns `false` | none |
| `Start` returns `true`, connection fails before `OnConnected` | exactly one `OnStopped` |
| `OnConnected` delivered | `OnConnected`, then `OnDisconnected`, then `OnStopped`, each once |
| Server handler created, but session endpoint creation, start, or admission fails | exactly one `OnStopped` |

```text
client, successful connection: OnConnected -> OnDisconnected -> OnStopped
client, failed establishment:  OnStopped
server, successful session:    OnConnected -> OnDisconnected -> OnStopped
server, failed session:        OnStopped
```

A transport `Start` that returns `false` **MUST NOT** invoke the client
handler's `OnStopped`. A client whose `Start` returned true and that terminates
before `OnConnected` **MUST** receive exactly one `OnStopped` and no
`OnDisconnected`.

A session that never reaches `OnConnected` receives exactly one `OnStopped` and
no `OnDisconnected`. This includes a server handler that was created by
`TryCreate` but whose session endpoint could not be created, started, or
admitted.

`OnConnected`, `OnReceived`, `OnDisconnected`, and `OnStopped` are serialized
and non-reentrant for one logical connection. `OnReceived` is never invoked
after `OnDisconnected`. During and after `OnDisconnected`, `IsConnected` is
`false` and `RemoteEndPoint` is `null`.

A server transport stop disconnects every active session. Each session handler
observes the documented sequence before the transport's own `onStopped`
callback is invoked.

## 7. Carrier notes

TCP is the only in-scope carrier for RawReliableNoAck. Its canonical scheme is
`tcp-noack`. `tcp` is not an alias for RawReliableNoAck; it remains the scheme
of the corresponding RawReliableAck carrier.

The TCP carrier owns the socket, framing, receiver, sender, connect and
teardown behavior. It derives no admission payload from the factory: rejection
is observed by the client as a closed connection.

Direct NoAck is reserved, not implemented; `direct-noack` is not a registered
scheme. NoAck conformance controls and a NoAck conformance suite are deferred.

Deferred future work includes consolidating the Ack and NoAck TCP endpoints
onto a shared client/server endpoint base. Until then the NoAck carrier is a
separate implementation that carries no acknowledgement frames.

## 8. Non-goals

- No acknowledgment handshake, acknowledgment payload, or acknowledgment
  response.
- No authentication, authorization, confidentiality, integrity, or replay
  protection; admission decisions made in `TryCreate` carry no security
  guarantee beyond what the application enforces.
- No automatic reconnect, session resume, or message retransmission.
- No NoAck conformance controls or conformance suite.
- No Direct NoAck carrier.
