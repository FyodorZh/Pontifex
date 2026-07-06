namespace Pontifex
{
    /// <summary>
    /// Common interface for all transport control or introspection mechanisms.
    /// </summary>
    public interface IControl
    {
        /// <summary>
        /// Gets the name of the control.
        /// </summary>
        string Name { get; }
    }
}