namespace Pontifex
{
    // Closed set of eight transport contracts formed by combining
    // Ack/NoAck, Raw/RR and Reliable/Unreliable. Values are a plain 0..7
    // index consumed by ConvertersGraph; they carry no flags semantics and
    // are ordered by the naming hierarchy {Raw|RR}{Reliable|Unreliable}{Ack|NoAck}.
    public enum TransportType
    {
        RawReliableAck = 0,
        RawReliableNoAck = 1,
        RawUnreliableAck = 2,
        RawUnreliableNoAck = 3,
        RRReliableAck = 4,
        RRReliableNoAck = 5,
        RRUnreliableAck = 6,
        RRUnreliableNoAck = 7,
    }
}
