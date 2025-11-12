namespace LogConsumers
{
    public interface ILogFormatter
    {
        void Format(Log.MessageData logMessage, CharBuffer buffer);
    }
}
