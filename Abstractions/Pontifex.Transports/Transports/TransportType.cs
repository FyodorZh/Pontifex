using System;

namespace Pontifex
{
    [Flags]
    public enum TransportType
    {
        AckRawReliable = _Ack | _Raw | _Reliable,
        AckRawUnreliable = _Ack | _Raw | _Unreliable,
        AckRRReliable = _Ack | _Rr | _Reliable,
        AckRRUnreliable = _Ack | _Rr | _Unreliable,
        NoAckRawReliable = _NoAck | _Raw | _Reliable,
        NoAckRawUnreliable = _NoAck | _Raw | _Unreliable,
        NoAckRRReliable = _NoAck | _Rr | _Reliable,
        NoAckRRUnreliable = _NoAck | _Rr | _Unreliable,
        
        _Ack = 1,
        _NoAck = 0,
        _Raw = 2,
        _Rr = 0,
        _Reliable = 4,
        _Unreliable = 0,
    }
}