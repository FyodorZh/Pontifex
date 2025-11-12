using System;
using System.Collections.Generic;

namespace Geom2d
{
    public class CircleObjectSortedSet<TData> : BaseCircleObjectSet<TData>
    {
        private class ObjectComparer : IComparer<CircleObjectImpl>
        {
            public static readonly ObjectComparer Instance = new ObjectComparer();

            private ObjectComparer()
            {
            }

            public int Compare(CircleObjectImpl x, CircleObjectImpl y)
            {
                return x.CurPosition.x.CompareTo(y.CurPosition.x);
            }
        }

        private int mCount = 0;
        private CircleObjectImpl[] mObjects = new CircleObjectImpl[100];

        private float mMaxOjbectSize = 0;

        public override float MaxUnitSize
        {
            get
            {
                return mMaxOjbectSize;
            }
        }

        public void Prepare()
        {
            Array.Sort(mObjects, 0, mCount, ObjectComparer.Instance);
        }

        protected override void Resized(BaseCircleObjectSet<TData>.CircleObjectImpl obj)
        {
            if (obj.Size >= mMaxOjbectSize - C.EPS)
            {
                mMaxOjbectSize = 0;
                for (int i = 0; i < mCount; ++i)
                {
                    mMaxOjbectSize = Math.Max(mMaxOjbectSize, mObjects[i].Size);
                }
            }
        }

        protected override CircleObject<TData> DoCreateObject(float size, TData userData)
        {
            CircleObjectImpl point = new CircleObjectImpl(this, size, userData);

            if (mCount + 1 == mObjects.Length)
            {
                Array.Resize(ref mObjects, mObjects.Length * 2);
            }
            mObjects[mCount++] = point;
            mMaxOjbectSize = Math.Max(mMaxOjbectSize, size);

            return point;
        }

        protected override bool DoRemoveObject(CircleObject<TData> obj)
        {
            int id = FindLeftmostObjectId(obj.Position.x);
            if (id != -1)
            {
                while (id < mCount)
                {
                    if (mObjects[id].Position.x > obj.Position.x)
                    {
                        Log.e("Katastrofa!!! Sorting failure");
                        return false;
                    }

                    if (mObjects[id] == obj)
                    {
                        for (int i = id + 1; i < mCount; ++i)
                        {
                            mObjects[i - 1] = mObjects[i];
                        }
                        mCount -= 1;
                        mObjects[mCount] = null;

                        if (obj.Size >= mMaxOjbectSize - C.EPS)
                        {
                            mMaxOjbectSize = 0;
                            for (int i = 0; i < mCount; ++i)
                            {
                                mMaxOjbectSize = Math.Max(mMaxOjbectSize, mObjects[i].Size);
                            }
                        }

                        // It's OK
                        return false;
                    }

                    ++id;
                }
            }

            Log.e("Katastrofa!!! Can't remove entity from sorted set");
            return false;
        }

        // public override CollectableEnumerable<TData> Select(Circle circle)
        // {
        //     CollectableEnumerable<TData> list = ObjectPool<CollectableEnumerable<TData>>.Allocate();
        //
        //     float r = circle.r + mMaxOjbectSize;
        //     int id = FindLeftmostObjectId(circle.x - r);
        //     if (id != -1)
        //     {
        //         float rightBorder = circle.x + r;
        //
        //         while (id < mCount)
        //         {
        //             if (mObjects[id].Position.x > rightBorder)
        //             {
        //                 break;
        //             }
        //
        //             if (circle.Intersect(mObjects[id].CurCircle))
        //             {
        //                 list.Add(mObjects[id].UserDataRef);
        //             }
        //             ++id;
        //         }
        //     }
        //
        //     return list;
        // }

        private int FindLeftmostObjectId(float xPos)
        {
            if (GetAndClearRefreshFlag())
            {
                Prepare();
            }

            if (mCount > 0)
            {
                int a = 0;
                int b = mCount - 1;
                int m;
                while (a + 1 < b)
                {
                    m = (a + b) / 2;
                    if (mObjects[m].CurPosition.x < xPos)
                    {
                        a = m;
                    }
                    else
                    {
                        b = m;
                    }
                }
                if (a != b)
                {
                    m = mObjects[a].CurPosition.x < xPos ? b : a;
                }
                else
                {
                    m = a;
                }
                if (mObjects[m].CurPosition.x < xPos)
                {
                    m = -1;
                }

                return m;
            }
            return -1;
        }
    }
}
