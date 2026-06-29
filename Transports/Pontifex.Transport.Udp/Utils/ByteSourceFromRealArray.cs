using System;

namespace Actuarius.Memory
{
    public struct ByteSourceFromRealArray : IByteSource
    {
        private readonly byte[] _array;
        private readonly int _offset;
        private readonly int _count;
        private int _position;
        
        public ByteSourceFromRealArray(byte[] array, int offset, int count)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (offset < 0 || offset >= array.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > array.Length) throw new ArgumentOutOfRangeException(nameof(count));

            _array = array;
            _offset = offset;
            _count = count;
            _position = 0;
        }

        public bool TryPop(out byte value)
        {
            if (_position < _count)
            {
                value = _array[_offset + _position++];
                return true;
            }

            value = 0;
            return false;
        }

        public bool TakeMany(IMultiRefByteArray dst)
        {
            if (_position + dst.Count <= _count)
            {
                Buffer.BlockCopy(_array, _offset + _position, dst.Array, dst.Offset, dst.Count);
                _position += dst.Count;
                return true;
            }

            return false;
        }
    }
}