using System.Collections.Generic;
using System.IO;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording <see cref="ILogConsumer"/> for tests that assert an exception was logged without
/// depending on a concrete log sink. Each message is formatted to text while it is still owned by
/// the logger.
/// </summary>
internal sealed class RecordingLogConsumer : ILogConsumer
{
    private readonly List<string> _messages = new();

    /// <summary>
    /// Gets the formatted message texts in publication order.
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    public void Message(MessageData logMessage)
    {
        var writer = new StringWriter();
        logMessage.WriteMessageTo(writer);
        _messages.Add(writer.ToString());
    }

    public void AddRef()
    {
    }

    public void Release()
    {
    }
}
