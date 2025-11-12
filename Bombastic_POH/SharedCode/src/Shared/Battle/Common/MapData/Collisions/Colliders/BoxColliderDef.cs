using Geom2d;

namespace Shared.Battle.Collisions
{
    public sealed class BoxColliderDef : ColliderDef
    {
        const string FN_OFFSET = "Offset";
        const string FN_SIZE = "Size";

        public Vector Offset;
        public Vector Size;

        public override ColliderDefType Type()
        {
            return ColliderDefType.Box;
        }

        protected override void OnDeserialize(StorageFolder from)
        {
            Offset = SerializationUtils.DeserializeVector(from.GetFolder(FN_OFFSET));
            Size = SerializationUtils.DeserializeVector(from.GetFolder(FN_SIZE));
        }

        protected override void OnSerialize(StorageFolder to)
        {
            to.AddItem(SerializationUtils.SerializeVector(FN_OFFSET, Offset));
            to.AddItem(SerializationUtils.SerializeVector(FN_SIZE, Size));
        }
    }
}
