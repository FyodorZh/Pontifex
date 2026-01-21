using Archivarius;
using Pontifex.Api;

namespace Pontifex.Api
{
    public struct Int1 : IDataStruct
    {
        public int A;
        public void Serialize(ISerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
    
    public struct Int2 : IDataStruct
    {
        public int A;
        public int B;
        public void Serialize(ISerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
    
    
    public class BigApiSubApi : SubApi
    {
        public readonly RRDecl<Int2, Int1> Div = new();
    }
    
    public class BigApi : ApiRoot
    {
        public readonly BigApiSubApi SubApi = new();

        public readonly C2SMessageDecl<Int1> ToServer = new();
        public readonly S2CMessageDecl<Int1> FromServer = new();
    }

    namespace Client
    {
        public class BigApi_Client : BigApi
        {
            private TaskCompletionSource<int>? _tcs;
            
            public BigApi_Client()
            {
                FromServer.SetProcessor(r => _tcs!.SetResult(r.A));
            }

            public Task<int> MultiplyBy2(int x)
            {
                if (_tcs != null)
                {
                    return _tcs.Task;
                }

                _tcs = new TaskCompletionSource<int>();
                ToServer.Send(new Int1 {A = x});
                return _tcs.Task;
            }
        }
    }

    namespace Server
    {
        public class BigApi_Server : BigApi
        {
            public BigApi_Server()
            {
                SubApi.Div.SetProcessor(r =>
                {
                    if (r.Data.B == 0)
                    {
                        r.Fail("Division by zero");
                        return;
                    }
                    r.Response(new Int1
                    {
                        A = r.Data.A / r.Data.B
                    });
                });

                ToServer.SetProcessor(v =>
                {
                    FromServer.Send(new Int1() {A = v.A * 2});
                });
            }
        }
    }
}