using Archivarius;
using Pontifex.Api;

namespace Pontifex.AckRawReliable.Tests.InvariantChecker;

public struct KickMessage : IDataStruct
{
    public void Serialize(ISerializer serializer) { }
}

public class InvariantCheckerApi : ApiRoot
{
    public readonly S2CMessageDecl<KickMessage> OnKick = new();
}

public class InvariantCheckerApiClient : InvariantCheckerApi
{
}

public class InvariantCheckerApiServer : InvariantCheckerApi
{
}
