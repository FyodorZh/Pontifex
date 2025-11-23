using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public class UnionDataList : MultiRefResource
    {
        private readonly CycleQueue<UnionData> _data = new();

        public IReadOnlyArray<UnionData> Elements => _data;
        
        public UnionDataList() 
            : base(false)
        {
        }
        
        protected override void OnReleased()
        {
            throw new System.NotImplementedException();
        }

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

        public IMultiRefByteArray Serialize(IPool<IMultiRefByteArray, int> pool)
        {
            int count = 4;
            foreach (var element in _data.Enumerate())
            {
                count += element.GetDataSize();
            }

            var buffer = pool.Acquire(count);

            IByteSink sink = new ByteSinkFromArray(buffer, 0);
            UnionDataMemoryAlias alias = count;
            alias.WriteTo4(sink);
            
            foreach (var element in _data.Enumerate())
            {
                element.WriteTo(sink);
            }

            return buffer;
        }

        public bool Deserialize(IByteSource source, IPool<IMultiRefByteArray, int> pool)
        {
            _data.Clear();
            
            UnionDataMemoryAlias alias = new();
            if (!alias.ReadFrom4(source))
            {
                return false;
            }

            int count = alias.IntValue;
            for (int i = 0; i < count; i++)
            {
                if (!UnionData.ReadFrom(source, pool, out var element))
                {
                    return false;
                }

                _data.Put(element);
            }

            return true;
        }
    }

    public static class UnionDataList_Ext
    {
        // public static bool Check(this UnionDataList data, string value)
        // {
        //     return data.TryGetFirst(out var r) && r.Type == UnionDataType.String && r.Text == value;
        // }

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