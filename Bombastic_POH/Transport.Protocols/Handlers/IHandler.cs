using System;

namespace Transport.Protocols.Handlers
{
    public interface IHandler
    {
        Type HandleType { get; }
    }
}
