using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pontifex.TransportFormula.Registry.Internal
{
    // Immutable registry snapshot. Lookups take an already-normalized lowercase
    // scheme and never normalize again, filter by expected type, or prefer the
    // canonical constructor; those are semantics concerns.
    internal sealed class TransportRegistry : IRegistry
    {
        private readonly ITransportTypeCatalog _types;
        private readonly ReadOnlyCollection<TransportConstructorInfo> _constructors;
        private readonly Dictionary<string, TransportConstructorInfo> _canonical;
        private readonly Dictionary<string, ReadOnlyCollection<TransportConstructorInfo>> _aliases;
        private readonly ReadOnlyCollection<string> _allSchemeNames;

        public TransportRegistry(
            ITransportTypeCatalog types,
            IReadOnlyList<TransportConstructorInfo> constructors,
            IReadOnlyDictionary<string, TransportConstructorInfo> canonical,
            IReadOnlyDictionary<string, IReadOnlyList<TransportConstructorInfo>> aliases)
        {
            _types = types ?? throw new ArgumentNullException(nameof(types));

            var constructorList = new List<TransportConstructorInfo>(constructors);
            _constructors = constructorList.AsReadOnly();

            _canonical = new Dictionary<string, TransportConstructorInfo>(StringComparer.Ordinal);
            foreach (var pair in canonical)
            {
                _canonical.Add(pair.Key, pair.Value);
            }

            _aliases = new Dictionary<string, ReadOnlyCollection<TransportConstructorInfo>>(StringComparer.Ordinal);
            foreach (var pair in aliases)
            {
                var candidates = new List<TransportConstructorInfo>(pair.Value);
                candidates.Sort(CompareAliasCandidates);
                _aliases.Add(pair.Key, candidates.AsReadOnly());
            }

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var name in _canonical.Keys)
            {
                names.Add(name);
            }

            foreach (var name in _aliases.Keys)
            {
                names.Add(name);
            }

            var sortedNames = new List<string>(names.Count);
            sortedNames.AddRange(names);
            _allSchemeNames = sortedNames.AsReadOnly();
        }

        public ITransportTypeCatalog Types => _types;

        public IReadOnlyList<TransportConstructorInfo> Constructors => _constructors;

        public IReadOnlyList<string> AllSchemeNames => _allSchemeNames;

        public TransportConstructorInfo? FindCanonical(string scheme)
        {
            if (scheme is not null && _canonical.TryGetValue(scheme, out var constructor))
            {
                return constructor;
            }

            return null;
        }

        public IReadOnlyList<TransportConstructorInfo> FindCandidates(string scheme)
        {
            var canonical = FindCanonical(scheme);
            if (scheme is not null && _aliases.TryGetValue(scheme, out var aliases))
            {
                if (canonical is null)
                {
                    return aliases;
                }

                var candidates = new List<TransportConstructorInfo>(aliases.Count + 1) { canonical };
                candidates.AddRange(aliases);
                return candidates;
            }

            return canonical is null
                ? Array.Empty<TransportConstructorInfo>()
                : new[] { canonical };
        }

        // Canonical scheme name, then transport type. Alias groups contain at most
        // one constructor per transport type (registration rule 9).
        private int CompareAliasCandidates(TransportConstructorInfo x, TransportConstructorInfo y)
        {
            var byName = string.CompareOrdinal(x.Name, y.Name);
            if (byName != 0)
            {
                return byName;
            }

            return string.CompareOrdinal(_types.GetCanonicalName(x.Type), _types.GetCanonicalName(y.Type));
        }
    }
}
