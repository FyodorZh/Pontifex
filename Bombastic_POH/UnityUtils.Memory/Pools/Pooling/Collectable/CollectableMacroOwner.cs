using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Shared.Pooling
{
    public sealed class CollectableMacroOwner<TObject> : MultiRefCollectable<CollectableMacroOwner<TObject>>, IMacroOwner<TObject>, IConsumer<TObject>
        where TObject : IReleasableResource
    {
        private readonly List<TObject> mObjects = new List<TObject>();

        public bool Put(TObject inObj)
        {
            if (inObj != null)
            {
                if (IsAlive)
                {
                    mObjects.Add(inObj);
                    return true;
                }
                inObj.Release();
            }

            return false;
        }

        protected override void OnCollected()
        {
            for (int i = 0; i < mObjects.Count; ++i)
            {
                mObjects[i].Release();
            }

            mObjects.Clear();
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public int Count
        {
            get { return mObjects.Count; }
        }

        public TObject this[int id]
        {
            get { return mObjects[id]; }
        }
    }
}