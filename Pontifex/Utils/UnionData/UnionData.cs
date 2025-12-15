using System;
using System.Globalization;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    public struct UnionData : IEquatable<UnionData>
    {
        private UnionDataType _type;
        private UnionDataMemoryAlias _alias;
        private IMultiRefReadOnlyByteArray? _bytes;
        
        public UnionDataType Type => _type;
        public UnionDataMemoryAlias Alias => _alias;
        public IMultiRefReadOnlyByteArray? Bytes => _bytes;
        
        public UnionData(bool value) 
        {
            _type = UnionDataType.Bool;
            _alias = value;
            _bytes = null;
        }
        
        public UnionData(byte value)
        {
            _type = UnionDataType.Byte;
            _alias = value;
            _bytes = null;
        }

        public UnionData(Char value)
        {
            _type = UnionDataType.Char;
            _alias = value;
            _bytes = null;
        }

        public UnionData(short value)
        {
            _type = UnionDataType.Short;
            _alias = value;
            _bytes = null;
        }
        
        public UnionData(ushort value)
        {
            _type = UnionDataType.UShort;
            _alias = value;
            _bytes = null;
        }

        public UnionData(int value)
        {
            _type = UnionDataType.Int;
            _alias = value;
            _bytes = null;
        }
        
        public UnionData(uint value)
        {
            _type = UnionDataType.UInt;
            _alias = value;
            _bytes = null;
        }

        public UnionData(long value)
        {
            _type = UnionDataType.Long;
            _alias = value;
            _bytes = null;
        }
        
        public UnionData(ulong value)
        {
            _type = UnionDataType.ULong;
            _alias = value;
            _bytes = null;
        }

        public UnionData(float value)
        {
            _type = UnionDataType.Float;
            _alias = value;
            _bytes = null;
        }

        public UnionData(double value)
        {
            _type = UnionDataType.Double;
            _alias = value;
            _bytes = null;
        }
        
        public UnionData(decimal value)
        {
            _type = UnionDataType.Decimal;
            _alias = value;
            _bytes = null;
        }

        public UnionData(IMultiRefReadOnlyByteArray? value)
        {
            _type = value != null ? UnionDataType.Array : UnionDataType.NullArray;
            _alias = 0;
            _bytes = value;
        }
        
        public static implicit operator UnionData(bool value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(byte value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(char value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(short value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(ushort value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(int value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(uint value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(long value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(ulong value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(float value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(double value)
        {
            return new UnionData(value);
        }
        public static implicit operator UnionData(decimal value)
        {
            return new UnionData(value);
        }
        
        public bool Equals(UnionData other)
        {
            if (_type != other._type)
            {
                return false;
            }
            
            switch (_type)
            {
                case UnionDataType.Bool:
                case UnionDataType.Byte:
                    return _alias.Equals1(other._alias);
                case UnionDataType.Char:
                case UnionDataType.Short:
                case UnionDataType.UShort:
                    return _alias.Equals2(other._alias);
                case UnionDataType.Int:
                case UnionDataType.UInt:
                case UnionDataType.Float:
                    return _alias.Equals4(other._alias);
                case UnionDataType.Long:
                case UnionDataType.ULong:
                case UnionDataType.Double:
                    return _alias.Equals8(other._alias);
                case UnionDataType.Decimal: 
                    return _alias.Equals16(other._alias);
                case UnionDataType.Array:
                    return Bytes.EqualByContent(other.Bytes!);
                case UnionDataType.NullArray:
                    return true;
                default: 
                    return false;
            }
        }
        
        public int GetDataSize()
        {
            switch (_type)
            {
                case UnionDataType.Bool:
                case UnionDataType.Byte:
                    return 1 + 1;
                case UnionDataType.Char:
                case UnionDataType.Short:
                case UnionDataType.UShort:
                    return 1 + 2;
                case UnionDataType.Int:
                case UnionDataType.UInt:
                case UnionDataType.Float:
                    return 1 + 4;
                case UnionDataType.Long:
                case UnionDataType.ULong:
                case UnionDataType.Double:
                    return 1 + 8;
                case UnionDataType.Decimal:
                    return 1 + 16;
                case UnionDataType.Array:
                    return 1 + 4 + Bytes!.Count;
                case UnionDataType.NullArray:
                    return 1;
                default:
                    throw new InvalidOperationException();
            }
        }
        
        public void WriteTo<TByteSink>(ref TByteSink byteSink)
            where TByteSink : IByteSink
        {
            byteSink.Put((byte)_type);
            switch (_type)
            {
                case UnionDataType.Bool:
                case UnionDataType.Byte:
                    byteSink.Put(_alias.Byte0);
                    return;
                case UnionDataType.Char:
                case UnionDataType.Short:
                case UnionDataType.UShort:
                    _alias.WriteTo2(ref byteSink);
                    return;
                case UnionDataType.Int:
                case UnionDataType.UInt:
                case UnionDataType.Float:
                    _alias.WriteTo4(ref byteSink);
                    return;
                case UnionDataType.Long:
                case UnionDataType.ULong:
                case UnionDataType.Double:
                    _alias.WriteTo8(ref byteSink);
                    return;
                case UnionDataType.Decimal:
                    _alias.WriteTo16(ref byteSink);
                    return;
                case UnionDataType.Array:
                {
                    UnionDataMemoryAlias size = _bytes!.Count;
                    size.WriteTo4(ref byteSink);
                    byteSink.PutMany(_bytes);
                }
                    return;
                case UnionDataType.NullArray:
                    return;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static bool ReadFrom<TByteSource>(ref TByteSource bytes, IPool<IMultiRefByteArray, int> pool, out UnionData unionData)
            where TByteSource : IByteSource
        {
            unionData = new UnionData();
            if (!bytes.TryPop(out var typeByte))
                return false;
            unionData._type = (UnionDataType)typeByte;
            UnionDataMemoryAlias alias = new();
            switch (unionData._type)
            {
                case UnionDataType.Bool:
                case UnionDataType.Byte:
                    if (alias.ReadFrom1(ref bytes))
                    {
                        unionData._alias = alias;
                        return true;
                    }
                    return false;
                case UnionDataType.Char:
                case UnionDataType.Short:
                case UnionDataType.UShort:
                    if (alias.ReadFrom2(ref bytes))
                    {
                        unionData._alias = alias;
                        return true;
                    }
                    return false;
                case UnionDataType.Int:
                case UnionDataType.UInt:
                case UnionDataType.Float:
                    if (alias.ReadFrom4(ref bytes))
                    {
                        unionData._alias = alias;
                        return true;
                    }
                    return false;
                case UnionDataType.Long:
                case UnionDataType.ULong:
                case UnionDataType.Double:
                    if (alias.ReadFrom8(ref bytes))
                    {
                        unionData._alias = alias;
                        return true;
                    }
                    return false;
                case UnionDataType.Decimal:
                    if (alias.ReadFrom16(ref bytes))
                    {
                        unionData._alias = alias;
                        return true;
                    }
                    return false;
                case UnionDataType.Array:
                {
                    UnionDataMemoryAlias size = new();
                    if (!size.ReadFrom4(ref bytes))
                    {
                        return false;
                    }

                    var buffer = pool.Acquire(size.IntValue);
                    if (!bytes.TakeMany(buffer))
                    {
                        return false;
                    }

                    unionData._bytes = buffer;
                    return true;
                }
                case UnionDataType.NullArray:
                    return true;
                default:
                    return false;
            }
            
        }
        
        public string ValueToString()
        {
            switch (_type)
            {
                case UnionDataType.Bool: return _alias.BoolValue.ToString();
                case UnionDataType.Byte: return _alias.ByteValue.ToString();
                case UnionDataType.Char: return _alias.CharValue.ToString();
                case UnionDataType.Short: return _alias.ShortValue.ToString();
                case UnionDataType.Int: return _alias.IntValue.ToString();
                case UnionDataType.Long: return _alias.LongValue.ToString();
                case UnionDataType.Float: return _alias.FloatValue.ToString(CultureInfo.InvariantCulture);
                case UnionDataType.Double: return _alias.DoubleValue.ToString(CultureInfo.InvariantCulture);
                case UnionDataType.Decimal: return _alias.DecimalValue.ToString(CultureInfo.InvariantCulture);
                case UnionDataType.Array: return Bytes != null ? ("[" + string.Join(",", Bytes.Enumerate()) + "]") : "null";
                case UnionDataType.NullArray: return "null";
                default: return "INVALID";
            }
        }

        public override string ToString()
        {
            return _type + ":" + ValueToString();
        }
    }
}