using System;
using Geom2d;

namespace Shared.Battle
{
    public interface IMapObjectData
    {
        Vector MapPosition { get; }
    }

    [Serializable]
    public abstract class MapObjectData : IMapObjectData, ISerializable
    {
        public Vector Position;
        public Vector MapPosition { get { return Position; } }

        public const string FN_POSITION_X = "PositionX";
        public const string FN_POSITION_Y = "PositionY";

        protected MapObjectData()
        {
        }

        protected MapObjectData(Vector position)
        {
            Position = position;
        }

        public virtual void Deserialize(StorageFolder from)
        {
            float posX = from.GetItemAsFloat(FN_POSITION_X);
            float posY = from.GetItemAsFloat(FN_POSITION_Y);
            Position = new Vector(posX, posY);
        }

        public virtual void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageFloat(FN_POSITION_X, Position.x));
            to.AddItem(new StorageFloat(FN_POSITION_Y, Position.y));
        }
    }
}