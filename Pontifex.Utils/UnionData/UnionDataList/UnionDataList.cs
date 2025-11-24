using System.Diagnostics.CodeAnalysis;
using System.Text;
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
            foreach (var element in _data.Enumerate())
            {
                element.Bytes?.Release();
            }
            _data.Clear();
        }

        public bool PutFirst(UnionData value)
        {
            return _data.PutToHead(value);
        }

        public bool PutLast(UnionData value)
        {
            return _data.Put(value);
        }
         
        public bool TryGetFirst([MaybeNullWhen(false)] out UnionData value)
        {
            return _data.TryPop(out value);
        }

        public IMultiRefByteArray Serialize(ICollectablePool collectablePool, IPool<IMultiRefByteArray, int> bytesPool)
        {
            int count = 4;
            foreach (var element in _data.Enumerate())
            {
                count += element.GetDataSize();
            }

            var buffer = bytesPool.Acquire(count);
            using var sink = collectablePool.Acquire<ByteSinkFromArray>().AsDisposable();
            sink.Value.Reset(buffer, 0);

            UnionDataMemoryAlias alias = count;
            alias.WriteTo4(sink.Value);
            
            foreach (var element in _data.Enumerate())
            {
                element.WriteTo(sink.Value);
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
        public static bool PutFirst(this UnionDataList data, string value)
        {
            return data.PutFirst(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(value))));
        }
        public static bool Check(this UnionDataList data, string value)
        {
            return data.TryGetFirst(out var r) && r.Type == UnionDataType.Array && Encoding.UTF8.GetString(r.Bytes!.ToArray(null)!) == value;
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