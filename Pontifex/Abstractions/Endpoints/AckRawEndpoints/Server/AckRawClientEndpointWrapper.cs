using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Abstractions.Endpoints.Server
{
    public class AckRawClientEndpointWrapper : AckRawBaseEndpointWrapper, IAckRawClientEndpoint
    {
        private readonly IControl[] _controls;
        
        public AckRawClientEndpointWrapper(IAckRawClientEndpoint? core,  
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