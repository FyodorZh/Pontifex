using System;
using System.Collections.Generic;
using Pontifex.Factory.Registry;
using Pontifex.Factory.Semantics;

namespace Pontifex.Factory.Canonicalization.Internal;

// One argument in canonical print order. Positional entries carry no name;
// named entries print as `name=value`.
internal readonly struct ParamPrintEntry
{
    public ParamPrintEntry(ParamBinding binding, bool isNamed)
    {
        Binding = binding;
        IsNamed = isNamed;
    }

    public ParamBinding Binding { get; }

    public bool IsNamed { get; }
}

// Computes the canonical positional/named arrangement from parsing.md §10.2:
// the longest provided prefix of positional-capable parameters prints
// positionally, and every other provided parameter prints named in declaration
// order. Named-only parameters are always named. Parameters are matched by
// reference identity.
internal static class PositionalFormatter
{
    public static IReadOnlyList<ParamPrintEntry> Arrange(
        TransportConstructorInfo constructor,
        IReadOnlyList<ParamBinding> provided)
    {
        if (constructor is null)
        {
            throw new ArgumentNullException(nameof(constructor));
        }

        if (provided is null)
        {
            throw new ArgumentNullException(nameof(provided));
        }

        var entries = new List<ParamPrintEntry>(provided.Count);
        var emitted = new List<Param>(provided.Count);

        for (var i = 0; i < constructor.Params.Count; i++)
        {
            var parameter = constructor.Params[i];
            if (!parameter.Positional)
            {
                continue;
            }

            var binding = FindBinding(provided, parameter);
            if (binding is null)
            {
                break;
            }

            entries.Add(new ParamPrintEntry(binding, isNamed: false));
            emitted.Add(parameter);
        }

        // Emit every remaining provided parameter named, in declaration order.
        // This includes a positional parameter skipped because an earlier
        // positional-capable parameter was absent.
        for (var i = 0; i < constructor.Params.Count; i++)
        {
            var parameter = constructor.Params[i];
            if (ContainsReference(emitted, parameter))
            {
                continue;
            }

            var binding = FindBinding(provided, parameter);
            if (binding is null)
            {
                continue;
            }

            entries.Add(new ParamPrintEntry(binding, isNamed: true));
        }

        return entries;
    }

    private static ParamBinding? FindBinding(IReadOnlyList<ParamBinding> provided, Param parameter)
    {
        for (var i = 0; i < provided.Count; i++)
        {
            if (ReferenceEquals(provided[i].Param, parameter))
            {
                return provided[i];
            }
        }

        return null;
    }

    private static bool ContainsReference(IReadOnlyList<Param> parameters, Param parameter)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (ReferenceEquals(parameters[i], parameter))
            {
                return true;
            }
        }

        return false;
    }
}
