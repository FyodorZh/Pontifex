using System;
using System.Threading;
using Shared;
using Transport.Transports.ProtocolWrapper.AckRaw;
using Shared.Buffer;

namespace Transport.Protocols.Monitoring.AckRaw
{
    internal class AckRawMonitoringServerLogic : IAckRawWrapperServerLogic
    {
        private class PingInfo
        {
            private readonly long mClientTime;
            private readonly DateTime mConstructTime;

            public PingInfo(long clientTime)
            {
                mClientTime = clientTime;
                mConstructTime = HighResDateTime.UtcNow;
            }

            public long ClientTime
            {
                get
                {
                    var dt = HighResDateTime.UtcNow - mConstructTime;
                    return (DateTime.FromBinary(mClientTime) + dt).ToBinary();
                }
            }
        }

        private PingInfo mTick;
        
        ByteArraySegment IAckRawWrapperServerLogic.ProcessAckData(ByteArraySegment data)
        {
            return AckUtils.CheckPrefix(data, "AckRawMonitoring");
        }

        void IAckRawWrapperServerLogic.OnConnected()
        {
            // TODO
        }

        void IAckRawWrapperServerLogic.OnDisconnected()
        {
            // TODO
        }

        bool IAckRawWrapperServerLogic.ProcessReceivedData(IMemoryBuffer receivedData)
        {
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
                Interlocked.CompareExchange(ref mTick, new PingInfo(tick), null);
            }

            return true;
        }
        
        bool IAckRawWrapperServerLogic.ProcessSentData(IMemoryBuffer sentData)
        {
            PingInfo tick = Interlocked.Exchange(ref mTick, null);

            if (tick != null)
            {
                sentData.PushInt64(tick.ClientTime);
            }
            sentData.PushByte((byte)((tick != null) ? 1 : 0));
            
            return true;
        }
    }
}
