using Serializer.BinarySerializer;

namespace Transport.Protocols.MessageProtocol
{
    internal interface IMessage
    {
        long requestId { get; }
        Message.Type comandType { get; }
        short command { get; }
        IDataStruct data { get; }
    }

    public class Message : IMessage, IDataStruct
    {
        public enum Type : byte
        {
            Event,
            Request,
            Response
        }

        public long requestId;
        public Type comandType;
        public short command;
        public IDataStruct data;

        public Message()
        {
        }

        public Message(long requestId, Type comandType, short command, IDataStruct data)
        {
            this.requestId = requestId;
            this.comandType = comandType;
            this.command = command;
            this.data = data;
        }

        long IMessage.requestId
        {
            get { return requestId; }
        }

        Type IMessage.comandType
        {
            get { return comandType; }
        }

        short IMessage.command
        {
            get { return command; }
        }

        IDataStruct IMessage.data
        {
            get { return data; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref requestId);
            var t = (byte) comandType;
            dst.Add(ref t);
            comandType = (Type) t;
            dst.Add(ref command);
            dst.Add(ref data);
            return true;
        }
    }
}