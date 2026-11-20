namespace Pontifex
{
    // Closed set of seven transport contracts. Values are a plain 0..6
    // index consumed by ConvertersGraph; they carry no flags semantics.
    // RawReliable is the single reliable raw contract; the remaining entries
    // follow the {Raw|RR}{Reliable|Unreliable}{Ack|NoAck} naming hierarchy.
    public enum TransportType
    {
        RawReliable = 0,
        RawUnreliableAck = 1,
        RawUnreliableNoAck = 2,
        RRReliableAck = 3,
        RRReliableNoAck = 4,
        RRUnreliableAck = 5,
        RRUnreliableNoAck = 6,
    }
}
