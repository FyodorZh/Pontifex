using Pontifex.Utils;

namespace Pontifex.VirtualDelivery
{
    /// <summary>
    /// Delivery system installed on a disposed channel so a racing send still releases its message.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: It is shared and stateless. <see cref="Deliver"/> always releases
    /// the message, <see cref="Clear"/> is a no-op, and no subscribers are required.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class DiscardingDeliverySystem : IDeliverySystem
    {
        public static readonly DiscardingDeliverySystem Instance = new DiscardingDeliverySystem();

        private DiscardingDeliverySystem()
        {
        }

        /// <inheritdoc/>
        public event System.Action<UnionDataList>? Delivered
        {
            add { }
            remove { }
        }

        /// <inheritdoc/>
        public void Deliver(UnionDataList message)
        {
            message.Release();
        }

        /// <inheritdoc/>
        public void Clear()
        {
        }
    }
}
