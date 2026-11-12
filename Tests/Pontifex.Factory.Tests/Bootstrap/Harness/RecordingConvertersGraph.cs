using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Converters;
using Scriba;

namespace Pontifex.Factory.Tests.Bootstrap.Harness
{
    // Hand-written converter graph double. It records each TryConvert call and
    // returns a scripted conversion factory, or null when no path is scripted.
    // The factory receives the inner-constructor delegate so a test can prove the
    // compiler supplied the parsed transport. No mocking library is used.
    internal sealed class RecordingConvertersGraph : IConvertersGraph
    {
        private readonly Func<Func<ITransport>, ITransport>? _conversion;
        private readonly List<ConversionCall> _calls = new();

        public RecordingConvertersGraph(Func<Func<ITransport>, ITransport>? conversion)
        {
            _conversion = conversion;
        }

        public IReadOnlyList<ConversionCall> Calls => _calls;

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

            _calls.Add(new ConversionCall(
                fromType,
                toType,
                innerTransportCtor,
                memoryRental,
                logger));

            if (_conversion is null)
            {
                return null;
            }

            var conversion = _conversion;
            return () => conversion(innerTransportCtor);
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
}
