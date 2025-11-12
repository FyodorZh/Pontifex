using Geom2d;

namespace Shared.Battle.Collisions
{
    public sealed class EdgeColliderDef : ColliderDef
    {
        public override ColliderDefType Type()
        {
            return ColliderDefType.Edge;
        }

        const string FN_POINTS = "Points";
        const string FN_IS_LOOP = "IsLoop";

        public Vector[] Points;
        public bool IsLoop;
        
        protected override void OnDeserialize(StorageFolder from)
        {
            var pointsFolder = from.GetFolder(FN_POINTS);
            Points = new Vector[pointsFolder.Count];
            
            for(int i = 0; i < Points.Length; ++i)
            {
                Points[i] = SerializationUtils.DeserializeVector(pointsFolder.GetItem(i) as StorageFolder);
            }
            IsLoop = from.GetItemAsBool(FN_IS_LOOP);
        } 

        protected override void OnSerialize(StorageFolder to)
        {
            if (Points != null && Points.Length > 0)
            {
                StorageFolder pointsFolder = new StorageFolder(FN_POINTS);

                for (int i = 0; i < Points.Length; i++)
                {
                    pointsFolder.AddItem(SerializationUtils.SerializeVector(i.ToString(), Points[i]));
                }
                to.AddItem(pointsFolder);
                to.AddItem(new StorageBool(FN_IS_LOOP, IsLoop));
            }
        }
    }
}
