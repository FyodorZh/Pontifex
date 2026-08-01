namespace Pontifex
{
    // Closed set of eight transport contracts formed by combining
    // Ack/NoAck, Raw/RR and Reliable/Unreliable. Values are a plain 0..7
    // index consumed by ConvertersGraph; they carry no flags semantics and
    // are ordered by the naming hierarchy {Raw|RR}{Reliable|Unreliable}{Ack|NoAck}.
    public enum TransportType
    {
        AckRawReliable = 0,
        NoAckRawReliable = 1,
        AckRawUnreliable = 2,
        NoAckRawUnreliable = 3,
        AckRRReliable = 4,
        NoAckRRReliable = 5,
        AckRRUnreliable = 6,
        NoAckRRUnreliable = 7,
    }
}
