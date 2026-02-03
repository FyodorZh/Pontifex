using System;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class UnidirectionalModelPipeIn<TModel> : IUnidirectionalModelPipeIn<TModel>
        where TModel : struct, IDataStruct
    {
        private readonly IUnidirectionalRawPipeIn _rawPipeIn;
        private readonly ProtocolSerializer _protocolSerializer;
        private readonly IMemoryRental _memoryRental;

        private readonly ILogger Log;
        
        public UnidirectionalModelPipeIn(IUnidirectionalRawPipeIn rawPipeIn, ProtocolSerializer serializer, IMemoryRental memoryRental, ILogger logger)
        {
            _rawPipeIn = rawPipeIn;
            _protocolSerializer = serializer;
            _memoryRental = memoryRental;
            Log = logger;
        }

        public SendResult Send(TModel model)
        {
            try
            {
                using var bytesHolder = 
                    _protocolSerializer.Serialize(model, _memoryRental.ByteArraysPool).AsDisposable();
                UnionDataList data = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                data.PutFirst(bytesHolder.Resource.Acquire());
                return _rawPipeIn.Send(data);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return SendResult.Error;
            }
        }
    }
}