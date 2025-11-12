using Serializer.BinarySerializer;
using Geom2d;

namespace Shared
{
    namespace CommonData
    {
        public sealed class UnitShapesMapData : IDataStruct
        {
            public ShapeStateType ShapeStateType;
            public Shape Shape;

            public bool Serialize(IBinarySerializer dst)
            {
                int shapeStateType = (int)ShapeStateType;
                dst.Add(ref shapeStateType);
                ShapeStateType = (ShapeStateType)shapeStateType;

                //dst.Add(ref );
                Shape.Serialize(dst);

                return true;
            }
        }
    }
}
