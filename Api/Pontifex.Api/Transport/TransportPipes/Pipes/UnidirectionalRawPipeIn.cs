using System;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class UnidirectionalRawPipeIn : IUnidirectionalRawPipeIn
    {
        private readonly short _pipeId;
        private readonly Func<UnionDataList, SendResult> _globalSender;

        public UnidirectionalRawPipeIn(short pipeId, Func<UnionDataList, SendResult> globalSender)
        {
            _pipeId = pipeId;
            _globalSender = globalSender;
        }
            
        public SendResult Send(UnionDataList data)
        {
            data.PutFirst(new UnionData(_pipeId));
            return _globalSender.Invoke(data);
        }
    }
}