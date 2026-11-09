using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Actuarius.Memory.Internal;
using NUnit.Framework;

namespace Pontifex.Tests.Delivery.Harness
{
    /// <summary>
    /// Pool facade that monitors every resource handed to a test and asserts that the test
    /// returns it exactly once. It wraps the byte-array and collectable pools used by the
    /// delivery subsystem; production code sees ordinary <see cref="IPool{TResource,TParam0}"/>
    /// and <see cref="ICollectablePool"/> instances.
    /// </summary>
    public sealed class TrackedResources : IDisposable
    {
        private readonly List<TrackedResource> _tracked = new();
        private readonly List<ResourceUsageErrorType> _errors = new();

        public TrackedResources(IMemoryRental memory)
        {
            Pool = new TrackingByteArrayPool(memory.ByteArraysPool, this);
            CollectablePool = new TrackingCollectablePool(memory.CollectablePool, this);
        }

        public IPool<IMultiRefByteArray, int> Pool { get; }

        public ICollectablePool CollectablePool { get; }

        public void Track(IMultiRefResource resource)
        {
            if (resource is not IMultiRefResource_Debug debug)
            {
                return;
            }

            var monitor = (MultiRefResourceMonitor)resource.ConstructMonitor(
                collectHistory: false, trackLeaks: false, traceEnabled: false);
            foreach (var existing in _tracked)
            {
                if (ReferenceEquals(existing.Monitor, monitor))
                {
                    return;
                }
            }

            monitor.Error += OnError;
            _tracked.Add(new TrackedResource(monitor, debug.RefCount));
        }

        public void AssertAllReleased()
        {
            Assert.That(_errors, Is.Empty,
                "A rented resource was misused: " + string.Join(", ", _errors));

            var alive = 0;
            foreach (var tracked in _tracked)
            {
                if (tracked.Counter.RefCount > 0)
                {
                    alive += 1;
                }
            }

            Assert.That(alive, Is.Zero,
                $"{alive} rented resource(s) were still alive at teardown.");
        }

        public void Dispose()
        {
            foreach (var tracked in _tracked)
            {
                tracked.Monitor.Error -= OnError;
                tracked.Monitor.Detach();
            }

            _tracked.Clear();
        }

        private void OnError(string resourceName, ResourceUsageErrorType error)
        {
            _errors.Add(error);
        }

        private readonly struct TrackedResource
        {
            public TrackedResource(MultiRefResourceMonitor monitor, IRefCounterView counter)
            {
                Monitor = monitor;
                Counter = counter;
            }

            public MultiRefResourceMonitor Monitor { get; }
            public IRefCounterView Counter { get; }
        }

        private sealed class TrackingByteArrayPool : IPool<IMultiRefByteArray, int>
        {
            private readonly IPool<IMultiRefByteArray, int> _inner;
            private readonly TrackedResources _owner;

            public TrackingByteArrayPool(IPool<IMultiRefByteArray, int> inner, TrackedResources owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public IMultiRefByteArray Acquire(int param0)
            {
                var resource = _inner.Acquire(param0);
                _owner.Track(resource);
                return resource;
            }

            public (IMultiRefByteArray resource, IPoolSink<IMultiRefByteArray> poolSink) AcquireEx(int param0)
            {
                var pair = _inner.AcquireEx(param0);
                _owner.Track(pair.resource);
                return pair;
            }

            public void Release(IMultiRefByteArray? resource)
            {
                _inner.Release(resource);
            }
        }

        private sealed class TrackingCollectablePool : ICollectablePool
        {
            private readonly ICollectablePool _inner;
            private readonly TrackedResources _owner;

            public TrackingCollectablePool(ICollectablePool inner, TrackedResources owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public TResource Acquire<TResource>()
                where TResource : class, ICollectableResource<TResource>, new()
            {
                var resource = _inner.Acquire<TResource>();
                if (resource is IMultiRefResource multiRef)
                {
                    _owner.Track(multiRef);
                }

                return resource;
            }
        }
    }
}
