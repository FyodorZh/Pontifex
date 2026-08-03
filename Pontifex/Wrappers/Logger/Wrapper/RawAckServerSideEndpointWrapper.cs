using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    public class RawAckServerSideEndpointWrapper : RawAckBaseEndpointWrapper, IRawReliableAckServerSideEndpoint
    {
        private readonly IControl[] _controls;
        
        public RawAckServerSideEndpointWrapper(IRawReliableAckServerSideEndpoint? core,  
            Func<IRawReliableAckBaseEndpoint?, UnionDataList, SendResult> sender, 
            Func<IRawReliableAckBaseEndpoint?, StopReason, bool> disconnector,
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