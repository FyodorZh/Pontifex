using Archivarius.DataModels;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Scriba;

namespace Pontifex.Test
{
    public class BigApi : ApiRoot
    {
        public readonly RRDecl<BytesWrapper, BytesWrapper> Ping = new();
    }

    public class BigApiClient : BigApi
    {
        public BigApiClient(int size, ILogger Log)
        {
            Connected += () => Task.Run(async () =>
            {
                Random rnd = new();
                byte[] data = new byte[size];
                for (int i = 0; i < size; ++i)
                {
                    data[i] = (byte)rnd.Next();
                }

                Log.i("Request sending...");
                var res = await Ping.RequestAsync(new BytesWrapper(data));
                Log.i("Response received");
                for (int i = 0; i < size; ++i)
                {
                    if (res.Value![i] != data[i])
                    {
                        throw new Exception("Data mismatch");
                    }
                }
                Log.i("Response is OK");
            });
        }
    }
    
    public class BigApiServer : BigApi
    {
        public BigApiServer(ILogger logger)
        {
            Ping.SetProcessor( (request) =>
            {
                logger.i("Received");
                var bytes = request.Data.Value!;
                var res = request.Response(new BytesWrapper(bytes));
                logger.i("Responded");
            });
        }
    }
}