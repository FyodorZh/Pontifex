using System;
using System.Collections.Generic;
using Pontifex.Factory.Registration;
using Pontifex.TransportFormula.Internal;

namespace Pontifex.Factory
{
    /// <summary>
    /// Mutable builder for the runtime constructor registry. Entries are keyed by
    /// the ASCII-normalized canonical scheme and the core transport type. The
    /// bootstrap seals the registry once the factory is built.
    /// </summary>
    public sealed class TransportConstructorRegistry : ITransportConstructorRegistry
    {
        private readonly Dictionary<Key, ITransportRegistration> _registrations =
            new Dictionary<Key, ITransportRegistration>();

        private bool _sealed;

        /// <summary>
        /// Adds a registration. The scheme is normalized to ASCII lowercase.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="registration"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// A registration for the same scheme and type already exists.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The registry is already sealed.
        /// </exception>
        public void Register(ITransportRegistration registration)
        {
            if (registration is null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            if (_sealed)
            {
                throw new InvalidOperationException(
                    "The transport constructor registry is sealed.");
            }

            var key = new Key(NameNormalizer.Normalize(registration.Name), registration.Type);
            if (_registrations.ContainsKey(key))
            {
                throw new ArgumentException(
                    $"A transport constructor for scheme '{key.Scheme}' and type '{key.Type}' is already registered.",
                    nameof(registration));
            }

            _registrations.Add(key, registration);
        }

        /// <summary>
        /// Freezes the registry. Further <see cref="Register"/> calls fail;
        /// <see cref="Find"/> remains available.
        /// </summary>
        public void Seal()
        {
            _sealed = true;
        }

        public ITransportRegistration? Find(string canonicalScheme, TransportType type)
        {
            if (canonicalScheme is null)
            {
                throw new ArgumentNullException(nameof(canonicalScheme));
            }

            var key = new Key(NameNormalizer.Normalize(canonicalScheme), type);
            return _registrations.TryGetValue(key, out var registration)
                ? registration
                : null;
        }

        private readonly struct Key : IEquatable<Key>
        {
            public Key(string scheme, TransportType type)
            {
                Scheme = scheme;
                Type = type;
            }

            public string Scheme { get; }

            public TransportType Type { get; }

            public bool Equals(Key other)
            {
                return Type == other.Type
                       && string.Equals(Scheme, other.Scheme, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(Scheme) * 397) ^ (int)Type;
                }
            }
        }
    }
}
