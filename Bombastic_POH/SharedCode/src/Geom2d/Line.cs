using System;
namespace Geom2d
{
    public struct Line
    {
        private readonly Geom2d.Vector p1;
        private readonly Geom2d.Vector p2;
        private readonly float mag;

        public Line(Geom2d.Vector _p1, Geom2d.Vector _p2, float _mag)
        {
            p1 = _p1;
            p2 = _p2;
            mag = _mag;
        }
        
        public Line(Geom2d.Vector _p1, Geom2d.Vector _p2) : this(_p1, _p2, (_p1 - _p2).Magnitude()) { }

        public float Magnitude
        {
            get
            {
                return mag;
            }
        }

        public bool IsNearZero
        {
            get
            {
                return Magnitude <= C.EPS;
            }
        }

        public Geom2d.Vector Start
        {
            get { return p1; }
        }

        public Geom2d.Vector End
        {
            get { return p2; }
        }

        public Geom2d.Vector Middle
        {
            get { return (p1 + p2) / 2; }
        }

        public Geom2d.Vector Direction
        {
            get
            {
                return p2 - p1;
            }
        }

        public Geom2d.Vector Normal
        {
            get
            {
                var delta = p2 - p1;
                return new Geom2d.Vector(-delta.y, delta.x) / mag;
            }
        }

        /// <summary>
        /// Расстояние до бесконечной прямой, на которой лежит отрезок
        /// ( dist &lt; 0 - прямая строго справа, dist &gt; 0 - прямая строго слева, dist == 0 - точка лежит на прямой)
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public float Distance(Geom2d.Vector p)
        {
            return ((p2.y - p1.y) * p.x - (p2.x - p1.x) * p.y + p2.x * p1.y - p2.y * p1.x) / mag;
        }

        /// <summary>
        /// Характеристический параметр расположения точки и бесконечной прямой, на которой лежит отрезок.
        /// Знак обратный Distanse
        /// ( &lt; 0 - прямая строго слева, &gt; 0 - прямая строго справа, == 0 - точка лежит на прямой)
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public float PlacementValue(Geom2d.Vector p)
        {
            return (p.y - p1.y) * (p2.x - p1.x) - (p.x - p1.x) * (p2.y - p1.y);
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится слева от точки или касается её
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PlacedAtLeftOrLie(Geom2d.Vector p)
        {
            return PlacementValue(p) <= 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится строго слева от точки
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PlacedAtLeft(Geom2d.Vector p)
        {
            return PlacementValue(p) < 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится справа от точки или касается её
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PlacedAtRightOrLie(Geom2d.Vector p)
        {
            return PlacementValue(p) >= 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится строго справа от точки
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PlacedAtRight(Geom2d.Vector p)
        {
            return PlacementValue(p) > 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится слева от окружности или касается её
        /// </summary>
        /// <param name="с"></param>
        /// <returns></returns>
        public bool PlacedAtLeftOrLie(Geom2d.Circle c)
        {
            return Distance(c.Position) + c.r >= 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится строго слева от окружности
        /// </summary>
        /// <param name="с"></param>
        /// <returns></returns>
        public bool PlacedAtLeft(Geom2d.Circle c)
        {
            return Distance(c.Position) + c.r > 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится справа от окружности или касается её
        /// </summary>
        /// <param name="с"></param>
        /// <returns></returns>
        public bool PlacedAtRightOrLie(Geom2d.Circle c)
        {
            return Distance(c.Position) + c.r <= 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, находится строго справа от окружности
        /// </summary>
        /// <param name="с"></param>
        /// <returns></returns>
        public bool PlacedAtRight(Geom2d.Circle c)
        {
            return Distance(c.Position) + c.r < 0;
        }

        /// <summary>
        /// Прямая, на которой лежит отрезок, пересекает окружность или касается её
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Intersect(Geom2d.Circle c)
        {
            return Math.Abs(Distance(c.Position)) - c.r <= 0;
        }

        /// <summary>
        /// Окружность пересекает отрезок между начальной и конечной точками или касается его
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool IntersectSegment(Geom2d.Circle c)
        {
            float dist = Distance(c.Position);

            if (Math.Abs(dist) > c.r)
            {
                return false;
            }

            float r_2 = c.r * c.r;
            if ((p1 - c.Position).Magnitude2() <= r_2) return true;
            if ((p2 - c.Position).Magnitude2() <= r_2) return true;

            var dir = p2 - p1;
            var pointOnLine = c.Position + dist * new Geom2d.Vector(-dir.y, dir.x) / mag;
            return Geom2d.Vector.Dot(dir, pointOnLine - p1) > 0 && Geom2d.Vector.Dot(dir, p2 - pointOnLine) > 0;
        }

        public static bool Intersect(Geom2d.Line l1, Geom2d.Line l2)
        {
            if (l1.getBoundingBox().Intersect(l2.getBoundingBox()))
            {
                return lineSegmentTouchesOrCrossesLine(l1, l2) && lineSegmentTouchesOrCrossesLine(l2, l1);
            }

            return false;
        }

        public Geom2d.Vector Project(Vector p)
        {
            float dist = Distance(p);
            var pointOnLine = p + dist * Normal;
            return pointOnLine;
        }

        public float ProjectParametric(Vector p)
        {
            var pointOnLine = Project(p);
            if (Math.Abs(p2.x - p1.x) > C.EPS)
            {
                return (pointOnLine.x - p1.x) / (p2.x - p1.x);
            }
            return (pointOnLine.y - p1.y) / (p2.y - p1.y);
        }

        public Geom2d.Rect getBoundingBox()
        {
            return new Geom2d.Rect(
                new Geom2d.Vector(Math.Min(Start.x, End.x), Math.Min(Start.y, End.y)),
                new Geom2d.Vector(Math.Max(Start.x, End.x), Math.Max(Start.y, End.y))
                );
        }

        public static bool IsPointOnLine(Geom2d.Line a, Geom2d.Vector b)
        {
            Geom2d.Vector delta = a.End - a.Start;
            Geom2d.Vector bb = b - a.Start;

            return Math.Abs(Geom2d.Vector.Cross(delta, bb)) < Geom2d.C.EPS;
        }

        public static bool IsPointOnLine(Geom2d.Line a, Geom2d.Vector b, float threshold)
        {
            Geom2d.Vector delta = a.End - a.Start;
            Geom2d.Vector bb = b - a.Start;

            return Math.Abs(Geom2d.Vector.Cross(delta, bb)) < threshold;
        }

        static bool isPointRightOfLine(Geom2d.Line a, Geom2d.Vector b)
        {
            Geom2d.Vector delta = a.End - a.Start;
            Geom2d.Vector bb = b - a.Start;

            return Geom2d.Vector.Cross(delta, bb) < 0;
        }

        static bool lineSegmentTouchesOrCrossesLine(Geom2d.Line a, Geom2d.Line b)
        {
            return IsPointOnLine(a, b.Start)
                    || IsPointOnLine(a, b.End)
                    || (isPointRightOfLine(a, b.Start) ^ isPointRightOfLine(a,
                            b.End));
        }

        public override string ToString()
        {
            return string.Format("{0} -> {1}, len: {2}", Start, End, mag);
        }

        public string ToString(IFormatProvider formatProvider)
        {
            return string.Format(formatProvider, "{0} -> {1}, len: {2}", Start, End, mag);
        }
    }
}