using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;
using Archivarius.UnionDataListBackend;

namespace Shared
{
    public class UnionDataList
    {
        private readonly CycleQueue<UnionData> _data = new();

        public void PutFirst(UnionData value)
        {
            _data.PutToHead(value);
        }

        public void PutLast(UnionData value)
        {
            _data.Put(value);
        }
         
        public bool TryGetFirst([MaybeNullWhen(false)] out UnionData value)
        {
            return _data.TryPop(out value);
        }
         
        //public bool TryGetFirst(UnionDataType type, [MaybeNullWhen(false)] out UnionData value)
    }
}