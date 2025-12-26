using System;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    internal class LogicEndpoint<TEndpoint> : IEndPoint
        where TEndpoint : class, IAckRawBaseEndpoint
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