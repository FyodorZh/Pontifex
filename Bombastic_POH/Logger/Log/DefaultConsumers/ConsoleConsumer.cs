using System;
using JsonFactory;

namespace LogConsumers
{
    public class ConsoleConsumer : Log.ILogConsumer
    {
        private readonly System.IO.StringWriter mBuffer = new System.IO.StringWriter();

        public void Message(Log.MessageData logMessage)
        {
            lock (mBuffer)
            {
                mBuffer.Write(logMessage.Severity);
                mBuffer.Write(": ");
                logMessage.WriteMessageTo(mBuffer);
                logMessage.WriteStackTrace("\t", mBuffer);

                Console.WriteLine(mBuffer);
                mBuffer.GetStringBuilder().Length = 0;
            }
        }

        void Log.ILogConsumer.AddRef()
        {
            // DO NOTHING
        }

        void Log.ILogConsumer.Release()
        {
            // DO NOTHING
        }
    }
}
