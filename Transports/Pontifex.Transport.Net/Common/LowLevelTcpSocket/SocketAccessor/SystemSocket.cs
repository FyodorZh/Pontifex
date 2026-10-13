using System;
using System.Net.Sockets;

namespace Pontifex.NetSockets.Tcp
{
    /// <summary>
    /// Socket accessor over a real <see cref="Socket"/>.
    /// </summary>
    /// <remarks>
    /// Construction validates that the socket is a live, connected stream (TCP) socket and rejects
    /// anything else <b>without disposing the caller's socket</b>; ownership transfers only on
    /// success. The connectivity check is a momentary validation: disconnection may race it and is
    /// not treated as protection against later network failure. <see cref="SystemSocket"/> exposes
    /// no <c>Connect</c>, so a rejected unconnected socket could never become usable for this
    /// accessor anyway.
    /// </remarks>
    internal sealed class SystemSocket : ISocketAccessor
    {
        private readonly Socket _socket;

        public SystemSocket(Socket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));

            if (IsDisposed(socket))
            {
                throw new ObjectDisposedException(nameof(socket));
            }

            if (socket.SocketType != SocketType.Stream || socket.ProtocolType != ProtocolType.Tcp)
            {
                throw new ArgumentException(
                    "LowLevelTcpSocket requires a connected stream (TCP) socket.",
                    nameof(socket));
            }

            if (!socket.Connected)
            {
                throw new ArgumentException(
                    "LowLevelTcpSocket requires a connected stream (TCP) socket.",
                    nameof(socket));
            }
        }

        /// <summary>
        /// Detects a disposed socket without relying on <c>Socket.SafeHandle</c> (not part of the
        /// netstandard2.1 surface). Accessing an endpoint throws <see cref="ObjectDisposedException"/>
        /// on a disposed socket.
        /// </summary>
        private static bool IsDisposed(Socket socket)
        {
            try
            {
                _ = socket.LocalEndPoint;
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        public SocketAsyncEventArgs CreateEventArgs() => new SocketAsyncEventArgs();

        public bool SendAsync(SocketAsyncEventArgs e) => _socket.SendAsync(e);

        public bool ReceiveAsync(SocketAsyncEventArgs e) => _socket.ReceiveAsync(e);

        public void Shutdown(SocketShutdown how) => _socket.Shutdown(how);

        public void DisposeEventArgs(SocketAsyncEventArgs e) => e.Dispose();

        public void Dispose() => _socket.Dispose();
    }
}
