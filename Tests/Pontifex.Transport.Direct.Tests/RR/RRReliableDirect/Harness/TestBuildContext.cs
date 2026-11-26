using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Scriba;

namespace Pontifex.Tests.RR.RRReliableDirect.Harness;

/// <summary>
/// Hand-written build context for the reliable Direct binding tests.
/// </summary>
internal sealed class TestBuildContext : ITransportBuildContext
{
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _numbers = new(StringComparer.OrdinalIgnoreCase);

    public TestBuildContext(TransportRole role, ILogger logger, IMemoryRental memory)
    {
        Role = role;
        Logger = logger;
        Memory = memory;
    }

    public TransportRole Role { get; }

    public ILogger Logger { get; }

    public IMemoryRental Memory { get; }

    public TestBuildContext WithString(string name, string value)
    {
        _strings[name] = value;
        return this;
    }

    public TestBuildContext WithInt64(string name, long value)
    {
        _numbers[name] = value;
        return this;
    }

    public bool TryGetString(string parameterName, out string value)
    {
        return _strings.TryGetValue(parameterName, out value!);
    }

    public bool TryGetInt64(string parameterName, out long value)
    {
        return _numbers.TryGetValue(parameterName, out value);
    }

    public bool TryGetDecimal(string parameterName, out decimal value)
    {
        value = 0;
        return false;
    }

    public bool TryGetTransport(string parameterName, out NestedTransportFactory factory)
    {
        factory = null!;
        return false;
    }

    public bool TryGetTransportList(
        string parameterName,
        out IReadOnlyList<NestedTransportFactory> factories)
    {
        factories = Array.Empty<NestedTransportFactory>();
        return false;
    }

    public bool TryGetStruct(string parameterName, out ITransportStructValue value)
    {
        value = null!;
        return false;
    }
}
