using JsonFactory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

internal static class LogMessageBuilder
{
    class DateTimeFormatWrapper : IExternalJson
    {
        private DateTime _value;

        public DateTimeFormatWrapper(DateTime value)
        {
            _value = value;
        }

        public void WriteTo(TextWriter output)
        {
            output.Write('\"');

            Format(output, _value);

            output.Write('\"');
        }

        public void WriteToAsText(TextWriter output)
        {
            Format(output, _value);
        }

        public void Release()
        {
        }

        private static void Format(TextWriter output, DateTime value)
        {
            AppendDigits(output, value.Year, 4);
            output.Write('-');

            AppendDigits(output, value.Month, 2);
            output.Write('-');

            AppendDigits(output, value.Day, 2);
            output.Write(' ');

            AppendDigits(output, value.Hour, 2);
            output.Write(':');

            AppendDigits(output, value.Minute, 2);
            output.Write(':');

            AppendDigits(output, value.Second, 2);
            output.Write(',');

            AppendDigits(output, value.Millisecond, 3);
        }

        private static void AppendDigits(TextWriter output, int digits, int digitsCount)
        {
            for (int digitIndex = digitsCount - 1; digitIndex >= 0; --digitIndex)
            {
                int tmp;
                switch (digitIndex)
                {
                    case 3: tmp = digits / 1000; break;
                    case 2: tmp = digits / 100; break;
                    case 1: tmp = digits / 10; break;
                    default: tmp = digits; break;
                }

                output.Write((char)('0' + tmp % 10));
            }
        }
    }

    public static IJsonObject Build(Log.Severity severity, Log.Severity ignoreStackFor, string message, params object[] list)
    {
        IJsonObject logMessage = JsonObject.Construct();

        logMessage.AddElement(MessageAttributes.Time, new DateTimeFormatWrapper(DateTime.UtcNow));
        logMessage.AddElement(MessageAttributes.Severity, severity.Serialize());
        if (list != null && list.Length > 0)
        {
            logMessage.AddMultiElement(MessageAttributes.Message, message, list);
        }
        else
        {
            logMessage.AddElement(MessageAttributes.Message, message);
        }

        if (severity < ignoreStackFor)
        {
            IJsonArray jsonStack = logMessage.AddArray(MessageAttributes.Stack);

            var stack = new StackTrace(3, true);

            for (int i = 0; i < stack.FrameCount; ++i)
            {
                StackFrame sf = stack.GetFrame(i);
                var method = sf.GetMethod();

                IJsonObject frame = jsonStack.AddObject();
                frame.AddElement(MessageAttributes.StackFrameClass, method.DeclaringType.Name);
                frame.AddElement(MessageAttributes.StackFrameMethod, method.ToString());

                string fileName = sf.GetFileName();
                if (fileName != null)
                {
                    frame.AddElement(MessageAttributes.StackFrameFile, fileName);
                    frame.AddElement(MessageAttributes.StackFrameLine, sf.GetFileLineNumber());
                }
            }
        }
        return logMessage;
    }
}

internal class Context : Log.IContext
{
    private readonly List<Log.ILogConsumer> mLogConsumers = new List<Log.ILogConsumer>();

    public Log.Severity LogFor { get; set; }

    public Log.Severity IgnoreStackFor { get; set; }

    public string AppId { get; set; }

    public string MachineName { get; set; }

    public Log.TagList Tags { get; private set; }

    public Context()
    {
        LogFor = Log.Severity.DEBUG;
        IgnoreStackFor = Log.Severity.ERROR; // полное отключение логирования колстека
        MachineName = Environment.MachineName;
        AppId = "unknown";
        Tags = new Log.TagList();
    }

    public void AddConsumer(Log.ILogConsumer logConsumer, bool unique)
    {
        if (unique)
        {
            RemoveConsumerByType(logConsumer.GetType());
        }
        mLogConsumers.Add(logConsumer);
    }

    public void RemoveConsumer(Log.ILogConsumer logConsumer)
    {
        logConsumer.Release();
        mLogConsumers.Remove(logConsumer);
    }

    public void RemoveConsumerByType(Type type)
    {
        int j = 0;
        for (int i = 0; i < mLogConsumers.Count; ++i)
        {
            if (mLogConsumers[i].GetType() == type)
            {
                mLogConsumers[i].Release();
                mLogConsumers[i] = null;
            }
            else
            {
                mLogConsumers[j++] = mLogConsumers[i];
            }
        }

        mLogConsumers.RemoveRange(j, mLogConsumers.Count - j);
    }

    public void Message(IJsonObject message)
    {
        message.AddElement(MessageAttributes.AppId, AppId);
        message.AddElement(MessageAttributes.MachineName, MachineName);

        {
            IJsonArray tags;
            if (!message.Get(MessageAttributes.Tags).TryGet(out tags))
            {
                tags = message.AddArray(MessageAttributes.Tags);
            }
            if (tags != null)
            {
                Tags.WriteTo(tags);
            }
        }

        for (int i = mLogConsumers.Count - 1; i >= 0; --i)
        {
            try
            {
                mLogConsumers[i].Message(new Log.MessageData(message));
            }
            catch
            {
                // TODO
            }
        }
        message.Free();
    }

    public void Destroy()
    {
        for (int i = 0; i < mLogConsumers.Count; ++i)
        {
            mLogConsumers[i].Release();
        }
        mLogConsumers.Clear();
    }
}
