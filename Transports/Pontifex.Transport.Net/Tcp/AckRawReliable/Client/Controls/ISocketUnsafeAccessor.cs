using System.Net.Sockets;

namespace Pontifex.Ack.Raw.Reliable.Tcp
{
    public interface ISocketUnsafeAccessor : IControl
    {
        Socket? GetSocketUnsafe();
    }
}