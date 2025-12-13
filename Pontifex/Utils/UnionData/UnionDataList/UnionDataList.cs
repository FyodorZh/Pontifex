using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public class UnionDataList : MultiRefCollectableResource<UnionDataList>
    {
        private readonly CycleQueue<UnionData> _data = new();

        public IReadOnlyArray<UnionData> Elements => _data;

        public void Clear()
        {
            foreach (var element in _data.Enumerate())
            {
                element.Bytes?.Release();
            }
            _data.Clear();
        }
        
        protected override void OnCollected()
        {
            Clear();
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public void PutFirst(UnionData value)
        {
            _data.PutToHead(value);
        }

        public void PutLast(UnionData value)
        {
            _data.Put(value);
        }

        public UnionData PopFirst()
        {
            if (!_data.TryPop(out var element))
            {
                throw new Exception();
            }

            return element;
        }
         
        public bool TryPopFirst([MaybeNullWhen(false)] out UnionData value)
        {
            return _data.TryPop(out value);
        }

        public UnionDataType PeekFirstType()
        {
            return _data.Count > 0 ? _data[0].Type : UnionDataType.Unknown;
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

            UnionDataMemoryAlias alias = _data.Count;
            alias.WriteTo4(sink.Value);
            
            foreach (var element in _data.Enumerate())
            {
                element.WriteTo(sink.Value);
            }

            return buffer;
        }

        public bool Deserialize(IByteSource source, IPool<IMultiRefByteArray, int> pool)
        {
            Clear();
            
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

        public override string ToString()
        {
            return "[" + string.Join(",", _data.Enumerate()) + "]";
        }
    }

    public static class UnionDataList_Ext
    {
        public static void PutFirst(this UnionDataList data, short value)
        {
            data.PutFirst(new UnionData(value));
        }
        
        public static void PutFirst(this UnionDataList data, int value)
        {
            data.PutFirst(new UnionData(value));
        }
        
        public static void PutFirst(this UnionDataList data, IMultiRefByteArray value)
        {
            data.PutFirst(new UnionData(value));
        }
        
        public static void PutFirst(this UnionDataList data, string value)
        {
            data.PutFirst(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(value))));
        }
        
        public static bool TryPopFirst(this UnionDataList data, out bool value)
        {
            if (data.PeekFirstType() == UnionDataType.Bool)
            {
                value = data.PopFirst().Alias.BoolValue;
                return true;
            }
            value = false;
            return false;
        }

        public static bool TryPopFirst(this UnionDataList data, out short value)
        {
            if (data.PeekFirstType() == UnionDataType.Short)
            {
                value = data.PopFirst().Alias.ShortValue;
                return true;
            }
            value = 0;
            return false;
        }
        
        public static bool TryPopFirst(this UnionDataList data, out ushort value)
        {
            if (data.PeekFirstType() == UnionDataType.UShort)
            {
                value = data.PopFirst().Alias.UShortValue;
                return true;
            }
            value = 0;
            return false;
        }
        
        public static bool TryPopFirst(this UnionDataList data, out int value)
        {
            if (data.PeekFirstType() == UnionDataType.Int)
            {
                value = data.PopFirst().Alias.IntValue;
                return true;
            }
            value = 0;
            return false;
        }
        
        public static bool TryPopFirst(this UnionDataList data, [MaybeNullWhen(false)] out IMultiRefReadOnlyByteArray value)
        {
            if (data.PeekFirstType() == UnionDataType.Array)
            {
                value = data.PopFirst().Bytes;
                return value != null;
            }
            value = null;
            return false;
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