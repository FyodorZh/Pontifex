using System;
using System.Collections.Generic;

public interface ILogger
{
    Log.Severity LogFor { get; set; }
    Log.Severity IgnoreStackFor { get; set; }
    string AppId { get; set; }
    string MachineName { get; set; }
    Log.TagList Tags { get; }

    void d(string format, params object[] args);
    void i(string format, params object[] args);
    void w(string format, params object[] args);
    void e(string format, params object[] args);

    void wtf(string message, Exception exception);
    void wtf(Exception exception);

    void json(JsonFactory.IJsonObject message);
}

public static class LogSeveritySerializer
{
    private static readonly string[] mSeverityMap;

    static LogSeveritySerializer()
    {
        Type eType = typeof(Log.Severity);
        var fields = eType.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        mSeverityMap = new string[fields.Length];
        for (int i = 0; i < fields.Length; ++i)
        {
            mSeverityMap[i] = fields[i].GetValue(null).ToString();
        }
    }

    public static string Serialize(this Log.Severity severity)
    {
        int id = (int)severity;
        if (id >= 0 && id < mSeverityMap.Length)
        {
            return mSeverityMap[id];
        }
        return "UNKNOWN";
    }
}

public static partial class Log
{
    public enum Severity : byte
    {
        ERROR = 0,
        WARN = 1,
        INFO = 2,
        DEBUG = 3
    }

    public interface ILogConsumer
    {
        void Message(MessageData logMessage);

        void AddRef();
        void Release();        
    }

    public interface IContext
    {
        void Destroy();
    }

    public static readonly ILogger VoidLogger = new VoidLogger();

    public static readonly ILogger StaticLogger = new StaticLogger();

    public static readonly ILogger ConsoleLogger = LoggerInstance(new LogConsumers.ConsoleConsumer());

    public static ILogger LoggerInstance(ILogConsumer consumer)
    {
        return new Logger(consumer);
    }

    public static ILogger LoggerInstance(IEnumerable<ILogConsumer> consumers)
    {
        return new Logger(consumers);
    }
}