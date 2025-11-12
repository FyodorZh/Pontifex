using System;
using JsonFactory;

internal class StaticLogger : ILogger
{
    public Log.Severity LogFor
    {
        get { return Log.LogFor; }
        set { Log.LogFor = value; }
    }

    public Log.Severity IgnoreStackFor
    {
        get { return Log.IgnoreStackFor; }
        set { Log.IgnoreStackFor = value; }
    }

    public string AppId
    {
        get { return Log.AppId; }
        set { Log.AppId = value; }
    }

    public string MachineName
    {
        get { return Log.MachineName; }
        set { Log.MachineName = value; }
    }

    public Log.TagList Tags
    {
        get { return Log.Tags; }
    }

    public void d(string format, params object[] args)
    {
        Log.d(format, args);
    }

    public void i(string format, params object[] args)
    {
        Log.i(format, args);
    }

    public void w(string format, params object[] args)
    {
        Log.w(format, args);
    }

    public void e(string format, params object[] args)
    {
        Log.e(format, args);
    }

    public void wtf(string message, Exception exception)
    {
        Log.wtf(message, exception);
    }

    public void wtf(Exception exception)
    {
        Log.wtf(exception);
    }

    public void json(IJsonObject message)
    {
        Log.json(message);
    }
}