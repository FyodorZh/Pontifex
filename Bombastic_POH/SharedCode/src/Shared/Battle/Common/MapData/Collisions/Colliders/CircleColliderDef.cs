using Geom2d;

namespace Shared.Battle.Collisions
{
    public sealed class CircleColliderDef : ColliderDef
    {
        const string FN_OFFSET = "Offset";
        const string FN_RADIUS = "Size";

        public Vector Offset;
        public float Radius;

        public override ColliderDefType Type()
        {
            return ColliderDefType.Circle;
        }

        protected override void OnDeserialize(StorageFolder from)
        {
            Offset = SerializationUtils.DeserializeVector(from.GetFolder(FN_OFFSET));
            Radius = from.GetItemAsFloat(FN_RADIUS);
        }

        protected override void OnSerialize(StorageFolder to)
        {
            to.AddItem(SerializationUtils.SerializeVector(FN_OFFSET, Offset));
            to.AddItem(new StorageFloat(FN_RADIUS, Radius));
        }
    }
}
