using System;
using JsonFactory;

internal class VoidLogger : ILogger
{
    public VoidLogger()
    {
    }

    public bool IsActive
    {
        get { return false; }
    }

    public Log.Severity LogFor { get; set; }

    public Log.Severity IgnoreStackFor { get; set; }
    public string AppId { get; set; }
    public string MachineName { get; set; }

    public Log.TagList Tags { get; private set; }

    public void d(string format, params object[] args)
    {
        // DO NOTHING
    }

    public void i(string format, params object[] args)
    {
        // DO NOTHING
    }

    public void w(string format, params object[] args)
    {
        // DO NOTHING
    }

    public void e(string format, params object[] args)
    {
        // DO NOTHING
    }

    public void wtf(string message, Exception exception)
    {
        // DO NOTHING
    }

    public void wtf(Exception exception)
    {
        // DO NOTHING
    }

    public void json(IJsonObject message)
    {
        message.Free();
    }
}
