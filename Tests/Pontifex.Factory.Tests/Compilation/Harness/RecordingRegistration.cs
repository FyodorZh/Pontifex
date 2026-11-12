using System;
using System.Collections.Generic;
using Pontifex.Factory.Registration;

namespace Pontifex.Factory.Tests.Compilation.Harness;

// Hand-written core registration double. It records each Create invocation, the
// context it received, and the transport it produced, so the suite can prove
// call counts and inspect the build context through its public contract. The
// creation callback allows a test to script failures without a mocking library.
internal sealed class RecordingRegistration : ITransportRegistration
{
    private readonly Func<ITransportBuildContext, ITransport> _create;
    private readonly List<ITransport> _created = new();

    public RecordingRegistration(string name, TransportType type)
        : this(
            name,
            type,
            Array.Empty<TransportParameter>(),
            Array.Empty<string>(),
            _ => new FakeTransport(type, name))
    {
    }

    public RecordingRegistration(string name, TransportType type, ITransport transport)
        : this(
            name,
            type,
            Array.Empty<TransportParameter>(),
            Array.Empty<string>(),
            _ => transport)
    {
    }

    public RecordingRegistration(
        string name,
        TransportType type,
        IReadOnlyList<TransportParameter> parameters,
        IReadOnlyList<string> aliases,
        Func<ITransportBuildContext, ITransport> create)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Aliases = aliases ?? throw new ArgumentNullException(nameof(aliases));
        _create = create ?? throw new ArgumentNullException(nameof(create));
    }

    public string Name { get; }

    public TransportType Type { get; }

    public IReadOnlyList<TransportParameter> Parameters { get; }

    public IReadOnlyList<string> Aliases { get; }

    public int InvocationCount { get; private set; }

    public ITransportBuildContext? LastContext { get; private set; }

    public IReadOnlyList<ITransport> Created => _created;

    public ITransport Create(ITransportBuildContext context)
    {
        InvocationCount++;
        LastContext = context;
        var transport = _create(context);
        _created.Add(transport);
        return transport;
    }
}
