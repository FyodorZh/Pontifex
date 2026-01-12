using System;
using Actuarius.Memory;
using Archivarius;
using Archivarius.BinaryBackend;

namespace Pontifex.Api.Protocol
{
    public class NullTypeSerializer : ITypeSerializer
    {
        public void Serialize(IWriter writer, Type type)
        {
            throw new NotSupportedException();
        }
    }

    public class NullTypeDeserializer : ITypeDeserializer
    {
        public Type? Deserialize(IReader reader)
        {
            throw new NotSupportedException();
        }
    }
    
    public class ProtocolSerializer
    {
        private readonly BinaryWriter _writer = new BinaryWriter();
        private readonly HierarchicalSerializer _serializer;


        /// <summary>
        /// Polymorphism is OFF
        /// </summary>
        public ProtocolSerializer()
        {
            _serializer = new HierarchicalSerializer(_writer, false);
        }

        /// <summary>
        /// Polymorphism is ON
        /// </summary>
        /// <param name="models"></param>
        // public ProtocolSerialization(IEnumerable<Type> models)
        // {
        //     var list = models.ToList();
        //     _serializer = new HierarchicalSerializer(
        //         _writer, 
        //         new NullTypeSerializer(), 
        //         null, 
        //         false,
        //         0,
        //         list);
        //     _deserializer = new HierarchicalDeserializer(
        //         _reader,
        //         new NullTypeDeserializer(), 
        //         null, 
        //         _ => list, 
        //         false);
        // }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="pool"></param>
        /// <typeparam name="TModel"></typeparam>
        /// <returns></returns>
        public IMultiRefByteArray Serialize<TModel>(TModel model, IPool<IMultiRefByteArray, int> pool)
            where TModel : class, IDataStruct
        {
            lock (_serializer)
            {
                try
                {
                    _serializer.Prepare(false); // no polymorphism case
                    model.Serialize(_serializer);
                    var buffer = _writer.GetBufferUnsafe(out var size);
                    var res = pool.Acquire(size);
                    res.CopyFrom(buffer, 0, 0, size);
                    return res;
                }
                finally
                {
                    _writer.Clear();
                }
            }
        }
    }
}