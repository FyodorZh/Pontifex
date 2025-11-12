using System;
using System.Collections.Generic;
using System.Threading;
#if !UNITY_2017_1_OR_NEWER
using System.Threading.Tasks;
#endif
using Serializer.BinarySerializer;
using Shared;
using Shared.Buffer;
using Transport;
using TimeSpan = System.TimeSpan;


namespace NewProtocol
{
    public interface IRequest<out TRequest, in TResponse>
    {
        TRequest Data { get; }
        void Response(TResponse response);
        void Fail(string errorMessage);
    }

    public interface IRequestInfo
    {
        string Name { get; }
        DeltaTime NetworkTime { get; }
        DeltaTime ProcessTime { get; }
    }

    public interface IRequestSuccess<out TResponse> : IRequestInfo
    {
        TResponse Response { get; }
    }

    public interface IRequestFail : IRequestInfo
    {
        string Error { get; }
    }

    public class RequestFail : IRequestFail
    {
        public string Name
        {
            get { return ""; }
        }

        public DeltaTime NetworkTime
        {
            get { return DeltaTime.Zero; }
        }

        public DeltaTime ProcessTime
        {
            get { return DeltaTime.Zero; }
        }

        public string Error { get; set; }
    }

    internal interface IRequester<in TRequest, TResponse>
        where TRequest : IDataStruct, new()
        where TResponse : IDataStruct, new()
    {
        void Request(TRequest request, Action<IRequestSuccess<TResponse>> onResponse, Action<IRequestFail> onFail);
    }

    internal interface IResponder<TRequest, TResponse>
        where TRequest : IDataStruct
        where TResponse : IDataStruct
    {
        void SetProcessor(Action<IRequest<TRequest, TResponse>> processor);
    }

    public class RRDecl<TRequest, TResponse> : Declaration, IRequester<TRequest, TResponse>, IResponder<TRequest, TResponse>
        where TRequest : IDataStruct, new()
        where TResponse : IDataStruct, new()
    {
        private struct RequestMessage : IDataStruct
        {
            private long mMessageId;
            private TRequest mRequest;

            public RequestMessage(long messageId, TRequest request)
            {
                mMessageId = messageId;
                mRequest = request;
            }

            public long Id
            {
                get { return mMessageId; }
            }

            public TRequest Request
            {
                get { return mRequest; }
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mMessageId);
                if (!dst.isReader)
                {
                    mRequest.Serialize(dst);
                }
                else
                {
                    mRequest = new TRequest();
                    mRequest.Serialize(dst);
                }

                return true;
            }
        }

        private struct ResponseMessage : IDataStruct
        {
            private long mMessageId;
            private bool mIsOK;
            private string mErrorMessage;
            private TResponse mResponse;
            private float mProcessTime;

            public long Id
            {
                get { return mMessageId; }
            }

            public bool IsOK
            {
                get { return mIsOK; }
            }

            public string ErrorMessage
            {
                get { return mErrorMessage; }
            }

            public TResponse Response
            {
                get { return mResponse; }
            }

            public TimeSpan ProcessTime
            {
                get { return TimeSpan.FromMilliseconds(mProcessTime); }
            }

            public ResponseMessage(long messageId, string error, TimeSpan processTime)
            {
                mMessageId = messageId;
                mIsOK = false;
                mErrorMessage = error;
                mResponse = default(TResponse);
                mProcessTime = (float) processTime.TotalMilliseconds;
            }

            public ResponseMessage(long messageId, TResponse response, TimeSpan processTime)
            {
                mMessageId = messageId;
                // ReSharper disable once HeapView.BoxingAllocation
                if (response != null)
                {
                    mIsOK = true;
                    mErrorMessage = null;
                    mResponse = response;
                }
                else
                {
                    mIsOK = false;
                    mErrorMessage = "null response (server fault)";
                    mResponse = default(TResponse);
                }

                mProcessTime = (float) processTime.TotalMilliseconds;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mMessageId);
                dst.Add(ref mIsOK);
                dst.Add(ref mProcessTime);
                if (mIsOK)
                {
                    if (!dst.isReader)
                    {
                        mResponse.Serialize(dst);
                    }
                    else
                    {
                        mResponse = new TResponse();
                        mResponse.Serialize(dst);
                    }
                }
                else
                {
                    dst.Add(ref mErrorMessage);
                }

                return true;
            }
        }

        private class Request : IRequest<TRequest, TResponse>
        {
            private readonly RRDecl<TRequest, TResponse> mOwner;
            private readonly TRequest mData;
            private readonly DateTime mRequestTime;
            private readonly long mRequestId;

            public Request(RRDecl<TRequest, TResponse> owner, TRequest data, long requestId)
            {
                mOwner = owner;
                mData = data;
                mRequestTime = HighResDateTime.UtcNow;
                mRequestId = requestId;
            }

            TRequest IRequest<TRequest, TResponse>.Data
            {
                get { return mData; }
            }

            void IRequest<TRequest, TResponse>.Response(TResponse response)
            {
                mOwner.Send(new ResponseMessage(mRequestId, response, HighResDateTime.UtcNow - mRequestTime));
            }

            void IRequest<TRequest, TResponse>.Fail(string errorMessage)
            {
                mOwner.Send(new ResponseMessage(mRequestId, errorMessage, HighResDateTime.UtcNow - mRequestTime));
            }
        }

        private struct ActionPair
        {
            public readonly Action<IRequestSuccess<TResponse>> OnResponse;
            public readonly Action<IRequestFail> OnFail;
            public readonly DateTime RequestTime;

            public ActionPair(Action<IRequestSuccess<TResponse>> onResponse, Action<IRequestFail> onFail, DateTime requestTime)
            {
                OnResponse = onResponse;
                OnFail = onFail;
                RequestTime = requestTime;
            }
        }

        private class RequestInfo : IRequestSuccess<TResponse>, IRequestFail
        {
            private readonly Declaration mOwner;

            public DeltaTime NetworkTime { get; private set; }
            public DeltaTime ProcessTime { get; private set; }

            public TResponse Response { get; private set; }
            public string Error { get; private set; }

            public string Name
            {
                get { return mOwner.Name; }
            }

            public RequestInfo(Declaration owner)
            {
                mOwner = owner;
            }

            public void Setup(DeltaTime networkTime, DeltaTime processTime, TResponse response)
            {
                NetworkTime = networkTime;
                ProcessTime = processTime;
                Response = response;
                Error = "";
            }

            public void Setup(DeltaTime networkTime, DeltaTime processTime, string error)
            {
                NetworkTime = networkTime;
                ProcessTime = processTime;
                Response = default(TResponse);
                Error = error;
            }
        }

        private readonly Type[] mTypesToRegister;
        private readonly Dictionary<long, ActionPair> mPendingActions = new Dictionary<long, ActionPair>();

        private readonly RequestInfo mCurrentRequestInfo;

        private Action<IRequest<TRequest, TResponse>> mProcessor;
        private bool mStopped;

        private long mLastSentRequestId;

        public RRDecl(params Type[] typesToRegister)
        {
            mTypesToRegister = typesToRegister;
            mCurrentRequestInfo = new RequestInfo(this);
        }

        protected override void FillFactoryModels(HashSet<Type> types)
        {
            if (mTypesToRegister != null)
            {
                foreach (var type in mTypesToRegister)
                {
                    types.Add(type);
                }
            }
        }

        protected sealed override void FillNonFactoryModels(HashSet<Type> types)
        {
            types.Add(typeof(TRequest));
            types.Add(typeof(TResponse));
        }

        protected override void Prepare(bool isServerMode)
        {
            if (isServerMode != (mProcessor != null))
            {
                throw new InvalidOperationException("Wrong " + GetType() + " work mode on " + (isServerMode ? "server" : "client"));
            }
        }

        protected override bool OnReceived(IMemoryBufferHolder buffer)
        {
            throw new InvalidOperationException("RRDecl doesn't support raw data");
        }

        protected sealed override bool OnReceived(IBinarySerializer received)
        {
            var processor = mProcessor;
            if (processor != null)
            {
                // Работаем в режиме сервера
                RequestMessage request = new RequestMessage();
                request.Serialize(received);
                processor.Invoke(new Request(this, request.Request, request.Id));
            }
            else
            {
                if (!mStopped)
                {
                    //Работаем в режиме клиента
                    ResponseMessage rMessage = new ResponseMessage();

                    rMessage.Serialize(received);

                    ActionPair reaction;
                    bool hasReaction = false;

                    lock (mPendingActions)
                    {
                        if (mPendingActions.TryGetValue(rMessage.Id, out reaction))
                        {
                            mPendingActions.Remove(rMessage.Id);
                            hasReaction = true;
                        }
                    }

                    if (!hasReaction)
                    {
                        Log.w("Failed to find messageId={0} for RRDecl={1}", rMessage.Id, GetType());
                        return false;
                    }

                    DeltaTime totalTime = DeltaTime.FromSeconds((HighResDateTime.UtcNow - reaction.RequestTime).TotalSeconds);
                    DeltaTime processTime = DeltaTime.FromSeconds(rMessage.ProcessTime.TotalSeconds);

                    if (rMessage.IsOK)
                    {
                        mCurrentRequestInfo.Setup(totalTime - processTime, processTime, rMessage.Response);
                        reaction.OnResponse(mCurrentRequestInfo);
                    }
                    else
                    {
                        mCurrentRequestInfo.Setup(totalTime - processTime, processTime, rMessage.ErrorMessage != null ? rMessage.ErrorMessage : "");
                        reaction.OnFail(mCurrentRequestInfo);
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public override void Stop()
        {
            lock (mPendingActions)
            {
                mStopped = true;
                foreach (var eachPair in mPendingActions)
                {
                    var reaction = eachPair.Value;

                    DeltaTime totalTime = DeltaTime.FromSeconds((HighResDateTime.UtcNow - reaction.RequestTime).TotalSeconds);
                    mCurrentRequestInfo.Setup(totalTime, DeltaTime.Zero, "Declaration " + Name + " was stopped");
                    reaction.OnFail(mCurrentRequestInfo);
                }

                mPendingActions.Clear();
            }
        }

        void IRequester<TRequest, TResponse>.Request(TRequest request, Action<IRequestSuccess<TResponse>> onResponse, Action<IRequestFail> onFail)
        {
            lock (mPendingActions)
            {
                if (!mStopped)
                {
                    long messageId = System.Threading.Interlocked.Increment(ref mLastSentRequestId);
                    var sendResult = Send(new RequestMessage(messageId, request));
                    if (sendResult == SendResult.Ok)
                    {
                        mPendingActions.Add(messageId, new ActionPair(onResponse, onFail, HighResDateTime.UtcNow));
                    }
                    else
                    {
                        var requestInfo = CreateErrorRequestInfo("Send result at status " + sendResult);
                        onFail(requestInfo);
                    }
                }
                else
                {
                    var requestInfo = CreateErrorRequestInfo("Declaration " + Name + " is not ready or stopped");
                    onFail(requestInfo);
                }
            }
        }

        private RequestInfo CreateErrorRequestInfo(string error)
        {
            var requestInfo = new RequestInfo(this);
            requestInfo.Setup(DeltaTime.Zero, DeltaTime.Zero, error);
            return requestInfo;
        }

        void IResponder<TRequest, TResponse>.SetProcessor(Action<IRequest<TRequest, TResponse>> processor)
        {
            mProcessor = processor;
        }
    }

    namespace Client
    {
        public static class RRDecl
        {
            public static void Request<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl,
                TRequest request,
                Action<IRequestSuccess<TResponse>> response,
                Action<IRequestFail> onFail)
                where TRequest : IDataStruct, new()
                where TResponse : IDataStruct, new()
            {
                ((IRequester<TRequest, TResponse>) decl).Request(request, response, onFail);
            }
#if !UNITY_2017_1_OR_NEWER
            public static async Task<TResponse> RequestAsync<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, TRequest request, TimeSpan? timeout = null)
                where TRequest : class, IDataStruct, new()
                where TResponse : class, IDataStruct, new()
            {
                var requestTask = RequestWithInfoAsync(decl, request);

                using (var cts = new CancellationTokenSource())
                {
                    var finalTimeout = timeout ?? TimeSpan.FromSeconds(15);
                    var completedTask = await Task.WhenAny(requestTask, Task.Delay(finalTimeout, cts.Token));
                    if (completedTask == requestTask)
                    {
                        cts.Cancel();
                        return ((Task<SuccessResponseInfo<TResponse>>)completedTask).Result.Response;
                    }

                    throw new TimeoutException($"Request {request.GetType().Name} timeout ({finalTimeout.TotalSeconds.ToString()}s.)");
                }
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
#endif
        }
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

    namespace Server
    {
        public static class RRDecl
        {
            public static void SetProcessor<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, Action<IRequest<TRequest, TResponse>> processor)
                where TRequest : IDataStruct, new()
                where TResponse : IDataStruct, new()
            {
                ((IResponder<TRequest, TResponse>) decl).SetProcessor(processor);
            }

#if !UNITY_2017_1_OR_NEWER
            public static void SetAsyncProcessor<TRequest, TResponse>(this RRDecl<TRequest, TResponse> decl, Func<TRequest, Task<TResponse>> processor)
                where TRequest : class, IDataStruct, new()
                where TResponse : class, IDataStruct, new()
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

            public static void RegisterAsync<TMessage>(this C2SMessageDecl<TMessage> decl, Func<TMessage, Task> processor)
                where TMessage : IDataStruct, new()
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
#endif
        }
    }
}