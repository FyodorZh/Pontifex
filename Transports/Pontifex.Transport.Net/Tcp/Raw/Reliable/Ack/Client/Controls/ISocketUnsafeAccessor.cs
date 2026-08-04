using System.Net.Sockets;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    public interface ISocketUnsafeAccessor : IControl
    {
        Socket? GetSocketUnsafe();
    }
}