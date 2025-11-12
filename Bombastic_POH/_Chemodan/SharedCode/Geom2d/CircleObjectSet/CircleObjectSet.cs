using System;
using System.Collections.Generic;
namespace Geom2d
{
    public abstract class BaseCircleObjectSet<TData>
    {
        protected class CircleObjectImpl : CircleObject<TData>
        {
            private readonly BaseCircleObjectSet<TData> mOwner;
            public readonly TData UserDataRef;
            public Vector CurPosition;
            public Circle CurCircle;
            
            public CircleObjectImpl(BaseCircleObjectSet<TData> owner, float size, TData userData)
            {
                UserDataRef = userData;
                mOwner = owner;
                CurCircle = new Circle(CurPosition, size);
            }

            public override void Destroy()
            {
                mOwner.RemoveObject(this);
            }

            public override TData UserData
            {
                get { return UserDataRef; }
            }

            public override float Size
            {
                get { return CurCircle.r; }
                set
                {
                    CurCircle = new Circle(CurPosition, value);
                    mOwner.Resized(this);
                }
            }

            public override Vector Position
            {
                get { return CurPosition; }
                set
                {
                    CurPosition = value;
                    CurCircle.Position = value;
                    mOwner.mDataRefreshed = true;
                }
            }
        }

        private bool mDataRefreshed = false;

        public CircleObject<TData> CreateObject(float size, TData userData)
        {
            mDataRefreshed = true;
            return DoCreateObject(size, userData);
        }

        public void RemoveObject(CircleObject<TData> obj)
        {
            mDataRefreshed |= DoRemoveObject(obj);
        }

        protected virtual void Resized(CircleObjectImpl obj)
        {
        }

        public virtual float MaxUnitSize
        {
            get
            {
                throw new InvalidOperationException();
            }
        }
        
        //public abstract Shared.Pool.CollectableEnumerable<TData> Select(Circle region);

        protected abstract CircleObject<TData> DoCreateObject(float size, TData userData);

        protected abstract bool DoRemoveObject(CircleObject<TData> obj);

        protected bool GetAndClearRefreshFlag()
        {
            var flag = mDataRefreshed;
            mDataRefreshed = false;
            return flag;
        }
    }
}