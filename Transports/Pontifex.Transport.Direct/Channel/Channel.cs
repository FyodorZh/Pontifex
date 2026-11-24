using System;
using System.Threading;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex
{
    public sealed class Channel : IDisposable
    {
        private readonly IEndPoint _clientEp;
        private readonly IEndPoint _serverEp;
        private volatile Action<UnionDataList>? _clientHandler;
        private volatile Action<IEndPoint, UnionDataList>? _serverHandler;
        private IDeliverySystem _clientDeliverySystem;
        private IDeliverySystem _serverDeliverySystem;
        private int _disposed;

        /// <summary>
        /// Raised exactly once when the channel is disposed. Both peers share this channel instance, so
        /// the event notifies both sides. The notification carries no reason: a channel dispose is equivalent
        /// to closing a socket.
        /// </summary>
        public event Action? Disposed;

        public Channel(IEndPoint clientEp, IEndPoint serverEp)
        {
            _clientEp = clientEp;
            _serverEp = serverEp;
            _clientDeliverySystem = new PerfectDeliverySystem();
            _serverDeliverySystem = new PerfectDeliverySystem();
            _clientDeliverySystem.Delivered += OnClientDeliveredMessage;
            _serverDeliverySystem.Delivered += OnServerDeliveredMessage;
        }

        public IEndPoint ClientEp => _clientEp;

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public Action<UnionDataList>? ClientHandler
        {
            set => _clientHandler = value;
        }

        public Action<IEndPoint, UnionDataList>? ServerHandler
        {
            set => _serverHandler = value;
        }

        /// <summary>
        /// Gets the installed server handler, for carrier tests that must invoke a handler captured before a
        /// stop.
        /// </summary>
        internal Action<IEndPoint, UnionDataList>? ServerHandlerForTests => _serverHandler;

        /// <summary>
        /// It is possible and acceptable for messages that are processed right now to be undelivered.
        /// The most important invariant is to release messages.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Ownership</b>: the channel owns the installed system and releases it
        /// when the channel is disposed. A system installed after disposal, or losing a race with a
        /// concurrent disposal, is released by this call instead, because the channel can no longer
        /// own it. The caller therefore never owns an installed system.</description></item>
        /// </list>
        /// </remarks>
        public void SetClientDeliverySystem(IDeliverySystem clientDeliverySystem)
        {
            InstallDeliverySystem(ref _clientDeliverySystem, clientDeliverySystem,
                OnClientDeliveredMessage);
        }

        /// <summary>
        /// It is possible and acceptable for messages that are processed right now to be undelivered.
        /// The most important invariant is to release messages.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Ownership</b>: the channel owns the installed system and releases it
        /// when the channel is disposed. A system installed after disposal, or losing a race with a
        /// concurrent disposal, is released by this call instead, because the channel can no longer
        /// own it. The caller therefore never owns an installed system.</description></item>
        /// </list>
        /// </remarks>
        public void SetServerDeliverySystem(IDeliverySystem serverDeliverySystem)
        {
            InstallDeliverySystem(ref _serverDeliverySystem, serverDeliverySystem,
                OnServerDeliveredMessage);
        }

        /// <summary>
        /// Installs one delivery system, keeping the ownership rule that a system is released either
        /// by the channel's disposal or by the install itself, never by both and never by neither.
        /// </summary>
        private void InstallDeliverySystem(ref IDeliverySystem field, IDeliverySystem system,
            Action<UnionDataList> handler)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (Volatile.Read(ref _disposed) != 0)
            {
                SafeRelease(system);
                return;
            }

            try
            {
                system.Delivered += handler;
            }
            catch (Exception)
            {
                // The subscription failed before the channel took ownership; the caller still owns the
                // system, so the install must not release it.
                throw;
            }

            IDeliverySystem old = Interlocked.Exchange(ref field, system);

            if (ReferenceEquals(old, system))
            {
                // Re-installing the current instance keeps a single subscription.
                system.Delivered -= handler;
                return;
            }

            try
            {
                old.Delivered -= handler;

                // The same instance may still be referenced by the other direction. In that case the
                // other field (or disposal) owns the single release.
                if (!ReferenceEquals(old, _clientDeliverySystem)
                    && !ReferenceEquals(old, _serverDeliverySystem))
                {
                    SafeRelease(old);
                }
            }
            catch (Exception)
            {
                // Once the exchange succeeded the channel owns the new system. A later subscription or
                // release failure must not escape and must not make the caller release the system again.
            }

            if (Volatile.Read(ref _disposed) == 0)
            {
                return;
            }

            // A disposal raced this install. The disposal exchanges each field and releases what it
            // finds, so if it exchanged before the install it never saw the new system. The
            // compare-exchange succeeds only in that case: take the system back and release it.
            if (ReferenceEquals(Interlocked.CompareExchange(ref field, old, system), system))
            {
                try
                {
                    system.Delivered -= handler;
                }
                catch (Exception)
                {
                }

                SafeRelease(system);
            }
        }
        
        private static void SafeRelease(IDeliverySystem? system)
        {
            if (system == null)
            {
                return;
            }

            try
            {
                DeliverySystemLifetime.Release(system);
            }
            catch (Exception)
            {
            }
        }

        private void OnClientDeliveredMessage(UnionDataList message)
        {
            var handler = _clientHandler;

            if (handler != null)
                handler(message);
            else
                message.Release();
        }

        private void OnServerDeliveredMessage(UnionDataList message)
        {
            var handler = _serverHandler;

            if (handler != null)
                handler(_clientEp, message);
            else
                message.Release();
        }

        public SendResult SendToClient(UnionDataList message)
        {
            if (IsDisposed)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            Volatile.Read(ref _clientDeliverySystem).Deliver(message);
            return SendResult.Ok;
        }

        public SendResult SendToServer(UnionDataList message)
        {
            if (IsDisposed)
            {
                message.Release();
                return SendResult.NotConnected;
            }

            Volatile.Read(ref _serverDeliverySystem).Deliver(message);
            return SendResult.Ok;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            var oldClient = Interlocked.Exchange(ref _clientDeliverySystem,
                DiscardingDeliverySystem.Instance);
            try
            {
                oldClient.Delivered -= OnClientDeliveredMessage;
            }
            catch (Exception)
            {
            }

            SafeRelease(oldClient);

            var oldServer = Interlocked.Exchange(ref _serverDeliverySystem,
                DiscardingDeliverySystem.Instance);
            try
            {
                oldServer.Delivered -= OnServerDeliveredMessage;
            }
            catch (Exception)
            {
            }

            if (!ReferenceEquals(oldServer, oldClient))
            {
                SafeRelease(oldServer);
            }

            _clientHandler = null;
            _serverHandler = null;

            var disposed = Disposed;
            Disposed = null;

            if (disposed == null)
            {
                return;
            }

            // Teardown must complete even when a subscriber misbehaves. The channel has no logger, so
            // failures are swallowed, but every subscriber still receives its notification.
            foreach (var subscriber in disposed.GetInvocationList())
            {
                try
                {
                    ((Action)subscriber)();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
