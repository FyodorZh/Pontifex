using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Geom2d
{
    public struct AxisAlignedRectShape : IShape, IDataStruct
    {
        private float mCircumcircleRadius;

        private Rect mRect;

        public AxisAlignedRectShape(Vector size, Vector offset)
            : this()
        {
            Vector halfSize = size / 2.0f;
            mCircumcircleRadius = halfSize.Magnitude();

            mRect = new Rect(offset.x - halfSize.x, offset.y - halfSize.y, size.x, size.y);
        }

        public Vector Size
        {
            get { return mRect.Size; }
        }

        #region IShape
        public Vector Center { get { return mRect.Center; } }

        public float CircumcircleRadius { get { return mCircumcircleRadius; } }

        public Circle CircumCircle(Vector selfPosition)
        {
            return new Circle(selfPosition + mRect.Center, mCircumcircleRadius);
        }

        public float Distance(Vector selfPosition, Vector position)
        {
            var rect = Rect.Translate(mRect, selfPosition);
            return rect.Distance(position);
        }

        public bool Intersect(Vector selfPosition, Circle c)
        {
            var rect = Rect.Translate(mRect, selfPosition);
            return rect.Intersect(c);
        }

        public bool ContainsIn(Vector selfPosition, Circle c)
        {
            // Окружность меньше чем описанный радиус прамоугольника не может включать его в себя полностью
            if (c.r > CircumcircleRadius)
            {
                return false;
            }

            var rect = Rect.Translate(mRect, selfPosition);
            return c.Contains(rect);
        }

        public bool Intersect(Vector selfPosition, CircleSector cs)
        {
            var rect = Rect.Translate(mRect, selfPosition);
            return rect.Intersect(cs);
        }

        public bool Intersect(Vector selfPosition, Quad q)
        {
            var rect = Rect.Translate(mRect, selfPosition);
            return q.Intersect(rect);
        }

        public bool ContainsIn(Vector selfPosition, Quad q)
        {
            var rect = Rect.Translate(mRect, selfPosition);
            return q.Contains(rect);
        }
        #endregion

        #region IDataSrtruct
        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddGeom(ref mRect);
            if (dst.isReader)
            {
                mCircumcircleRadius = mRect.Size.Magnitude() * 0.5f;
            }
            return true;
        }
        #endregion
    }
}
