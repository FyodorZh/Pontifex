using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Archivarius;
using Operarius;
using Pontifex.UserApi;

namespace Pontifex.Api.Protocol.Client
{
    public static class RRDecl
    {
        public static void Request<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl,
            TRequest request,
            Action<IRequestSuccess<TResponse>> response,
            Action<IRequestFail> onFail)
            where TRequest : class, IDataStruct, new()
            where TResponse : class, IDataStruct, new()
        {
            ((IRequester<TRequest, TResponse>)decl).Request(request, response, onFail);
        }

        public static async Task<TResponse> RequestAsync<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, TRequest request, TimeSpan? timeout = null)
            where TRequest : class, IDataStruct, new()
            where TResponse : class, IDataStruct, new()
        {
            var requestTask = decl.RequestWithInfoAsync(request);

            using var cts = new CancellationTokenSource();
            var finalTimeout = timeout ?? TimeSpan.FromSeconds(15);
            var completedTask = await Task.WhenAny(requestTask, Task.Delay(finalTimeout, cts.Token));
            if (completedTask == requestTask)
            {
                cts.Cancel();
                return ((Task<SuccessResponseInfo<TResponse>>)completedTask).Result.Response;
            }

            throw new TimeoutException($"Request {request.GetType().Name} timeout ({finalTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s.)");
        }

        private static Task<SuccessResponseInfo<TResponse>> RequestWithInfoAsync<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, TRequest request)
            where TRequest : class, IDataStruct, new()
            where TResponse : class, IDataStruct, new()
        {
            var tcs = new TaskCompletionSource<SuccessResponseInfo<TResponse>>();

            // синхронно перекладываем данные из калбека потому что протокол переиспользует IRequestSuccess и IRequestFail
            decl.Request(request,
                response: r =>
                {
                    var successResponse = new SuccessResponseInfo<TResponse>(new CommonResponseInfo(r.Name, r.NetworkTime, r.ProcessTime), r.Response);
                    Task.Run(() => tcs.TrySetResult(successResponse));
                },
                onFail: r =>
                {
                    var errorResponse = new ErrorResponseInfo(new CommonResponseInfo(r.Name, r.NetworkTime, r.ProcessTime), r.Error);
                    Task.Run(() => tcs.TrySetException(new Exception(" Exception when sent " + errorResponse.CommonResponseInfo.Name + ". Error=" + errorResponse.Error)));
                });

            return tcs.Task;
        }
        
        
        public struct SuccessResponseInfo<TResponse>
        {
            public SuccessResponseInfo(CommonResponseInfo commonResponseInfo, TResponse response)
            {
                CommonResponseInfo = commonResponseInfo;
                Response = response;
            }

            public CommonResponseInfo CommonResponseInfo { get; private set; }

            public TResponse Response { get; private set; }
        }

        public struct ErrorResponseInfo
        {
            public ErrorResponseInfo(CommonResponseInfo commonResponseInfo, string error)
            {
                CommonResponseInfo = commonResponseInfo;
                Error = error;
            }

            public CommonResponseInfo CommonResponseInfo { get; private set; }

            public string Error { get; private set; }
        }

        public struct CommonResponseInfo
        {
            public CommonResponseInfo(string name, DeltaTime networkTime, DeltaTime processTime)
            {
                Name = name;
                NetworkTime = networkTime;
                ProcessTime = processTime;
            }

            public string Name { get; private set; }
            public DeltaTime NetworkTime { get; private set; }
            public DeltaTime ProcessTime { get; private set; }
        }
    }
}