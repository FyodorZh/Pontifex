using System;
using System.Collections.Generic;

internal class Logger : ILogger
{
    private readonly Context mContext = new Context();

    public Logger(Log.ILogConsumer consumer)
    {
        if (consumer != null)
        {
            mContext.AddConsumer(consumer, false);
        }
    }

    public Logger(IEnumerable<Log.ILogConsumer> consumers)
    {
        foreach (var consumer in consumers)
        {
            if (consumer != null)
            {
                mContext.AddConsumer(consumer, false);
            }
        }
    }

    public Log.Severity LogFor
    {
        get { return mContext.LogFor; }
        set { mContext.LogFor = value; }
    }

    public Log.Severity IgnoreStackFor
    {
        get { return mContext.IgnoreStackFor; }
        set { mContext.IgnoreStackFor = value; }
    }

    public string AppId
    {
        get { return mContext.AppId; }
        set { mContext.AppId = value; }
    }

    public string MachineName
    {
        get { return mContext.MachineName; }
        set { mContext.MachineName = value; }
    }

    public Log.TagList Tags
    {
        get { return mContext.Tags; }
    }

    public void d(string format, params object[] args)
    {
        Message(Log.Severity.DEBUG, format, args);
    }

    public void i(string format, params object[] args)
    {
        Message(Log.Severity.INFO, format, args);
    }

    public void w(string format, params object[] args)
    {
        Message(Log.Severity.WARN, format, args);
    }

    public void e(string format, params object[] args)
    {
        Message(Log.Severity.ERROR, format, args);
    }

    public void wtf(string message, Exception exception)
    {
        Message(Log.Severity.ERROR, "Exception ({text}): {exception}", message, exception.ToString());
    }

    public void wtf(Exception exception)
    {
        Message(Log.Severity.ERROR, "Exception : {exception}", exception.ToString());
    }

    public void json(JsonFactory.IJsonObject message)
    {
        mContext.Message(message);
    }

    private void Message(Log.Severity severity, string format, params object[] args)
    {
        if (severity <= mContext.LogFor)
        {
            mContext.Message(LogMessageBuilder.Build(severity, mContext.IgnoreStackFor, format, args));
        }
    }
}
