using System;
using System.Linq;
using System.Windows.Forms;

namespace TransportAnalyzer
{
    class ListBoxLogConsumer : Log.ILogConsumer
    {
        private readonly ListBox mLog;
        private readonly int mCapacity;

        private readonly System.IO.StringWriter mBuffer = new System.IO.StringWriter();

        public ListBoxLogConsumer(ListBox list, int capacity)
        {
            mLog = list;
            mCapacity = capacity;
        }

        public bool IsOptional => false;

        public void Message(Log.MessageData logMessage)
        {
            try
            {
                string severity = logMessage.Severity;
                string message;
                lock (mBuffer)
                {
                    logMessage.WriteMessageTo(mBuffer);
                    message = mBuffer.ToString();
                    mBuffer.GetStringBuilder().Length = 0;
                }

                string str = string.Format("{0}-{1}: {2}", DateTime.Now, severity, message);
                foreach (var line in str.Split('\n').Reverse())
                {
                    Action a = () =>
                    {
                        if (!mLog.IsDisposed)
                        {
                            mLog.Items.Insert(0, line);
                            while (mLog.Items.Count > mCapacity)
                            {
                                mLog.Items.RemoveAt(mLog.Items.Count - 1);
                            }
                        }
                    };

                    if (!mLog.IsDisposed)
                    {
                        mLog.Invoke(a);
                    }
                }
            }
            catch
            {
            }
        }

        public void AddRef()
        {
        }

        public void Release()
        {
        }
    }
}
