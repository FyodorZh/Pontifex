using System;

namespace NewProtocol
{
    public interface IModelsHashDB
    {
        bool TryGetHash(Type type, out string hash);
    }
}