using System;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class UnidirectionalRawPipeOut : IUnidirectionalRawPipeOut
    {
        private Func<UnionDataList, bool>? _receiver;
        
        public bool OnReceived(UnionDataList data)
        {
            var receiver = _receiver;
            if (receiver != null)
            {
                return receiver.Invoke(data);
            }
            data.Release();
            return false;
        }
        
        public void SetReceiver(Func<UnionDataList, bool>? receiver)
        {
            _receiver = receiver;
        }
    }
}