using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Logger
{
    public class RawReliableServerSideEndpointWrapper : RawReliableBaseEndpointWrapper, IRawReliableEndpoint
    {
        private readonly IControl[] _controls;
        
        public RawReliableServerSideEndpointWrapper(IRawReliableEndpoint? core,  
            Func<IRawReliableEndpoint?, UnionDataList, SendResult> sender, 
            Func<IRawReliableEndpoint?, StopReason, bool> disconnector,
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
