using System;
using Geom3d;

namespace Shared.Battle
{
    public static class ArrayExtensions
    {
        public static int Sum(this int[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            int result = 0;
            for (int i = 0; i < source.Length; i++)
            {
                result += source[i];
            }
            return result;
        }

        public static int Mult(this int[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            int result = 1;
            for (int i = 0; i < source.Length; i++)
            {
                result *= source[i];
            }
            return result;
        }

        public static int Max(this int[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            int num1 = 0;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                int num2 = source[i];
                if (flag)
                {
                    if (num2 > num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning int.MaxValue");
            return int.MaxValue;
        }

        public static int Min(this int[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            int num1 = 0;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                int num2 = source[i];
                if (flag)
                {
                    if (num2 < num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning int.MinValue");
            return int.MinValue;
        }

        public static byte Sum(this byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            byte result = 0;
            for (int i = 0; i < source.Length; i++)
            {
                result += source[i];
            }
            return result;
        }

        public static byte Mult(this byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            byte result = 1;
            for (int i = 0; i < source.Length; i++)
            {
                result *= source[i];
            }
            return result;
        }

        public static byte Max(this byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            byte num1 = 0;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                byte num2 = source[i];
                if (flag)
                {
                    if (num2 > num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning int.MaxValue");
            return byte.MaxValue;
        }

        public static byte Min(this byte[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            byte num1 = 0;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                byte num2 = source[i];
                if (flag)
                {
                    if (num2 < num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning int.MinValue");
            return byte.MinValue;
        }

        public static float Sum(this float[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            float result = 0;
            for (int i = 0; i < source.Length; i++)
            {
                result += source[i];
            }
            return result;
        }

        public static float Mult(this float[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            float result = 1;
            for (int i = 0; i < source.Length; i++)
            {
                result *= source[i];
            }
            return result;
        }

        public static float Max(this float[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            float num1 = 0;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                float num2 = source[i];
                if (flag)
                {
                    if (num2 > num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning float.MaxValue");
            return float.MaxValue;
        }

        public static float Min(this float[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            float num1 = 0;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                float num2 = source[i];
                if (flag)
                {
                    if (num2 < num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning float.MinValue");
            return float.MinValue;
        }

        public static DeltaTime Sum(this DeltaTime[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            DeltaTime result = DeltaTime.Zero;
            for (int i = 0; i < source.Length; i++)
            {
                DeltaTime item = source[i];
                if (item == DeltaTime.Infinity)
                {
                    return DeltaTime.Infinity;
                }
                result += item;
            }
            return result;
        }

        public static DeltaTime Mult(this DeltaTime[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            double result = 1;
            for (int i = 0; i < source.Length; i++)
            {
                DeltaTime item = source[i];
                if (item == DeltaTime.Infinity)
                {
                    return DeltaTime.Infinity;
                }
                result *= item.Seconds;
            }
            return DeltaTime.FromSeconds(result);
        }

        public static DeltaTime Max(this DeltaTime[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            DeltaTime num1 = DeltaTime.Zero;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                DeltaTime num2 = source[i];
                if (flag)
                {
                    if (num2 > num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning DeltaTime.Zero");
            return DeltaTime.Zero;
        }

        public static DeltaTime Min(this DeltaTime[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            DeltaTime num1 = DeltaTime.Zero;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                DeltaTime num2 = source[i];
                if (flag)
                {
                    if (num2 < num1)
                    {
                        num1 = num2;
                    }
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning DeltaTime.Zero");
            return DeltaTime.Zero;
        }

        public static Vector3 Sum(this Vector3[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            Vector3 result = Vector3.zero;
            for (int i = 0; i < source.Length; i++)
            {
                result += source[i];
            }
            return result;
        }

        public static Vector3 Mult(this Vector3[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            Vector3 result = Vector3.one;
            for (int i = 0; i < source.Length; i++)
            {
                result *= source[i];
            }
            return result;
        }

        public static Vector3 Max(this Vector3[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            Vector3 num1 = Vector3.zero;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 num2 = source[i];
                if (flag)
                {
                    num1 = Vector3.Max(num1, num2);
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning Vector3.zero");
            return Vector3.zero;
        }

        public static Vector3 Min(this Vector3[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            Vector3 num1 = Vector3.zero;
            bool flag = false;
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 num2 = source[i];
                if (flag)
                {
                    num1 = Vector3.Min(num1, num2);
                }
                else
                {
                    flag = true;
                    num1 = num2;
                }
            }
            if (flag)
            {
                return num1;
            }
            Log.e("List is empty, returning Vector3.zero");
            return Vector3.zero;
        }
    }
}