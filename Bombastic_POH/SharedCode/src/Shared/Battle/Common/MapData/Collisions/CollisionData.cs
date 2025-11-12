using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shared.Battle.Collisions
{
    public sealed class CollisionData : ISerializable
    {
        const string FN_COLLIDERS = "Colliders";

        public List<ColliderDef> Colliders;

        public void Deserialize(StorageFolder from)
        {
            var collidersF = from.GetFolder(FN_COLLIDERS);
            if (collidersF != null)
            {
                Colliders = new List<ColliderDef>(collidersF.Count);
                for (int i = 0; i < collidersF.Count; i++)
                {
                    var item = collidersF.GetItem(i);
                    Colliders.Add(ColliderDef.FromFolder(item as StorageFolder));
                }
            }
        }

        public void Serialize(StorageFolder to)
        {
            if (Colliders != null && Colliders.Count > 0)
            {
                var collidersF = new StorageFolder(FN_COLLIDERS);
                foreach(var c in Colliders)
                {
                    var colliderF = new StorageFolder();
                    c.Serialize(colliderF);
                    collidersF.AddItem(colliderF);
                }

                to.AddItem(collidersF);
            }
        }
    }
}
