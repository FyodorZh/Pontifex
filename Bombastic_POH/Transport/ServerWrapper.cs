using System;

namespace Transport
{
    interface IServerWrapper
    {
        ITServer FindFirstNested<ITServer>() where ITServer : class, IServer<IClientEndPointHandler>;
    }

    public abstract class ServerWrapper<ITHandler> : IServer<ITHandler>, IServerWrapper
        where ITHandler : class, IClientEndPointHandler
    {
        private readonly IServer<ITHandler> mCoreTransport;

        protected ServerWrapper(IServer<ITHandler> coreTransport)
        {
            mCoreTransport = coreTransport;
        }

        #region IServer2<ITHandler>

        public abstract string Type { get; }

        public virtual bool Start(IAcknowledger<ITHandler> acknowledger, Action onStopped)
        {
            return mCoreTransport.Start(acknowledger, onStopped);
        }

        public virtual bool Stop()
        {
            return mCoreTransport.Stop();
        }

        #endregion

        #region Implementation of IServerWrapper

        public ITServer FindFirstNested<ITServer>() where ITServer : class, IServer<IClientEndPointHandler>
        {
            var res = this as ITServer;
            if (null == res)
            {
                res = mCoreTransport as ITServer;
                if (null == res)
                {
                    var nested = mCoreTransport as ServerWrapper<ITHandler>;
                    if (null != nested)
                    {
                        return nested.FindFirstNested<ITServer>();
                    }
                }
            }

            return res;
        }

        #endregion
    }
}