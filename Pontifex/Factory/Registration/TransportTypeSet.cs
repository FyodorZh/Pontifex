using System;
using System.Collections.Generic;

namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// Selects which transport types a parameter accepts.
    /// </summary>
    public enum TransportTypeSetKind
    {
        Any,
        Exactly,
        OneOf
    }

    /// <summary>
    /// Immutable set of acceptable transport types for a parameter.
    /// A default instance is <see cref="Any"/>.
    /// </summary>
    public readonly struct TransportTypeSet
    {
        private static readonly TransportType[] NoTypes = Array.Empty<TransportType>();

        private readonly TransportTypeSetKind _kind;
        private readonly IReadOnlyList<TransportType> _types;

        private TransportTypeSet(
            TransportTypeSetKind kind,
            IReadOnlyList<TransportType> types)
        {
            _kind = kind;
            _types = types;
        }

        /// <summary>
        /// Accepts every transport type.
        /// </summary>
        public static TransportTypeSet Any { get; } =
            new TransportTypeSet(TransportTypeSetKind.Any, NoTypes);

        /// <summary>
        /// Accepts exactly one transport type.
        /// </summary>
        public static TransportTypeSet Exactly(TransportType type)
        {
            return new TransportTypeSet(
                TransportTypeSetKind.Exactly,
                new[] { type });
        }

        /// <summary>
        /// Accepts any of the supplied transport types.
        /// </summary>
        public static TransportTypeSet OneOf(params TransportType[] types)
        {
            if (types is null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            if (types.Length == 0)
            {
                throw new ArgumentException(
                    "A one-of transport type set must contain at least one type.",
                    nameof(types));
            }

            var copy = new TransportType[types.Length];
            Array.Copy(types, copy, types.Length);
            return new TransportTypeSet(TransportTypeSetKind.OneOf, copy);
        }

        public TransportTypeSetKind Kind => _kind;

        /// <summary>
        /// The accepted types. Empty for <see cref="Any"/>.
        /// </summary>
        public IReadOnlyList<TransportType> Types => _types ?? NoTypes;

        public bool IsAny => _kind == TransportTypeSetKind.Any;

        public bool Contains(TransportType type)
        {
            if (IsAny)
            {
                return true;
            }

            var types = Types;
            for (var i = 0; i < types.Count; i++)
            {
                if (types[i] == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
