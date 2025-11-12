using System;
using Serializer.BinarySerializer;
using Transport.Protocols.MessageProtocol;
using Transport.Serializer;
using Transport;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Handlers;

namespace Transport.Protocols
{
    public abstract class MessageProtocolAckCSP : BaseAckCSP<IDataStruct>, IConnectorUser
    {
        private readonly MessageConnector mMessageConnector;

        protected abstract void RequestHandler(IRequest request);

        protected MessageProtocolAckCSP(IAckRawClient client, byte[] ackData, IWaitResponseStorage waitResponseStorage, long responseTimeoutMs = 15000)
            : base(client, ackData, new DataStructTransportSerializer())
        {
            mMessageConnector = new MessageConnector(waitResponseStorage, this, responseTimeoutMs);
        }

        #region Implementation of IConnectorUser

        bool IConnectorUser.IsConnected
        {
            get { return IsConnected; }
        }

        bool IConnectorUser.SendData(IDataStruct data)
        {
            return SendData(data);
        }

        void IConnectorUser.ReceiveRequest(IRequest request)
        {
            RequestHandler(request);
        }

        #endregion

        #region Overrides of BaseAckCSP<IDataStruct>

        protected override void OnAcknowledged()
        {
            ProtocolState = CSPState.Active;
        }

        protected override void OnDisconnected(DisconnectReason reason)
        {
            Clear();
        }

        protected override void OnReceived(IDataStruct data)
        {
            mMessageConnector.OnReceiveData(data);
        }

        #endregion

        public bool SendData(short command, IDataStruct data, Action<Response> responseAction = null)
        {
            return mMessageConnector.SendData(command, data, responseAction);
        }

        public void Clear()
        {
            mMessageConnector.Clear();
        }
    }
}