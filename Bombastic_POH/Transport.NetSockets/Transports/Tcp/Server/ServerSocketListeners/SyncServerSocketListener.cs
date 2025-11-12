using System;
using System.Net.Sockets;
using System.Threading;

namespace Transport.Transports.Tcp
{
    internal class SyncServerSocketListener : IServerSocketListener
    {
        private readonly Socket mListener;
        private Thread mThread;

        public event Action<Socket> Connected;
        public event Action Stopped;
        public event Action<Exception> Failed;

        public SyncServerSocketListener(Socket listener)
        {
            mListener = listener;
        }

        public bool Start()
        {
            if (mThread == null)
            {
                mThread = new Thread(Worker);
                mThread.IsBackground = true;
                mThread.Start();
                return true;
            }
            return false;
        }

        public void Stop()
        {
            try
            {
                var thread = mThread;
                if (thread != null)
                {
                    mListener.Close();
                    thread.Abort();
                    mThread = null;
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
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void Fail(Exception ex)
        {
            var failed = Failed;
            if (failed != null)
            {
                try
                {
                    failed(ex);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void Worker()
        {
            try
            {
                while (true)
                {
                    var socket = mListener.Accept();
                    var connected = Connected;
                    if (connected != null)
                    {
                        try
                        {
                            connected(socket);
                        }
                        catch (Exception ex)
                        {
                            Fail(ex);
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                mListener.Close();
            }
            catch (Exception ex)
            {
                Fail(ex);
                Stop();
            }
            finally
            {
                mListener.Close();
                var stopped = Stopped;
                if (stopped != null)
                {
                    try
                    {
                        stopped();
                    }
                    catch (Exception ex)
                    {
                        Fail(ex);
                    }
                }
            }
        }
    }
}
