using System;
using System.Reflection;

namespace Serializer.Factory
{
    public struct TypePair
    {
        public readonly Type WriteType;
        public readonly Type ReadType;

        public static implicit operator TypePair(Type wt) { return new TypePair(wt); }

        public TypePair(Type wt) : this(wt, wt) { }

        public TypePair(Type wt, Type rt)
        {
            WriteType = wt;
            ReadType = rt;
        }
    }

    public class ModelWrapperTinyFactory : IDataStructFactory
    {
        private readonly Type[] mStaticConstructorTypes = { };
        private readonly object[] mStaticConstructorParams = { };

        private readonly int mCount;
        private readonly TypePair[] mTypes;
        private readonly ConstructorInfo[] mConstructors;
        public ModelWrapperTinyFactory(TypePair[] models)
        {
            mCount = models.Length;
            mTypes = models;
            mConstructors = new ConstructorInfo[mCount];

            for (int i = 0; i < mCount; ++i)
            {
                ConstructorInfo constructInfo = mTypes[i].ReadType.GetConstructor(mStaticConstructorTypes);
                if (constructInfo == null)
                {
                    throw new InvalidOperationException(string.Format("Type {0} has no default constructor", mTypes[i].ReadType));
                }

                mConstructors[i] = constructInfo;
            }
        }

        object IDataStructFactory.CreateDataStruct(short modelIndex)
        {
            if (modelIndex >= 0 && modelIndex < mCount)
            {
                return mConstructors[modelIndex].Invoke(mStaticConstructorParams);
            }
            return null;
        }

        short IDataStructFactory.GetIndexForModel(Type modelType)
        {
            for (int i = 0; i < mCount; ++i)
            {
                if (mTypes[i].WriteType == modelType)
                {
                    return (short)i;
                }
            }
            return -1;
        }

        bool IDataStructFactory.SerializeModelIndex()
        {
            return true;
        }
    }
}
