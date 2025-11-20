using Actuarius.Memory;

namespace Shared.Buffer
{
    public interface IMemoryBufferPool : IPool<IMemoryBuffer>
    {
        /// <summary>
        /// Хак для экономии памяти в MemoryBufferHolder
        /// </summary>
        ILogger Log { get; }
    }

    public static class MemoryBufferPoolExt
    {
        private static IMemoryBufferHolder Wrap(IMemoryBufferPool pool, IMemoryBuffer buffer)
        {
            MemoryBufferAsHolder holder = buffer as MemoryBufferAsHolder;
            if (holder != null)
            {
                holder.Init(pool);
                return holder;
            }
            return new MemoryBufferHolder(pool, buffer);
        }

        public static IMemoryBufferHolder Allocate(this IMemoryBufferPool pool)
        {
            var buffer = pool.Acquire();
            return Wrap(pool, buffer);
        }

        public static IMemoryBufferHolder AllocateAndPush(this IMemoryBufferPool pool, byte[] data)
        {
            var buffer = pool.Acquire();
            buffer.PushArray(new ByteArraySegment(data));
            return Wrap(pool, buffer);
        }

        public static bool AllocateAndDeserializeGeneric<TBytes>(this IMemoryBufferPool pool, TBytes data, out IMemoryBufferHolder bufferAccessor)
            where TBytes : IMultiRefLowLevelByteArray
        {
            var buffer = pool.Acquire();
            if (buffer.ReadFrom(data))
            {
                bufferAccessor = Wrap(pool, buffer);
                return true;
            }
            pool.Release(buffer);
            bufferAccessor = null;
            return false;
        }

        public static bool AllocateAndDeserialize(this IMemoryBufferPool pool, ByteArraySegment data, out IMemoryBufferHolder bufferAccessor)
        {
            return pool.AllocateAndDeserializeGeneric(data, out bufferAccessor);
        }

        public static bool AllocateAndDeserialize(this IMemoryBufferPool pool, IReadOnlyBytes data, out IMemoryBufferHolder bufferAccessor)
        {
            using (var bufferOwner = data.ToLowLevelByteArray().Own())
            {
                return pool.AllocateAndDeserializeGeneric(bufferOwner.Array, out bufferAccessor);
            }
        }

        public static bool AllocateAndClone(this IMemoryBufferPool pool, IMemoryBufferHolder srcBuffer, out IMemoryBufferHolder bufferAccessor)
        {
            var buffer = pool.Acquire();
            if (buffer.CloneFrom(srcBuffer))
            {
                bufferAccessor = Wrap(pool, buffer);
                return true;
            }
            pool.Release(buffer);
            bufferAccessor = null;
            return false;
        }

        /// <summary>
        /// TODO: Переделать на реальные снапшоты для оптимизации
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="srcBuffer"></param>
        /// <returns></returns>
        public static IMemoryBufferHolder SnapshotOf(this IMemoryBufferPool pool, IMemoryBufferHolder srcBuffer)
        {
            IMemoryBufferHolder clone;
            pool.AllocateAndClone(srcBuffer, out clone);
            return clone;
        }

    }
}
