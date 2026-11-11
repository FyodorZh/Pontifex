using System;
using System.Collections.Generic;
using Pontifex.Factory.Internal;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics.Internal
{
    // Selects exactly one constructor for a scheme and expected type set, following
    // parsing.md §7.3: normalize, gather candidates, filter by expected type,
    // prefer the canonical constructor after filtering, reject ambiguity. There is
    // no schema overload, no converter tie-break, and no backtracking.
    internal sealed class ConstructorSelector
    {
        private readonly IRegistry _registry;

        public ConstructorSelector(IRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public TransportConstructorInfo Select(
            string rawScheme,
            SourceSpan schemeSpan,
            TransportTypeSet expected)
        {
            var scheme = NameNormalizer.Normalize(rawScheme);
            var allCandidates = _registry.FindCandidates(scheme);

            if (allCandidates.Count == 0)
            {
                throw new SemanticAbort(
                    SemanticErrorFactory.UnknownScheme(rawScheme, schemeSpan.Start, _registry));
            }

            var candidates = allCandidates;
            if (!expected.IsAny)
            {
                var filtered = new List<TransportConstructorInfo>(allCandidates.Count);
                for (var i = 0; i < allCandidates.Count; i++)
                {
                    if (expected.Contains(allCandidates[i].Type))
                    {
                        filtered.Add(allCandidates[i]);
                    }
                }

                if (filtered.Count == 0)
                {
                    throw new SemanticAbort(SemanticErrorFactory.NoVariantForExpectedType(
                        rawScheme,
                        schemeSpan.Start,
                        expected,
                        allCandidates,
                        _registry.Types));
                }

                candidates = filtered;
            }

            // Canonical preference applies after filtering, for any expected set.
            var canonical = _registry.FindCanonical(scheme);
            if (canonical is not null)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (ReferenceEquals(candidates[i], canonical))
                    {
                        return canonical;
                    }
                }
            }

            if (candidates.Count > 1)
            {
                throw new SemanticAbort(SemanticErrorFactory.AmbiguousScheme(
                    rawScheme,
                    schemeSpan.Start,
                    candidates,
                    _registry.Types));
            }

            return candidates[0];
        }
    }
}
