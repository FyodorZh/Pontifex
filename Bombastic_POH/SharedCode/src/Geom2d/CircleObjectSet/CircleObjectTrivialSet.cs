using System;
using System.Collections.Generic;
using Shared.Pool;

namespace Geom2d
{
    public class CircleObjectTrivialSet<TData> : BaseCircleObjectSet<TData>
    {
        private int mCount = 0;
        private CircleObjectImpl[] mObjects = new CircleObjectImpl[10];

        protected override CircleObject<TData> DoCreateObject(float size, TData userData)
        {
            if (mCount + 1 == mObjects.Length)
            {
                Array.Resize(ref mObjects, mObjects.Length * 2);
            }

            var obj = new CircleObjectImpl(this, size, userData);
            mObjects[mCount++] = obj;
            return obj;
        }

        protected override bool DoRemoveObject(CircleObject<TData> obj)
        {
            for (int i = 0; i < mCount; ++i)
            {
                if (mObjects[i] == obj)
                {
                    mObjects[i] = mObjects[mCount - 1];
                    mCount -= 1;
                    mObjects[mCount] = null;
                }
            }
            return false;
        }

        public override CollectableEnumerable<TData> Select(Circle region)
        {
            CollectableEnumerable<TData> list = ObjectPool<CollectableEnumerable<TData>>.Allocate();
            for (int i = 0; i < mCount; ++i)
            {
                if (mObjects[i].CurCircle.Intersect(region))
                {
                    list.Add(mObjects[i].UserDataRef);
                }
            }
            return list;
        }
    }

}
