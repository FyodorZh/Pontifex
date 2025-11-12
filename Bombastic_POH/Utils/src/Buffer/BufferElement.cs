using System;
using Shared;
namespace Shared.Buffer
{
    public struct BufferElement
    {
        private const int ParamBitSize = 4;
        private const int MaxParam = (1 << ParamBitSize) - 1;

        private readonly byte mType;
        private readonly UInt64 mIntValue;
        private Object mObject; // IMemoryBufferObject or byte[]

        public BufferElementType Type
        {
            get { return (BufferElementType)(mType >> ParamBitSize); }
        }

        private static byte BuildTypeData(BufferElementType type, int param)
        {
            return (byte)(((int)type << ParamBitSize) + param);
        }

        private int ArrayOffset()
        {
            return (int)(mIntValue >> 32);
        }

        private int ArraySize()
        {
            return (int)(mIntValue & 0xFFFFFFFFL);
        }

        private static UInt64 SetArray(int offset, int size)
        {
            return (((UInt64)offset) << 32) + (UInt64)size;
        }

        private int InternalParam
        {
            get { return mType & MaxParam; }
        }

        public BufferElement(ByteArraySegment bytes)
        {
            if (!bytes.IsValid)
            {
                mType = BuildTypeData(BufferElementType.Array, 0);
                mIntValue = 0;
                mObject = null;
            }
            else
            {
                int len = bytes.Count;
                mType = BuildTypeData(BufferElementType.Array, len < MaxParam - 1 ? len + 1 : MaxParam);
                mIntValue = SetArray(bytes.Offset, len);
                mObject = bytes.Array;
            }
        }

        public BufferElement(Boolean value)
        {
            mType = BuildTypeData(BufferElementType.Bool, value ? 1 : 0);
            mIntValue = 0;
            mObject = null;
        }

        public BufferElement(Byte value)
        {
            if (value < MaxParam)
            {
                mType = BuildTypeData(BufferElementType.Byte, value + 1);
                mIntValue = 0;
            }
            else
            {
                mType = BuildTypeData(BufferElementType.Byte, 0);
                mIntValue = value;
            }
            mObject = null;
        }

        public BufferElement(UInt16 value)
        {
            if (value <= MaxParam - 2)
            {
                mType = BuildTypeData(BufferElementType.UInt16, value);
                mIntValue = 0;
            }
            else if (value < 256)
            {
                mType = BuildTypeData(BufferElementType.UInt16, MaxParam - 1);
                mIntValue = value;
            }
            else
            {
                mType = BuildTypeData(BufferElementType.UInt16, MaxParam);
                mIntValue = value;
            }
            mObject = null;
        }

        public BufferElement(Int32 value)
        {
            if (value == 0 || value == 1 || value == -1)
            {
                mType = BuildTypeData(BufferElementType.Int32, value + 1); // 0, 1, 2
                mIntValue = 0;
            }
            else if (value == unchecked((sbyte)value))
            {
                mType = BuildTypeData(BufferElementType.Int32, MaxParam - 2);
                mIntValue = unchecked((byte)(sbyte)value);
            }
            else if (value == unchecked((short)value))
            {
                mType = BuildTypeData(BufferElementType.Int32, MaxParam - 1);
                mIntValue = unchecked((UInt16)(Int16)value);
            }
            else
            {
                mType = BuildTypeData(BufferElementType.Int32, MaxParam);
                mIntValue = unchecked((UInt32)value);
            }
            mObject = null;
        }

        public BufferElement(Int64 value)
        {
            mType = BuildTypeData(BufferElementType.Int64, 0);
            mIntValue = unchecked((UInt64)value);
            mObject = null;
        }

        public BufferElement(Single value)
        {
            mType = BuildTypeData(BufferElementType.Single, 0);

            var union = new Serializer.BinarySerializer.ManagedUnions4();
            union.floatValue = value;
            mIntValue = union.uintValue;

            mObject = null;
        }

        public BufferElement(Double value)
        {
            mType = BuildTypeData(BufferElementType.Double, 0);

            var union = new Serializer.BinarySerializer.ManagedUnions8();
            union.doubleValue = value;
            mIntValue = union.ulongValue;

            mObject = null;
        }

        public BufferElement(IMemoryBufferHolder buffer)
        {
            if (buffer != null)
            {
                mType = BuildTypeData(BufferElementType.Buffer, 0);
                mIntValue = 0;
                mObject = buffer;
            }
            else
            {
                mType = BuildTypeData(BufferElementType.Unknown, 0);
                mIntValue = 0;
                mObject = null;
            }
        }

        public BufferElement(IMultiRefByteArray array)
        {
            if (array == null || !array.IsValid)
            {
                mType = BuildTypeData(BufferElementType.AbstractArray, 0);
                mIntValue = 0;
                mObject = null;
            }
            else
            {
                int len = array.Count;
                mType = BuildTypeData(BufferElementType.AbstractArray, len < MaxParam - 1 ? len + 1 : MaxParam);
                mIntValue = SetArray(0, len);
                mObject = array;
            }
        }

        private BufferElement(byte type, ulong intValue, Object obj)
        {
            mType = type;
            mIntValue = intValue;
            mObject = obj;
        }

        internal BufferElement Clone()
        {
            IMemoryBufferHolder srcBuffer = mObject as IMemoryBufferHolder;
            if (srcBuffer != null)
            {
                IMemoryBufferHolder buffer;
                if (ConcurrentUsageMemoryBufferPool.Instance.AllocateAndClone(srcBuffer, out buffer) && buffer != null)
                {
                    return new BufferElement(mType, mIntValue, buffer);
                }
                return new BufferElement();
            }

            IMultiRefByteArray abstractArray = mObject as IMultiRefByteArray;
            if (abstractArray != null)
            {
                abstractArray.AddRef();
            }

            return new BufferElement(mType, mIntValue, mObject);
        }

        public static int MaxSize(BufferElementType type)
        {
            switch (type)
            {
                case BufferElementType.Unknown:
                case BufferElementType.Bool:
                    return 1;
                case BufferElementType.Byte:
                    return 1 + sizeof(byte);
                case BufferElementType.UInt16:
                    return 1 + sizeof(UInt16);
                case BufferElementType.Int32:
                    return 1 + sizeof(Int32);
                case BufferElementType.Int64:
                    return 1 + sizeof(Int64);
                case BufferElementType.Single:
                    return 1 + sizeof(Single);
                case BufferElementType.Double:
                    return 1 + sizeof(Double);
                case BufferElementType.Buffer:
                    return 1 + 3 + 0;
                case BufferElementType.Array:
                    return 1 + 3 + 0;
                case BufferElementType.AbstractArray:
                    return 1 + 3 + 0;
                default:
                    return 0;
            }
        }

        public int Size
        {
            get
            {
                switch (Type)
                {
                    case BufferElementType.Unknown:
                    case BufferElementType.Bool:
                        return 1;
                    case BufferElementType.Byte:
                        return 1 + (InternalParam == 0 ? sizeof(Byte) : 0);
                    case BufferElementType.UInt16:
                        switch (InternalParam)
                        {
                            case MaxParam:
                                return 1 + sizeof(UInt16);
                            case MaxParam - 1:
                                return 1 + sizeof(Byte);
                            default:
                                return 1;
                        }
                    case BufferElementType.Int32:
                        switch (InternalParam)
                        {
                            case MaxParam:
                                return 1 + sizeof(Int32);
                            case MaxParam - 1:
                                return 1 + sizeof(UInt16);
                            case MaxParam - 2:
                                return 1 + sizeof(Byte);
                            default:
                                return 1;
                        }
                    case BufferElementType.Int64:
                        return 1 + sizeof(Int64);
                    case BufferElementType.Single:
                        return 1 + sizeof(Single);
                    case BufferElementType.Double:
                        return 1 + sizeof(Double);
                    case BufferElementType.Buffer:
                        {
                            var buffer = ((IMemoryBufferHolder)mObject).ShowBufferUnsafe();
                            return 1 + 3 + buffer.Size;
                        }
                    case BufferElementType.Array:
                    case BufferElementType.AbstractArray:
                        {
                            var param = InternalParam;
                            switch (param)
                            {
                                case 0:
                                case 1:
                                    return 1;
                                case MaxParam:
                                    return 1 + 3 + ArraySize();
                                default:
                                    return 1 + (param - 1);
                            }
                        }
                    default:
                        return 0;
                }
            }
        }

        public BufferElement(byte[] data, ref int offset, MemoryBuffer owner)
        {
            try
            {
                mIntValue = 0;
                mObject = null;
                mType = data[offset++];
                switch (Type)
                {
                    case BufferElementType.Unknown:
                    case BufferElementType.Bool:
                        // Do nothing
                        break;
                    case BufferElementType.Byte:
                        if (InternalParam == 0)
                        {
                            mIntValue = data[offset++];
                        }
                        break;
                    case BufferElementType.UInt16:
                        switch (InternalParam)
                        {
                            case MaxParam:
                                mIntValue = data[offset++];
                                mIntValue += (uint)data[offset++] << 8;
                                break;
                            case MaxParam - 1:
                                mIntValue = data[offset++];
                                break;
                        }
                        break;
                    case BufferElementType.Int32:
                        switch (InternalParam)
                        {
                            case MaxParam:
                                mIntValue = data[offset++];
                                mIntValue += (uint)data[offset++] << 8;
                                mIntValue += (uint)data[offset++] << 16;
                                mIntValue += (uint)data[offset++] << 24;
                                break;
                            case MaxParam - 1:
                                mIntValue = data[offset++];
                                mIntValue += (uint)data[offset++] << 8;
                                break;
                            case MaxParam - 2:
                                mIntValue = data[offset++];
                                break;
                        }
                        break;
                    case BufferElementType.Single:
                        {
                            mIntValue = data[offset++];
                            mIntValue += (uint)data[offset++] << 8;
                            mIntValue += (uint)data[offset++] << 16;
                            mIntValue += (uint)data[offset++] << 24;
                        }
                        break;
                    case BufferElementType.Int64:
                    case BufferElementType.Double:
                        {
                            mIntValue = data[offset++];
                            mIntValue += (UInt64)data[offset++] << 8;
                            mIntValue += (UInt64)data[offset++] << 16;
                            mIntValue += (UInt64)data[offset++] << 24;
                            mIntValue += (UInt64)data[offset++] << 32;
                            mIntValue += (UInt64)data[offset++] << 40;
                            mIntValue += (UInt64)data[offset++] << 48;
                            mIntValue += (UInt64)data[offset++] << 56;
                        }
                        break;
                    case BufferElementType.Buffer:
                        {
                            int len = data[offset++];
                            len += data[offset++] << 8;
                            len += data[offset++] << 16;

                            IMemoryBufferHolder buffer;
                            if (owner != null && owner.Source != null)
                            {
                                using (var subBuffer = owner.Source.Sub(offset - owner.SourceBaseOffset, len).Own())
                                {
                                    ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(subBuffer.Array, out buffer);
                                }
                            }
                            else
                            {
                                ConcurrentUsageMemoryBufferPool.Instance.AllocateAndDeserialize(new ByteArraySegment(data, offset, len), out buffer);
                            }

                            if (buffer != null)
                            {
                                mObject = buffer;
                            }
                            else
                            {
                                throw new InvalidOperationException();
                            }
                            offset += len;
                        }
                        break;
                    case BufferElementType.Array:
                    case BufferElementType.AbstractArray:
                        {
                            var param = InternalParam;
                            switch (param)
                            {
                                case 0:
                                    mObject = null;
                                    break;
                                case MaxParam:
                                    {
                                        int len = data[offset++];
                                        len += data[offset++] << 8;
                                        len += data[offset++] << 16;

                                        mIntValue = SetArray(offset, len);
                                        mObject = data;
                                        offset += len;
                                    }
                                    break;
                                default:
                                    {
                                        int len = param - 1;
                                        mIntValue = SetArray(offset, len);
                                        mObject = data;
                                        offset += len;
                                    }
                                    break;
                            }
                            if (Type == BufferElementType.AbstractArray)
                            {
                                if (mObject != null)// param != 0
                                {
                                    int size = ArraySize();
                                    if (size > 0)
                                    {
                                        if (owner != null && owner.Source != null)
                                        {
                                            mObject = owner.Source.Sub(ArrayOffset() - owner.SourceBaseOffset, ArraySize());
                                            //mIntValue = 0;
                                        }
                                        else
                                        {
                                            mObject = new ByteArraySegment(data, ArrayOffset(), ArraySize()); // boxing
                                        }
                                    }
                                    else
                                    {
                                        mObject = EmptyByteArray.Instance;
                                        //mIntValue = 0;
                                    }
                                }
                            }
                            else
                            {
                                if (owner != null && owner.Source != null)
                                {
                                    mObject = new ByteArraySegment(data, ArrayOffset(), ArraySize()).Clone();
                                    mIntValue = SetArray(0, ArraySize());
                                    //Log.w("Allocated " + ArraySize() + " bytes");
                                }
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (offset > data.Length)
                {
                    throw new InvalidOperationException();
                }
            }
            catch
            {
                mType = BuildTypeData(BufferElementType.Unknown, 0);
                mIntValue = 0;
                mObject = null;
            }
        }

        public bool TryWriteTo(IByteSink sink)
        {
            try
            {
                sink.Push(mType);
                switch (Type)
                {
                    case BufferElementType.Unknown:
                    case BufferElementType.Bool:
                        // Do nothing
                        break;
                    case BufferElementType.Byte:
                        if (InternalParam == 0)
                        {
                            sink.Push((byte)mIntValue);
                        }
                        break;
                    case BufferElementType.UInt16:
                        switch (InternalParam)
                        {
                            case MaxParam:
                                UInt16 value = (UInt16)mIntValue;
                                sink.Push((byte)((value >> 0) & 0xFF));
                                sink.Push((byte)((value >> 8) & 0xFF));
                                break;
                            case MaxParam - 1:
                                sink.Push((byte)mIntValue);
                                break;
                        }
                        break;
                    case BufferElementType.Int32:
                        {
                            switch (InternalParam)
                            {
                                case MaxParam:
                                    {
                                        UInt32 value = (UInt32)mIntValue;
                                        sink.Push((byte)((value >> 0) & 0xFF));
                                        sink.Push((byte)((value >> 8) & 0xFF));
                                        sink.Push((byte)((value >> 16) & 0xFF));
                                        sink.Push((byte)((value >> 24) & 0xFF));
                                    }
                                    break;
                                case MaxParam - 1:
                                    {
                                        UInt16 value = unchecked((UInt16)(Int16)mIntValue);
                                        sink.Push((byte)((value >> 0) & 0xFF));
                                        sink.Push((byte)((value >> 8) & 0xFF));
                                    }
                                    break;
                                case MaxParam - 2:
                                    sink.Push(unchecked((byte)(sbyte)mIntValue));
                                    break;
                            }
                        }
                        break;
                    case BufferElementType.Single:
                        {
                            UInt32 value = (UInt32)mIntValue;
                            sink.Push((byte)((value >> 0) & 0xFF));
                            sink.Push((byte)((value >> 8) & 0xFF));
                            sink.Push((byte)((value >> 16) & 0xFF));
                            sink.Push((byte)((value >> 24) & 0xFF));
                        }
                        break;
                    case BufferElementType.Int64:
                    case BufferElementType.Double:
                        {
                            sink.Push((byte)((mIntValue >> 0) & 0xFF));
                            sink.Push((byte)((mIntValue >> 8) & 0xFF));
                            sink.Push((byte)((mIntValue >> 16) & 0xFF));
                            sink.Push((byte)((mIntValue >> 24) & 0xFF));
                            sink.Push((byte)((mIntValue >> 32) & 0xFF));
                            sink.Push((byte)((mIntValue >> 40) & 0xFF));
                            sink.Push((byte)((mIntValue >> 48) & 0xFF));
                            sink.Push((byte)((mIntValue >> 56) & 0xFF));
                        }
                        break;
                    case BufferElementType.Buffer:
                        {
                            var buffer = ((IMemoryBufferHolder)mObject).ShowBufferUnsafe();
                            int len = buffer.Size;
                            sink.Push((byte)((len >> 0) & 0xFF));
                            sink.Push((byte)((len >> 8) & 0xFF));
                            sink.Push((byte)((len >> 16) & 0xFF));

                            if (!buffer.TryWriteTo(sink))
                            {
                                throw new InvalidOperationException();
                            }
                        }
                        break;

                    case BufferElementType.Array:
                    case BufferElementType.AbstractArray:
                        {
                            var param = InternalParam;
                            switch (param)
                            {
                                case 0:
                                    // Do nothing
                                    break;
                                case MaxParam:
                                    {
                                        int len = ArraySize();
                                        sink.Push((byte)((len >> 0) & 0xFF));
                                        sink.Push((byte)((len >> 8) & 0xFF));
                                        sink.Push((byte)((len >> 16) & 0xFF));

                                        byte[] bytes = mObject as byte[];
                                        if (bytes != null)
                                        {
                                            sink.Push(new ByteArraySegment(bytes, ArrayOffset(), len));
                                        }
                                        else
                                        {
                                            sink.Push((IMultiRefByteArray)mObject);
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        int len = param - 1;
                                        byte[] bytes = mObject as byte[];
                                        if (bytes != null)
                                        {
                                            sink.Push(new ByteArraySegment(bytes, ArrayOffset(), len));
                                        }
                                        else
                                        {
                                            sink.Push((IMultiRefByteArray)mObject);
                                        }
                                    }
                                    break;
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void WriteTo(System.Text.StringBuilder sb)
        {
            sb.Append(Type.ToString());
            switch (Type)
            {
                case BufferElementType.Unknown:
                case BufferElementType.Bool:
                    // Do nothing
                    break;
                case BufferElementType.Byte:
                    sb.Append(": ");
                    if (InternalParam == 0)
                    {
                        sb.Append(mIntValue.ToString());
                    }
                    else
                    {
                        sb.Append(InternalParam - 1);
                    }
                    break;
                case BufferElementType.UInt16:
                    sb.Append(": ");
                    switch (InternalParam)
                    {
                        case MaxParam:
                        case MaxParam - 1:
                            sb.Append(mIntValue.ToString());
                            break;
                        default:
                            sb.Append(InternalParam.ToString());
                            break;
                    }
                    break;
                case BufferElementType.Int32:
                    sb.Append(": ");
                    switch (InternalParam)
                    {
                        case MaxParam:
                            sb.Append(unchecked((Int32)mIntValue).ToString());
                            break;
                        case MaxParam - 1:
                            sb.Append(unchecked((Int16)mIntValue).ToString());
                            break;
                        case MaxParam - 2:
                            sb.Append(unchecked((sbyte)mIntValue).ToString());
                            break;
                        default:
                            sb.Append((InternalParam - 1).ToString());
                            break;
                    }
                    break;
                case BufferElementType.Int64:
                    sb.Append(": ");
                    sb.Append(mIntValue.ToString());
                    break;
                case BufferElementType.Single:
                    {
                        var union = new Serializer.BinarySerializer.ManagedUnions4();
                        union.uintValue = (uint)mIntValue;
                        sb.Append(": ");
                        sb.Append(union.floatValue.ToString());
                    }
                    break;
                case BufferElementType.Double:
                    {
                        var union = new Serializer.BinarySerializer.ManagedUnions8();
                        union.ulongValue = mIntValue;
                        sb.Append(": ");
                        sb.Append(union.doubleValue.ToString());
                    }
                    break;
                case BufferElementType.Buffer:
                    sb.Append(": ");
                    sb.Append(((IMemoryBufferHolder)mObject).ToString());
                    break;
                case BufferElementType.Array:
                    sb.Append(": ");
                    sb.Append(new ByteArraySegment((byte[])mObject, ArrayOffset(), ArraySize()).ToString());
                    break;
                case BufferElementType.AbstractArray:
                    sb.Append(": ");
                    sb.Append(mObject.ToString());
                    break;
            }
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            WriteTo(sb);
            return sb.ToString();
        }

        public bool AsBoolean(out Boolean value)
        {
            if (Type == BufferElementType.Bool)
            {
                var param = InternalParam;
                value = param != 0;
                return param < 2;
            }

            Clear();
            value = false;
            return false;
        }

        public bool AsByte(out Byte value)
        {
            if (Type != BufferElementType.Byte)
            {
                Clear();
                value = default(Byte);
                return false;
            }

            var param = InternalParam;
            if (param == 0)
            {
                value = (Byte)mIntValue;
            }
            else
            {
                value = (byte)(param - 1);
            }

            return true;
        }

        public bool AsUInt16(out UInt16 value)
        {
            if (Type != BufferElementType.UInt16)
            {
                Clear();
                value = default(UInt16);
                return false;
            }

            switch (InternalParam)
            {
                case MaxParam:
                case MaxParam - 1:
                    value = (UInt16)mIntValue;
                    break;
                default:
                    value = (UInt16)InternalParam;
                    break;
            }

            return true;
        }

        public bool AsInt32(out Int32 value)
        {
            if (Type != BufferElementType.Int32)
            {
                Clear();
                value = default(Int32);
                return false;
            }

            switch (InternalParam)
            {
                case MaxParam:
                    value = unchecked((Int32)mIntValue);
                    break;
                case MaxParam - 1:
                    value = unchecked((Int16)mIntValue);
                    break;
                case MaxParam - 2:
                    value = unchecked((sbyte)mIntValue);
                    break;
                default:
                    value = InternalParam - 1;
                    break;
            }
            return true;
        }

        public bool AsInt64(out Int64 value)
        {
            if (Type != BufferElementType.Int64)
            {
                Clear();
                value = default(Int64);
                return false;
            }
            value = unchecked((Int64)mIntValue);
            return true;
        }

        public bool AsSingle(out Single value)
        {
            if (Type != BufferElementType.Single)
            {
                Clear();
                value = default(Single);
                return false;
            }

            var union = new Serializer.BinarySerializer.ManagedUnions4();
            union.uintValue = (UInt32)mIntValue;
            value = union.floatValue;

            return true;
        }

        public bool AsDouble(out Double value)
        {
            if (Type != BufferElementType.Double)
            {
                Clear();
                value = default(Double);
                return false;
            }

            var union = new Serializer.BinarySerializer.ManagedUnions8();
            union.ulongValue = mIntValue;
            value = union.doubleValue;

            return true;
        }

        public bool AsBuffer(out IMemoryBufferHolder buffer)
        {
            if (Type != BufferElementType.Buffer)
            {
                Clear();
                buffer = default(IMemoryBufferHolder);
                return false;
            }

            buffer = mObject as IMemoryBufferHolder;
            mObject = null;
            return true;
        }

        public bool AsArray(out ByteArraySegment value)
        {
            if (Type != BufferElementType.Array)
            {
                Clear();
                value = default(ByteArraySegment);
                return false;
            }

            value = InternalParam == 0 ? default(ByteArraySegment) : new ByteArraySegment(mObject as byte[], ArrayOffset(), ArraySize());
            mObject = null;
            return true;
        }

        public bool AsAbstractArray(out IMultiRefByteArray value)
        {
            if (Type != BufferElementType.AbstractArray)
            {
                Clear();
                value = null;
                return false;
            }

            value = InternalParam == 0 ? VoidByteArray.Instance : mObject as IMultiRefByteArray;
            mObject = null;
            return true;
        }

        public void Clear()
        {
            switch (Type)
            {
                case BufferElementType.Buffer:
                    var buffer = mObject as IMemoryBufferHolder;
                    if (buffer != null)
                    {
                        buffer.Release();
                    }

                    mObject = null;
                    break;
                case BufferElementType.AbstractArray:
                    var array = mObject as IMultiRefByteArray;
                    if (array != null)
                    {
                        array.Release();
                    }

                    mObject = null;
                    break;
            }
        }
    }
}
