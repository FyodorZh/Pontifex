using System;
using System.Net.Sockets;

namespace Pontifex.Transports.Tcp
{
    internal class TcpReceiver
    {
        private volatile bool mStopped;

        private readonly Socket mSocket;
        private readonly PacketCompositor mPacketCompositor = new PacketCompositor();

        private Action<Exception> mOnFailed;
        private Action mOnStopped;

        private readonly Action<Packet> mOnReceived;

        private volatile SocketAsyncEventArgs mAsyncArgs = new SocketAsyncEventArgs();

        private int mWasStopped;

        public TcpReceiver(Socket socket, Action<Packet> onReceived, Action<Exception> onFailed, Action onStopped)
        {
            if (onReceived == null)
            {
                throw new ArgumentNullException("onReceived");
            }
            if (onFailed == null)
            {
                throw new ArgumentNullException("onFailed");
            }

            byte[] buffer = new byte[1024 * 4];

            mAsyncArgs.Completed += ReadCallback;
            mAsyncArgs.SocketFlags = SocketFlags.None;
            mAsyncArgs.SetBuffer(buffer, 0, buffer.Length);

            mSocket = socket;

            mOnReceived = onReceived;
            mOnFailed = onFailed;
            mOnStopped = onStopped;
        }

        public void Start()
        {
            try
            {
                if (!mSocket.ReceiveAsync(mAsyncArgs))
                {
                    ReadCallback(mSocket, mAsyncArgs);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public void Stop()
        {
            mStopped = true;
        }

        private void OnStopped()
        {
            if (System.Threading.Interlocked.Exchange(ref mWasStopped, 1) == 0)
            {
                if (mAsyncArgs != null)
                {
                    mAsyncArgs.Dispose();
                    mAsyncArgs = null;
                }

                mPacketCompositor.Destroy();

                if (mOnStopped != null)
                {
                    mOnStopped();
                }
            }
        }

        private void ReadCallback(object sender, SocketAsyncEventArgs args)
        {
            bool isSyncWork = true;
            while (isSyncWork)
            {
                isSyncWork = false;
                try
                {
                    if (args.SocketError != SocketError.Success &&
                        args.SocketError != SocketError.WouldBlock)
                    {
                        throw new SocketException((int)args.SocketError);
                    }

                    int bytesRead = args.BytesTransferred;
                    if (bytesRead > 0)
                    {
                        mPacketCompositor.DecodePackets(args.Buffer, args.Offset, args.BytesTransferred, mOnReceived);

                        // show must go on
                        if (!mStopped)
                        {
                            isSyncWork = !mSocket.ReceiveAsync(mAsyncArgs);
                            sender = mSocket;
                            args = mAsyncArgs;
                        }
                        else
                        {
                            OnStopped();
                        }
                    }
                    else
                    {
                        // Client call Shutdown - No more data will be received
                        Stop();
                        OnStopped();
                    }
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
        }

        private void Fail(Exception ex)
        {
            Stop();
            var failedHandler = System.Threading.Interlocked.Exchange(ref mOnFailed, null);
            if (failedHandler != null)
            {
                failedHandler(ex);
            }
            OnStopped();
        }
    }
}
