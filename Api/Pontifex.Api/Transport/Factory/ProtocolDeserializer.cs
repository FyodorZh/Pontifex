using System;
using Actuarius.Memory;
using Archivarius;
using Archivarius.BinaryBackend;

namespace Pontifex.Api
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
            _deserializer = HierarchicalDeserializer.From(_reader).SetAutoPrepare(false).SetMonomorphic().Build();
        }
        
        public bool Deserialize<TModel>(IMultiRefReadOnlyByteArray buffer, out TModel model)
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
                    model = new();
                    model.Serialize(_deserializer);
                    return true;
                }
                finally
                {
                    _reader.Reset(Array.Empty<byte>());
                }
            }
        }
    }
}