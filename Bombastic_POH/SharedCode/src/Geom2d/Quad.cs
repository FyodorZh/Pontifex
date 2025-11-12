using System;

namespace Geom2d
{

    public struct Quad
    {
        private Vector mP0, mP1, mP2, mP3;
        private Line mL0, mL1, mL2, mL3;

        public Quad(Vector p0, Vector p1, Vector p2, Vector p3)
        {
            
            mP0 = p0;
            mP1 = p1;
            mP2 = p2;
            mP3 = p3;

            DBG.Diagnostics.Assert(checkConvex(p0, p1, p2, p3), "Quad is not convex");

            mL0 = new Line(mP0, mP1);
            mL1 = new Line(mP1, mP2);
            mL2 = new Line(mP2, mP3);
            mL3 = new Line(mP3, mP0);
        }

        public Vector P0
        {
            get { return mP0; }
        }

        public Vector P1
        {
            get { return mP1; }
        }

        public Vector P2
        {
            get { return mP2; }
        }

        public Vector P3
        {
            get { return mP3; }
        }

        public Line L0
        {
            get { return mL0; }
        }

        public Line L1
        {
            get { return mL1; }
        }

        public Line L2
        {
            get { return mL2; }
        }

        public Line L3
        {
            get { return mL3; }
        }

        public static Boolean Equals(Quad lhs, Quad rhs)
        {
            Boolean result = lhs.P0 == rhs.P0 && lhs.P1 == rhs.P1 && lhs.P2 == rhs.P2 && lhs.P3 == rhs.P3;
            return result;
        }

        /// <summary>
        /// ПРОТИВ ЧАСОВОЙ СТРЕЛКИ!!!!
        /// </summary>
        /// <param name="rc"></param>
        /// <returns></returns>
        public static Quad Rect(Rect rc)
        {
            return new Quad(rc.Pt11, rc.Pt10, rc.Pt00, rc.Pt01);
        }

        /// <summary>
        /// ПРОТИВ ЧАСОВОЙ СТРЕЛКИ!!!!
        /// </summary>
        /// <param name="position"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="ox"></param>
        /// <param name="oy"></param>
        /// <returns></returns>
        public static Quad Parallelogram(Vector position, float width, float height, Vector ox, Vector oy)
        {
            ox = ox.Normalized();
            oy = oy.Normalized();

            float w_div_2 = width/2;
            float h_div_2 = height/2;

            return new Quad(position + w_div_2 * ox + h_div_2 * oy,
                    position - w_div_2 * ox + h_div_2 * oy,
                    position - w_div_2 * ox - h_div_2 * oy,
                    position + w_div_2 * ox - h_div_2 * oy
                );
        }

        public static Quad Rect(Vector position, float width, float height, Vector ox)
        {
            return Parallelogram(position, width, height, ox, new Vector(-ox.y, ox.x));
        }

        public static Quad RectWithOffset(Vector position, float width, float height, Vector ox, Vector newT)
        {
            ox = ox.Normalized();
            Vector oy = new Vector(-ox.y, ox.x);
            var newPosition = position + newT.x * ox + newT.y * oy;
            return Rect(newPosition, width, height, ox);
        }

        /// <summary>
        /// Точка лежит внутри прямоугольника или касается его грани
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool Contains(Vector pos)
        {
            // Обход против часовой стрелки
            return mL0.PlacedAtRightOrLie(pos) && mL1.PlacedAtRightOrLie(pos) && mL2.PlacedAtRightOrLie(pos) && mL3.PlacedAtRightOrLie(pos);
        }

        /// <summary>
        /// Пересечение, включение или касание с окружностью
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Intersect(Geom2d.Circle c)
        {
            return Contains(c.Position) || mL0.IntersectSegment(c) || mL1.IntersectSegment(c) || mL2.IntersectSegment(c) || mL3.IntersectSegment(c);
        }

        /// <summary>
        /// Прямоугольник полностью включает в себя окружность или касается её
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Contains(Geom2d.Circle c)
        {
            // Обход против часовой стрелки
            return mL0.PlacedAtRightOrLie(c) && mL1.PlacedAtRightOrLie(c) && mL2.PlacedAtRightOrLie(c) && mL3.PlacedAtRightOrLie(c);
        }

        /// <summary>
        /// Прямоугольник полностью включает в себя rect или касается его
        /// </summary>
        /// <param name="rc"></param>
        /// <returns></returns>
        public bool Contains(Geom2d.Rect rc)
        {
            // проверяем потенциальную пересекаемость 
            if (!BBox.Intersect(rc))
            {
                return false;
            }
            
            // все точки прямоугольника должны лежать внутри
            return Contains(rc.pt00) && Contains(rc.Pt01) && Contains(rc.pt11) && Contains(rc.Pt10);
        }

        /// <summary>
        /// Пересечение, включение или касание с rect
        /// </summary>
        /// <param name="rc"></param>
        /// <returns></returns>
        public bool Intersect(Rect rc)
        {
            // проверяем потенциальную пересекаемость 
            if (!BBox.Intersect(rc))
            {
                return false;
            }

            // Сначала очень быстрая проверка - хотя бы одна вершина quad внутри rect
            if (rc.Contains(P0) ||
                rc.Contains(P1) ||
                rc.Contains(P2) ||
                rc.Contains(P3))
            {
                return true;
            }

            // Потом чуть-чуть помедленнее - хотя бы одна вершина rect внутри quad
            if (Contains(rc.pt00) ||
                Contains(rc.Pt01) ||
                Contains(rc.pt11) ||
                Contains(rc.Pt10))
            {
                return true;
            }

            // а сейчас начинается жесть
            // Хотя бы одна сторона пересекает прямоугольник
            Line rcL0 = rc.L0;
            Line rcL1 = rc.L1;
            Line rcL2 = rc.L2;
            Line rcL3 = rc.L3;

            if (Line.Intersect(L0, rcL0) ||
                Line.Intersect(L0, rcL1) ||
                Line.Intersect(L0, rcL2) ||
                Line.Intersect(L0, rcL3))
            {
                return true;
            }

            if (Line.Intersect(L1, rcL0) ||
                Line.Intersect(L1, rcL1) ||
                Line.Intersect(L1, rcL2) ||
                Line.Intersect(L1, rcL3))
            {
                return true;
            }
/*
            // очень сильно врядли, что пересечения для выпуклого четырехугольника смогут появиться после проверки 2 граней
            if (Line.Intersect(L2, rcL0) ||
                Line.Intersect(L2, rcL1) ||
                Line.Intersect(L2, rcL2) ||
                Line.Intersect(L2, rcL3))
            {
                Log.e("Miracle 1");
                return true;
            }

            if (Line.Intersect(L3, rcL0) ||
                Line.Intersect(L3, rcL1) ||
                Line.Intersect(L3, rcL2) ||
                Line.Intersect(L3, rcL3))
            {
                Log.e("Miracle 1");
                return true;
            }
*/
            return false;
        }

        public bool IsConvex
        {
            get
            {
                return checkConvex(mP0, mP1, mP2, mP3);
            }
        }

        private static bool checkConvex(Vector p0, Vector p1, Vector p2, Vector p3)
        {
            var s1 = crossProductSign(p0, p1, p2);
            var s2 = crossProductSign(p1, p2, p3);
            var s3 = crossProductSign(p2, p3, p0);
            var s4 = crossProductSign(p3, p0, p1);
            return s2 == s1 && s3 == s1 && s4 == s1; // all same sign
        }

        private static int crossProductSign(Vector p0, Vector p1, Vector p2)
        {
            return Math.Sign(Vector.Cross(p1 - p0, p2 - p1));
        }

        public Rect BBox
        {
            get
            {
                float x0 = Math.Min(Math.Min(mP0.x, mP1.x), Math.Min(mP2.x, mP3.x));
                float y0 = Math.Min(Math.Min(mP0.y, mP1.y), Math.Min(mP2.y, mP3.y));
                float x1 = Math.Max(Math.Max(mP0.x, mP1.x), Math.Max(mP2.x, mP3.x));
                float y1 = Math.Max(Math.Max(mP0.y, mP1.y), Math.Max(mP2.y, mP3.y));
                return new Rect(x0, y0, x1 - x0, y1 - y0);
            }
        }

        public Rect Rectangle
        {
            get
            {
                float x0 = Math.Min(Math.Min(mP0.x, mP1.x), Math.Min(mP2.x, mP3.x));
                float y0 = Math.Min(Math.Min(mP0.y, mP1.y), Math.Min(mP2.y, mP3.y));
                float x1 = Math.Max(Math.Max(mP0.x, mP1.x), Math.Max(mP2.x, mP3.x));
                float y1 = Math.Max(Math.Max(mP0.y, mP1.y), Math.Max(mP2.y, mP3.y));
                return new Rect(x0, y0, x1 - x0, y1 - y0);
            }
        }
    }
}
