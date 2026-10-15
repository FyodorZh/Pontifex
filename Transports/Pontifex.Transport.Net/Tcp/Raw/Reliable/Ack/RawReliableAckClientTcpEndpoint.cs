using System;
using System.Net.Sockets;
using Actuarius.Memory;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.Raw.Reliable.Ack.Tcp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    internal class RawReliableAckTcpEndpoint : RawReliableAckClientEndpoint
    {
        private readonly IpEndPoint _endPoint;
        
        private Socket? _socket;
        
        private TcpReceiver? _socketReceiver;
        private TcpSender? _socketSender;

        public RawReliableAckTcpEndpoint(
            string transportName,
            IpEndPoint remoteEndPoint, 
            IRawReliableAckClientHandler handler,
            IMemoryRental memory,
            ILogger log) 
            : base(
                transportName, 
                remoteEndPoint, 
                handler, 
                TcpInfo.DefaultMessageMaxSize, 
                TcpInfo.DefaultBufferCapacity,
                memory,
                log,
                new PingOptions(TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(30)))
        {
            _endPoint = remoteEndPoint;
        }
        
        protected override void BeginConnect()
        {
            _socket = new Socket(_endPoint.EP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _socket.ReceiveTimeout = (int)TcpInfo.DefaultDisconnectTimeout.TotalMilliseconds;
            _socket.SendTimeout = (int)TcpInfo.DefaultDisconnectTimeout.TotalMilliseconds;
            _socket.NoDelay = true;
            
            _socket.BeginConnect(_endPoint.EP, ar =>
            {
                try
                {
                    var socket = (Socket)ar.AsyncState;
                    socket.EndConnect(ar);

                    _socketReceiver = new TcpReceiver(socket, ProcessInboundMessage, OnFailed, null, 
                        MessageMaxByteSize, Memory, Log, this);
                    _socketReceiver.Start();

                    _socketSender = new TcpSender(socket, MessageMaxByteSize, _socket.SendBufferSize - 4, Memory, Log);
                    _socketSender.ErrorOccured += OnFailed;
                    _socketSender.Stopped += StopSocket;
                    ILogicDriver<INonPeriodicLogicDriverCtl> driver =
                        new SingleJobLogicDriver<INonPeriodicLogicDriverCtl>(new ThreadBasedNonPeriodicLogicMultiDriver(NowDateTimeProvider.Instance));
                    driver.Start(_socketSender);
                
                    FinishConnect(() =>
                    {
                        Stop(new TextFail(TransportName, "FinishConnect method fail"), false);
                    });
                }
                catch (Exception ex)
                {
                    Stop(new ExceptionFail(TransportName, ex, "ConnectCallback failed"), false);
                }
            }, _socket);
        }

        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            base.BeginDisconnect(reason, tryBeGentle);
            _socketReceiver?.Stop();
            _socketSender?.Stop();
        }

        private void OnFailed(Exception ex)
        {
            Stop(new ExceptionFail(TransportName, ex), false);
        }

        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            var res = _socketSender?.Send(bufferToSend) ?? SendResult.NotConnected;
            if (res != SendResult.Ok)
            {
                Stop(new TextFail(TransportName, $"Failed to send message with error: '{res}'"), false);
            }
        }

        private void StopSocket()
        {
            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
                _socket = null;
            }
            catch (Exception)
            {
                // ignored
            }

            Log.i("Stopped.");
        }
    }
}