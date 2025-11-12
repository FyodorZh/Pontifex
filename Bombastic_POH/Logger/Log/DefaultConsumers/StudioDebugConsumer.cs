#define DEBUG

using System.Diagnostics;
namespace LogConsumers
{
    public class StudioDebugConsumer : Log.ILogConsumer
    {
        private readonly System.IO.StringWriter mBuffer = new System.IO.StringWriter();

        public void Message(Log.MessageData logMessage)
        {
            string text;
            lock (mBuffer)
            {
                mBuffer.Write(logMessage.Severity);
                mBuffer.Write(": ");
                logMessage.WriteMessageTo(mBuffer);

                text = mBuffer.ToString();
                mBuffer.GetStringBuilder().Length = 0;
            }

            Debug.WriteLine(text);
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
