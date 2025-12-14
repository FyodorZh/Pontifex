using System;
using System.Net.Sockets;

namespace Pontifex.Transports.Tcp
{
    class AsyncServerSocketListener : IServerSocketListener
    {
        private enum State
        {
            Constructed,
            Started,
            Stopped
        }

        private readonly Socket mListener;

        private SocketAsyncEventArgs mAcceptEventArg;

        private readonly object mLocker = new object();
        private State mState = State.Constructed;

        public event Action<Socket> Connected;
        public event Action Stopped;
        public event Action<Exception> Failed;

        public AsyncServerSocketListener(Socket listener)
        {
            mListener = listener;
        }

        public bool Start()
        {
            lock (mLocker)
            {
                if (mState != State.Constructed)
                {
                    return false;
                }
                mState = State.Started;
            }

            mAcceptEventArg = new SocketAsyncEventArgs();
            mAcceptEventArg.Completed += (sender, eventArgs) => ProcessAccept();

            return StartAccept();
        }

        public void Stop()
        {
            lock (mLocker)
            {
                if (mState != State.Started)
                {
                    return;
                }
                mState = State.Stopped;
            }

            Connected = null;
            if (Stopped != null)
            {
                try
                {
                    Stopped();
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
                Stopped = null;
            }
            Failed = null;

            mAcceptEventArg.Dispose();

            try
            {
                mListener.Close();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void Fail(Exception ex)
        {
            if (Failed != null)
            {
                try
                {
                    Failed(ex);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private bool StartAccept()
        {
            mAcceptEventArg.AcceptSocket = null;

            try
            {
                bool willRaiseEvent = mListener.AcceptAsync(mAcceptEventArg);
                if (!willRaiseEvent)
                {
                    ProcessAccept();
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
                Stop();
                return false;
            }
            return true;
        }

        private void ProcessAccept()
        {
            var connected = Connected;
            if (connected != null)
            {
                try
                {
                    connected(mAcceptEventArg.AcceptSocket);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }

            StartAccept();
        }
    }
}
