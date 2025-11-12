using System;
using Serializer.BinarySerializer;

namespace Transport.Protocols.MessageProtocol
{
    public interface IRequest
    {
        short Command { get; }
        IDataStruct Data { get; }
        bool SendResponse(IDataStruct responseData);
    }

    internal class Request : IRequest
    {
        private readonly IMessage mMessage;
        private readonly IResponseSender mConnector;

        public Request(IMessage message, IResponseSender connector)
        {
            mMessage = message;
            mConnector = connector;
        }

        public short Command { get { return mMessage.command; } }
        public IDataStruct Data { get { return mMessage.data; } }

        bool IRequest.SendResponse(IDataStruct responseData)
        {
            if (mMessage.comandType != Message.Type.Request)
            {
                throw new InvalidOperationException();
            }
            return mConnector.SendResponse(mMessage.command, responseData, mMessage.requestId);
        }
    }
}
