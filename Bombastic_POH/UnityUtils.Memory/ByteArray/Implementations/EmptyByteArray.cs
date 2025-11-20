using System;

namespace Shared
{
    /// <summary>
    /// Эквивалентно new byte[0]
    /// Является синглтоном. Позволяет возвращать пустые массивы без аллокаций
    /// </summary>
    public class EmptyByteArray : IMultiRefLowLevelByteArray
    {
        public static readonly IMultiRefLowLevelByteArray Instance = new EmptyByteArray();

        private readonly byte[] mArray = new byte[0];

        private EmptyByteArray()
        {
        }

        public int Count
        {
            get { return 0; }
        }
        public bool IsValid
        {
            get { return true; }
        }
        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            return dst != null && dstOffset >= 0 && srcOffset >= 0 && count >= 0 && dstOffset + count <= dst.Length && srcOffset + count <= 0;
        }

        public void Release()
        {
            // DO NOTHING
        }

        public bool IsAlive
        {
            get { return true; }
        }

        public void AddRef()
        {
            // DO NOTHING
        }

        public byte[] ReadOnlyArray
        {
            get { return mArray; }
        }

        public int Offset
        {
            get { return 0; }
        }

        public byte this[int id]
        {
            get { throw new IndexOutOfRangeException();}
        }
    }
}