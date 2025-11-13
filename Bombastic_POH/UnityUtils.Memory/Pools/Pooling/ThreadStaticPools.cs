using Shared.Pooling;

namespace Shared
{
    public static class ThreadStaticPools
    {
//        public static readonly IPool<byte[], int> Pow2ByteArrays = new Pow2ByteArrayPool();
//
//        /// <summary>
//        /// Создаёт объёкт являющийся полноценным "коллектабл"
//        /// </summary>
//        public static TObject ConstructCollectable<TObject>()
//            where TObject : class, INewCollectable<TObject>, new()
//        {
//            return CollectablePoolNest<TObject>.Instance.Acquire();
//        }
//
//        /// <summary>
//        /// Создаёт "коллектабл"-врапер над произвольным объектом
//        /// </summary>
//        public static IOwner<TObject> ConstructWrapper<TObject>()
//            where TObject : class, new()
//        {
//            var res = PoolNest<CollectableWrapper<TObject>>.Instance.Acquire();
//            if (res.Value == null)
//            {
//                res.SetValue(new TObject());
//            }
//
//            return res;
//        }

        /// <summary>
        /// Создаёт произвольный объект без контроля владения. Максимально быстро.
        /// </summary>
        public static TObject ConstructRaw<TObject>()
            where TObject : class, new()
        {
            return ThreadStaticPool<TObject>.Acquire();
        }

        public static void ReleaseRaw<TObject>(TObject obj)
            where TObject : class, new()
        {
            if (obj != null)
            {
                ThreadStaticPool<TObject>.Release(obj);
            }
        }
    }
}