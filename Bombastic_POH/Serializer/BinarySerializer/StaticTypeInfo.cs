using System;
using System.Reflection;

namespace Serializer.BinarySerializer
{
    internal static class StaticTypeInfo
    {
        public static readonly Type[] StaticConstructorTypes = new Type[] { };
        public static readonly object[] StaticConstructorParams = new object[] { };
    }

    // ReSharper disable StaticMemberInGenericType
    public static class StaticTypeInfo<T>
    {
        public static readonly bool IsValueType;
        private static readonly ConstructorInfo mDefaultClassConstructor;

        static StaticTypeInfo()
        {
            Type type = typeof(T);

            IsValueType = type.IsValueType;
            if (!IsValueType)
            {
                mDefaultClassConstructor = type.GetConstructor(StaticTypeInfo.StaticConstructorTypes);
            }
        }

        public static T New()
        {
            if (mDefaultClassConstructor != null)
            {
                return (T)mDefaultClassConstructor.Invoke(StaticTypeInfo.StaticConstructorParams);
            }
            return default(T);
        }
    }
}
