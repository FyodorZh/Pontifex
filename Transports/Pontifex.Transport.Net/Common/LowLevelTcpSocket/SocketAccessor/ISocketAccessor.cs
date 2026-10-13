using System;
using System.Net.Sockets;

namespace Pontifex.NetSockets.Tcp
{
    internal interface ISocketAccessor : IDisposable
    {
        SocketAsyncEventArgs CreateEventArgs();

        bool SendAsync(SocketAsyncEventArgs e);

        bool ReceiveAsync(SocketAsyncEventArgs e);

        void Shutdown(SocketShutdown how);

        /// <summary>
        /// Releases an <see cref="SocketAsyncEventArgs"/> obtained from <see cref="CreateEventArgs"/>.
        /// This is the narrow lifetime seam around args disposal (public <c>Dispose</c> is
        /// virtual-but-sealed, so a derived args type cannot observe calls made through the base
        /// type). Implementations must perform the real disposal.
        /// </summary>
        void DisposeEventArgs(SocketAsyncEventArgs e);
    }
}
