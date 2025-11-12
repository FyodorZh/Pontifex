using System.Collections.Generic;
using System.Linq;
using Serializer.BinarySerializer;

namespace Serializer.Extensions
{
    public static class ListTypesExtensions
    {
        public static void AddList(this IBinarySerializer serializer, ref List<long> value)
        {
            var listAsArray = value.ToArray();
            serializer.Add(ref listAsArray);
            value = listAsArray == null
                ? new List<long>()
                : listAsArray.ToList();
        }

        public static void AddList(this IBinarySerializer serializer, ref List<short> value)
        {
            var listAsArray = value.ToArray();
            serializer.Add(ref listAsArray);
            value = listAsArray == null
                ? new List<short>()
                : listAsArray.ToList();
        }

        public static void AddList(this IBinarySerializer serializer, ref List<int> value)
        {
            var listAsArray = value.ToArray();
            serializer.Add(ref listAsArray);
            value = listAsArray == null
                ? new List<int>()
                : listAsArray.ToList();
        }
    }
}
