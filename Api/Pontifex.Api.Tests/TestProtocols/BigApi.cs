using Archivarius;
using Pontifex.UserApi;

namespace Pontifex.Api.Protocol
{
    public class BigApiSubApi : ProtocolSubApi
    {
        public readonly RRDecl<Request, Response> Div = new();

        public struct Request : IDataStruct
        {
            public int A;
            public int B;
            public void Serialize(ISerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        public struct Response : IDataStruct
        {
            public int C;
            public void Serialize(ISerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
    
    public class BigApi : ProtocolApi
    {
        public readonly BigApiSubApi SubApi = new();

        public readonly C2SMessageDecl<IntValue> ToServer = new();
        public readonly S2CMessageDecl<IntValue> FromServer = new();
        
        public struct IntValue : IDataStruct
        {
            public int Value;
            public void Serialize(ISerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }

    namespace Client
    {
        public class BigApi_Client : BigApi
        {
            private TaskCompletionSource<int>? _tcs;
            
            public BigApi_Client()
            {
                FromServer.SetProcessor(r => _tcs!.SetResult(r.Value));
            }

            public Task<int> MultiplyOnce(int x)
            {
                if (_tcs != null)
                {
                    return _tcs.Task;
                }

                _tcs = new TaskCompletionSource<int>();
                ToServer.Send(new IntValue {Value = x});
                return _tcs.Task;
            }
        }
    }

    namespace Server
    {
        public class BigApi_Server : BigApi
        {
            public bool Disconnected { get; private set; }
            
            public BigApi_Server()
            {
                Disconnect.SetProcessor(msg =>
                {
                    Disconnected = true;
                });

                SubApi.Div.SetProcessor(r =>
                {
                    if (r.Data.B == 0)
                    {
                        r.Fail("Division by zero");
                        return;
                    }
                    r.Response(new BigApiSubApi.Response
                    {
                        C = r.Data.A / r.Data.B
                    });
                });

                ToServer.SetProcessor(v =>
                {
                    FromServer.Send(new IntValue {Value = v.Value * 2});
                });
            }
        }
    }
}