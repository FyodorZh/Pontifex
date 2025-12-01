using System;

namespace NewProtocol
{
    public class VoidHashDB : IModelsHashDB
    {
        public static readonly VoidHashDB Instance = new VoidHashDB();

        public bool TryGetHash(Type type, out string hash)
        {
            hash = "";
            return true;
        }
    }
}