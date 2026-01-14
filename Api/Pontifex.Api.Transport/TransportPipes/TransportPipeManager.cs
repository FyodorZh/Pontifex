using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class TransportPipeManager : IPipeAllocator
    {
        private readonly ProtocolSerializer _serializer;
        private readonly ProtocolDeserializer _deserializer;
        private readonly IMemoryRental _memoryRental;

        private readonly List<UnidirectionalRawPipeOut?> _rawPipeMap = new();

        private readonly Func<UnionDataList, SendResult> _globalSender;

        public TransportPipeManager(Func<UnionDataList, SendResult> sender, IMemoryRental memoryRental)
        {
            _serializer = new ProtocolSerializer();
            _deserializer = new ProtocolDeserializer();
            _memoryRental = memoryRental;
            
            _globalSender = sender;
        }

        public bool OnReceived(UnionDataList data)
        {
            using var disposer = data.AsDisposable();
            if (data.TryPopFirst(out short pipeId) && pipeId < _rawPipeMap.Count)
            {
                var pipe = _rawPipeMap[pipeId];
                if (pipe != null)
                {
                    return pipe.OnReceived(data.Acquire());
                }
            }

            return false;
        }
        
        #region IPipeAllocator
        
        public IUnidirectionalRawPipeIn AllocateRawPipeIn()
        {
            var pipe =  new UnidirectionalRawPipeIn((short)_rawPipeMap.Count, _globalSender);
            _rawPipeMap.Add(null);
            return pipe;
        }

        public IUnidirectionalRawPipeOut AllocateRawPipeOut()
        {
            var pipe = new UnidirectionalRawPipeOut();
            _rawPipeMap.Add(pipe);
            return pipe;
        }

        public IUnidirectionalModelPipeIn<TModel> AllocateModelPipeIn<TModel>() where TModel : struct, IDataStruct
        {
            return new UnidirectionalModelPipeIn<TModel>(AllocateRawPipeIn(), _serializer, _memoryRental);
        }

        public IUnidirectionalModelPipeOut<TModel> AllocateModelPipeOut<TModel>() where TModel : struct, IDataStruct
        {
            return new UnidirectionalModelPipeOut<TModel>(AllocateRawPipeOut(), _deserializer);
        }
        
        #endregion
    }
}