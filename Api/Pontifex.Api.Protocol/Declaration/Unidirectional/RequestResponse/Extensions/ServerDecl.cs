using System;
using System.Threading.Tasks;
using Archivarius;
using Pontifex.UserApi;
using Scriba;

namespace Pontifex.Api.Server
{
    public static class RRDecl_Ext
    {
        public static void SetProcessor<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, Action<IRequest<TRequest, TResponse>> processor)
            where TRequest : struct, IDataStruct
            where TResponse : struct, IDataStruct
        {
            ((IResponder<TRequest, TResponse>) decl).SetProcessor(processor);
        }

        public static void SetProcessorAsync<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, Func<TRequest, Task<TResponse>> processor)
            where TRequest : struct, IDataStruct
            where TResponse : struct, IDataStruct
        {
            ((IResponder<TRequest, TResponse>) decl).SetProcessor(r =>
                Task.Run(async () =>
                {
                    try
                    {
                        var response = await processor(r.Data);
                        r.Response(response);
                    }
                    catch (Exception e)
                    {
                        r.Fail(e.Message);
                        Log.wtf(e);
                    }
                }));
        }

        public static void SetProcessorAsync<TMessage>(this C2SMessageDecl<TMessage> decl, Func<TMessage, Task> processor)
            where TMessage : struct, IDataStruct
        {
            ((IReceiver<TMessage>) decl).SetProcessor(r =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await processor(r);
                    }
                    catch (Exception e)
                    {
                        Log.wtf(e);
                    }
                });
            });
        }
    }
}