using System;
using System.Collections.Generic;

namespace Shared
{
    public class ThreadCache
    {
        private class ThreadDictionary<TK, T> : ThreadSingleton<ThreadDictionary<TK, T>>
        {
            private readonly Dictionary<TK, T> dict = new Dictionary<TK, T>();

            public static T GetOrCreate(TK key, ObjectFactory<T> factory)
            {
                T data;
                if (!Instance.dict.TryGetValue(key, out data))
                {
                    data = factory();
                    Instance.dict[key] = data;
                }
                return data;
            }
        }

        private class ThreadBox<T> : ThreadSingleton<ThreadBox<T>>
        {
            private T poolObject;

            public static T GetOrCreate(ObjectFactory<T> factory)
            {
                T pObject = Instance.poolObject;
                if (pObject == null)
                {
                    pObject = factory();
                    Instance.poolObject = pObject;
                }
                return pObject;
            }
        }


        public static T GetOrCreate<TK, T>(TK key, ObjectFactory<T> factory)
        {
            return ThreadDictionary<TK, T>.GetOrCreate(key, factory);
        }

        public static T GetOrCreateByType<T>(ObjectFactory<T> factory)
        {
            return ThreadBox<T>.GetOrCreate(factory);
        }

        public delegate T ObjectFactory<out T>();
    }
}
