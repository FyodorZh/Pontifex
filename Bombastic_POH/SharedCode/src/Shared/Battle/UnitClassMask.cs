using System;
using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared.Battle
{
    public struct UnitClassValue : IDataStruct
    {
        private UnitClassType _value;

        public UnitClassType Value
        {
            get { return _value; }
        }

        public static UnitClassValue Unknown
        {
            get
            {
                return new UnitClassValue(UnitClassMask.Unknown);
            }
        }

        public static UnitClassValue UnitMask
        {
            get
            {
                return new UnitClassValue(UnitClassMask.Unit);
            }
        }     
   
        public static UnitClassValue PseudoUnitMask
        {
            get
            {
                return new UnitClassValue(UnitClassMask.PseudoUnit);
            }
        }

        public static UnitClassValue UnitAndNotAUnitMask
        {
            get
            {
                return new UnitClassValue(UnitClassMask.Unit | UnitClassMask.NotAUnit);
            }
        }

        public UnitClassValue(UnitClassValue value)
        {
            _value = value.Value;
        }

        public UnitClassValue(UnitClassType value)
        {
            _value = value;
        }

        public UnitClassValue(UnitClassMask value)
        {
            _value = (UnitClassType)value;
        }

        public UnitClassValue(byte value)
        {
            _value = (UnitClassType)value;
        }

        public bool IsAlive
        {
            get
            {
                return !Matches(UnitClassType.Corpse, true);
            }
        }

        public bool Matches(UnitClassMask value, bool absolute = false)
        {
            if (absolute)
            {
                return (_value & (UnitClassType)value) == (UnitClassType)value;
            }
            else
            {
                return (_value & (UnitClassType)value) > 0;
            }
        }

        public bool Matches(UnitClassValue value, bool absolute = false)
        {
            if (absolute)
            {
                return (_value & value.Value) == value.Value;
            }
            else
            {
                return (_value & value.Value) > 0;
            }
        }

        public bool Matches(UnitClassType value, bool absolute = false)
        {
            if (absolute)
            {
                return (_value & value) == value;
            }
            else
            {
                return (_value & value) > 0;
            }
        }

        public string[] GetNames(ref int[] classIds)
        {
            List<string> result = new List<string>();
            List<int> ids = new List<int>();
            UnitClassType[] types = (UnitClassType[])Enum.GetValues(typeof(UnitClassType));
            {
                for (int i = 0; i < types.Length; ++i)
                {
                    if (types[i] == UnitClassType.Unknown)
                    {
                        continue;
                    }

                    if (Matches(types[i]))
                    {
                        UnitClassType type = types[i];
                         result.Add(type.ToString());
                         ids.Add((int)type);
                    }
                }
            }
            classIds = ids.ToArray();
            return result.ToArray();
        }

        public static UnitClassValue operator |(UnitClassValue maskLeft, UnitClassValue maskRight)
        {
            var resultMask = maskLeft.Value | maskRight.Value;
            return new UnitClassValue(resultMask);
        }

        public static UnitClassValue operator |(UnitClassValue maskLeft, UnitClassMask unitClassType)
        {
            var resultMask = maskLeft.Value | (UnitClassType)unitClassType;
            return new UnitClassValue(resultMask);
        }

        public static UnitClassValue operator |(UnitClassMask unitClassType, UnitClassValue mask)
        {
            var resultMask = mask.Value | (UnitClassType)unitClassType;
            return new UnitClassValue(resultMask);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            short value = (short)_value;
            dst.Add(ref value);
            _value = (UnitClassType)value;
            return true;
        }
    }
}