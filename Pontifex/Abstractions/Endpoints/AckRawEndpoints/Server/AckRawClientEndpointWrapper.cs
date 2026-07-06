using System;
using System.Collections.Generic;
using Pontifex.Ack.Raw;
using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    public class AckRawServerSideEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawServerSideEndpoint
    {
        private readonly IControl[] _controls;
        
        public AckRawServerSideEndpointWrapper(IAckRawServerSideEndpoint? core,  
            Func<IAckRawBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IAckRawBaseEndpoint?, StopReason, bool> disconnector,
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