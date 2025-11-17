using Actuarius.Memoria;
using Serializer.BinarySerializer;
using Serializer.Factory;
using Shared;
using Shared.Buffer;

namespace Transport.Protocols.Reliable.Delivery
{
    internal interface IMessage : IMultiRef
    {
        void WriteTo(IMemoryBuffer dst);
        bool ReadFrom(IMemoryBuffer dst);
    }

    internal class MessageSerializer
    {
        private readonly IDataStructFactory mFactory;

        public MessageSerializer(IDataStructFactory factory)
        {
            mFactory = factory;
        }

        public IMessage Deserialize(IMemoryBuffer buffer)
        {
            try
            {
                ushort modelId;
                if (!buffer.PopFirst().AsUInt16(out modelId))
                {
                    return null;
                }

                IMessage msg = (IMessage)mFactory.CreateDataStruct(unchecked((short)modelId));

                if (!msg.ReadFrom(buffer))
                {
                    Log.e("Failed to deserialize buffer");
                    return null;
                }

                if (buffer.Count > 0)
                {
                    Log.e("Deserialized buffer is not empty");
                    return null; // TODO: Show error
                }

                return msg;
            }
            catch
            {
                return null; //TODO: Show error
            }
        }

        public IMemoryBufferHolder Serialize(IMessage message)
        {
            var bufferHolder = ConcurrentUsageMemoryBufferPool.Instance.Allocate();
            var buffer = bufferHolder.ShowBufferUnsafe();
            buffer.PushUInt16(unchecked((ushort)mFactory.GetIndexForModel(message.GetType())), false);
            message.WriteTo(buffer);
            return bufferHolder;
        }
    }
}
