using JsonFactory;

namespace LogConsumers.Tcp
{
    public class LogStashLogFormatter : ILogFormatter
    {
        public void Format(Log.MessageData logMessage, CharBuffer buffer)
        {
            logMessage.Data.Serialize(buffer);
            buffer.Write('\n');
        }
    }
}