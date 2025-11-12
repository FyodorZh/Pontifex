using System;
using System.Threading;
using Serializer.BinarySerializer;

namespace Transport.Protocols.MessageProtocol
{
    public interface IConnectorUser
    {
        bool IsConnected { get; }
        bool SendData(IDataStruct data);
        void ReceiveRequest(IRequest request);
    }

    public interface IResponseSender
    {
        bool SendResponse(short command, IDataStruct data, long requestId);
    }

    public interface IWaitResponseStorage
    {
        void ExecuteResponseAction(Message message);
        void ExecuteTimeoutAction(long requestId);

        bool TryAdd(long requestId, WaitResponse response);
        bool TryRemove(long responseId);

        void Clear();
    }

    public class MessageConnector : IResponseSender
    {
        protected IWaitResponseStorage mWaitResponseStorage;
        protected IConnectorUser mConnectorUser;

        private long mResponseTimeoutMillis;
        private long mNextRequestId = 1;

        protected long NextRequestId()
        {
            return Interlocked.Increment(ref mNextRequestId);
        }

        public MessageConnector(IWaitResponseStorage waitResponseStorage, IConnectorUser user, long responseTimeoutMillis = 15000)
        {
            mWaitResponseStorage = waitResponseStorage;
            mConnectorUser = user;
            mResponseTimeoutMillis = responseTimeoutMillis;
        }

        public void SetConfig(long responseTimeoutMillis)
        {
            mResponseTimeoutMillis = Math.Max(mResponseTimeoutMillis, responseTimeoutMillis);
        }

        public bool SendData(short command, IDataStruct data, Action<Response> responseAction = null)
        {
            if (!mConnectorUser.IsConnected)
            {
                return false;
            }

            var requestId = null == responseAction ? 0L : NextRequestId();
            var type = null == responseAction ? Message.Type.Event : Message.Type.Request;
            var msg = new Message(requestId, type, command, data);
            if (null != responseAction)
            {
                var wait = new WaitResponse(requestId, command, mResponseTimeoutMillis, responseAction, OnTimeout);
                if (!mWaitResponseStorage.TryAdd(requestId, wait))
                {
                    Log.e("Unexpected error, request with id: {0} already exists, command: {1}", requestId, command);
                    return false;
                }
            }

            var res = mConnectorUser.SendData(msg);
            if (!res && null != responseAction)
            {
                mWaitResponseStorage.TryRemove(requestId);
            }
            return res;
        }

        public bool SendResponse(short command, IDataStruct data, long requestId)
        {
            if (!mConnectorUser.IsConnected)
            {
                return false;
            }

            var msg = new Message(requestId, Message.Type.Response, command, data);
            return mConnectorUser.SendData(msg);
        }

        public void OnReceiveData(IDataStruct msg)
        {
            var message = msg as Message;
            if (message == null)
            {
                return;
            }

            if (message.comandType == Message.Type.Response)
            {
                mWaitResponseStorage.ExecuteResponseAction(message);
            }
            else
            {
                mConnectorUser.ReceiveRequest(new Request(message, this));
            }
        }

        private void OnTimeout(long requestId)
        {
            mWaitResponseStorage.ExecuteTimeoutAction(requestId);
        }

        public void Clear()
        {
            mWaitResponseStorage.Clear();
        }
    }
}
