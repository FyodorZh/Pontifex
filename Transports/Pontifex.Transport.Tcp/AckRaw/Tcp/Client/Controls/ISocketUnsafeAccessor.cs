using System.Net.Sockets;
using Pontifex.Abstractions;

namespace Pontifex.Transports.Tcp
{
    public interface ISocketUnsafeAccessor : IControl
    {
        Socket? GetSocketUnsafe();
    }
}