using LogConsumers;

namespace FileLogConsumer
{
    public class FileLogFormatter : ILogFormatter
    {
        public void Format(Log.MessageData logMessage, CharBuffer buffer)
        {
            logMessage.WriteTimeTo(buffer);
            buffer.Write(" ");

            buffer.Write(logMessage.Severity);
            buffer.Write(":\t\t");
            logMessage.WriteMessageTo(buffer);
            buffer.WriteLine();
            logMessage.WriteStackTrace("\t\t\t\t\t", buffer);
        }
    }
}
