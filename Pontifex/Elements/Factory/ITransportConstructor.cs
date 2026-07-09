using System;
using System.Collections.Generic;
using Pontifex.Factory;

namespace Pontifex
{
    public interface ITransportConstructor
    {
        TransportType Type { get; }
        string Name { get; }
        ITransport ConstructServer(ITransportBuilder builder, IDescription description);
        ITransport ConstructClient(ITransportBuilder builder, IDescription description);
        IEnumerable<(string name, Func<string, IDescriptionUriFactory, Description?> uriParser)> GetUriParsers();
    }
}