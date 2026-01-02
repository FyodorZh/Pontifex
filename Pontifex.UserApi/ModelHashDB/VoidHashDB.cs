using System;

namespace Pontifex.UserApi
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