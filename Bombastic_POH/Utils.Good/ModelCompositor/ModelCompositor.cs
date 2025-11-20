using System;

namespace Archivarius.Tools
{
    public class ModelCompositor<TModel>
        where TModel : class, IDataStruct, new()
    {
        private readonly Action<TModel?> _onModelComposed;
        private readonly BufferCompositor _bufferCompositor;
        
        private readonly BinaryBackend.BinaryReader _binaryReader;
        private readonly HierarchicalDeserializer _deserializer;

        public ModelCompositor(Action<TModel?> onModelComposed, int maxPacketSize = 1024 * 1024)
        {
            _onModelComposed = onModelComposed;
            _bufferCompositor = new BufferCompositor(BufferProcessor, maxPacketSize);
            _binaryReader = new BinaryBackend.BinaryReader(Array.Empty<byte>());
            _deserializer = new HierarchicalDeserializer(_binaryReader, false);
        }

        public void PushData(byte[] bytes, int start, int count)
        {
            _bufferCompositor.PushData(bytes, start, count);
        }

        private void BufferProcessor(byte[] bytes, int count)
        {
            _binaryReader.Reset(bytes, count);
            _deserializer.Prepare();
            TModel? model = null;
            _deserializer.AddClass(ref model);
            _onModelComposed?.Invoke(model);
        }

        public void PrepareData(TModel model, byte[] outBuffer, int position, int count)
        {
            
        }
    }
}