using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public class CloseConnection : IDataStruct
    {
        private byte _reason;
        public CloseConnectionReason reason
        {
            get { return (CloseConnectionReason) _reason; }
            set { _reason = (byte) value; }
        }

        public string desc;

        #region Implementation of IDataStruct

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _reason);
            dst.Add(ref desc);
            return true;
        }

        #endregion

        public static CloseConnection create(CloseConnectionReason reason, string desc)
        {
            return new CloseConnection {reason = reason, desc = desc};
        }
    }
}