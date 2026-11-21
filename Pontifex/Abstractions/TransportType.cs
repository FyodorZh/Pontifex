namespace Pontifex
{
    // Closed set of five transport contracts. Values are a plain 0..4
    // index consumed by ConvertersGraph; they carry no flags semantics.
    // RawReliable and RawUnreliable are the single reliable and unreliable raw
    // contracts, RRUnreliable is the single unreliable RR contract, and the
    // remaining entries follow the RRReliable{Ack|NoAck} naming hierarchy.
    public enum TransportType
    {
        RawReliable = 0,
        RawUnreliable = 1,
        RRReliableAck = 2,
        RRReliableNoAck = 3,
        RRUnreliable = 4,
    }
}
