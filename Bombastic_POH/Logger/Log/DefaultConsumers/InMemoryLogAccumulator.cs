public class InMemoryLogList
{
    public struct LogEntity
    {
        private readonly System.DateTime mTime;
        private readonly string mSeverity;
        private readonly string mText;
        private readonly string[] mStack;

        public System.DateTime Time
        {
            get { return mTime; }
        }

        public string Severity
        {
            get { return mSeverity; }
        }

        public string Text
        {
            get { return mText; }
        }

        public string[] Stack
        {
            get { return mStack; }
        }

        public LogEntity(System.DateTime time, string severity, string text, string[] stack)
        {
            mTime = time;
            mSeverity = severity;
            mText = text;
            mStack = stack;
        }
    }

    private int mCapacity;
    private LogEntity[] mQueue;

    private readonly System.IO.StringWriter mBuffer = new System.IO.StringWriter();

    private int mPosition;
    private int mCount;

    public InMemoryLogList()
        : this(0)
    {
    }

    public InMemoryLogList(int capacity)
    {
        Reinit(capacity);
    }

    public void Reinit(int capacity)
    {
        mCapacity = capacity;
        mQueue = new LogEntity[capacity];
        mPosition = 0;
        mCount = 0;
    }

    public int Count
    {
        get { return mCount; }
    }

    public void Append(Log.MessageData logMessage)
    {
        if (mCount == mCapacity)
        {
            Dequeue();
        }

        string message;
        string[] stack;

        lock (mBuffer)
        {
            logMessage.WriteMessageTo(mBuffer);
            message = mBuffer.ToString();
            mBuffer.GetStringBuilder().Length = 0;

            int stackDepth = logMessage.StackTraceDepth;
            stack = new string[stackDepth];
            for (int i = 0; i < stackDepth; ++i)
            {
                logMessage.WriteStackFrame(i, "", mBuffer);
                stack[i] = mBuffer.ToString();
                mBuffer.GetStringBuilder().Length = 0;
            }
        }
        Enqueue(new LogEntity(Now(), logMessage.Severity, message, stack));
    }

    public LogEntity At(int id)
    {
        return mQueue[(mPosition + id) % mCapacity];
    }

    private void Dequeue()
    {
        mPosition += 1;
        mCount -= 1;
    }

    protected void Enqueue(LogEntity entity)
    {
        mQueue[(mPosition + mCount) % mCapacity] = entity;
        mCount += 1;
    }

    protected virtual System.DateTime Now()
    {
        return System.DateTime.UtcNow;
    }
}

namespace LogConsumers
{
    public class InMemoryLogAccumulator : Log.ILogConsumer
    {
        private readonly InMemoryLogList mLogs;

        public InMemoryLogList Logs { get { return mLogs; } }

        public InMemoryLogAccumulator(InMemoryLogList accumulator)
        {
            mLogs = accumulator;
        }

        #region ILogConsumer

        void Log.ILogConsumer.AddRef()
        {
            // DO NOTHING
        }

        void Log.ILogConsumer.Message(Log.MessageData logMessage)
        {
            mLogs.Append(logMessage);
        }

        void Log.ILogConsumer.Release()
        {
            // DO NOTHING
        }

        #endregion
    }

}
