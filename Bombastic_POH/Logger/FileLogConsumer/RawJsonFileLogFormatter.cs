using JsonFactory;

namespace LogConsumers
{
    public class RawJsonFileLogFormatter : ILogFormatter
    {
        public void Format(Log.MessageData logMessage, CharBuffer buffer)
        {
            logMessage.Data.Serialize(buffer);
            buffer.WriteLine();
        }
    }
}