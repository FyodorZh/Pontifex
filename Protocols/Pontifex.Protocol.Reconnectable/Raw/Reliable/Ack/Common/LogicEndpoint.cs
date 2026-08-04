using System;
using Pontifex.Raw.Reliable.Ack;

namespace Pontifex.Raw.Reliable.Ack.Reconnectable
{
    internal class LogicEndpoint<TEndpoint> : IEndPoint
        where TEndpoint : class, IRawReliableEndpoint
    {
        private readonly ReconnectableBaseLogic<TEndpoint> mOwner;

        public LogicEndpoint(ReconnectableBaseLogic<TEndpoint> owner)
        {
            mOwner = owner;
        }

        public override string ToString()
        {
            var endpoint = mOwner.UnderlyingEndpoint;
            string baseEP = endpoint?.RemoteEndPoint?.ToString() ?? "not-connected";
            return $"[{mOwner.Id} over '{baseEP}']";
        }

        bool IEquatable<IEndPoint>.Equals(IEndPoint other)
        {
            if (other is LogicEndpoint<TEndpoint> o)
            {
                return mOwner.Id.Equals(o.mOwner.Id);
            }
            return false;
        }
    }
}