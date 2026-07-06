namespace Pontifex
{
    /// <summary>
    /// Common interface for all transport endpoints.
    /// Allows uniquely identifying transport endpoints and comparing them with each other.
    /// </summary>
    public interface IEndPoint : System.IEquatable<IEndPoint>
    {
    }
}
