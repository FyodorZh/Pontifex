using Shared;
using System;
using System.Collections.Generic;

namespace SharedCode.Shared
{
    public sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] first, byte[] second)
        {
            if (first == second)
            {
                return true;
            }
            if (first == null || second == null)
            {
                return false;
            }
            if (first.Length != second.Length)
            {
                return false;
            }
            for (int i = 0; i < first.Length; i++)
            {
                if (!(first[i] == second[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(byte[] array)
        {
            return CalcHashCode(array);
        }

        public static int CalcHashCode(byte[] array)
        {
            unchecked
            {
                if (array == null)
                {
                    return 0;
                }
                int hash = 17;
                for (int i = 0, c = array.Length; i < c; i++)
                {
                    hash = hash * 31 + array[i];
                }
                return hash;
            }
        }
    }
}
