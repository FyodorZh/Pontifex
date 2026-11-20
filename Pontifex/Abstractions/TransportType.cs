namespace Pontifex
{
    // Closed set of eight transport contracts. Values are a plain 0..7
    // index consumed by ConvertersGraph; they carry no flags semantics.
    // RawReliable is the single reliable raw contract; the remaining entries
    // follow the {Raw|RR}{Reliable|Unreliable}{Ack|NoAck} naming hierarchy.
    public enum TransportType
    {
        RawReliableAck = 0,
        RawReliable = 1,
        RawUnreliableAck = 2,
        RawUnreliableNoAck = 3,
        RRReliableAck = 4,
        RRReliableNoAck = 5,
        RRUnreliableAck = 6,
        RRUnreliableNoAck = 7,
    }
}
