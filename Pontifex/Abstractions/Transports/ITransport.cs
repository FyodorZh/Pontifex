using System;
using System.Collections.Generic;
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
        /// Starts the transport system. A successful return means local transport
        /// initialization completed. A transport that uses connections MAY begin
        /// asynchronous connection work; connectionless transports do not establish
        /// or await a connection.
        /// </summary>
        /// <param name="onStopped">Callback invoked once when a successfully started transport stops.</param>
        /// <exception cref="ArgumentNullException"><paramref name="onStopped"/> is null.</exception>
        /// <returns>
        /// Returns true if the transport system started successfully. A failed local
        /// initialization invalidates the transport; a terminal transport may also
        /// return false without attempting initialization.
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
        
        void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }
}
