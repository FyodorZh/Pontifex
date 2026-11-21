namespace Pontifex
{
    // Closed set of four transport contracts. Values are a plain 0..3
    // index consumed by ConvertersGraph; they carry no flags semantics.
    // RawReliable and RawUnreliable are the single reliable and unreliable raw
    // contracts. RRReliable and RRUnreliable are the single reliable and
    // unreliable RR contracts.
    public enum TransportType
    {
        RawReliable = 0,
        RawUnreliable = 1,
        RRReliable = 2,
        RRUnreliable = 3,
    }
}
