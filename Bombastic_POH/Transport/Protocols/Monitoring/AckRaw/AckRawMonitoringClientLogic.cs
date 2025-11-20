using System;
using Actuarius.Collections;
using Shared;
using Transport.Abstractions;
using Transport.Abstractions.Controls;
using Transport.Transports.ProtocolWrapper.AckRaw;
using Shared.Buffer;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Monitoring.AckRaw
{
    internal class AckRawMonitoringClientLogic : IAckRawWrapperClientLogic, IPingCollector
    {
        private readonly Utils.TrafficCollector mTrafficCollector = new Utils.TrafficCollector(UtcNowDateTimeProvider.Instance, MonitoringInfo.TransportName);

        private readonly CycleQueue<double> mPings = new CycleQueue<double>();
        private bool mCollectPing = true;

        private readonly IControlProvider mControlProvider;

        public AckRawMonitoringClientLogic()
        {
            mControlProvider = new Utils.CombinedControlProvider(new Utils.SingleControlProvider(this), new Utils.SingleControlProvider(mTrafficCollector));
        }

        string IControl.Name
        {
            get { return MonitoringInfo.TransportName; }
        }

        IControlProvider IAckRawWrapperClientLogic.Controls
        {
            get { return mControlProvider; }
        }

        void IAckRawWrapperClientLogic.OnConnected()
        {
        }

        void IAckRawWrapperClientLogic.OnDisconnected()
        {
        }

        void IAckRawWrapperClientLogic.UpdateAckData(UnionDataList ackData)
        {
            ackData.PutFirst("AckRawMonitoring");
        }

        bool IAckRawWrapperClientLogic.ProcessReceivedData(IMemoryBuffer receivedData)
        {
            int size = receivedData.Count;
            mTrafficCollector.IncInTraffic(size);

            byte flag;
            if (!receivedData.PopFirst().AsByte(out flag) || flag != 0 && flag != 1)
            {
                return false;
            }

            if (flag == 1)
            {
                long tick;
                if (!receivedData.PopFirst().AsInt64(out tick))
                {
                    return false;
                }

                TimeSpan ts = HighResDateTime.UtcNow - DateTime.FromBinary(tick);
                if (ts.Milliseconds > 0)
                {
                    lock (mPings)
                    {
                        if (mCollectPing)
                        {
                            mPings.Put(ts.Milliseconds);
                            if (mPings.Count > 10)
                            {
                                double tmp;
                                mPings.TryPop(out tmp);
                            }
                        }
                    }
                }
            }

            return true;
        }

        bool IAckRawWrapperClientLogic.ProcessSentData(IMemoryBuffer sentData)
        {
            bool collectPing = mCollectPing;
            if (collectPing)
            {
                sentData.PushInt64(HighResDateTime.UtcNow.ToBinary());
            }
            sentData.PushByte((byte)(collectPing ? 1 : 0));

            mTrafficCollector.IncOutTraffic(sentData.Size);

            return true;
        }

        bool IPingCollector.CollectPing
        {
            get
            {
                return mCollectPing;
            }
            set
            {
                mCollectPing = value;
                if (!value)
                {
                    lock (mPings)
                    {
                        mPings.Clear();
                    }
                }
            }
        }

        bool IPingCollector.GetPing(out int minPing, out int maxPing, out int avgPing)
        {
            double max = 0;
            double min = 1e6;
            double avg = 0;
            int count;

            lock (mPings)
            {
                count = mPings.Count;
                foreach (var p in mPings.Enumerate())
                {
                    max = Math.Max(max, p);
                    min = Math.Min(min, p);
                    avg += p;
                }

                if (count > 0)
                {
                    avg /= count;
                }
            }

            minPing = (int)min;
            maxPing = (int)max;
            avgPing = (int)avg;

            return count > 0;
        }
    }
}
