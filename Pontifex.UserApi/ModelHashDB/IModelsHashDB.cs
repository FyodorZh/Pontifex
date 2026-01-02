using System;

namespace Pontifex.UserApi
{
    public interface IModelsHashDB
    {
        bool TryGetHash(Type type, out string hash);
    }
}