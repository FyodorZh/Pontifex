namespace JsonFactory
{
    public interface ILogExternalJsonFactory
    {
        IExternalJson Create(object value);
    }
}
