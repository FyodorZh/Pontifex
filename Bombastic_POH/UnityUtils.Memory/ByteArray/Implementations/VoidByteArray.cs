using System;

namespace Shared
{
    /// <summary>
    /// Эквивалентно default(byte[]) или default(ByteArraySegment)
    /// Является синглтоном. Позволяет возвращать невалидные массивы без аллокаций
    /// </summary>
    public class VoidByteArray : IMultiRefLowLevelByteArray
    {
        public static readonly IMultiRefLowLevelByteArray Instance = new VoidByteArray();

        private VoidByteArray()
        {
        }

        public int Count
        {
            get { return 0; }
        }
        public bool IsValid
        {
            get { return false; }
        }
        public bool CopyTo(byte[] dst, int dstOffset, int srcOffset, int count)
        {
            return false;
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
            get { return null; }
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