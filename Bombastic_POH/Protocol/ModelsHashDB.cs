using System;
using System.Collections;
using System.Collections.Generic;

namespace NewProtocol
{
    public interface IModelsHashDB
    {
        bool TryGetHash(Type type, out string hash);
    }

    public class VoidHashDB : IModelsHashDB
    {
        public static readonly VoidHashDB Instance = new VoidHashDB();

        public bool TryGetHash(Type type, out string hash)
        {
            hash = "";
            return true;
        }
    }

    public sealed class ModelsHashDB : IModelsHashDB, IEnumerable<KeyValuePair<string, string>>
    {
        private readonly Dictionary<string, string> mHashes = new Dictionary<string, string>();

        public int Count
        {
            get { return mHashes.Count; }
        }

        public bool Add(string typeName, string typeHash)
        {
            if (!mHashes.ContainsKey(typeName))
            {
                mHashes.Add(typeName, typeHash);
                return true;
            }

            return false;
        }

        public bool TryGetHash(Type type, out string hash)
        {
            return mHashes.TryGetValue(type.ToString(), out hash);
        }

        public bool TryGetHash(string typeName, out string hash)
        {
            return mHashes.TryGetValue(typeName, out hash);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return mHashes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
