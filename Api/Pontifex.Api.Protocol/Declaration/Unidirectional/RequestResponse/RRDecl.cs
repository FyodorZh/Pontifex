using System;
using System.Collections.Generic;
using Archivarius;
using Operarius;
using Pontifex.Api.Protocol;
using Pontifex.Utils;
using Scriba;


namespace Pontifex.UserApi
{

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

    public class RRDecl<TRequest, TResponse> : Declaration, IRequester<TRequest, TResponse>, IResponder<TRequest, TResponse>
        where TRequest : class, IDataStruct, new()
        where TResponse : class, IDataStruct, new()
    {
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
                mOwner.Send(new ResponseMessage<TResponse>(mRequestId, response, HighResDateTime.UtcNow - mRequestTime));
            }

            void IRequest<TRequest, TResponse>.Fail(string errorMessage)
            {
                mOwner.Send(new ResponseMessage<TResponse>(mRequestId, errorMessage, HighResDateTime.UtcNow - mRequestTime));
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
            private readonly Declaration _owner;
            private TResponse? _response;
            private string? _error;

            public DeltaTime NetworkTime { get; private set; }
            public DeltaTime ProcessTime { get; private set; }

            public TResponse Response => _response ?? throw new InvalidOperationException("Response is not available");
            public string Error => _error ?? throw new InvalidOperationException("Error is not available");

            public string Name => _owner.Name;

            public RequestInfo(Declaration owner)
            {
                _owner = owner;
            }

            public void Setup(DeltaTime networkTime, DeltaTime processTime, TResponse response)
            {
                NetworkTime = networkTime;
                ProcessTime = processTime;
                _response = response;
                _error = null;
            }

            public void Setup(DeltaTime networkTime, DeltaTime processTime, string error)
            {
                NetworkTime = networkTime;
                ProcessTime = processTime;
                _response = null;
                _error = error;
            }
        }

        private readonly Type[] mTypesToRegister;
        private readonly Dictionary<long, ActionPair> mPendingActions = new Dictionary<long, ActionPair>();

        private readonly RequestInfo mCurrentRequestInfo;

        private Action<IRequest<TRequest, TResponse>>? _processor;
        private bool mStopped;

        private long mLastSentRequestId;

        public RRDecl(params Type[] typesToRegister)
        {
            mTypesToRegister = typesToRegister;
            mCurrentRequestInfo = new RequestInfo(this);
        }

        protected override void FillFactoryModels(HashSet<Type> types)
        {
            foreach (var type in mTypesToRegister)
            {
                types.Add(type);
            }
        }

        protected sealed override void FillNonFactoryModels(HashSet<Type> types)
        {
            types.Add(typeof(TRequest));
            types.Add(typeof(TResponse));
        }

        protected override void Prepare(bool isServerMode)
        {
            if (isServerMode != (_processor != null))
            {
                throw new InvalidOperationException("Wrong " + GetType() + " work mode on " + (isServerMode ? "server" : "client"));
            }
        }

        protected override bool OnReceived(UnionDataList buffer)
        {
            throw new InvalidOperationException("RRDecl doesn't support raw data");
        }

        protected sealed override bool OnReceived(ISerializer received)
        {
            var processor = _processor;
            if (processor != null)
            {
                // Работаем в режиме сервера
                var request = new RequestMessage<TRequest>();
                request.Serialize(received);
                processor.Invoke(new Request(this, request.Request, request.Id));
            }
            else
            {
                if (!mStopped)
                {
                    //Работаем в режиме клиента
                    var rMessage = new ResponseMessage<TResponse>();

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
                        mCurrentRequestInfo.Setup(totalTime - processTime, processTime, rMessage.Response ?? throw new InvalidOperationException("Response is null"));
                        reaction.OnResponse(mCurrentRequestInfo);
                    }
                    else
                    {
                        mCurrentRequestInfo.Setup(totalTime - processTime, processTime, rMessage.ErrorMessage ?? "null");
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
                    var sendResult = Send(new RequestMessage<TRequest>(messageId, request));
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
            _processor = processor;
        }
    }
}