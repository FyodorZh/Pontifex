using System.Collections.Generic;
using Pontifex;

namespace Pontifex.Factory.Registry;

public interface ITransportTypeCatalog
{
    IReadOnlyList<TransportTypeInfo> Types { get; }

    IReadOnlyList<TransportType> AllTypes { get; }

    bool TryResolve(string name, out TransportTypeInfo? type);

    bool TryGetCanonicalName(TransportType type, out string? canonicalName);

    string GetCanonicalName(TransportType type);
}
