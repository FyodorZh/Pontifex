using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Pontifex.Factory.Internal;
using Pontifex.Factory.Parsing;
using Pontifex.Factory.Registry;

namespace Pontifex.Factory.Semantics.Internal
{
    // Binds positional and named arguments to constructor parameters following
    // parsing.md §7.4. Positional arguments fill positional-capable parameters in
    // declaration order; named arguments then fill by normalized name. Errors are
    // reported in §7.9 order: too many positional, then named arguments in source
    // order (unknown before duplicate), then the first missing required parameter.
    internal sealed class ArgumentBinder
    {
        public IReadOnlyList<BoundParameter> Bind(
            AstTransportCall call,
            TransportConstructorInfo constructor)
        {
            var positionalParams = new List<Param>();
            for (var i = 0; i < constructor.Params.Count; i++)
            {
                if (constructor.Params[i].Positional)
                {
                    positionalParams.Add(constructor.Params[i]);
                }
            }

            var positionalArgs = new List<AstArgument>();
            for (var i = 0; i < call.Arguments.Count; i++)
            {
                if (call.Arguments[i].Name is null)
                {
                    positionalArgs.Add(call.Arguments[i]);
                }
            }

            if (positionalArgs.Count > positionalParams.Count)
            {
                throw new SemanticAbort(SemanticErrorFactory.TooManyPositional(
                    call.Scheme,
                    positionalParams.Count,
                    positionalArgs[positionalParams.Count].Value.Span.Start));
            }

            var bound = new Dictionary<Param, AstValue>(ParamReferenceComparer.Instance);
            for (var i = 0; i < positionalArgs.Count; i++)
            {
                bound.Add(positionalParams[i], positionalArgs[i].Value);
            }

            for (var i = 0; i < call.Arguments.Count; i++)
            {
                var argument = call.Arguments[i];
                if (argument.Name is null)
                {
                    continue;
                }

                var name = NameNormalizer.Normalize(argument.Name);
                var parameter = FindByName(constructor.Params, name);
                var offset = NameOffset(argument);

                if (parameter is null)
                {
                    throw new SemanticAbort(SemanticErrorFactory.UnknownParam(
                        argument.Name,
                        call.Scheme,
                        offset,
                        constructor.Params));
                }

                if (bound.ContainsKey(parameter))
                {
                    throw new SemanticAbort(
                        SemanticErrorFactory.DuplicateParam(argument.Name, offset));
                }

                bound.Add(parameter, argument.Value);
            }

            // Absent parameters are absent; no defaults are synthesized.
            var provided = new List<BoundParameter>();
            for (var i = 0; i < constructor.Params.Count; i++)
            {
                var parameter = constructor.Params[i];
                if (!bound.TryGetValue(parameter, out var value))
                {
                    if (parameter.Required)
                    {
                        throw new SemanticAbort(SemanticErrorFactory.MissingRequiredParam(
                            parameter.Name,
                            call.Scheme,
                            call.CloseParenOffset));
                    }

                    continue;
                }

                provided.Add(new BoundParameter(parameter, value));
            }

            return provided.AsReadOnly();
        }

        private static Param? FindByName(IReadOnlyList<Param> parameters, string normalizedName)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (string.Equals(parameters[i].Name, normalizedName, StringComparison.Ordinal))
                {
                    return parameters[i];
                }
            }

            return null;
        }

        private static int NameOffset(AstArgument argument)
        {
            return argument.NameSpan.HasValue
                ? argument.NameSpan.Value.Start
                : argument.Span.Start;
        }

        // Record equality is structural; binding and duplicate detection require
        // reference identity to the constructor's parameter instances.
        private sealed class ParamReferenceComparer : IEqualityComparer<Param>
        {
            public static readonly ParamReferenceComparer Instance = new();

            public bool Equals(Param? x, Param? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(Param obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    // A parameter that received an argument, in constructor declaration order.
    internal sealed record BoundParameter(
        Param Param,
        AstValue Value);
}
