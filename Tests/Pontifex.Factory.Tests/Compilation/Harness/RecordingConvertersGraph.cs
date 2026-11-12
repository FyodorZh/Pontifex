using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Converters;
using Scriba;

namespace Pontifex.Factory.Tests.Compilation.Harness;

// Hand-written converter graph double. It records every TryConvert argument and
// returns a scripted conversion factory or null. Converter-factory failures are
// modelled by passing a factory that throws.
internal sealed class RecordingConvertersGraph : IConvertersGraph
{
    private readonly Func<ITransport>? _conversion;
    private readonly List<ConversionCall> _calls = new();

    public RecordingConvertersGraph(Func<ITransport>? conversion)
    {
        _conversion = conversion;
    }

    public IReadOnlyList<ConversionCall> Calls => _calls;

    public ConversionCall? LastCall => _calls.Count == 0 ? null : _calls[_calls.Count - 1];

    public Func<ITransport>? TryConvert(
        TransportType fromType,
        TransportType toType,
        Func<ITransport> innerTransportCtor,
        IMemoryRental? memoryRental = null,
        ILogger? logger = null)
    {
        if (innerTransportCtor is null)
        {
            throw new ArgumentNullException(nameof(innerTransportCtor));
        }

        _calls.Add(new ConversionCall(fromType, toType, innerTransportCtor, memoryRental, logger));

        return _conversion;
    }

    public bool HasPath(TransportType from, TransportType to, out int length)
    {
        length = _conversion is null ? 0 : 1;
        return _conversion is not null;
    }

    internal sealed record ConversionCall(
        TransportType FromType,
        TransportType ToType,
        Func<ITransport> InnerTransportCtor,
        IMemoryRental? MemoryRental,
        ILogger? Logger);
}
