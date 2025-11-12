namespace Shared.Battle
{ 
    public interface IBaseUnknown
    {
       TImplementation QueryImplementator<TImplementation>() where TImplementation : class, IBaseUnknown;
    }
}