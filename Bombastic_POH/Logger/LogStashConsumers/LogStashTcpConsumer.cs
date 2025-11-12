using System.Net;
using System.Threading;
using LogConsumers.Tcp;
using Shared.Utils;

namespace LogConsumers
{
    public class LogStashTcpConsumer : Log.ILogConsumer
    {
        private readonly IPEndPoint mEndpoint;
        private readonly IPeriodicLogicRunner mRunner;

        private int mRefCount = 1;

        private TcpEndpoint mLoggerEp;
        private bool mPaused;

        private readonly BytesBuffer mAccumulator = new BytesBuffer(new LogStashLogFormatter());

        public bool Paused
        {
            get { return mPaused; }
            set
            {
                mPaused = value;
                var ep = mLoggerEp;
                if (ep != null)
                {
                    ep.Paused = mPaused;
                }
            }
        }

        public LogStashTcpConsumer(string address, int port, IPeriodicLogicRunner logicRunner)
        {
            var ip = IPAddress.Parse(address);
            mEndpoint = new IPEndPoint(ip, port);
            mRunner = logicRunner;

            Reconnect(null, false);
        }

        void Log.ILogConsumer.Message(Log.MessageData logMessage)
        {
            mAccumulator.Write(logMessage);
        }

        void Log.ILogConsumer.AddRef()
        {
            Interlocked.Increment(ref mRefCount);
        }

        void Log.ILogConsumer.Release()
        {
            if (Interlocked.Decrement(ref mRefCount) == 0)
            {
                var ep = mLoggerEp;
                if (ep != null)
                {
                    ep.Stop();
                }
                mLoggerEp = null;
            }
        }

        private void OnDisconnected()
        {
            mLoggerEp = null;
            if (mRefCount > 0)
            {
                var wrapper = new RegisteredWaitHandleWrapper
                {
                    Event = new AutoResetEvent(false)
                };

                wrapper.Handle = ThreadPool.RegisterWaitForSingleObject(wrapper.Event, Reconnect, wrapper, 5000, true);
            }
        }

        private void Reconnect(object param, bool isTimeout)
        {
            var wrapper = param as RegisteredWaitHandleWrapper;
            if (wrapper != null)
            {
                if (wrapper.Event != null)
                {
                    wrapper.Event.Close();
                    wrapper.Event = null;
                }

                if (wrapper.Handle != null)
                {
                    wrapper.Handle.Unregister(null);
                    wrapper.Handle = null;
                }
            }

            if (mRefCount > 0)
            {
                mLoggerEp = new TcpEndpoint(mAccumulator, mEndpoint);
                mLoggerEp.Paused = mPaused;
                mLoggerEp.Disconnected += OnDisconnected;
                mRunner.Run(mLoggerEp, Shared.DeltaTime.FromMiliseconds(100));
            }
        }

        class RegisteredWaitHandleWrapper
        {
            public RegisteredWaitHandle Handle;
            public AutoResetEvent Event;
        }
    }
}
