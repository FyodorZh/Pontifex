using System;
using System.IO;
using JsonFactory;

namespace LogConsumers
{
    public class UnityLogConsumer : Log.ILogConsumer
    {
        private readonly StringWriter mBuffer = new StringWriter();

        public void Message(Log.MessageData logMessage)
        {
            lock (mBuffer)
            {
                mBuffer.Write("{0:HH:mm:ss,fff} ", DateTime.UtcNow);
                logMessage.WriteMessageTo(mBuffer);
                mBuffer.WriteLine();
                logMessage.WriteStackTrace("\t\t", mBuffer);

                //mBuffer.WriteLine("---JSON----");
                //logMessage.Data.Serialize(mBuffer);

                switch (logMessage.Severity)
                {
                    case "WARN":
                        UnityEngine.Debug.LogWarning(mBuffer);
                        break;
                    case "INFO":
                        UnityEngine.Debug.Log(mBuffer);
                        break;
                    case "DEBUG":
                        UnityEngine.Debug.Log(mBuffer);
                        break;
                    default:
                        UnityEngine.Debug.LogError(mBuffer);
                        break;
                }

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
