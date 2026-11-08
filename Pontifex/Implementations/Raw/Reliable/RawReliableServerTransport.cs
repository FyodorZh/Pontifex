using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Scriba;
using Transport.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base class for reliable raw server transports. Owns the bounded handshake and session admission gates,
    /// the live-session registry, and the single periodic ticker shared by every session of the transport.
    /// </summary>
    /// <typeparam name="TServerHandler">The session handler contract exposed by the transport.</typeparam>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. Handshake and session counters are lock-free bounded counters; the
    /// session registry is guarded by <c>_sessionsLock</c> and the lock is never held across user handler
    /// code.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The transport must be initialized before it can
    /// be started. The lifecycle is one-shot. A reserved session slot is released exactly once, either when
    /// the session stops or when admission fails.</description></item>
    /// <item><description><b>Other notes</b>: A concrete carrier supplies <see cref="StartCarrier"/> and
    /// <see cref="StopCarrier"/> and creates non-started endpoints that are admitted with
    /// <see cref="TryStartReservedSession"/>.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableServerTransport<TServerHandler>
        : RawReliableTransport, IRawReliableServerTransport<TServerHandler>
        where TServerHandler : class, IRawReliableServerHandler
    {
        private readonly object _sessionsLock = new();
        private readonly HashSet<SessionEntry> _sessions = new();

        private readonly int _maxConcurrentHandshakes;
        private readonly int _maxActiveSessions;
        private readonly int _tickPeriodMs;

        private volatile bool _stopping;
        private int _initialized;
        private int _handshakes;
        private int _activeSessions;
        private SessionTicker? _ticker;

        private RawReliableServerEndpoint[] _tickSnapshot = Array.Empty<RawReliableServerEndpoint>();

        /// <summary>
        /// Initializes the shared server transport machinery.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="conformanceControl">The conformance control shared with the transport's
        /// endpoints.</param>
        /// <param name="maxConcurrentHandshakes">The maximum number of handshakes in flight; must be at least
        /// 1.</param>
        /// <param name="maxActiveSessions">The maximum number of admitted sessions; must be at least 1.</param>
        /// <param name="tickPeriodMs">The period of the shared session ticker in milliseconds; must be at least
        /// 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">A numeric limit is out of range.</exception>
        protected RawReliableServerTransport(
            string typeName,
            ILogger logger,
            IMemoryRental memory,
            RawReliableConformanceControl conformanceControl,
            int maxConcurrentHandshakes,
            int maxActiveSessions,
            int tickPeriodMs)
            : base(typeName, logger, memory, conformanceControl)
        {
            if (maxConcurrentHandshakes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentHandshakes));
            }

            if (maxActiveSessions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxActiveSessions));
            }

            if (tickPeriodMs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tickPeriodMs));
            }

            _maxConcurrentHandshakes = maxConcurrentHandshakes;
            _maxActiveSessions = maxActiveSessions;
            _tickPeriodMs = tickPeriodMs;
        }

        /// <summary>
        /// Gets the inclusive maximum message size, in bytes, supported by the carrier.
        /// </summary>
        public abstract override int MessageMaxByteSize { get; }

        /// <summary>
        /// Gets a value indicating whether a stop has been requested.
        /// </summary>
        protected bool IsStopping => _stopping;

        /// <summary>
        /// Gets the number of currently admitted sessions.
        /// </summary>
        protected int ActiveSessions => Volatile.Read(ref _activeSessions);

        /// <summary>
        /// Creates and starts the concrete carrier.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Called at most once, after initialization,
        /// while the transport lock is held. An implementation that fails must release every resource it
        /// acquired during the attempt, because <see cref="StopCarrier"/> is not guaranteed to run after a
        /// failed start.</description></item>
        /// </list>
        /// </remarks>
        protected abstract bool StartCarrier();

        /// <summary>
        /// Stops and releases the concrete carrier.
        /// </summary>
        /// <param name="reason">The reason the transport is stopping.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Must be safe to call when the carrier was never fully
        /// started.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void StopCarrier(StopReason reason);

        /// <summary>
        /// Marks the transport as initialized exactly once.
        /// </summary>
        /// <returns><see langword="true"/> for the first successful call; otherwise
        /// <see langword="false"/>.</returns>
        protected bool TryMarkInitialized()
        {
            return Interlocked.Exchange(ref _initialized, 1) == 0;
        }

        /// <summary>
        /// Reserves one concurrent-handshake slot.
        /// </summary>
        /// <returns><see langword="true"/> when a slot was reserved; otherwise
        /// <see langword="false"/>.</returns>
        protected bool TryBeginHandshake()
        {
            if (_stopping || !IsStarted)
            {
                return false;
            }

            return TryIncrement(ref _handshakes, _maxConcurrentHandshakes);
        }

        /// <summary>
        /// Releases one concurrent-handshake slot reserved by <see cref="TryBeginHandshake"/>.
        /// </summary>
        protected void EndHandshake()
        {
            Interlocked.Decrement(ref _handshakes);
        }

        /// <summary>
        /// Reserves one active-session slot.
        /// </summary>
        /// <returns><see langword="true"/> when a slot was reserved; otherwise
        /// <see langword="false"/>.</returns>
        protected bool TryReserveSession()
        {
            if (_stopping || !IsStarted)
            {
                return false;
            }

            return TryIncrement(ref _activeSessions, _maxActiveSessions);
        }

        /// <summary>
        /// Releases one active-session slot reserved by <see cref="TryReserveSession"/>.
        /// </summary>
        protected void ReleaseSession()
        {
            Interlocked.Decrement(ref _activeSessions);
        }

        /// <summary>
        /// Starts and registers a non-started session endpoint whose session slot was already reserved.
        /// </summary>
        /// <param name="session">The non-started endpoint. Must be non-null.</param>
        /// <returns><see langword="true"/> when the endpoint was registered; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread. The registry lock is not held
        /// across <see cref="RawEndpoint.Start"/> or user handler code.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The caller owns the reserved session slot.
        /// On failure this method releases that slot exactly once, either directly or through the endpoint's
        /// terminal <see cref="RawEndpoint.Stopped"/> notification. <see cref="RawEndpoint.Start"/> is called
        /// at most once on the endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected bool TryStartReservedSession(RawReliableServerEndpoint session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            // Read the lifecycle state before taking the registry lock: IsStarted takes the base transport
            // lock, which the stop path already holds while it waits for the registry lock.
            bool isStarted = IsStarted;
            bool alive;
            lock (_sessionsLock)
            {
                alive = !_stopping && isStarted;
            }

            if (!alive)
            {
                TryStartThenStop(session, new TextFail(Name, "Transport is stopping"));
                ReleaseSession();
                return false;
            }

            var entry = new SessionEntry(session);
            entry.StoppedHandler = reason => OnSessionEnded(entry, reason);
            session.Stopped += entry.StoppedHandler;

            bool started;
            try
            {
                started = session.Start();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                started = false;
            }

            isStarted = IsStarted;
            lock (_sessionsLock)
            {
                if (entry.Ended)
                {
                    return false;
                }

                if (started && !_stopping && isStarted)
                {
                    _sessions.Add(entry);
                    return true;
                }
            }

            try
            {
                session.Stop(new TextFail(Name, "Transport is stopping"), false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            return false;
        }

        /// <summary>
        /// Invoked after a registered session reaches its terminal stopped stage and its slot is released.
        /// </summary>
        /// <param name="session">The stopped endpoint.</param>
        /// <param name="reason">The reason the endpoint stopped.</param>
        protected virtual void OnSessionStopped(RawReliableServerEndpoint session, StopReason reason)
        {
        }

        protected override bool TryStart()
        {
            if (Volatile.Read(ref _initialized) == 0)
            {
                return false;
            }

            _stopping = false;

            try
            {
                return StartCarrier();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
        }

        protected override void OnStarted()
        {
            var ticker = new SessionTicker(_tickPeriodMs, TickSessions);
            _ticker = ticker;
            ticker.Start();
        }

        protected override void OnStopped(StopReason reason)
        {
            RawReliableServerEndpoint[] snapshot;
            lock (_sessionsLock)
            {
                _stopping = true;
                snapshot = SnapshotSessions();
            }

            StopCarrier(reason);

            var ticker = _ticker;
            _ticker = null;
            if (ticker != null)
            {
                ticker.Stop();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Stop(reason, false);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
        }

        private void TryStartThenStop(RawReliableServerEndpoint session, StopReason reason)
        {
            try
            {
                session.Start();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            try
            {
                session.Stop(reason, false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private void OnSessionEnded(SessionEntry entry, StopReason reason)
        {
            lock (_sessionsLock)
            {
                if (entry.Ended)
                {
                    return;
                }

                entry.Ended = true;
                if (entry.StoppedHandler != null)
                {
                    entry.Endpoint.Stopped -= entry.StoppedHandler;
                }

                _sessions.Remove(entry);
            }

            ReleaseSession();
            OnSessionStopped(entry.Endpoint, reason);
        }

        private void TickSessions()
        {
            if (_stopping || !IsStarted)
            {
                return;
            }

            int count;
            lock (_sessionsLock)
            {
                count = _sessions.Count;
                if (count == 0)
                {
                    return;
                }

                if (_tickSnapshot.Length < count)
                {
                    _tickSnapshot = new RawReliableServerEndpoint[count];
                }

                int i = 0;
                foreach (var entry in _sessions)
                {
                    _tickSnapshot[i++] = entry.Endpoint;
                }
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    _tickSnapshot[i].PeriodicTick();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }

            Array.Clear(_tickSnapshot, 0, count);
        }

        private RawReliableServerEndpoint[] SnapshotSessions()
        {
            if (_sessions.Count == 0)
            {
                return Array.Empty<RawReliableServerEndpoint>();
            }

            var snapshot = new RawReliableServerEndpoint[_sessions.Count];
            int i = 0;
            foreach (var entry in _sessions)
            {
                snapshot[i++] = entry.Endpoint;
            }

            return snapshot;
        }

        private static bool TryIncrement(ref int counter, int limit)
        {
            int current = Volatile.Read(ref counter);
            while (current < limit)
            {
                if (Interlocked.CompareExchange(ref counter, current + 1, current) == current)
                {
                    return true;
                }

                current = Volatile.Read(ref counter);
            }

            return false;
        }

        private sealed class SessionEntry
        {
            public readonly RawReliableServerEndpoint Endpoint;
            public Action<StopReason>? StoppedHandler;
            public bool Ended;

            public SessionEntry(RawReliableServerEndpoint endpoint)
            {
                Endpoint = endpoint;
            }
        }

        private sealed class SessionTicker : PeriodicLogic
        {
            private readonly Action _tick;

            public SessionTicker(int periodMs, Action tick)
                : base(periodMs)
            {
                _tick = tick;
            }

            protected override void LogicTick()
            {
                _tick();
            }
        }
    }
}
