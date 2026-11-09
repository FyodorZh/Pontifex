using System;
using System.Collections.Generic;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Utils;

namespace Pontifex.Tests.Delivery.Harness
{
    /// <summary>
    /// Base fixture that supplies tracked byte-array and collectable pools and verifies at
    /// teardown that every rented resource was released. Tests may register cleanup actions
    /// (for example a manager <c>Clear</c>) and captured messages that the fixture releases.
    /// </summary>
    public abstract class TrackedFixture
    {
        private readonly List<Action> _cleanup = new();
        private readonly List<UnionDataList> _captured = new();

        protected TrackedResources Resources { get; private set; } = null!;

        protected IPool<IMultiRefByteArray, int> Pool => Resources.Pool;

        protected ICollectablePool CPool => Resources.CollectablePool;

        [SetUp]
        public void SetUpTrackedResources()
        {
            Resources = new TrackedResources(new MemoryRental());
            SetUp();
        }

        [TearDown]
        public void ReleaseTrackedResources()
        {
            try
            {
                foreach (var cleanup in _cleanup)
                {
                    cleanup();
                }

                _cleanup.Clear();

                foreach (var message in _captured)
                {
                    message.Release();
                }

                _captured.Clear();

                Resources.AssertAllReleased();
            }
            finally
            {
                Resources.Dispose();
            }
        }

        protected virtual void SetUp()
        {
        }

        protected void AddCleanup(Action cleanup)
        {
            _cleanup.Add(cleanup);
        }

        protected void TrackCaptured(UnionDataList message)
        {
            _captured.Add(message);
        }
    }
}
