using System;
using System.Threading.Tasks;

namespace Pontifex.NetSockets.Tcp
{
    public enum RemoteHalfClosePolicy
    {
        KeepSendOpen,
        GracefulSendShutdown,
        AbortConnection
    }

    public enum SocketCompletionReason
    {
        Closed,
        Disposed,

        /// <summary>
        /// The local side aborted the connection in response to the remote endpoint's half-close
        /// (see <see cref="RemoteHalfClosePolicy.AbortConnection"/>), as opposed to a local
        /// <see cref="Disposed"/> teardown.
        /// </summary>
        RemoteAborted,

        Faulted
    }

    public enum SenderCompletionReason
    {
        GracefulShutdown,
        ImmediateShutdown,
        Aborted,
        Faulted
    }

    public enum ReceiverCompletionReason
    {
        RemoteEof,
        LocalShutdown,
        Aborted,
        Faulted
    }

    public readonly struct SocketCompletionResult
    {
        public SocketCompletionReason Reason { get; }
        public Exception? Error { get; }
        public Exception? SecondaryError { get; }

        /// <summary>The send direction's terminal result, when it has been observed.</summary>
        public SenderCompletionResult? Sender { get; }

        /// <summary>The receive direction's terminal result, when it has been observed.</summary>
        public ReceiverCompletionResult? Receiver { get; }

        internal SocketCompletionResult(
            SocketCompletionReason reason,
            Exception? error,
            Exception? secondaryError,
            SenderCompletionResult? sender,
            ReceiverCompletionResult? receiver)
        {
            Reason = reason;
            Error = error;
            SecondaryError = secondaryError;
            Sender = sender;
            Receiver = receiver;
        }
    }

    public readonly struct SenderCompletionResult
    {
        public SenderCompletionReason Reason { get; }
        public Exception? Error { get; }

        internal SenderCompletionResult(SenderCompletionReason reason, Exception? error)
        {
            Reason = reason;
            Error = error;
        }
    }

    public readonly struct ReceiverCompletionResult
    {
        public ReceiverCompletionReason Reason { get; }
        public Exception? Error { get; }

        internal ReceiverCompletionResult(ReceiverCompletionReason reason, Exception? error)
        {
            Reason = reason;
            Error = error;
        }
    }

    /// <summary>
    /// Raised when a send completes with <see cref="System.Net.Sockets.SocketError.Success"/> but transfers
    /// no bytes, carrying the operation context needed to diagnose the anomaly.
    /// </summary>
    public sealed class SendCompletedWithoutProgressException : Exception
    {
        public System.Net.Sockets.SocketError SocketError { get; }
        public int BufferOffset { get; }
        public int BufferCount { get; }
        public int BytesSent { get; }

        internal SendCompletedWithoutProgressException(
            System.Net.Sockets.SocketError socketError,
            int bufferOffset,
            int bufferCount,
            int bytesSent)
            : base($"SendAsync completed without transferring any bytes " +
                   $"(socketError={socketError}, offset={bufferOffset}, count={bufferCount}, sent={bytesSent}).")
        {
            SocketError = socketError;
            BufferOffset = bufferOffset;
            BufferCount = bufferCount;
            BytesSent = bytesSent;
        }
    }

    public interface ITcpSocketSender
    {
        /// <summary>Logical send completion (first-terminal result); may precede reader quiescence.</summary>
        Task<SenderCompletionResult> Completion { get; }

        /// <summary>
        /// Resolves once the transport has permanently stopped reading the outbound source: the
        /// sender's terminal state is established, no source read is reserved or active, and any
        /// buffer popped after termination has been released. Producers must seal publication and
        /// start draining only after this signal; <see cref="Completion"/> is logical completion
        /// and may resolve earlier (possibly while a source read is still blocked).
        /// </summary>
        /// <remarks>
        /// This member is source-breaking for external implementers of this interface: there is
        /// deliberately no default implementation, because treating logical completion as reader
        /// quiescence would permit overlapping readers on a single-reader source.
        /// </remarks>
        Task SourceQuiesced { get; }

        void Pump();

        /// <summary>
        /// Requests a send-side shutdown. A parameterless graceful request uses the constructor
        /// graceful deadline as its default (first request only; repeated calls are idempotent and do
        /// not extend the absolute deadline), and a parameterless immediate escalation preserves an
        /// active deadline unless it is explicitly replaced.
        /// </summary>
        void Shutdown(bool isGraceful);

        // Directional shutdown with an explicit deadline: the deadline covers the entire
        // shutdown/drain, not a single operation. Expiry survives source-read and completion gaps
        // (it is persistent sender lifecycle state), prevents any further send posting, and
        // force-finalizes the connection (for either a graceful or an immediate shutdown).
        // A finite value replaces any active deadline; Timeout.InfiniteTimeSpan cancels it. The
        // deadline must be exactly Timeout.InfiniteTimeSpan or in
        // [TimeSpan.Zero, int.MaxValue milliseconds]; an invalid value is rejected with
        // ArgumentOutOfRangeException before any lifecycle state changes. An ObjectDisposedException
        // for an already disposed socket takes precedence over argument validation.
        // Replacement safety: callbacks from a replaced/cancelled deadline are invalidated by
        // generation and cannot abort the connection.
        void Shutdown(bool isGraceful, TimeSpan deadline);

        // Edge-triggered notification that the source was observed temporarily empty.
        // It does not mean production is complete. It becomes eligible again only
        // after a successful pop, and callers must Pump after publishing new data.
        //
        // Admission contract: each handler is admitted atomically against terminal state. A handler
        // is never admitted after the sender is terminal; an already-admitted handler may enter or
        // finish later. Completion does not wait for drain handlers (they are outside its quiescence
        // scope). Handlers run outside all transport gates and their exceptions are isolated.
        event Action? SourceDrained;
    }

    public interface ITcpSocketReceiver
    {
        Task<ReceiverCompletionResult> Completion { get; }
        void Start();
        void Shutdown();
    }

    internal interface ILowLevelTcpSocket : IDisposable, IAsyncDisposable
    {
        ITcpSocketSender Sender { get; }
        ITcpSocketReceiver Receiver { get; }
        Task<SocketCompletionResult> Completion { get; }

        void Close();
    }
}