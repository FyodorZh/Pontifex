namespace Pontifex
{
    /// <summary>
    /// Common interface for all transport endpoints.
    /// Allows uniquely identifying transport endpoints and comparing them with each other.
    /// Implementations must ensure endpoints equal under Equals return the same
    /// GetHashCode value while they are valid for use as dictionary keys.
    /// </summary>
    public interface IEndPoint : System.IEquatable<IEndPoint>
    {
    }
}
