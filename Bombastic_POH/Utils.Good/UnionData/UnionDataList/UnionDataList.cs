using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;
using Archivarius;

namespace Shared
{
    public class UnionDataList
    {
        private readonly CycleQueue<UnionData> _data = new();

        public IReadOnlyArray<UnionData> Elements => _data;

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

        public UnionDataList Clone()
        {
            var res = new UnionDataList();
            foreach (var element in _data.Enumerate())
            {
                res.PutLast(element);
            }
            return res;
        }
    }

    public static class UnionDataList_Ext
    {
        public static bool Check(this UnionDataList data, string value)
        {
            return data.TryGetFirst(out var r) && r.Type == UnionDataType.String && r.Text == value;
        }

        public static bool EqualByContent(this UnionDataList d1, UnionDataList d2)
        {
            if (d1.Elements.Count != d2.Elements.Count)
                return false;
            
            int count = d1.Elements.Count;
            for (int i = 0; i < count; i++)
            {
                if (!d1.Elements[i].Equals(d2.Elements[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}