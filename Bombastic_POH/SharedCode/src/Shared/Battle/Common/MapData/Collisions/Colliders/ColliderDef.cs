using System;
using System.Collections.Generic;

namespace Shared.Battle.Collisions
{
    [Flags]
    public enum CollisionMask
    {
        None = 0,
        Player = 1,
        Enemies = 2,
        Projectiles = 4,

        // FLAGS
        BothSides = 8
    }

    public enum ColliderDefType
    {
        Edge,
        Box,
        Polygon,
        Circle
    }

    public struct ColliderDefTypeComparer : IEqualityComparer<ColliderDefType>
    {
        public bool Equals(ColliderDefType x, ColliderDefType y)
        {
            return x == y;
        }

        public int GetHashCode(ColliderDefType val)
        {   
            return ((int)val).GetHashCode();
        }
    }

    public abstract class ColliderDef : ISerializable
    {
        public abstract ColliderDefType Type();
        public Geom2d.Vector Position;
        public Geom2d.Rotation Rotation;
        public Geom2d.Vector Scale;
        public int ID;
        public CollisionMask CollisionLayer;

        public bool IsTrigger = false;

        const string FN_TYPE = "Type";
        const string FN_POSITION = "Position";
        const string FN_ROTATION = "Rotation";
        const string FN_SCALE = "Scale";
        const string FN_LAYER = "Layer";
        const string FN_ID = "ID";
        const string FN_IS_TRIGGER = "IsTrigger";

        public Geom2d.Vector TransformPoint(Geom2d.Vector localPoint)
        {
            return Position + (Scale * localPoint).RotateRadians(Rotation.Angle);
        }

        public static ColliderDef FromFolder(StorageFolder from)
        {
            var typeS = from.GetItemAsString(FN_TYPE);
            var type = (ColliderDefType)Enum.Parse(typeof(ColliderDefType), typeS);
            ColliderDef result = null;
            switch (type)
            {
                case ColliderDefType.Edge:
                    result = new EdgeColliderDef();
                    break;
                case ColliderDefType.Box:
                    result = new BoxColliderDef();
                    break;
                case ColliderDefType.Polygon:
                    result = new PolygonColliderDef();
                    break;
                case ColliderDefType.Circle:
                    result = new CircleColliderDef();
                    break;
                default:
                    Log.e("Unsupported type {0}", typeS);
                    break;
            }
            if (result != null)
            {
                result.Deserialize(from);
            }
            return result;
        }

        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageString(FN_TYPE, Type().ToString()));
            to.AddItem(SerializationUtils.SerializeVector(FN_POSITION, Position));
            to.AddItem(SerializationUtils.SerializeRotation(FN_ROTATION, Rotation));
            to.AddItem(SerializationUtils.SerializeVector(FN_SCALE, Scale));
            to.AddItem(new StorageString(FN_LAYER, CollisionLayer.ToString()));
            to.AddItem(new StorageInt(FN_ID, ID));
            if (IsTrigger)
            {
                to.AddItem(new StorageBool(FN_IS_TRIGGER, IsTrigger));
            }
            OnSerialize(to);
        }

        protected abstract void OnSerialize(StorageFolder to);

        public void Deserialize(StorageFolder from)
        {   
            Position = SerializationUtils.DeserializeVector(from.GetFolder(FN_POSITION));
            Rotation = SerializationUtils.DeserializeRotation(from.GetItem(FN_ROTATION));
            Scale = SerializationUtils.DeserializeVector(from.GetFolder(FN_SCALE));
            ID = from.GetItemAsInt(FN_ID);
            CollisionLayer = (CollisionMask)Enum.Parse(typeof(CollisionMask), from.GetItemAsString(FN_LAYER));
            IsTrigger = from.GetItemAsBool(FN_IS_TRIGGER, false);
            OnDeserialize(from);
        }

        protected abstract void OnDeserialize(StorageFolder from);
    }
}
