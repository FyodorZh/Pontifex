using System;
using System.Collections.Generic;
using Archivarius;
using Scriba;


namespace Pontifex.Api
{

    public interface IRequestInfo
    {
        string Name { get; }
        TimeSpan NetworkTime { get; }
        TimeSpan ProcessTime { get; }
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
        where TRequest : struct, IDataStruct
        where TResponse : struct, IDataStruct
    {
        private readonly Dictionary<long, ActionPair> _pendingRequests = new Dictionary<long, ActionPair>();

        private readonly RequestInfo _currentRequestInfo_Reusable;

        private Action<IRequest<TRequest, TResponse>>? _processor;
        private bool _stopped;

        private long _lastSentRequestId;
        
        private IUnidirectionalModelPipeIn<ResponseMessage<TResponse>>? _responseSender;
        private IUnidirectionalModelPipeIn<RequestMessage<TRequest>>? _requestSender;
        private IUnidirectionalModelPipeOut<RequestMessage<TRequest>>? _requestReceiver;
        private IUnidirectionalModelPipeOut<ResponseMessage<TResponse>>? _responseReceiver;

        public RRDecl()
        {
            _currentRequestInfo_Reusable = new RequestInfo(this);
        }


        protected override void Start(bool isServerMode, IPipeSystem pipeSystem)
        {
            if (isServerMode != (_processor != null))
            {
                throw new InvalidOperationException("Wrong " + GetType() + " work mode on " + (isServerMode ? "server" : "client"));
            }

            if (isServerMode)
            {
                // order is important
                _responseSender = pipeSystem.AllocateModelPipeIn<ResponseMessage<TResponse>>();
                _requestReceiver = pipeSystem.AllocateModelPipeOut<RequestMessage<TRequest>>();
                _requestReceiver.SetReceiver(Receiver);
            }
            else
            {
                // order is important
                _responseReceiver = pipeSystem.AllocateModelPipeOut<ResponseMessage<TResponse>>();
                _requestSender = pipeSystem.AllocateModelPipeIn<RequestMessage<TRequest>>();
                _responseReceiver.SetReceiver(Receiver);
            }
        }
        
        private void Receiver(RequestMessage<TRequest> requestMessage)
        {
            //Working in server mode
            if (!_stopped)
            {
                var processor = _processor;
                if (processor != null)
                {
                    processor.Invoke(new Request(this, requestMessage.Request, requestMessage.Id));
                }
            }
        }

        private void Receiver(ResponseMessage<TResponse> responseMessage)
        {
            //Working in client mode
            if (!_stopped)
            {
                ActionPair reaction;
                bool hasReaction = false;

                lock (_pendingRequests)
                {
                    if (_pendingRequests.TryGetValue(responseMessage.Id, out reaction))
                    {
                        _pendingRequests.Remove(responseMessage.Id);
                        hasReaction = true;
                    }
                }

                if (!hasReaction)
                {
                    Log.w("Failed to find messageId={0} for RRDecl={1}", responseMessage.Id, GetType());
                    return;
                }

                TimeSpan totalTime = TimeSpan.FromSeconds((DateTime.UtcNow - reaction.RequestTime).TotalSeconds);
                TimeSpan processTime = TimeSpan.FromSeconds(responseMessage.ProcessTime.TotalSeconds);

                if (responseMessage.IsOK)
                {
                    _currentRequestInfo_Reusable.Setup(totalTime - processTime, processTime, responseMessage.Response);
                    reaction.OnResponse(_currentRequestInfo_Reusable);
                }
                else
                {
                    _currentRequestInfo_Reusable.Setup(totalTime - processTime, processTime, responseMessage.ErrorMessage);
                    reaction.OnFail(_currentRequestInfo_Reusable);
                }
            }
        }

        public override void Stop()
        {
            lock (_pendingRequests)
            {
                _stopped = true;
                foreach (var eachPair in _pendingRequests)
                {
                    var reaction = eachPair.Value;

                    TimeSpan totalTime = TimeSpan.FromSeconds((DateTime.UtcNow - reaction.RequestTime).TotalSeconds);
                    _currentRequestInfo_Reusable.Setup(totalTime, TimeSpan.Zero, "Declaration " + Name + " was stopped");
                    reaction.OnFail(_currentRequestInfo_Reusable);
                }

                _pendingRequests.Clear();
            }
        }

        SendResult IRequester<TRequest, TResponse>.Request(TRequest request, Action<IRequestSuccess<TResponse>> onResponse, Action<IRequestFail> onFail)
        {
            lock (_pendingRequests)
            {
                if (_stopped)
                {
                    var requestInfo = CreateErrorRequestInfo($"Declaration '{Name}' is not ready or stopped");
                    onFail(requestInfo);
                    return SendResult.NotConnected;
                }
                
                long messageId = System.Threading.Interlocked.Increment(ref _lastSentRequestId);
                _pendingRequests.Add(messageId, new ActionPair(onResponse, onFail, DateTime.UtcNow));
                var sendResult = _requestSender?.Send(new RequestMessage<TRequest>(messageId, request)) ?? SendResult.Error;
                if (sendResult != SendResult.Ok)
                {
                    _pendingRequests.Remove(messageId);
                    var requestInfo = CreateErrorRequestInfo("Send result at status " + sendResult);
                    onFail(requestInfo);
                }

                return sendResult;
            }
        }
        
        void IResponder<TRequest, TResponse>.SetProcessor(Action<IRequest<TRequest, TResponse>> processor)
        {
            _processor = processor;
        }

        private SendResult Response(ResponseMessage<TResponse> responseMessage)
        {
            return _responseSender?.Send(responseMessage) ?? SendResult.Error;
        }

        private RequestInfo CreateErrorRequestInfo(string error)
        {
            var requestInfo = new RequestInfo(this);
            requestInfo.Setup(TimeSpan.Zero, TimeSpan.Zero, error);
            return requestInfo;
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
                mRequestTime = DateTime.UtcNow;
                mRequestId = requestId;
            }

            TRequest IRequest<TRequest, TResponse>.Data => mData;

            SendResult IRequest<TRequest, TResponse>.Response(TResponse response)
            {
                return mOwner.Response(new ResponseMessage<TResponse>(mRequestId, response, DateTime.UtcNow - mRequestTime));
            }

            SendResult IRequest<TRequest, TResponse>.Fail(string errorMessage)
            {
                return mOwner.Response(new ResponseMessage<TResponse>(mRequestId, errorMessage, DateTime.UtcNow - mRequestTime));
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

            public TimeSpan NetworkTime { get; private set; }
            public TimeSpan ProcessTime { get; private set; }

            public TResponse Response => _response ?? throw new InvalidOperationException("Response is not available");
            public string Error => _error ?? throw new InvalidOperationException("Error is not available");

            public string Name => _owner.Name;

            public RequestInfo(Declaration owner)
            {
                _owner = owner;
            }

            public void Setup(TimeSpan networkTime, TimeSpan processTime, TResponse response)
            {
                NetworkTime = networkTime;
                ProcessTime = processTime;
                _response = response;
                _error = null;
            }

            public void Setup(TimeSpan networkTime, TimeSpan processTime, string error)
            {
                NetworkTime = networkTime;
                ProcessTime = processTime;
                _response = null;
                _error = error;
            }
        }
    }
}