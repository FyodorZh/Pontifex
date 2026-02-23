using Actuarius.Memory;
using Archivarius;
using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Scriba;

namespace TransportAnalyzer.TestLogic
{
    public struct Int2 : IDataStruct
    {
        public int A;
        public int B;

        public void Serialize(ISerializer dst)
        {
            dst.Add(ref A);
            dst.Add(ref B);
        }
    }

    public class AckRawProtocol : ApiRoot
    {
        public readonly RRDecl<Int2, Int2> EP = new RRDecl<Int2, Int2>();
        
        public ILogger Log { get; }
        public IMemoryRental Memory { get; }

        protected AckRawProtocol(IMemoryRental memory, ILogger logger)
        {
            Log = logger;
            Memory = memory;
        }
    }

    public class AckRawProtocol_Server : AckRawProtocol
    {
        public AckRawProtocol_Server(IMemoryRental memory, ILogger logger) 
            : base(memory, logger)
        {
            EP.SetProcessor(request =>
            {
                if (request.Data.A == request.Data.B)
                {
                    request.Response(new Int2() { A = -request.Data.A, B = -request.Data.A });
                }
                else
                {
                    request.Fail("Wrong message");
                    GracefulShutdown(TimeSpan.FromSeconds(10));
                }
            });
        }
    }

    public class AckRawProtocol_Client : AckRawProtocol
    {
        public AckRawProtocol_Client(IMemoryRental memory, ILogger logger) 
            : base(memory, logger)
        {
            Connected += () => Task.Run(Test);
        }

        public async Task Test()
        {
            try
            {
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 4; ++i)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int k = 0; k < 10000; ++k)
                        {
                            var request = new Int2() { A = k, B = k };
                            var response = await EP.RequestAsync(request, TimeSpan.FromSeconds(10));
                            if (response.A != -k || response.B != -k)
                            {
                                Log.e("Wrong response #{k}: {@response}", k, response);
                                GracefulShutdown(null);
                                break;
                            }
                            else
                            {
                                Log.d("Response #{k}: OK", k);
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                Log.d("All done");
                GracefulShutdown(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                GracefulShutdown(null, ex.ToString());
            }
        }
    }
}
