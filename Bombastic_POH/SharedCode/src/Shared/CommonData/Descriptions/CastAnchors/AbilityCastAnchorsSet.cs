using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IAbilityCastAnchorsSet
        {
            Geom2d.Vector GetOffset(AbilityCastAnchor anchor, Geom2d.Vector direction, ShapeStateType unitShapeStateType = ShapeStateType.Base);
        }

        public class AbilityCastAnchorsSet : IDataStruct, IAbilityCastAnchorsSet
        {
            public StateCastAnchorsData[] StateCastAnchors;

            private int StateAnchorsCount { get { return StateCastAnchors == null ? 0 : StateCastAnchors.Length; } }

            private Geom2d.Vector GetOffset(AbilityCastAnchor anchor, ShapeStateType unitShapeStateType = ShapeStateType.Base)
            {
                int count = StateAnchorsCount;
                for (int i = 0; i < count; i++)
                {
                    var val = StateCastAnchors[i];
                    if (val.ShapeStateType == unitShapeStateType)
                    {
                        return val.GetOffset(anchor);
                    }
                }

                if (count > 0)
                {
                    return StateCastAnchors[0].GetOffset(anchor);
                }

                return Geom2d.Vector.Zero;
            }

            public Geom2d.Vector GetOffset(AbilityCastAnchor anchor, Geom2d.Vector direction, ShapeStateType unitShapeStateType = ShapeStateType.Base)
            {
                Geom2d.Vector offset = GetOffset(anchor, unitShapeStateType);
                offset = direction * offset.x + new Geom2d.Vector(0, offset.y);
                return offset;
            }
            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref StateCastAnchors);
                return true;
            }
        }

        public interface IStateCastAnchorsData
        {

        }

        public class StateCastAnchorsData : IDataStruct, IStateCastAnchorsData
        {
            public CastAnchorData[] Anchors;
            public ShapeStateType ShapeStateType;

            private int AnchorsCount { get { return Anchors == null ? 0 : Anchors.Length; } }

            public Geom2d.Vector GetOffset(AbilityCastAnchor anchor, ShapeStateType unitShapeStateType = ShapeStateType.Base)
            {
                int count = AnchorsCount;
                for (int i = 0; i < count; i++)
                {
                    var val = Anchors[i];
                    if (val.Anchor == anchor)
                    {
                        return val.Offset;
                    }
                }

                return Geom2d.Vector.Zero;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                byte shapeStateType = (byte)ShapeStateType;
                dst.Add(ref shapeStateType);
                ShapeStateType = (ShapeStateType)shapeStateType;

                dst.Add(ref Anchors);

                return true;
            }
        }
    }
}
