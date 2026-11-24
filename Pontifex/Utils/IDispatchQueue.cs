using System;

namespace Pontifex.Utils
{
    /// <summary>
    /// A serialized callback queue that also exposes its internal exception notification.
    /// </summary>
    /// <typeparam name="T">The queued item type.</typeparam>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Internal seam for carriers that inject a manual queue in tests.
    /// The production queue already exposes the event; this interface lets the carrier subscribe through the
    /// abstraction.</description></item>
    /// </list>
    /// </remarks>
    internal interface IDispatchQueue<T> : ISerializedCallbackQueue<T>
    {
        /// <summary>
        /// Raised when a queued callback throws.
        /// </summary>
        event Action<Exception>? ExceptionHandler;
    }
}
