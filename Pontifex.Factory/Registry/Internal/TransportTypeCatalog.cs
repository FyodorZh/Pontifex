using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Pontifex;
using Pontifex.Factory.Internal;

namespace Pontifex.Factory.Registry.Internal
{
    // Immutable transport-type catalog. Names are stored with the caller-supplied
    // canonical spelling; all lookup is ASCII case-insensitive. Entries are
    // validated by TransportRegistryBuilder before this snapshot is created.
    internal sealed class TransportTypeCatalog : ITransportTypeCatalog
    {
        private readonly ReadOnlyCollection<TransportTypeInfo> _types;
        private readonly ReadOnlyCollection<TransportType> _allTypes;
        private readonly Dictionary<string, TransportTypeInfo> _byName;
        private readonly Dictionary<TransportType, string> _canonicalNames;

        public TransportTypeCatalog(IReadOnlyList<TransportTypeInfo> types)
        {
            var entries = new List<TransportTypeInfo>(types ?? Array.Empty<TransportTypeInfo>());
            _types = entries.AsReadOnly();
            _byName = new Dictionary<string, TransportTypeInfo>(StringComparer.Ordinal);
            _canonicalNames = new Dictionary<TransportType, string>();

            var allTypes = new List<TransportType>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                _byName[NameNormalizer.Normalize(entry.CanonicalName)] = entry;

                // "AllTypes" is the set of catalog types; a repeated type value
                // keeps its first canonical name and registration position.
                if (!_canonicalNames.ContainsKey(entry.Type))
                {
                    _canonicalNames.Add(entry.Type, entry.CanonicalName);
                    allTypes.Add(entry.Type);
                }
            }

            _allTypes = allTypes.AsReadOnly();
        }

        public IReadOnlyList<TransportTypeInfo> Types => _types;

        public IReadOnlyList<TransportType> AllTypes => _allTypes;

        public bool TryResolve(string name, out TransportTypeInfo? type)
        {
            if (name is not null
                && _byName.TryGetValue(NameNormalizer.Normalize(name), out var resolved))
            {
                type = resolved;
                return true;
            }

            type = null;
            return false;
        }

        public bool TryGetCanonicalName(TransportType type, out string? canonicalName)
        {
            if (_canonicalNames.TryGetValue(type, out var resolved))
            {
                canonicalName = resolved;
                return true;
            }

            canonicalName = null;
            return false;
        }

        public string GetCanonicalName(TransportType type)
        {
            if (TryGetCanonicalName(type, out var canonicalName))
            {
                return canonicalName!;
            }

            // The contract requires callers to ask only for catalog types.
            throw new InvalidOperationException(
                "transport type " + type + " is not present in the type catalog");
        }
    }
}
