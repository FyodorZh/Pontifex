using System;
using System.Collections.Generic;
using System.Reflection;

namespace Serializer.Factory
{
    public class RegularModelFactory : IConcurrentDataStructFactory
    {
        private static readonly Type[] mStaticConstructorTypes = { };
        private static readonly object[] mStaticConstructorParams = { };

        private readonly int mCount;
        private readonly ConstructorInfo[] mConstructors;
        private readonly Dictionary<Type, short> mIndex;

        public RegularModelFactory(Type[] models)
        {
            mCount = models.Length;
            mConstructors = new ConstructorInfo[mCount];
            mIndex = new Dictionary<Type, short>();

            for (int i = 0; i < mCount; ++i)
            {
                ConstructorInfo constructInfo = models[i].GetConstructor(mStaticConstructorTypes);
                if (constructInfo == null)
                {
                    throw new InvalidOperationException(string.Format("Type {0} has no default constructor", models[i]));
                }
                mConstructors[i] = constructInfo;

                mIndex.Add(models[i], (short)i);
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
            short modelId;
            if (!mIndex.TryGetValue(modelType, out modelId))
            {
                modelId = -1;
            }
            return modelId;
        }

        bool IDataStructFactory.SerializeModelIndex()
        {
            return true;
        }
    }
}
