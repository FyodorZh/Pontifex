using System;
using System.Reflection;

namespace Serializer.Factory
{
    public class ModelTinyFactory : IConcurrentDataStructFactory
    {
        private static readonly Type[] mStaticConstructorTypes = { };
        private static readonly object[] mStaticConstructorParams = { };

        private readonly int mCount;
        private readonly Type[] mTypes;
        private readonly ConstructorInfo[] mConstructors;

        public ModelTinyFactory(Type[] models)
        {
            mCount = models.Length;
            mTypes = models;
            mConstructors = new ConstructorInfo[mCount];

            for (int i = 0; i < mCount; ++i)
            {
                ConstructorInfo constructInfo = mTypes[i].GetConstructor(mStaticConstructorTypes);
                if (constructInfo == null)
                {
                    throw new InvalidOperationException(string.Format("Type {0} has no default constructor", mTypes[i]));
                }

                mConstructors[i] = constructInfo;
            }

            if (mCount > 10)
            {
                Log.w("Too many models ({0}) in ModelTinyFactory. Performance issues are possible. Consider to replace it with {1}", mCount, typeof(RegularModelFactory));
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
                if (mTypes[i] == modelType)
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
