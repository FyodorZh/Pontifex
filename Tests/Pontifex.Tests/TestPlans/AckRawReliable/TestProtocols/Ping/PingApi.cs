using Archivarius;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;

namespace Pontifex.AckRawReliable.Tests.Ping;

public struct PingRequest : IDataStruct
{
    public int Seq;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref Seq);
    }
}

public struct PongResponse : IDataStruct
{
    public int Seq;

    public void Serialize(ISerializer serializer)
    {
        serializer.Add(ref Seq);
    }
}

public class PingApi : ApiRoot
{
    public readonly RRDecl<PingRequest, PongResponse> Ping = new();
}

public class PingApiClient : PingApi
{
    public Task<PongResponse> SendPing(int seq)
    {
        return Ping.RequestAsync(new PingRequest { Seq = seq });
    }
}

public class PingApiServer : PingApi
{
    public PingApiServer()
    {
        Ping.SetProcessor(r =>
        {
            r.Response(new PongResponse { Seq = r.Data.Seq });
        });
    }
}
