using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Scriba;
using Scriba.Consumers;
using Scriba.JsonFactory;
using Scriba.JsonFactory.ExternalJsons;

namespace Pontifex.Tests;

/// <summary>
/// Logger that records error messages for assertions on the test thread. Scriba catches exceptions
/// thrown by log consumers, so assertions must never be made from inside the consumer.
/// </summary>
public sealed class RecordingLogger : ILogger
{
    private readonly Logger _inner;
    private readonly ConcurrentQueue<RecordedLogMessage> _messages = new();

    public RecordingLogger(bool failOnError, bool writeToConsole = true)
    {
        FailOnError = failOnError;
        var consumers = new List<ILogConsumer>();
        if (writeToConsole)
        {
            consumers.Add(new ConsoleConsumer());
        }

        consumers.Add(new RecordingConsumer(this));
        _inner = new Logger(consumers.ToArray());
        _inner.LogFor = Severity.WARN;
    }

    public bool FailOnError { get; }

    public ILogger Inner => _inner;

    public IReadOnlyList<RecordedLogMessage> Errors => _messages
        .Where(message => message.Severity is Severity.FATAL or Severity.ERROR)
        .ToArray();

    public void AssertNoErrors()
    {
        if (!FailOnError)
        {
            return;
        }

        Assert.That(Errors, Is.Empty,
            "Unexpected error logs were recorded: " + string.Join(" | ", Errors.Select(Format)));
    }

    public void AddErrorsTo(ConcurrentBag<string> errors, string context)
    {
        if (!FailOnError)
        {
            return;
        }

        foreach (var error in Errors)
        {
            errors.Add($"{context}: {Format(error)}");
        }
    }

    private static string Format(RecordedLogMessage message) =>
        $"{message.Severity}: {message.Text}";

    internal void Record(MessageData message)
    {
        string text;
        try
        {
            using var writer = new StringWriter();
            message.WriteMessageTo(writer);
            text = writer.ToString();
        }
        catch
        {
            text = message.ToString() ?? string.Empty;
        }

        _messages.Enqueue(new RecordedLogMessage(message.Severity, text));
    }

    public Severity LogFor { get => _inner.LogFor; set => _inner.LogFor = value; }
    public Severity IgnoreStackFor { get => _inner.IgnoreStackFor; set => _inner.IgnoreStackFor = value; }
    [AllowNull]
    public string AppId { get => _inner.AppId!; set => _inner.AppId = value!; }
    [AllowNull]
    public string MachineName { get => _inner.MachineName!; set => _inner.MachineName = value!; }
    public bool LogTime { get => _inner.LogTime; set => _inner.LogTime = value; }
    public ITagList Tags { get => _inner.Tags; set { } }

    public void AddConsumer(ILogConsumer consumer) => _inner.AddConsumer(consumer);
    public void RemoveConsumer(ILogConsumer consumer) => _inner.RemoveConsumer(consumer);
    public void RemoveConsumerByType(Type type) => _inner.RemoveConsumerByType(type);
    public void d(string message, params object[] args) => _inner.d(message, args);
    public void i(string message, params object[] args) => _inner.i(message, args);
    public void w(string message, params object[] args) => _inner.w(message, args);
    public void e(string message, params object[] args) => _inner.e(message, args);
    public void wtf(string message, Exception exception) => _inner.wtf(message, exception);
    public void wtf(Exception exception) => _inner.wtf(exception);
    public void json(IJsonObject json) => _inner.json(json);
    public void Dispose() => _inner.Dispose();

    private sealed class RecordingConsumer(RecordingLogger owner) : ILogConsumer
    {
        public void Message(MessageData logMessage) => owner.Record(logMessage);
        public void AddRef() { }
        public void Release() { }
    }
}

public sealed record RecordedLogMessage(Severity Severity, string Text);
