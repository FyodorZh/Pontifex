using System.Net.Sockets;
using System.Reflection;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// A <see cref="SocketAsyncEventArgs"/> whose completion can be driven manually by a test.
    /// <see cref="SocketAsyncEventArgs.BytesTransferred"/> is read-only and
    /// <see cref="SocketAsyncEventArgs.OnCompleted"/> is protected, so completion is simulated by
    /// writing the private transfer field and invoking the protected completion hook.
    /// </summary>
    /// <remarks>
    /// The native contract distinguishes a synchronous result (the posting call returns
    /// <c>false</c> and no event is raised) from an asynchronous completion (the posting call
    /// returns <c>true</c> and <see cref="SocketAsyncEventArgs.Completed"/> is raised exactly once).
    /// Use <see cref="SetSynchronousResult"/> for the former and <see cref="Complete"/> for the
    /// latter; <see cref="FakeSocketAccessor"/> in strict mode flags callers that conflate them.
    /// </remarks>
    internal sealed class TestSocketAsyncEventArgs : SocketAsyncEventArgs
    {
        private static readonly FieldInfo BytesTransferredField = FindBytesTransferredField();

        private int _completedCount;

        public TestSocketAsyncEventArgs()
        {
            SocketFlags = SocketFlags.None;
        }

        /// <summary>Number of times <see cref="SocketAsyncEventArgs.Completed"/> was raised by this instance.</summary>
        public int CompletedCount => Volatile.Read(ref _completedCount);

        /// <summary>
        /// Invoked just before the <see cref="SocketAsyncEventArgs.Completed"/> handlers run. The
        /// owning <see cref="FakeSocketAccessor"/> uses it to clear the args' in-use state before a
        /// callback can repost the same args.
        /// </summary>
        internal Action<TestSocketAsyncEventArgs>? CompletionRaising { get; set; }

        /// <summary>
        /// Writes the transfer result of a synchronously completed operation. Unlike
        /// <see cref="Complete"/> this never raises <see cref="SocketAsyncEventArgs.Completed"/>,
        /// matching a real accessor that returns <c>false</c> from its posting call.
        /// </summary>
        public void SetSynchronousResult(int bytesTransferred, SocketError socketError = SocketError.Success)
        {
            BytesTransferredField.SetValue(this, bytesTransferred);
            SocketError = socketError;
        }

        /// <summary>
        /// Marks the operation as asynchronously completed with the given transfer result and raises
        /// <see cref="SocketAsyncEventArgs.Completed"/> exactly once per call.
        /// </summary>
        public void Complete(int bytesTransferred, SocketError socketError = SocketError.Success)
        {
            BytesTransferredField.SetValue(this, bytesTransferred);
            SocketError = socketError;
            Interlocked.Increment(ref _completedCount);
            CompletionRaising?.Invoke(this);
            OnCompleted(this);
        }

        /// <summary>Marks the operation as completed with an error and raises <see cref="SocketAsyncEventArgs.Completed"/>.</summary>
        public void CompleteError(SocketError socketError) => Complete(0, socketError);

        /// <summary>
        /// True once <see cref="SocketAsyncEventArgs.Dispose"/> has run, detected through the public
        /// post-disposal behavior (<see cref="SocketAsyncEventArgs.SetBuffer(byte[]?, int, int)"/>
        /// throws <see cref="ObjectDisposedException"/>). <c>Dispose</c> itself is virtual-but-sealed,
        /// so a derived args type cannot observe the call directly.
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                try
                {
                    SetBuffer(null, 0, 0);
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            }
        }

        private static FieldInfo FindBytesTransferredField()
        {
            Type type = typeof(SocketAsyncEventArgs);

            foreach (string name in new[] { "_bytesTransferred", "m_BytesTransferred" })
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(int))
                {
                    return field;
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType == typeof(int) && field.Name.Contains("Transferred", StringComparison.Ordinal))
                {
                    return field;
                }
            }

            throw new InvalidOperationException(
                $"Cannot locate the BytesTransferred backing field on {type.FullName}.");
        }
    }
}
