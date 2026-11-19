using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Operarius;
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
    /// <see cref="TryStartAndRegisterSession"/>.</description></item>
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

        private IRawReliableServerEndpointImp[] _tickSnapshot = Array.Empty<IRawReliableServerEndpointImp>();

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
        /// Gets a value indicating whether the transport needs periodic checks for disconnection by timeout.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: When <see langword="true"/>, the base transport starts the
        /// shared session ticker, which calls <see cref="RawReliableServerEndpoint.PeriodicTick"/> for every
        /// active session. Carriers without an idle-session timeout can override this to
        /// <see langword="false"/> to avoid a dedicated ticker thread. The default is
        /// <see langword="true"/>.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool DisconnectionByTimeoutCheck => true;

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
        /// Creates the driver that runs the shared session ticker.
        /// </summary>
        /// <returns>The logic driver used to execute the session ticker.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default implementation returns a
        /// <see cref="ThreadBasedPeriodicMultiLogicDriver"/> configured with the transport's tick period.
        /// Override this method to supply a different driver.</description></item>
        /// </list>
        /// </remarks>
        protected virtual ILogicDriver<IPeriodicLogicDriverCtl> LogicDriverFactory()
        {
            return new ThreadBasedPeriodicMultiLogicDriver(
                NowDateTimeProvider.Instance,
                TimeSpan.FromMilliseconds(_tickPeriodMs));
        }

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
        protected bool TryStartAndRegisterSession(IRawReliableServerEndpointImp session)
        {
            return TryStartAndRegisterSession(session, out _);
        }

        /// <summary>
        /// Starts and registers a non-started session endpoint whose session slot was already reserved and
        /// reports whether the endpoint accepted its start.
        /// </summary>
        /// <param name="session">The non-started endpoint. Must be non-null.</param>
        /// <param name="sessionStarted">Receives <see langword="true"/> when <see cref="RawEndpoint.Start"/>
        /// was accepted, including the transport-stopping path where the endpoint is started only to be
        /// stopped again; otherwise <see langword="false"/> when the start attempt was rejected or
        /// threw.</param>
        /// <returns><see langword="true"/> when the endpoint was registered; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread. The registry lock is not held
        /// across <see cref="RawEndpoint.Start"/> or user handler code.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The caller owns the reserved session slot.
        /// On failure this method releases that slot exactly once, either directly or through the endpoint's
        /// terminal <see cref="RawEndpoint.Stopped"/> notification. The release may be deferred to that
        /// notification when the endpoint was already started, so a <see langword="false"/> return does not
        /// always mean the slot is free yet. <see cref="RawEndpoint.Start"/> is called at most once on the
        /// endpoint. <see cref="OnSessionStopped"/> is invoked only when the endpoint was successfully
        /// registered.</description></item>
        /// <item><description><b>Other notes</b>: A session whose start was accepted owns its terminal
        /// <see cref="IRawHandler.OnStopped"/> notification, even when registration fails afterwards; an
        /// admission fallback must not preempt it. Only <paramref name="sessionStarted"/> =
        /// <see langword="false"/> leaves the handler without a terminal notification from the endpoint
        /// itself.</description></item>
        /// </list>
        /// </remarks>
        protected bool TryStartAndRegisterSession(IRawReliableServerEndpointImp session, out bool sessionStarted)
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
                sessionStarted = TryStartThenStop(session, new TextFail(Name, "Transport is stopping"));
                ReleaseSession();
                return false;
            }

            var entry = new SessionEntry(this, session);
            session.Stopped += entry.OnStopped;

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

            sessionStarted = started;

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
        /// Reserves one admission for a new connection: one active-session slot and one concurrent-handshake
        /// slot.
        /// </summary>
        /// <returns><see langword="true"/> when both slots were reserved; otherwise
        /// <see langword="false"/> with no slots held.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from the carrier's accept callback.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A successful caller must eventually call
        /// either
        /// <see cref="CompleteAdmission(IRawReliableServerEndpointImp, IRawReliableServerHandler, StopReason)"/>
        /// or <see cref="AbandonAdmission()"/> exactly once, using the handler-aware overloads when it created
        /// a session handler.</description></item>
        /// </list>
        /// </remarks>
        protected bool TryBeginAdmission()
        {
            if (!TryReserveSession())
            {
                return false;
            }

            if (!TryBeginHandshake())
            {
                ReleaseSession();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts a successful admission into a registered session, notifying the session handler when the
        /// session could not even be started.
        /// </summary>
        /// <param name="session">The non-started session endpoint. Must be non-null.</param>
        /// <param name="handler">The stop-once gate wrapping the session handler for this admission, or
        /// <see langword="null"/> when no handler exists.</param>
        /// <param name="failureReason">The reason passed to the handler's
        /// <see cref="IRawHandler.OnStopped"/> when the session could not be started.</param>
        /// <returns><see langword="true"/> when the session was registered; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called exactly once for an admission
        /// reserved by <see cref="TryBeginAdmission"/>. The session slot is released by the base transport when
        /// the session ends, or by
        /// <see cref="TryStartAndRegisterSession(IRawReliableServerEndpointImp)"/> when registration
        /// fails.</description></item>
        /// <item><description><b>Other notes</b>: The handler is notified only when the session did not accept
        /// its start. A started session owns its terminal notification, including the path where the transport
        /// is stopping and registration fails; the shared gate deduplicates if both paths fire. A handler
        /// exception is logged and does not change the admission result.</description></item>
        /// </list>
        /// </remarks>
        protected bool CompleteAdmission(IRawReliableServerEndpointImp session,
            IRawReliableServerHandler? handler, StopReason failureReason)
        {
            EndHandshake();
            bool registered = TryStartAndRegisterSession(session, out bool sessionStarted);
            if (!sessionStarted)
            {
                try
                {
                    handler?.OnStopped(failureReason);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }

            return registered;
        }

        /// <summary>
        /// Releases an admission that will not become a session.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called exactly once for an admission
        /// reserved by <see cref="TryBeginAdmission"/> that is being rejected, timed out, or aborted. Must not
        /// be called after
        /// <see cref="CompleteAdmission(IRawReliableServerEndpointImp, IRawReliableServerHandler, StopReason)"/>.</description></item>
        /// <item><description><b>Other notes</b>: This overload is for carrier paths where no handler exists.
        /// A path that created a session handler must use the overload that notifies it.</description></item>
        /// </list>
        /// </remarks>
        protected void AbandonAdmission()
        {
            AbandonAdmission(null, new TextFail(Name, "Admission abandoned"));
        }

        /// <summary>
        /// Releases an admission that will not become a session and notifies the session handler exactly once.
        /// </summary>
        /// <param name="handler">The stop-once gate wrapping the session handler for this admission, or
        /// <see langword="null"/> when no handler exists.</param>
        /// <param name="reason">The reason passed to the handler's
        /// <see cref="IRawHandler.OnStopped"/>.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called exactly once for an admission
        /// reserved by <see cref="TryBeginAdmission"/> that is being rejected, timed out, or aborted. Must not
        /// be called after
        /// <see cref="CompleteAdmission(IRawReliableServerEndpointImp, IRawReliableServerHandler, StopReason)"/>.
        /// The handshake slot and the session slot are released before the handler is notified.</description></item>
        /// <item><description><b>Other notes</b>: A handler exception is logged and swallowed. The shared gate
        /// keeps the notification one-shot if another path already reported the stop.</description></item>
        /// </list>
        /// </remarks>
        protected void AbandonAdmission(IRawReliableServerHandler? handler, StopReason reason)
        {
            EndHandshake();
            ReleaseSession();

            try
            {
                handler?.OnStopped(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <summary>
        /// Invoked after a registered session reaches its terminal stopped stage and its slot is released.
        /// </summary>
        /// <param name="session">The stopped endpoint.</param>
        /// <param name="reason">The reason the endpoint stopped.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Invoked only for a session that was added to the transport's
        /// registry. A session that stops while it is still being admitted is not reported here; in that case
        /// <see cref="TryStartAndRegisterSession"/> returns <see langword="false"/> and the caller is
        /// responsible for any carrier-side cleanup. The slot is released before this method is
        /// invoked.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnSessionStopped(IRawReliableServerEndpointImp session, StopReason reason)
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
            if (!DisconnectionByTimeoutCheck)
            {
                return;
            }

            var ticker = new SessionTicker(TickSessions, LogicDriverFactory, Log);
            _ticker = ticker;
            ticker.Start();
        }

        protected override void OnStopped(StopReason reason)
        {
            IRawReliableServerEndpointImp[] snapshot;
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

        private bool TryStartThenStop(IRawReliableServerEndpointImp session, StopReason reason)
        {
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

            try
            {
                session.Stop(reason, false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            return started;
        }

        private void OnSessionEnded(SessionEntry entry, StopReason reason)
        {
            bool registered;
            lock (_sessionsLock)
            {
                if (entry.Ended)
                {
                    return;
                }

                entry.Ended = true;
                entry.Endpoint.Stopped -= entry.OnStopped;
                registered = _sessions.Remove(entry);
            }

            ReleaseSession();
            if (registered)
            {
                OnSessionStopped(entry.Endpoint, reason);
            }
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
                    _tickSnapshot = new IRawReliableServerEndpointImp[count];
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

        private IRawReliableServerEndpointImp[] SnapshotSessions()
        {
            if (_sessions.Count == 0)
            {
                return Array.Empty<IRawReliableServerEndpointImp>();
            }

            var snapshot = new IRawReliableServerEndpointImp[_sessions.Count];
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
            private readonly RawReliableServerTransport<TServerHandler> _owner;

            public readonly IRawReliableServerEndpointImp Endpoint;
            public bool Ended;

            public SessionEntry(RawReliableServerTransport<TServerHandler> owner, IRawReliableServerEndpointImp endpoint)
            {
                _owner = owner;
                Endpoint = endpoint;
            }

            public void OnStopped(StopReason reason)
            {
                _owner.OnSessionEnded(this, reason);
            }
        }

        private sealed class SessionTicker : IPeriodicLogic
        {
            private readonly Action _tick;
            private readonly Func<ILogicDriver<IPeriodicLogicDriverCtl>> _logicDriverFactory;
            private readonly ILogger _log;

            private ILogicDriver<IPeriodicLogicDriverCtl>? _driver;
            private volatile IPeriodicLogicDriverCtl? _driverCtl;

            public SessionTicker(Action tick, Func<ILogicDriver<IPeriodicLogicDriverCtl>> logicDriverFactory, ILogger log)
            {
                _tick = tick;
                _logicDriverFactory = logicDriverFactory;
                _log = log;
            }

            public void Start()
            {
                _driver = _logicDriverFactory();
                _driver.Start(this);
            }

            public void Stop()
            {
                var driver = _driver;
                _driver = null;

                _driverCtl?.Stop();

                if (driver == null)
                {
                    return;
                }

                // Stopping the per-logic control only asks the logic to stop. A thread-based driver owns a
                // dedicated thread that outlives that request, so the driver itself must be finished as well,
                // otherwise the thread keeps ticking forever after the transport stops.
                // Finish() is deliberately not awaited: stop runs under the transport lock, and waiting for the
                // ticker thread would deadlock if it is currently acquiring the same lock to observe IsStarted.
                try
                {
                    Task finish = driver.Finish();
                    finish.ContinueWith(
                        t => _log.wtf(t.Exception!),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }
            }

            bool ILogic<IPeriodicLogicDriverCtl>.LogicStarted(IPeriodicLogicDriverCtl driver)
            {
                _driverCtl = driver;
                return true;
            }

            void IPeriodicLogic.LogicTick(IPeriodicLogicDriverCtl driver)
            {
                _tick();
            }

            void ILogic<IPeriodicLogicDriverCtl>.LogicStopped()
            {
                _driverCtl = null;
            }
        }
    }
}
