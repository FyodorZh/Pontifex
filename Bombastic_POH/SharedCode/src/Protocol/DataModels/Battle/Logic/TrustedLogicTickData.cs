using Serializer.BinarySerializer;
using Serializer.Extensions;
using Serializer.Extensions.ID;
using Shared.Battle;

namespace Shared.Protocol
{
    public class TrustedLogicTickData : IDataStruct
    {
        private byte mProtocolVersion = 0;

        private ID<LogicTick> mTickId;            // ProtoVersion >= 0
        private Time mServerTime;                 // ProtoVersion >= 0
        private Time mGameTime;                 // ProtoVersion >= 0
        private ILogicMessage[] mMessages;        // ProtoVersion >= 0
        private System.DateTime mSerializingTime; // ProtoVersion >= 0

        public TrustedLogicTickData()
        {
        }

        public TrustedLogicTickData(ID<LogicTick> tickId, Time serverTime, Time gameTime, ILogicMessage[] messages)
        {
            mTickId = tickId;
            mServerTime = serverTime;
            mGameTime = gameTime;
            mMessages = messages;
        }

        public bool Serialize(IBinarySerializer saver)
        {
            saver.Add(ref mProtocolVersion);

            saver.AddId(ref mTickId);
            saver.AddTime(ref mServerTime);
            saver.AddTime(ref mGameTime);
            saver.Add(ref mMessages);
            if (saver.isReader)
            {
                long ticks = 0;
                saver.Add(ref ticks);
                mSerializingTime = new System.DateTime(ticks);
            }
            else
            {
                mSerializingTime = HighResDateTime.UtcNow;
                long ticks = mSerializingTime.Ticks;
                saver.Add(ref ticks);
            }

            return true;
        }

        public System.DateTime ReceiveTime { get; set; }

        public ID<LogicTick> TickId
        {
            get { return mTickId; }
        }

        public Time ServerTime
        {
            get { return mServerTime; }
        }

        public Time GameTime
        {
            get { return mGameTime; }
        }

        public ILogicMessage[] Messages
        {
            get { return mMessages; }
        }

        public System.DateTime SerializingTime
        {
            get { return mSerializingTime; }
        }
    }
}
