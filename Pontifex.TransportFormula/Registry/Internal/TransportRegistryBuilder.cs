using System;
using System.Collections.Generic;
using Pontifex;
using Pontifex.TransportFormula.Internal;

namespace Pontifex.TransportFormula.Registry.Internal
{
    // Mutable during setup only. Every registration follows the numbered validation
    // order from parsing.md §6.7 and reports the first failure.
    internal sealed class TransportRegistryBuilder : IRegistryBuilder
    {
        private readonly List<TransportTypeInfo> _types = new();
        private readonly HashSet<string> _typeNames = new(StringComparer.Ordinal);
        private readonly List<TransportConstructorInfo> _constructors = new();
        private readonly Dictionary<string, TransportConstructorInfo> _canonical = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<TransportConstructorInfo>> _aliases = new(StringComparer.Ordinal);
        private TransportRegistry? _built;

        public void RegisterType(TransportTypeInfo type)
        {
            EnsureNotBuilt();

            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            // Rule 0: the type catalog is validated before any registry rule.
            MetadataValidator.ValidateTypeName(type.CanonicalName);
            var normalizedName = NameNormalizer.Normalize(type.CanonicalName);
            if (!_typeNames.Add(normalizedName))
            {
                throw RegistryErrorFactory.DuplicateTypeName(normalizedName);
            }

            _types.Add(type);
        }

        public void RegisterTransport(TransportRegistration registration)
        {
            EnsureNotBuilt();

            if (registration is null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            var constructor = registration.Constructor
                ?? throw new ArgumentNullException(nameof(registration));
            var sourceParams = constructor.Params ?? Array.Empty<Param>();
            var sourceAliases = registration.Aliases ?? Array.Empty<string>();

            // Rule 2: canonical name, then parameter names in declaration order,
            // then alias names in input order.
            MetadataValidator.ValidateCanonicalName(constructor.Name);
            for (var i = 0; i < sourceParams.Count; i++)
            {
                MetadataValidator.ValidateParamName(sourceParams[i].Name);
            }

            for (var i = 0; i < sourceAliases.Count; i++)
            {
                MetadataValidator.ValidateAliasName(sourceAliases[i]);
            }

            // Rule 3: duplicate canonical scheme names.
            var canonicalName = NameNormalizer.Normalize(constructor.Name);
            if (_canonical.ContainsKey(canonicalName))
            {
                throw RegistryErrorFactory.DuplicateCanonicalName(canonicalName);
            }

            // Rules 4-7: duplicate parameter names, type sets, list ranges, struct
            // schemas.
            var parameters = MetadataValidator.NormalizeParameters(canonicalName, sourceParams);

            // Rule 8: normalize aliases and drop duplicates and self-aliases.
            var aliasNames = MetadataValidator.NormalizeAliases(canonicalName, sourceAliases);

            // Alias-to-type rules need the catalog snapshot for diagnostic type
            // names. The catalog itself was validated by RegisterType.
            var catalog = new TransportTypeCatalog(_types);

            // Rule 9: an alias name may not be registered twice for the same type
            // by different constructors. The canonical constructor is not part of
            // the alias map.
            for (var i = 0; i < aliasNames.Count; i++)
            {
                if (!_aliases.TryGetValue(aliasNames[i], out var owners))
                {
                    continue;
                }

                for (var ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
                {
                    if (owners[ownerIndex].Type == constructor.Type)
                    {
                        throw RegistryErrorFactory.DuplicateAliasForType(
                            aliasNames[i],
                            MetadataValidator.FormatTypeName(constructor.Type, catalog));
                    }
                }
            }

            // Rule 11: constructor type and every Expected member must be present.
            MetadataValidator.ValidateCatalogMembership(constructor.Type, parameters, catalog);

            var info = new TransportConstructorInfo(
                canonicalName,
                constructor.Type,
                parameters,
                NormalizeSuggestedAliases(constructor.SuggestedAliases));

            _constructors.Add(info);
            _canonical.Add(canonicalName, info);

            for (var i = 0; i < aliasNames.Count; i++)
            {
                if (!_aliases.TryGetValue(aliasNames[i], out var owners))
                {
                    owners = new List<TransportConstructorInfo>();
                    _aliases.Add(aliasNames[i], owners);
                }

                owners.Add(info);
            }
        }

        public IRegistry Build()
        {
            if (_built is not null)
            {
                return _built;
            }

            var catalog = new TransportTypeCatalog(_types);

            var canonical = new Dictionary<string, TransportConstructorInfo>(StringComparer.Ordinal);
            foreach (var pair in _canonical)
            {
                canonical.Add(pair.Key, pair.Value);
            }

            var aliases = new Dictionary<string, IReadOnlyList<TransportConstructorInfo>>(StringComparer.Ordinal);
            foreach (var pair in _aliases)
            {
                aliases.Add(pair.Key, pair.Value.ToArray());
            }

            _built = new TransportRegistry(catalog, _constructors.ToArray(), canonical, aliases);
            return _built;
        }

        private static IReadOnlyList<string> NormalizeSuggestedAliases(IReadOnlyList<string>? suggestedAliases)
        {
            if (suggestedAliases is null || suggestedAliases.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(suggestedAliases.Count);
            for (var i = 0; i < suggestedAliases.Count; i++)
            {
                var alias = suggestedAliases[i];
                if (alias is not null)
                {
                    result.Add(NameNormalizer.Normalize(alias));
                }
            }

            return result.AsReadOnly();
        }

        private void EnsureNotBuilt()
        {
            if (_built is not null)
            {
                throw RegistryErrorFactory.BuilderSealed();
            }
        }
    }
}
