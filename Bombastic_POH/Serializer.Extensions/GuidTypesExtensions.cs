using System;
using Serializer.BinarySerializer;

namespace Serializer.Extensions
{
    public static class GuidTypesExtensions
    {
        public static void AddGuid(this IBinarySerializer serializer, ref Guid v)
        {
            var guidBytes = v.ToByteArray();
            serializer.Add(ref guidBytes);
            if (serializer.isReader)
            {
                v = new Guid(guidBytes);
            }
        }
    }
}
