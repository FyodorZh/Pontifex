namespace Pontifex
{
    // Closed set of six transport contracts. Values are a plain 0..5
    // index consumed by ConvertersGraph; they carry no flags semantics.
    // RawReliable and RawUnreliable are the single reliable and unreliable raw
    // contracts; the remaining entries follow the
    // RR{Reliable|Unreliable}{Ack|NoAck} naming hierarchy.
    public enum TransportType
    {
        RawReliable = 0,
        RawUnreliable = 1,
        RRReliableAck = 2,
        RRReliableNoAck = 3,
        RRUnreliableAck = 4,
        RRUnreliableNoAck = 5,
    }
}
