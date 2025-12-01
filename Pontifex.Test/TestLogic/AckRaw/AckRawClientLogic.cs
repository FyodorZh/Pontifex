using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;
using Transport;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers.Client;

namespace TransportAnalyzer.TestLogic
{
    class AckRawClientLogic : AckRawCommonLogic, IAckRawClientHandler
    {
        private volatile IAckRawServerEndpoint? _endpoint;

        private long _sendId = 0;
        private long _receiveId = 0;

        private readonly int _unconfirmedTicks;
        private readonly long _lastTickId;

        public override string ToString()
        {
            return $"MessageId={Interlocked.Read(ref _receiveId)}";
        }

        public AckRawClientLogic(int unconfirmedTicks = 1, long lastTickId = -1)
        {
            _unconfirmedTicks = unconfirmedTicks;
            _lastTickId = lastTickId;
        }

        public void WriteAckData(UnionDataList ackData)
        {
            ackData.PutFirst(new UnionData(AckRequest));
        }

        public void OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            if (!ackResponse.PopFirstAsArray(out var response) || !AckResponse.EqualByContent(response))
            {
                endPoint.Disconnect(new Transport.StopReasons.TextFail("stress-test", "Wrong ack response"));
                return;
            }

            _endpoint = endPoint;
            var thread = new Thread(Work) { IsBackground = true };
            thread.Start();
        }

        public void OnDisconnected(StopReason reason)
        {
            _endpoint = null;
        }

        public void OnStopped(StopReason reason)
        {
            Log.i("TEST STOPPED");
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                var id = Interlocked.Increment(ref _receiveId);
                if (!receivedBuffer.PopFirstAsArray(out var buffer) || !CheckBuffer(id, buffer) || id == _lastTickId)
                {
                    Log.e("Message check failed #" + id);
                    var endpoint = _endpoint;
                    if (endpoint != null)
                    {
                        endpoint.Disconnect(new Transport.StopReasons.UserFail("Message check failed #" + id));
                    }
                }
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        private void Work()
        {
            while (_endpoint != null)
            {
                var endpoint = _endpoint;

                if (_sendId - _receiveId < _unconfirmedTicks)
                {
                    var buffer = GenBuffer(Interlocked.Increment(ref _sendId));
                    var dataToSend = Memory.CollectablePool.Acquire<UnionDataList>();
                    dataToSend.PutFirst(new UnionData(buffer));
                    Log.i("SendToServer");
                    endpoint.Send(dataToSend);
                }
                Thread.Sleep(50);
            }
            Log.i("StopWork");
        }
    }
}
