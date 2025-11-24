using System.Collections.Generic;
using Actuarius.Collections;

namespace Actuarius.Memory
{
    public sealed class CollectableMacroOwner<TObject> : MultiRefCollectableResource<CollectableMacroOwner<TObject>>, IMacroOwner<TObject>, IConsumer<TObject>
        where TObject : IReleasableResource
    {
        private readonly List<TObject> _objects = new List<TObject>();

        public bool Put(TObject inObj)
        {
            if (IsAlive)
            {
                _objects.Add(inObj);
                return true;
            }
            inObj.Release();
            return false;
        }

        protected override void OnCollected()
        {
            foreach (var obj in _objects)
            {
                obj.Release();
            }
            _objects.Clear();
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public int Count => _objects.Count;

        public TObject this[int id] => _objects[id];
    }
}