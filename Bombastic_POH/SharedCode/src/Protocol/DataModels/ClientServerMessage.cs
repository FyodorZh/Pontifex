using System.Linq;

namespace Shared.Protocol
{
    public struct ClientServerMessage
    {
        public enum Type : byte
        {
            Unknown = 0,
            DelayDisconnect,
            DataMessage
        }

        private byte mType;
        private byte[] mPayload;
        private byte[] mData;

        public Type MessageType
        {
            get { return (Type) mType; }

        }

        public byte[] Payload
        {
            get { return mPayload; }
        }

        public byte[] SerializedData
        {
            get { return mData; }
        }


        private ClientServerMessage(Type type, byte[] payload, byte[] dataBytes)
        {
            mType = (byte) type;
            mPayload = payload;
            mData = dataBytes;
        }

        private ClientServerMessage(Type type, byte[] payload)
        {
            mType = (byte) type;
            mPayload = payload;
            mData = new[] {mType}.Concat(mPayload).ToArray();
        }


        public static ClientServerMessage create(Type type, byte[] payload)
        {
            return new ClientServerMessage(type, payload);
        }

        public static ClientServerMessage CreateDelayDisconnect(byte[] payload)
        {
            return create(Type.DelayDisconnect, payload);
        }

        public static ClientServerMessage CreateDataMessage(byte[] payload)
        {
            return create(Type.DataMessage, payload);
        }

        public static ClientServerMessage FromBytes(ByteArraySegment dataBytes)
        {
            var type = Type.Unknown;
            var payload = (byte[]) null;

            if (dataBytes.IsValid && dataBytes.Count > 0)
            {
                type = (Type) dataBytes[0];
                if (dataBytes.Count > 1)
                {
                    var len = dataBytes.Count - 1;
                    payload = new byte[len];
                    System.Array.Copy(dataBytes.Array, dataBytes.Offset + 1, payload, 0, len);
                }
            }

            return new ClientServerMessage(type, payload, dataBytes.Clone());
        }
    }
}