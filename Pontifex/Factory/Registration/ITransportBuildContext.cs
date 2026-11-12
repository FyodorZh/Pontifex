using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// Which side a compiler instance builds. The role is compiler
    /// configuration, not formula data.
    /// </summary>
    public enum TransportRole
    {
        Server,
        Client
    }

    /// <summary>
    /// Creates a nested transport when the owning constructor needs it. Calling
    /// the delegate more than once creates additional independent transports,
    /// which is required by transports that reconnect or preserve a nested
    /// factory for later use.
    /// </summary>
    public delegate ITransport NestedTransportFactory();

    /// <summary>
    /// Build-time input for a transport constructor. Parameters are addressed by
    /// their canonical, lowercase names. A <c>TryGet</c> method returns
    /// <c>false</c> when the parameter is absent; required parameters are
    /// guaranteed by resolution. Struct fields may contain scalar arrays, but not
    /// transports or transport lists.
    /// </summary>
    public interface ITransportBuildContext
    {
        TransportRole Role { get; }

        ILogger Logger { get; }

        IMemoryRental Memory { get; }

        bool TryGetString(string parameterName, out string value);

        bool TryGetInt64(string parameterName, out long value);

        bool TryGetDecimal(string parameterName, out decimal value);

        bool TryGetTransport(string parameterName, out NestedTransportFactory factory);

        bool TryGetTransportList(
            string parameterName,
            out IReadOnlyList<NestedTransportFactory> factories);

        bool TryGetStruct(string parameterName, out ITransportStructValue value);
    }

    /// <summary>
    /// A resolved struct value. Fields are addressed by their canonical,
    /// lowercase names. Nested structs and arrays are exposed recursively;
    /// structs cannot contain transports or transport lists.
    /// </summary>
    public interface ITransportStructValue
    {
        bool TryGetString(string fieldName, out string value);

        bool TryGetInt64(string fieldName, out long value);

        bool TryGetDecimal(string fieldName, out decimal value);

        bool TryGetStruct(string fieldName, out ITransportStructValue value);

        bool TryGetArray(string fieldName, out ITransportArrayValue value);
    }

    /// <summary>
    /// A resolved homogeneous scalar array. An empty array has
    /// <see cref="Count"/> zero and is valid. Element accessors return
    /// <c>false</c> when the element has a different kind. An index below zero
    /// or at least <see cref="Count"/> is a programmer error and throws
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    public interface ITransportArrayValue
    {
        int Count { get; }

        bool TryGetString(int index, out string value);

        bool TryGetInt64(int index, out long value);

        bool TryGetDecimal(int index, out decimal value);
    }
}
