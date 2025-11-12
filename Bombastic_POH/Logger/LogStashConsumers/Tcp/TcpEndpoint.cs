using Shared.Utils;
using System.Net;
using System.Net.Sockets;

namespace LogConsumers
{
    internal class TcpEndpoint : IPeriodicLogic
    {
        private readonly BytesBuffer mDataSource;
        private readonly IPEndPoint mEndpoint;

        private Socket mSocket;

        private ILogicDriverCtl mDriver;

        private volatile bool mPaused;

        public bool Paused
        {
            get { return mPaused; }
            set { mPaused = value; }
        }

        public event System.Action Disconnected;

        public TcpEndpoint(BytesBuffer dataSource, IPEndPoint endPoint)
        {
            mDataSource = dataSource;
            mEndpoint = endPoint;
        }

        public void Stop()
        {
            var driver = mDriver;
            if (driver != null)
            {
                driver.Stop();
            }
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            try
            {
                mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                mSocket.Connect(mEndpoint);

                mDriver = driver;
                return true;
            }
            catch (System.Exception ex)
            {
                driver.Log.wtf(ex);
                return false;
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
            if (Disconnected != null)
            {
                Disconnected();
            }
            try
            {
                mDriver = null;
                if (mSocket != null)
                {
                    mSocket.Close();
                    mSocket = null;
                }
            }
            catch { }
        }

        void IPeriodicLogic.LogicTick()
        {
            if (!mPaused)
            {
                foreach (var buffer in mDataSource)
                {
                    try
                    {
                        mSocket.Send(buffer.Array, buffer.Count, SocketFlags.None);
                    }
                    catch
                    {
                        var driver = mDriver;
                        if (driver != null)
                        {
                            driver.Stop();
                        }
                    }
                }
            }
        }
    }
}
