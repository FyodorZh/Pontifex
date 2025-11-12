using System;
namespace Shared
{
    public interface IDSource<T>
    {
        int GenNewId();
    }

    public class IDSourceImpl<T> : IDSource<T>
    {
        private int mId;
        private readonly int mStride;
        public IDSourceImpl(int prevId, int stride)
        {
            mId = prevId;
            mStride = stride;
        }

        int IDSource<T>.GenNewId()
        {
            return (int)System.Threading.Interlocked.Add(ref mId, mStride);
        }
    }

    public struct ID<T> : IEquatable<ID<T>>, IComparable<ID<T>>
    {
        /// <summary>
        /// Invalid == ID(int.MinValue)
        /// </summary>
        public static readonly ID<T> Invalid = new ID<T>(int.MinValue);

        private readonly int mId;

        private ID(int mId)
        {
            this.mId = mId;
        }

        public ID(IDSource<T> source)
            : this(source.GenNewId())
        {
        }

        public int SerializeTo()
        {
            return mId;
        }

        public static ID<T> DeserializeFrom(int id)
        {
            return new ID<T>(id);
        }

        public bool IsValid
        {
            get
            {
                return mId != Invalid.mId;
            }
        }

        public static ID<T> operator +(ID<T> id, int shift)
        {
            if (!id.IsValid)
            {
                return id;
            }
            return new ID<T>((int)(id.mId + shift));
        }

        public override string ToString()
        {
            return mId != Invalid.mId ? mId.ToString() : "Invalid";
        }

        public static bool operator ==(ID<T> id1, ID<T> id2)
        {
            return id1.mId == id2.mId;
        }

        public static bool operator !=(ID<T> id1, ID<T> id2)
        {
            return id1.mId != id2.mId;
        }

        public static bool operator <=(ID<T> id1, ID<T> id2)
        {
            return id1.mId <= id2.mId;
        }

        public static bool operator >=(ID<T> id1, ID<T> id2)
        {
            return id1.mId >= id2.mId;
        }

        public static bool operator <(ID<T> id1, ID<T> id2)
        {
            return id1.mId < id2.mId;
        }

        public static bool operator >(ID<T> id1, ID<T> id2)
        {
            return id1.mId > id2.mId;
        }

        public override bool Equals(object obj)
        {
            if (obj is ID<T>)
            {
                return Equals((ID<T>)obj);
            }
            return false;
        }

        public bool Equals(ID<T> obj)
        {
            return mId == obj.mId;
        }

        public override int GetHashCode()
        {
            return (int)mId;
        }

        public int CompareTo(ID<T> other)
        {
            return mId.CompareTo(other.mId);
        }

        public static ID<T> Inc(ID<T> val)
        {
            if (val.IsValid)
            {
                return new ID<T>((int)(val.mId + 1));
            }
            return Invalid;
        }

        public static ID<T> Max(ID<T> l, ID<T> r)
        {
            return new ID<T>(System.Math.Max(l.mId, r.mId));
        }
    }


    public interface IDLongSource<T>
    {
        long GenNewId();
    }

    public class IDLongSourceImpl<T> : IDLongSource<T>
    {
        private long mId;
        private readonly long mStride;
        public IDLongSourceImpl(long prevId, long stride)
        {
            mId = prevId;
            mStride = stride;
        }

        long IDLongSource<T>.GenNewId()
        {
            return (long)System.Threading.Interlocked.Add(ref mId, mStride);
        }
    }

    public struct IDLong<T> : IEquatable<IDLong<T>>, IComparable<IDLong<T>>
    {
        /// <summary>
        /// Invalid == ID(long.MinValue)
        /// </summary>
        public static readonly IDLong<T> Invalid = new IDLong<T>(long.MinValue);

        private readonly long mId;

        private IDLong(long mId)
        {
            this.mId = mId;
        }

        public IDLong(IDLongSource<T> source)
            : this(source.GenNewId())
        {
        }

        public long SerializeTo()
        {
            return mId;
        }

        public static IDLong<T> DeserializeFrom(long id)
        {
            return new IDLong<T>(id);
        }

        public bool IsValid
        {
            get
            {
                return mId != Invalid.mId;
            }
        }

        public static IDLong<T> operator +(IDLong<T> id, int shift)
        {
            if (!id.IsValid)
            {
                return id;
            }
            return new IDLong<T>((long)(id.mId + shift));
        }

        public override string ToString()
        {
            return mId != Invalid.mId ? mId.ToString() : "Invalid";
        }

        public static bool operator ==(IDLong<T> id1, IDLong<T> id2)
        {
            return id1.mId == id2.mId;
        }

        public static bool operator !=(IDLong<T> id1, IDLong<T> id2)
        {
            return id1.mId != id2.mId;
        }

        public static bool operator <=(IDLong<T> id1, IDLong<T> id2)
        {
            return id1.mId <= id2.mId;
        }

        public static bool operator >=(IDLong<T> id1, IDLong<T> id2)
        {
            return id1.mId >= id2.mId;
        }

        public static bool operator <(IDLong<T> id1, IDLong<T> id2)
        {
            return id1.mId < id2.mId;
        }

        public static bool operator >(IDLong<T> id1, IDLong<T> id2)
        {
            return id1.mId > id2.mId;
        }

        public override bool Equals(object obj)
        {
            if (obj is IDLong<T>)
            {
                return Equals((IDLong<T>)obj);
            }
            return false;
        }

        public bool Equals(IDLong<T> obj)
        {
            return mId == obj.mId;
        }

        public override int GetHashCode()
        {
            return mId.GetHashCode();
        }

        public int CompareTo(IDLong<T> other)
        {
            return mId.CompareTo(other.mId);
        }

        public static IDLong<T> Inc(IDLong<T> val)
        {
            if (val.IsValid)
            {
                return new IDLong<T>((long)(val.mId + 1));
            }
            return Invalid;
        }

        public static IDLong<T> Max(IDLong<T> l, IDLong<T> r)
        {
            return new IDLong<T>(System.Math.Max(l.mId, r.mId));
        }
    }


    public interface IDShortSource<T>
    {
        short GenNewId();
    }

    public class IDShortSourceImpl<T> : IDShortSource<T>
    {
        private int mId;
        private readonly int mStride;
        public IDShortSourceImpl(int prevId, int stride)
        {
            mId = prevId;
            mStride = stride;
        }

        short IDShortSource<T>.GenNewId()
        {
            return (short)System.Threading.Interlocked.Add(ref mId, mStride);
        }
    }

    public struct IDShort<T> : IEquatable<IDShort<T>>, IComparable<IDShort<T>>
    {
        /// <summary>
        /// Invalid == ID(short.MinValue)
        /// </summary>
        public static readonly IDShort<T> Invalid = new IDShort<T>(short.MinValue);

        private readonly short mId;

        private IDShort(short mId)
        {
            this.mId = mId;
        }

        public IDShort(IDShortSource<T> source)
            : this(source.GenNewId())
        {
        }

        public short SerializeTo()
        {
            return mId;
        }

        public static IDShort<T> DeserializeFrom(short id)
        {
            return new IDShort<T>(id);
        }

        public bool IsValid
        {
            get
            {
                return mId != Invalid.mId;
            }
        }

        public static IDShort<T> operator +(IDShort<T> id, int shift)
        {
            if (!id.IsValid)
            {
                return id;
            }
            return new IDShort<T>((short)(id.mId + shift));
        }

        public override string ToString()
        {
            return mId != Invalid.mId ? mId.ToString() : "Invalid";
        }

        public static bool operator ==(IDShort<T> id1, IDShort<T> id2)
        {
            return id1.mId == id2.mId;
        }

        public static bool operator !=(IDShort<T> id1, IDShort<T> id2)
        {
            return id1.mId != id2.mId;
        }

        public static bool operator <=(IDShort<T> id1, IDShort<T> id2)
        {
            return id1.mId <= id2.mId;
        }

        public static bool operator >=(IDShort<T> id1, IDShort<T> id2)
        {
            return id1.mId >= id2.mId;
        }

        public static bool operator <(IDShort<T> id1, IDShort<T> id2)
        {
            return id1.mId < id2.mId;
        }

        public static bool operator >(IDShort<T> id1, IDShort<T> id2)
        {
            return id1.mId > id2.mId;
        }

        public override bool Equals(object obj)
        {
            if (obj is IDShort<T>)
            {
                return Equals((IDShort<T>)obj);
            }
            return false;
        }

        public bool Equals(IDShort<T> obj)
        {
            return mId == obj.mId;
        }

        public override int GetHashCode()
        {
            return (int)mId;
        }

        public int CompareTo(IDShort<T> other)
        {
            return mId.CompareTo(other.mId);
        }

        public static IDShort<T> Inc(IDShort<T> val)
        {
            if (val.IsValid)
            {
                return new IDShort<T>((short)(val.mId + 1));
            }
            return Invalid;
        }

        public static IDShort<T> Max(IDShort<T> l, IDShort<T> r)
        {
            return new IDShort<T>(System.Math.Max(l.mId, r.mId));
        }
    }


    public interface IDSByteSource<T>
    {
        sbyte GenNewId();
    }

    public class IDSByteSourceImpl<T> : IDSByteSource<T>
    {
        private int mId;
        private readonly int mStride;
        public IDSByteSourceImpl(int prevId, int stride)
        {
            mId = prevId;
            mStride = stride;
        }

        sbyte IDSByteSource<T>.GenNewId()
        {
            return (sbyte)System.Threading.Interlocked.Add(ref mId, mStride);
        }
    }

    public struct IDSByte<T> : IEquatable<IDSByte<T>>, IComparable<IDSByte<T>>
    {
        /// <summary>
        /// Invalid == ID(sbyte.MinValue)
        /// </summary>
        public static readonly IDSByte<T> Invalid = new IDSByte<T>(sbyte.MinValue);

        private readonly sbyte mId;

        private IDSByte(sbyte mId)
        {
            this.mId = mId;
        }

        public IDSByte(IDSByteSource<T> source)
            : this(source.GenNewId())
        {
        }

        public sbyte SerializeTo()
        {
            return mId;
        }

        public static IDSByte<T> DeserializeFrom(sbyte id)
        {
            return new IDSByte<T>(id);
        }

        public bool IsValid
        {
            get
            {
                return mId != Invalid.mId;
            }
        }

        public static IDSByte<T> operator +(IDSByte<T> id, int shift)
        {
            if (!id.IsValid)
            {
                return id;
            }
            return new IDSByte<T>((sbyte)(id.mId + shift));
        }

        public override string ToString()
        {
            return mId != Invalid.mId ? mId.ToString() : "Invalid";
        }

        public static bool operator ==(IDSByte<T> id1, IDSByte<T> id2)
        {
            return id1.mId == id2.mId;
        }

        public static bool operator !=(IDSByte<T> id1, IDSByte<T> id2)
        {
            return id1.mId != id2.mId;
        }

        public static bool operator <=(IDSByte<T> id1, IDSByte<T> id2)
        {
            return id1.mId <= id2.mId;
        }

        public static bool operator >=(IDSByte<T> id1, IDSByte<T> id2)
        {
            return id1.mId >= id2.mId;
        }

        public static bool operator <(IDSByte<T> id1, IDSByte<T> id2)
        {
            return id1.mId < id2.mId;
        }

        public static bool operator >(IDSByte<T> id1, IDSByte<T> id2)
        {
            return id1.mId > id2.mId;
        }

        public override bool Equals(object obj)
        {
            if (obj is IDSByte<T>)
            {
                return Equals((IDSByte<T>)obj);
            }
            return false;
        }

        public bool Equals(IDSByte<T> obj)
        {
            return mId == obj.mId;
        }

        public override int GetHashCode()
        {
            return (int)mId;
        }

        public int CompareTo(IDSByte<T> other)
        {
            return mId.CompareTo(other.mId);
        }

        public static IDSByte<T> Inc(IDSByte<T> val)
        {
            if (val.IsValid)
            {
                return new IDSByte<T>((sbyte)(val.mId + 1));
            }
            return Invalid;
        }

        public static IDSByte<T> Max(IDSByte<T> l, IDSByte<T> r)
        {
            return new IDSByte<T>(System.Math.Max(l.mId, r.mId));
        }
    }


}