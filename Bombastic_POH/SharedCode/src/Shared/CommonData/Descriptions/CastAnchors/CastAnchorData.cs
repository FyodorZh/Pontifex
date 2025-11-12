using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Shared
{
    namespace CommonData
    {
        public interface ICastAnchorData
        {
            AbilityCastAnchor Anchor { get; }
            Geom2d.Vector Offset { get; }
        }

        public class CastAnchorData : IDataStruct, ICastAnchorData
        {
            public AbilityCastAnchor Anchor;
            public Geom2d.Vector Offset;

            AbilityCastAnchor ICastAnchorData.Anchor { get { return Anchor; } }
            Geom2d.Vector ICastAnchorData.Offset { get { return Offset; } }

            public bool Serialize(IBinarySerializer dst)
            {
                byte anchor = (byte)Anchor;
                dst.Add(ref anchor);
                Anchor = (AbilityCastAnchor)anchor;

                dst.AddGeom(ref Offset);

                return true;
            }
        }
    }
}