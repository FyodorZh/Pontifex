using System;
using System.Collections.Generic;

namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// Immutable <see cref="ITransportRegistration"/> for transports that do not
    /// need a custom registration type.
    /// </summary>
    public sealed class TransportRegistration : ITransportRegistration
    {
        private readonly TransportParameter[] _parameters;
        private readonly string[] _aliases;
        private readonly Func<ITransportBuildContext, ITransport> _create;

        public TransportRegistration(
            string name,
            TransportType type,
            IReadOnlyList<TransportParameter> parameters,
            Func<ITransportBuildContext, ITransport> create,
            IReadOnlyList<string>? aliases = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            _parameters = CopyParameters(parameters);
            _aliases = CopyAliases(aliases);
            _create = create ?? throw new ArgumentNullException(nameof(create));
        }

        public string Name { get; }

        public TransportType Type { get; }

        public IReadOnlyList<TransportParameter> Parameters => _parameters;

        public IReadOnlyList<string> Aliases => _aliases;

        public ITransport Create(ITransportBuildContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return _create(context);
        }

        private static TransportParameter[] CopyParameters(
            IReadOnlyList<TransportParameter> parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var copy = new TransportParameter[parameters.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = parameters[i]
                    ?? throw new ArgumentException(
                        "A transport parameter cannot be null.",
                        nameof(parameters));
            }

            return copy;
        }

        private static string[] CopyAliases(IReadOnlyList<string>? aliases)
        {
            if (aliases is null || aliases.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[aliases.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = aliases[i]
                    ?? throw new ArgumentException(
                        "A transport alias cannot be null.",
                        nameof(aliases));
            }

            return copy;
        }
    }
}
