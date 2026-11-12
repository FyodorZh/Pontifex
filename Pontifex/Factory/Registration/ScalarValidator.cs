namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// Validates a decoded scalar string.
    /// Returns <c>null</c> when the value is valid, otherwise a non-empty
    /// reason fragment. Validators must be deterministic, side-effect-free,
    /// thread-safe, and must not throw.
    /// </summary>
    public delegate string? ScalarValidator(string value);
}
