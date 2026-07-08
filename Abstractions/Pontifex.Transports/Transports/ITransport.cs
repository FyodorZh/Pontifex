using Actuarius.Memory;
using Scriba;

namespace Pontifex
{
    public interface ITransport
    {
        TransportType Type { get; }
        
        /// <summary>
        /// Transport type. Unique identifier
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Flag indicating the validity of the transport system. In case of an error, it is set to false.
        /// A broken transport system cannot be restored.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Flag indicating whether the transport system is started (active).
        /// The transport system is created in a non-started state.
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// Starts the transport system. Non-blocking operation.
        /// Server: After successful completion of the method, the server can be connected to.
        /// Client: Successful completion of the method means that the transport has been initialized
        /// and has started the asynchronous process of connecting to the server.
        /// </summary>
        /// <param name="onStopped"> If true is returned, the onStopped callback should be invoked when
        /// the transport system stops (if it is not null) </param>
        /// <returns>
        /// Returns false if the transport system could not be started. After this, the transport system becomes invalid.
        /// Returns true if the operation to start the transport system was successfully initiated.
        /// </returns>
        bool Start(System.Action<StopReason> onStopped);

        /// <summary>
        /// Stops the transport system if it was started. Otherwise, does nothing.
        /// </summary>
        bool Stop(StopReason? reason = null);

        /// <summary>
        /// Logging system for the transport. It is used to log events and errors related to the transport system.
        /// </summary>
        ILogger Log { get; }
        
        /// <summary>
        /// Memory rental system for the transport. It is used to rent memory for data transmission and reception.
        /// </summary>
        IMemoryRental Memory { get; }
    }
}