using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Geom2d
{
    public struct CircleShape : IShape, IDataStruct
    {
        public static readonly CircleShape Void = new CircleShape();

        private float mRadius;
        private Vector mOffset;

        public CircleShape(float radius)
            : this()
        {
            mRadius = radius;
        }

        public CircleShape(float radius, Vector offset)
            : this()
        {
            mRadius = radius;
            mOffset = offset;
        }

        #region IShape
        public Vector Center { get { return mOffset; } }

        public float CircumcircleRadius { get { return mRadius; } }

        public Circle CircumCircle(Vector selfPosition)
        {
            return Circle(selfPosition);
        }

        private Circle Circle(Vector selfPosition)
        {
            return new Circle(selfPosition + mOffset, mRadius);
        }

        public float Distance(Vector selfPosition, Vector position)
        {
            return Circle(selfPosition).Distance(position);
        }

        public bool Intersect(Vector selfPosition, Circle c)
        {
            return c.Intersect(Circle(selfPosition));
        }

        public bool ContainsIn(Vector selfPosition, Circle c)
        {
            return c.Contains(Circle(selfPosition));
        }

        public bool Intersect(Vector selfPosition, CircleSector cs)
        {
            return cs.Intersect(Circle(selfPosition));
        }

        public bool Intersect(Vector selfPosition, Quad q)
        {
            return q.Intersect(Circle(selfPosition));
        }

        public bool ContainsIn(Vector selfPosition, Quad q)
        {
            return q.Contains(Circle(selfPosition));
        }
        #endregion

        #region IDataSrtruct
        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mRadius);
            dst.AddGeom(ref mOffset);
            return true;
        }
        #endregion
    }
}
