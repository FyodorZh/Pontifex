using System;
using Actuarius.Memory;
using Archivarius;
using Archivarius.BinaryBackend;

namespace Pontifex.Api.Protocol
{
    public class ProtocolDeserializer
    {
        private readonly BinaryReader _reader = new BinaryReader(Array.Empty<byte>());
        private readonly HierarchicalDeserializer _deserializer;
        
        /// <summary>
        /// Polymorphism is OFF
        /// </summary>
        public ProtocolDeserializer()
        {
            _deserializer = new HierarchicalDeserializer(_reader, false);
        }
        
        public TModel Deserialize<TModel>(IMultiRefReadOnlyByteArray buffer)
            where TModel : struct, IDataStruct
        {
            lock (_deserializer)
            {
                try
                {
                    if (buffer.Offset != 0)
                    {
                        throw new Exception("TODO");
                    }

                    _reader.Reset(buffer.ReadOnlyArray, buffer.Count);
                    _deserializer.Prepare();
                    TModel model = new();
                    model.Serialize(_deserializer);
                    return model;
                }
                finally
                {
                    _reader.Reset(Array.Empty<byte>());
                }
            }
        }
    }
}