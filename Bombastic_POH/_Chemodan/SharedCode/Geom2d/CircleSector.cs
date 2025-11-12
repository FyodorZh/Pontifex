using System;

namespace Geom2d
{
    public struct CircleSector
    {
        private readonly Vector mPosition;
        private readonly Vector mSectorStart;
        private readonly Vector mSectorEnd;
        private float angle;

        private readonly float mRadius;

        private readonly Vector _dir;

        public static Boolean Equals(CircleSector lhs, CircleSector rhs)
        {
            Boolean result = lhs.mPosition == rhs.mPosition && lhs._dir == rhs._dir && lhs.mRadius == rhs.mRadius && lhs.angle == rhs.angle;
            return result;
        }

        public Vector Position
        {
            get
            {
                return mPosition;
            }
        }

        public float AngleRadians
        {
            get
            {
                return angle;
            }
        }

        public float AngleDegrees
        {
            get
            {
                return Rotation.ToDegrees(angle);
            }
        }

        public float Radius
        {
            get
            {
                return mRadius;
            }
        }

        public Vector SectorStart
        {
            get
            {
                return mPosition + mSectorStart * mRadius;
            }
        }

        public Vector SectorStartDirection
        {
            get
            {
                return mSectorStart;
            }
        }

        public Vector SectorEnd
        {
            get
            {
                return mPosition + mSectorEnd * mRadius;
            }
        }

        public Vector SectorEndDirection
        {
            get
            {
                return mSectorEnd;
            }
        }

        public Vector SectorDirection
        {
            get
            {
                return mPosition + mRadius * _dir;
            }
        }

        public Vector SectorCenter
        {
            get
            {
                return mPosition + mRadius * _dir/ 2.0f;
            }
        }

        public Line StartSectorLine
        {
            get
            {
                return new Line(mPosition, SectorStart);
            }
        }

        public Line EndSectorLine
        {
            get
            {
                return new Line(mPosition, SectorEnd);
            }
        }

        public Line DirectionLine
        {
            get
            {
                return new Line(mPosition, SectorDirection);
            }
        }

        public Vector Direction
        {
            get
            {
                return _dir;
            }
        }

        public CircleSector(Vector position, float radius, Vector dir, float angle)
        {
            mPosition = position;
            mRadius = radius;

            _dir = dir.Normalized();

            this.angle = (float)Math.Min(Math.Max(angle, 0), Rotation.Pi2);

            mSectorStart = _dir.RotateRadians(-this.angle / 2);
            mSectorEnd = _dir.RotateRadians(this.angle / 2);
        }

        public CircleSector(Vector position, Vector dirPos, float angle)
        {
            var dir = (dirPos - position);

            mPosition = position;
            mRadius = dir.Magnitude();

            if (mRadius > 0)
            {
                _dir = dir / mRadius;
            }
            else
            {
                _dir = Vector.Abscissa;
            }

            this.angle = (float)Math.Min(Math.Max(angle, 0), Rotation.Pi2);

            mSectorStart = _dir.RotateRadians(-this.angle / 2);
            mSectorEnd = _dir.RotateRadians(this.angle / 2);
        }

        /// <summary>
        /// Находится ли точка внутри сектора или касается границ
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool Contains(Vector p)
        {
            var relP = p - mPosition;
            return (relP.Magnitude2() <= mRadius * mRadius) && InsideAngle(relP);
        }

        /// <summary>
        /// Находится ли вектор между начальним и конечным лучом или лежит на них
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public bool InsideAngle(Vector v)
        {
            if (angle < Math.PI)
            {
                return Vector.AreClockwise(v, mSectorStart) && Vector.AreClockwise(mSectorEnd, v);
            }
            else
            {
                return Vector.AreClockwise(v, mSectorStart) || Vector.AreClockwise(mSectorEnd, v);
            }
        }

        /// <summary>
        /// Пересечение и касание с окружностью
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Intersect(Circle c)
        {
            // Центр сектора находится внутри окружности или касается его
            Vector dPos = c.Position - mPosition;
            float mag2 = dPos.Magnitude2();
            if (mag2 <= c.r * c.r)
            {
                return true;
            }

            // Отбрасываем теоретически непересекаемую окружность
            float dr = c.r + mRadius;
            if (mag2 > dr * dr)
            {
                return false;
            }

            // Окружность пересекает или касается начальный луча
            if (StartSectorLine.IntersectSegment(c))
            {
                return true;
            }

            // Окружность пересекает или касается конечного луча
            if (EndSectorLine.IntersectSegment(c))
            {
                return true;
            }

            // Окружность полностью внутри сектора
            return InsideAngle(dPos);
        }

        public Rect BBox
        {
            get
            {
                var pSectorStart = SectorStart;
                var pSectorEnd = SectorEnd;

                float minX = Math.Min(Math.Min(pSectorStart.x, pSectorEnd.x), mPosition.x);
                float minY = Math.Min(Math.Min(pSectorStart.y, pSectorEnd.y), mPosition.y);
                float maxX = Math.Max(Math.Max(pSectorStart.x, pSectorEnd.x), mPosition.x);
                float maxY = Math.Max(Math.Max(pSectorStart.y, pSectorEnd.y), mPosition.y);

                var a = new Vector(mPosition.x + mRadius, mPosition.y);
                if (InsideAngle(a))
                {
                    minX = Math.Min(a.x, mPosition.x);
                    minY = Math.Min(a.y, mPosition.y);
                    maxX = Math.Max(a.x, mPosition.x);
                    maxY = Math.Max(a.y, mPosition.y);
                }

                var b = new Vector(mPosition.x - mRadius, mPosition.y);
                if (InsideAngle(a))
                {
                    minX = Math.Min(b.x, mPosition.x);
                    minY = Math.Min(b.y, mPosition.y);
                    maxX = Math.Max(b.x, mPosition.x);
                    maxY = Math.Max(b.y, mPosition.y);
                }

                var c = new Vector(mPosition.x, mPosition.y + mRadius);
                if (InsideAngle(a))
                {
                    minX = Math.Min(c.x, mPosition.x);
                    minY = Math.Min(c.y, mPosition.y);
                    maxX = Math.Max(c.x, mPosition.x);
                    maxY = Math.Max(c.y, mPosition.y);
                }

                var d = new Vector(mPosition.x, mPosition.y + mRadius);
                if (InsideAngle(a))
                {
                    minX = Math.Min(d.x, mPosition.x);
                    minY = Math.Min(d.y, mPosition.y);
                    maxX = Math.Max(d.x, mPosition.x);
                    maxY = Math.Max(d.y, mPosition.y);
                }

                return new Rect(new Vector(minX, minY), new Vector(maxX, maxY));
            }
        }
    }
}
