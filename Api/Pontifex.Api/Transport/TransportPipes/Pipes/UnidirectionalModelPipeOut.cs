using System;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class UnidirectionalModelPipeOut<TModel> : IUnidirectionalModelPipeOut<TModel>
        where TModel : struct, IDataStruct
    {
        private readonly IUnidirectionalRawPipeOut _rawPipeOut;
        private readonly ProtocolDeserializer _deserializer;
        
        private Action<TModel>? _receiver;

        public UnidirectionalModelPipeOut(IUnidirectionalRawPipeOut rawPipeOut, ProtocolDeserializer deserializer)
        {
            _rawPipeOut = rawPipeOut;
            _deserializer = deserializer;
            rawPipeOut.SetReceiver(OnReceive);
        }
        
        public void SetReceiver(Action<TModel>? receiver)
        {
            _receiver = receiver;
        }

        private bool OnReceive(UnionDataList data)
        {
            using var disposer = data.AsDisposable();
            
            var receiver = _receiver;
            if (receiver == null)
            {
                return false;
            }
            
            if (data.TryPopFirst(out IMultiRefReadOnlyByteArray? bytes))
            {
                using var bytesDisposable = bytes.AsDisposable();
                if (!_deserializer.Deserialize<TModel>(bytes, out var model))
                {
                    return false;
                }
                receiver.Invoke(model);
                return true;
            }

            return false;
        }
    }
}