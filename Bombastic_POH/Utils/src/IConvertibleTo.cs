namespace Shared
{
    public interface IConvertibleTo<T>
        where T : struct
    {
        T ConvertTo();
        void ConvertFrom(T value);
    }
}
