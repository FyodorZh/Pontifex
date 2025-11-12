using System;

namespace Serializer.Factory
{
    public class TrivialFactory : IDataStructFactory
    {
        public static readonly TrivialFactory Instance = new TrivialFactory();
        
        bool IDataStructFactory.SerializeModelIndex()
        {
            return false;
        }

        short IDataStructFactory.GetIndexForModel(Type modelType)
        {
            return -1;
        }

        object IDataStructFactory.CreateDataStruct(short modelIndex)
        {
            return null;
        }
    }
}
