using System;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api.Protocol
{
    public class UnidirectionalModelPipeOut<TModel> : IUnidirectionalModelPipeOut<TModel>
        where TModel : class, IDataStruct, new()
    {
        private readonly IUnidirectionalRawPipeOut _rawPipeOut;
        private readonly ProtocolDeserializer _deserializer;
        
        private Func<TModel, bool>? _receiver;

        public UnidirectionalModelPipeOut(IUnidirectionalRawPipeOut rawPipeOut, ProtocolDeserializer deserializer)
        {
            _rawPipeOut = rawPipeOut;
            _deserializer = deserializer;
            rawPipeOut.SetReceiver(OnReceive);
        }
        
        public void SetReceiver(Func<TModel, bool> receiver)
        {
            _receiver = receiver;
        }

        private bool OnReceive(UnionDataList data)
        {
            using var disposer = data.AsDisposable();
            if (data.TryPopFirst(out IMultiRefReadOnlyByteArray? bytes))
            {
                using var bytesDisposable = bytes.AsDisposable();
                var model = _deserializer.Deserialize<TModel>(bytes);
                return _receiver?.Invoke(model) ?? false;
            }

            return false;
        }
    }
}