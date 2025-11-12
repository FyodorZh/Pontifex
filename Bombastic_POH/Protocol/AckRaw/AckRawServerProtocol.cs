using System.Text;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers.Server;
using NewProtocol.Server;
using Shared;
using Transport;
using Transport.StopReasons;

namespace NewProtocol
{
    public abstract class AckRawServerProtocol : AnySideProtocol<IAckRawClientEndpoint>, IAckRawServerHandler
    {
        protected AckRawServerProtocol(IDateTimeProvider timeProvider)
            : base(timeProvider)
        {
        }

        protected override void BindToProtocol(Protocol protocol)
        {
            protocol.Disconnect.Register((msg) =>
            {
                Stop(new GracefulRemoteIntention("server-protocol"));
            });
            base.BindToProtocol(protocol);
        }

        protected sealed override bool TryStart()
        {
            // DO NOTHING
            return true;
        }

        protected abstract byte[] GetAckResponse();

        protected abstract void OnConnected();

        protected override void OnDisconnected(StopReason reason)
        {
            Log.i("ServerProtocol.Disconnected: {@reason}", reason.Print());
        }

        protected override void OnStopped(StopReason reason)
        {
            // DO NOTHING
        }

        public override bool IsServerMode
        {
            get { return true; }
        }

        byte[] IAckRawServerHandler.GetAckResponse()
        {
            byte[] hash = Encoding.UTF8.GetBytes(ProtocolHash);
            byte[] responseWithHash = Transport.AckUtils.AppendPrefix(GetAckResponse(), hash);
            return responseWithHash;
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            OnConnected(endPoint);
            OnConnected();
        }
    }
}
