using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SDILReader;
using Serializer.BinarySerializer;
using Shared;

namespace NewProtocol
{
    public static class HashDbBuilder
    {
        private static readonly Type mIDataStructType = typeof(IDataStruct);
        
        public static void Append(this ModelsHashDB self, Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                self.Append(type);
            }
        }

        public static void Append<TProtocol>(this ModelsHashDB self)
            where TProtocol: Protocol, new()
        {
            var protocolInfo = Protocol.GetProtocolInfo<TProtocol>(VoidHashDB.Instance);
            foreach (var model in protocolInfo.FactoryModels)
            {
                self.Append(model);
            }
            foreach (var model in protocolInfo.NonFactoryModels)
            {
                self.Append(model);
            }
        }

        public static void Append(this ModelsHashDB self, Type type)
        {
            if (type.IsGenericTypeDefinition)
            {
                List<Type> list = new List<Type>();
                for (int i = 0; i < type.GetGenericArguments().Length; ++i)
                {
                    list.Add(typeof(object));
                }
                type = type.MakeGenericType(list.ToArray());
            }

            if (type.IsInterface || type.IsAbstract)
            {
                return;
            }

            Type[] interfaces = type.FindInterfaces((tp, obj) => tp == mIDataStructType, type);

            if (interfaces.Length == 1)
            {
                Type[] pt = { typeof(IBinarySerializer) };
                MethodInfo method = type.GetMethod("Serialize", pt);
                if (method != null)
                {
                    MethodBodyReader reader = new MethodBodyReader(method);
                    string bodyCode = reader.GetBodyCode();
                    byte[] bodyCodeBytes = Encoding.UTF8.GetBytes(bodyCode);
                    string md5Hash = MD5Helper.GetMd5Hash(bodyCodeBytes);

                    if (type.IsGenericType)
                    {
                        type = type.GetGenericTypeDefinition();
                    }
                    self.Add(type.ToString(), md5Hash);
                }
            }
        }
    }
}
