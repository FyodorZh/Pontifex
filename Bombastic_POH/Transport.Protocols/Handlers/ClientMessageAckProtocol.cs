using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Shared.HandlerProtocol;
using Transport.Protocols.MessageProtocol;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Handlers;

namespace Transport.Protocols.Handlers
{
    public class ClientMessageAckProtocol : MessageProtocolAckCSP
    {
        private readonly Dictionary<Type, IFireAndForgetClientHandler> mFireAndForgetClientHandlers;
        private readonly Dictionary<Type, IClientHandler> mHandlers;
        private readonly Dictionary<Type, IClientHandlerWithResult> mHandlersWithResult;

        public ClientMessageAckProtocol(
            IAckRawClient client,
            byte[] ackData,
            IEnumerable<IClientHandler> clientHandlers,
            IEnumerable<IClientHandlerWithResult> clientHandlerWithResults,
            IEnumerable<IFireAndForgetClientHandler> fireAndForgetClientHandlers,
            IWaitResponseStorage waitResponseStorage)
            : base(client, ackData, waitResponseStorage)
        {
            mFireAndForgetClientHandlers = fireAndForgetClientHandlers.ToDictionary(h => h.HandleType, h => h);
            mHandlers = clientHandlers.ToDictionary(h => h.HandleType, h => h);
            mHandlersWithResult = clientHandlerWithResults.ToDictionary(h => h.HandleType, h => h);
        }

        #region Overrides of MessageProtocolAckCSP

        protected override void RequestHandler(IRequest request)
        {
            try
            {
                IClientHandler handler;
                IClientHandlerWithResult handlerWithResult;
                IFireAndForgetClientHandler fireAndForgetClientHandler;

                if (request.Data == null)
                {
                    throw new Exception("Request data is null - Handler cannot be determined: RequestCommand = " + request.Command);
                }

                var requestDataType = request.Data.GetType();

                if (mHandlers.TryGetValue(requestDataType, out handler))
                {
                    handler.Handle(request.Data, () =>
                    {
                        request.SendResponse(HandlerResponse.Success());
                    });
                }
                else if (mHandlersWithResult.TryGetValue(requestDataType, out handlerWithResult))
                {
                    handlerWithResult.Handle(request.Data, result =>
                    {
                        request.SendResponse(HandlerResponse.Success(result));
                    });
                }
                else if (mFireAndForgetClientHandlers.TryGetValue(requestDataType, out fireAndForgetClientHandler))
                {
                    fireAndForgetClientHandler.Handle(request.Data);
                }
                else
                {
                    Log.e(string.Format("Command {0} wasnt handled", request.Command));
                    Debugger.Break();
                }
            }
            catch (Exception e)
            {
                try
                {
                    Log.wtf(e);
                    request.SendResponse(HandlerResponse.Error());
                }
                catch (Exception)
                {

                }

                Debugger.Break();
            }
        }

        #endregion
    }
}