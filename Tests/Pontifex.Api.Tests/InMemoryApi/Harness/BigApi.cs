using Archivarius;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;

namespace Pontifex.Api.Tests.InMemoryApi.Harness;

internal struct Int1 : IDataStruct
{
    public int A;

    public void Serialize(ISerializer serializer)
    {
        throw new NotSupportedException("The in-memory model pipe does not serialize payloads.");
    }
}

internal struct Int2 : IDataStruct
{
    public int A;
    public int B;

    public void Serialize(ISerializer serializer)
    {
        throw new NotSupportedException("The in-memory model pipe does not serialize payloads.");
    }
}

internal sealed class BigApiSubApi : SubApi
{
    public readonly RRDecl<Int2, Int1> Div = new();
}

internal class BigApi : ApiRoot
{
    public readonly BigApiSubApi SubApi = new();
    public readonly C2SMessageDecl<Int1> ToServer = new();
    public readonly S2CMessageDecl<Int1> FromServer = new();
}

internal sealed class BigApiClient : BigApi
{
    private int _lastFromServer;

    public BigApiClient()
    {
        FromServer.SetProcessor(response => _lastFromServer = response.A);
    }

    public int MultiplyBy2(int value)
    {
        ToServer.Send(new Int1 { A = value });
        return _lastFromServer;
    }
}

internal sealed class BigApiServer : BigApi
{
    public BigApiServer()
    {
        SubApi.Div.SetProcessor(request =>
        {
            if (request.Data.B == 0)
            {
                request.Fail("Division by zero");
                return;
            }

            request.Response(new Int1 { A = request.Data.A / request.Data.B });
        });

        ToServer.SetProcessor(message => FromServer.Send(new Int1 { A = message.A * 2 }));
    }
}
