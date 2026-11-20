# RawReliable Transport Specification

## 1. Scope

`RawReliable` is the single reliable raw transport contract for opaque
`UnionDataList` messages. It is bidirectional, connection-oriented, and
guarantees in-order, duplicate-free delivery within a live logical connection.

Every `RawReliable` carrier performs a mandatory transport handshake before the
application handler observes `OnConnected`. The handshake confirms the
connection, rejects version and name mismatches, and negotiates
`MessageMaxByteSize` as the minimum of the two peers.

This specification defines:

- the public client/server, handler, and endpoint contracts;
- the transport handshake, rejection, timeout, and capacity rules;
- per-connection endpoint and callback lifecycles;
- ordered delivery and buffer-ownership rules;
- the `zip`/`reconnectable` wrapper negotiation and the transparent `log`
  wrapper;
- the `RawUnreliableNoAck -> RawReliable` converter handshake.

It does not define authentication, authorization, confidentiality, integrity,
or replay protection. Those concerns belong to the application or to a protocol
layered above `RawReliable`.

## 2. Normative language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**,
and **MAY** in this document are to be interpreted as described in
[RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

## 3. Public contract

```csharp
public interface IRawReliableClient
    : IRawReliableClientTransport<IRawReliableClientHandler>
{
}

public interface IRawReliableClientHandler : IRawReliableHandler
{
    void OnConnected(IRawReliableEndpoint endPoint);
}

public interface IRawReliableServer : IRawReliableTransport
{
    bool Init(IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory);
}

public interface IRawReliableServerHandlerFactory<out THandler>
    where THandler : IRawReliableServerHandler
{
    THandler? TryCreate(IEndPoint remote);
}

public interface IRawReliableServerHandler : IRawReliableHandler
{
    void OnConnected(IRawReliableEndpoint endPoint);
}
```

`IRawReliableClientTransport<TClientHandler>` supplies `Init(handler)` and is
constrained to `IRawReliableClientHandler`. `IRawReliableTransport` extends
`IRawTransport` and supplies `MessageMaxByteSize`. `IRawTransport` extends
`ITransport` and supplies `Name`, `IsValid`, `IsStarted`,
`Start(Action<StopReason>)`, `Stop(StopReason?)`, `Log`, `Memory`,
`MessageMaxByteSize`, and `GetControls`.

`IRawReliableHandler` extends `IRawHandler` and supplies
`OnDisconnected(StopReason)`. `IRawHandler` supplies `OnReceived(UnionDataList)`
and `OnStopped(StopReason)`.

`IRawReliableEndpoint` extends `IRawEndpoint` and supplies `RemoteEndPoint`,
`MessageMaxByteSize`, `GetControls`, and the merged `Send(UnionDataList)`
method. Client and server endpoints for one established connection report the
same `MessageMaxByteSize`.

Clients and servers expose neither a transport-level receive event nor a
transport-level send operation. A handler receives an `IRawReliableEndpoint`
through `OnConnected` and uses that endpoint for both sending and logical
disconnection.

## 4. Transport handshake

The handshake is owned by the transport, not by the application. It is a flat
`UnionDataList` dataset with an explicit frame type, not a DTO.

1. The client sends, as its first outbound frame after the low-level connection
   is up, a `HandshakeRequestMessage` carrying
   `[version (short), name (string), messageMaxByteSize (int)]`.
2. The server reads the length-prefixed frame, enforces `HandshakeMaxByteSize`
   (default 128 bytes), deserializes it, and verifies that the leading frame
   type is `HandshakeRequestMessage` before parsing the body.
3. The server validates the version and then the name (UTF-8 content
   equality). A version mismatch or a name mismatch rejects the connection.
4. The request maximum **MUST** be at least 1 and at least
   `HandshakeMaxByteSize`; otherwise the server rejects with
   `CapacityExceeded` because it could not send its response.
5. Only after these checks does the server call the application
   `IRawReliableServerHandlerFactory.TryCreate(remote)`. A `null` return, an
   exception, or a stopped/not-started transport rejects the connection.
6. On acceptance the server computes
   `agreedMax = min(request.messageMaxByteSize, server.MessageMaxByteSize)` and
   replies with a `HandshakeResponseMessage` carrying
   `[version, name, agreedMax]`.
7. The client validates the response version, the response name, and the
   response maximum, then applies `min(local, server)` as its
   `MessageMaxByteSize`. Only then does the handler observe `OnConnected`.
8. `MessageMaxByteSize` is applied once and is read-only afterwards. It is the
   value enforced by `Send` on both peers.

### 4.1 Rejection

A rejected connection receives a `HandshakeRejectMessage` carrying
`[serverVersion, serverName, status, text?]`, where `status` is one of
`Accepted`, `Rejected`, `VersionMismatch`, `NameMismatch`, `CapacityExceeded`,
or `Timeout`.

- `VersionMismatch` and `NameMismatch` are reported to the client as
  `ProtocolMismatch`.
- `Rejected` and `CapacityExceeded` are reported as `HandshakeRejected`.
- `Timeout` is reported as `TimeOut`.
- A rejected connection delivers no `OnConnected`. A client that started
  successfully and never connected receives exactly one `OnStopped`.

### 4.2 Timeout and linger

- The client handshake deadline defaults to 10 seconds and is configurable.
  Expiry stops the connection with `TimeOut`; the timer is stopped and disposed
  when the response, reject, or disconnect wins the race.
- The server handshake deadline defaults to 10 seconds.
- After writing a reject frame, the TCP carrier keeps the socket open for
  `RejectLinger` (default 2 seconds) so the client can observe the reject
  before the peer closes. The Direct carrier lingers for zero.
- The handshake request, response, and reject buffers are kept within
  `HandshakeMaxByteSize`; the length prefix is validated before the body is
  allocated.

Handshake frames are transport-internal: they are never delivered through
`OnReceived`.

## 5. Admission

1. The server **MUST** invoke `TryCreate(remote)` for each admitted connection
   attempt, and only after the handshake version and name are accepted.
2. Calls to `TryCreate` on a server **MUST NOT** overlap concurrently.
3. `TryCreate` **MUST** return a fresh server handler for each admitted
   session. A handler instance **MUST NOT** serve multiple sessions.
4. Returning `null` rejects the connection attempt. The server **MUST NOT**
   cache a null return: a later connection attempt from the same source is
   eligible to invoke `TryCreate` again.
5. An exception thrown by `TryCreate` is a rejection of that connection
   attempt. The server **MUST** catch and log the exception and **MUST NOT**
   stop or invalidate the transport solely because of it.
6. Every handler returned by the factory is wrapped in a stop-once gate, so the
   carrier, the session endpoint, and the admission fallback share one stop
   latch and the application handler observes at most one `OnStopped`.

Regular messages **MUST NOT** be exposed to application handlers before their
side has completed `OnConnected`. For an admitted session, the server creates
and starts the session endpoint and invokes its handler's `OnConnected` only
after the handshake response is scheduled successfully and the endpoint is
usable.

## 6. Lifecycle

The handler callback rules are:

| Situation | Handler callbacks |
| --- | --- |
| Transport `Start` returns `false` | none |
| `Start` returns `true`, connection fails before `OnConnected` | exactly one `OnStopped` |
| `OnConnected` delivered | `OnConnected`, then `OnDisconnected`, then `OnStopped`, each once |
| Server handler created, but session endpoint creation, start, or admission fails | exactly one `OnStopped` |
| Handshake rejected, timed out, or the response cannot be sent | exactly one `OnStopped` |

```text
client, successful connection: OnConnected -> OnDisconnected -> OnStopped
client, failed establishment:  OnStopped
server, successful session:    OnConnected -> OnDisconnected -> OnStopped
server, failed session:        OnStopped
```

A transport `Start` that returns `false` **MUST NOT** invoke handler callbacks.
A client whose `Start` returned true and that terminates before `OnConnected`
**MUST** receive exactly one `OnStopped` and no `OnDisconnected`.

`OnConnected`, `OnReceived`, `OnDisconnected`, and `OnStopped` are serialized
and non-reentrant for one logical connection. `OnReceived` is never invoked
after `OnDisconnected`. During and after `OnDisconnected`, `IsConnected` is
`false`. `RemoteEndPoint` remains the immutable route metadata supplied when
the endpoint was created; callers must use `IsConnected`, not a null remote
endpoint, to determine whether the connection is live.

A server transport stop disconnects every active session. The transport
schedules each session handler's terminal notification before invoking its own
`onStopped` callback. Endpoint callbacks for different sessions may still
complete asynchronously; this ordering requires notification scheduling, not
that every session handler has returned before the transport callback begins.

## 7. Carriers

### 7.1 TCP

The canonical scheme is `tcp`, and the handshake name is `tcp`. The TCP carrier
owns the socket, length-prefixed framing, receiver, sender, connect, accept,
and teardown behavior. It enforces `HandshakeMaxByteSize` on the inbound
handshake prefix, tracks handshake sockets, and closes them when the transport
stops.

Binding parameters: host and port (required), `disconnection_timeout`,
`handshake_max_byte_size` (default 128), `handshake_timeout` (seconds, default
10), and `reject_linger` (milliseconds, default 2000).

### 7.2 Direct

The canonical scheme is `direct`, and the handshake name is `direct`. The
Direct carrier is an in-process channel: the handshake request is the first
channel message, the server validates it synchronously, and the reject frame is
delivered synchronously before the channel closes. `RejectLinger` is
`TimeSpan.Zero`, and no `System.Threading.Timer` is created for the handshake.

Binding parameters: `id` (required) and `handshake_max_byte_size` (default
128).

## 8. Wrappers

`zip` and `reconnectable` are `RawReliable` wrappers. Each wraps exactly one
`RawReliable` transport and performs its own independent negotiation after the
inner transport reports `OnConnected`. The envelope is a flat
`UnionDataList`:

```text
Request:  [version, name, NegotiationStatus.Request,  ...custom tail]
Response: [version, name, NegotiationStatus.Accepted, ...custom tail]
Reject:   [serverVersion, serverName, NegotiationStatus.Rejected, text?]
```

The status element is always the discriminator, so a custom tail element (for
example an `int` session id) cannot be mistaken for a status. The wrapper
enforces its own `HandshakeMaxByteSize` (default 128) and handshake timeout
(default 10 seconds), and it waits `RejectLinger` (default 2 seconds) before
disconnecting the inner transport after a reject.

- `zip` uses the name `zip`, version `1`, and an empty custom tail.
- `reconnectable` uses the name `reconnectable`, version `1`, and a custom tail
  carrying the session id, generation, and secret.
- `log` is transparent. It performs no negotiation, adds no round trip, and
  changes neither `MessageMaxByteSize` nor the connection lifecycle.

The wrapper gates `OnReceived`, `Send`, and `IsConnected` until admission, and
it mirrors the reliable terminal lifecycle exactly once. Wrappers can be
stacked.

## 9. Converter

The registered converter is
`RawUnreliableNoAckToRawReliableConverter`. It converts a
`RawUnreliableNoAck` transport into an `IRawReliableClient`/`IRawReliableServer`
by emulating reliable delivery over a `DeliveryManager`.

The converter handshake runs as the first delivery-managed message, so it is
retried and deduplicated by the delivery layer:

```text
Request:  [version, "converter", messageMaxByteSize]
Accepted: [version, "converter", HandshakeStatus.Accepted, agreedMax]
Reject:   [serverVersion, "converter", HandshakeStatus.Rejected, text?]
```

The client inspects the status element before parsing the body. The local
maximum is `DeliveryManager.DeliveryMaxByteSize`; the server returns
`min(request.max, its own DeliveryMaxByteSize)` and both endpoints apply the
agreed value. The handshake message is never delivered to the application
handler, and `OnConnected` is raised only after the accepted response
validates.

## 10. No acknowledgment protocol

There is no application acknowledgment payload, acknowledgment response,
acknowledger, or `ack(...)` scheme. Reliability is a property of the transport
contract; the handshake only establishes compatibility, identity, and capacity.

## 11. Non-goals

- No authentication, authorization, confidentiality, integrity, or replay
  protection.
- No automatic reconnect, session resume, or message retransmission beyond the
  delivery-layer behavior of a carrier or converter.
- No wire compatibility with earlier reliable transport contracts.
