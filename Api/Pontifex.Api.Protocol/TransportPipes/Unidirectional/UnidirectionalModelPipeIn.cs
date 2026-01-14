using System;
using Actuarius.Memory;
using Archivarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api.Protocol
{
    public class UnidirectionalModelPipeIn<TModel> : IUnidirectionalModelPipeIn<TModel>
        where TModel : struct, IDataStruct
    {
        private readonly IUnidirectionalRawPipeIn _rawPipeIn;
        private readonly ProtocolSerializer _protocolSerializer;
        private readonly IMemoryRental _memoryRental;
        
        public UnidirectionalModelPipeIn(IUnidirectionalRawPipeIn rawPipeIn, ProtocolSerializer serializer, IMemoryRental memoryRental)
        {
            _rawPipeIn = rawPipeIn;
            _protocolSerializer = serializer;
            _memoryRental = memoryRental;
        }

        public SendResult Send(TModel model)
        {
            try
            {
                using var bytesHolder = 
                    _protocolSerializer.Serialize(model, _memoryRental.ByteArraysPool).AsDisposable();
                UnionDataList data = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                data.PutFirst(bytesHolder.Resource.Acquire());
                _rawPipeIn.Send(data);
                return SendResult.Ok;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return SendResult.Error;
            }
        }
    }
}