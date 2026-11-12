using Pontifex.Factory.Registration;

namespace Pontifex.Factory
{
    /// <summary>
    /// Frozen lookup of runtime transport registrations. The compiler addresses a
    /// registration by its canonical scheme plus the core <see cref="TransportType"/>.
    /// </summary>
    public interface ITransportConstructorRegistry
    {
        /// <summary>
        /// Returns the registration for the normalized canonical scheme and type,
        /// or <c>null</c> when the key is unknown.
        /// </summary>
        ITransportRegistration? Find(string canonicalScheme, TransportType type);
    }
}
