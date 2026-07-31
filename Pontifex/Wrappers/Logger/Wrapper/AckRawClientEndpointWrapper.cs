using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw.Reliable.Logger
{
    public class AckRawServerSideEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawReliableServerSideEndpoint
    {
        private readonly IControl[] _controls;
        
        public AckRawServerSideEndpointWrapper(IAckRawReliableServerSideEndpoint? core,  
            Func<IAckRawReliableBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawReliableBaseEndpoint?, StopReason, bool> disconnector,
            IControl[] controls)
            : base(core, sender, disconnector)
        {
            _controls = controls;
        }

        public override void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            base.GetControls(dst, predicate);
            foreach (var control in _controls)
            {
                if (predicate == null || predicate(control))
                {
                    dst.Add(control);
                }
            }
        }
    }
}