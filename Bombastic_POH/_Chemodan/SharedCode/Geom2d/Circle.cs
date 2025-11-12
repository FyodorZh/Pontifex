using System;

namespace Geom2d
{
    public struct Circle
    {
        public float x;
        public float y;
        public float r;

        public Circle(float r) : this(0, 0, r) { }

        public Circle(Geom2d.Vector pos, float r) : this(pos.x, pos.y, r) { }

        public Circle(float x, float y, float r)
        {
            this.x = x;
            this.y = y;
            this.r = r;
        }

        public static Boolean Equals(Geom2d.Circle lhs, Geom2d.Circle rhs)
        {
            Boolean result = lhs.x == rhs.x && lhs.y == rhs.y && lhs.r == rhs.r;
            return result;
        }

        public Geom2d.Vector Position
        {
            get
            {
                return new Geom2d.Vector(x, y);
            }
            set
            {
                x = value.x;
                y = value.y;
            }
        }

        /// <summary>
        /// Расстояние до окружности из произвольной точки. == 0, если точка внутри окружности
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public float Distance(Vector p)
        {
            float dx = p.x - x;
            float dy = p.y - y;
            float rr = r * r;

            float dr2 = dx * dx + dy * dy;

            if (dr2 <= rr)
            {
                return 0;
            }

            return (float) Math.Sqrt(dr2) - r;
        }

        /// <summary>
        /// Точка находится внутри или на окружности
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool Contains(Geom2d.Vector p)
        {
            float dx = x - p.x;
            float dy = y - p.y;
            return dx * dx + dy * dy - r * r <= 0;
        }

        /// <summary>
        /// Пересечение, включение или касание с окружностью
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Intersect(Geom2d.Circle c)
        {
            float dx = x - c.x;
            float dy = y - c.y;
            float rr = r + c.r;
            return dx * dx + dy * dy - rr * rr <= 0;
        }

        /// <summary>
        /// Окружность полностью находится внутри или касается
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Contains(Geom2d.Circle c)
        {
            // Большая окружность никогда не сможет быт внутри меньшей
            if (c.r > r)
            {
                return false;
            }

            float dx = x - c.x;
            float dy = y - c.y;
            float rr = r - c.r;
            return dx * dx + dy * dy - rr * rr <= 0;
        }

        /// <summary>
        /// Окружность полностью включает в себя прямоугольник или касается его хотя бы в одной вершине
        /// </summary>
        /// <param name="rc"></param>
        /// <returns></returns>
        public bool Contains(Geom2d.Rect rc)
        {
            // Расстояния по вершин не должны превосходить радиус
            Vector pt00 = rc.pt00;
            Vector pt11 = rc.pt11;

            float r2 = r * r;

            float dx00 = pt00.x - x;
            float dy00 = pt00.y - y;

            dx00 *= dx00;
            dy00 *= dy00;

            // o -> pt00
            if (dx00 + dy00 > r2)
            {
                return false;
            }

            float dy11 = pt11.y - y;
            dy11 *= dy11;

            // o -> pt10
            if (dx00 + dy11 > r2)
            {
                return false;
            }

            float dx11 = pt11.x - x;
            dx11 *= dx11;

            // o -> pt11
            if (dx11 + dy11 > r2)
            {
                return false;
            }

            // o -> pt01
            if (dx11 + dy00 > r2)
            {
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return string.Format("{{{0};{1}, R-{2}}}", x, y, r);
        }
    }
}
