using System;

namespace Pontifex.VirtualDelivery
{
    /// <summary>
    /// Releases an <see cref="IDeliverySystem"/> on every ownership hand-off.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: <see cref="IDeliverySystem"/> itself has no disposal member.
    /// Clearing first ends delivery and releases queued messages for every implementation; an implementation
    /// that owns a thread or a timer additionally implements <see cref="IDisposable"/> and is disposed
    /// afterwards. Repeated release is safe.</description></item>
    /// </list>
    /// </remarks>
    internal static class DeliverySystemLifetime
    {
        public static void Release(IDeliverySystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            try
            {
                system.Clear();
            }
            finally
            {
                (system as IDisposable)?.Dispose();
            }
        }
    }
}
