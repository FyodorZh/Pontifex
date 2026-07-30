using System.Runtime.CompilerServices;

namespace Pontifex.NoAck.Raw.Unreliable.Tests;

internal static class NoAckRawUnreliableDirectRegistration
{
    [ModuleInitializer]
    public static void Register()
    {
        ConformanceAdapterSource.Register(new NoAckRawUnreliableDirectAdapter());
    }
}
