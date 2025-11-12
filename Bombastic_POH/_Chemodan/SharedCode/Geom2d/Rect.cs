using System;

namespace Geom2d
{
    // AABB Rect
    public struct Rect
    {
        private const float EPS = 1e-5f;
        public Vector pt00;
        public Vector pt11;

        public static readonly Rect Empty = new Rect(0, 0, 0, 0);
        public static readonly Rect UnitSquare = new Rect(0, 0, 1, 1);

        public Rect(Vector _pt00, Vector _pt11)
        {
            pt00 = _pt00;
            pt11 = _pt11;
        }

        public Rect(Vector _pt00, Vector _pt11, bool horzFlip, bool vertFlip)
            : this(_pt00, _pt11)
        {
            if (horzFlip)
            {
                HorzFlip();
            }
            if (vertFlip)
            {
                VertFlip();
            }
        }

        public Rect(float _x, float _y, float _width, float _height)
        {
            pt00.x = _x;
            pt00.y = _y;
            pt11.x = _x + _width;
            pt11.y = _y + _height;
        }

        public Rect(Vector _pt00, float _width, float _height)
        {
            pt00 = _pt00;
            pt11.x = pt00.x + _width;
            pt11.y = pt00.y + _height;
        }

        public Rect(Rect _rect)
        {
            pt00 = _rect.pt00;
            pt11 = _rect.pt11;
        }

        public Vector Pt00
        {
            get
            {
                return pt00;
            }
            set
            {
                pt00.Copy(value);
            }
        }

        public Vector Pt11
        {
            get
            {
                return pt11;
            }
            set
            {
                pt11.Copy(value);
            }
        }

        public Vector Pt10
        {
            get
            {
                return new Vector(pt00.x, pt11.y);
            }
            set
            {
                pt00.x = value.x;
                pt11.y = value.y;
            }
        }

        public Vector Pt01
        {
            get
            {
                return new Vector(pt11.x, pt00.y);
            }
            set
            {
                pt11.x = value.x;
                pt00.y = value.y;
            }
        }

        public Line L0
        {
            get { return new Line(pt00, Pt10); }
        }

        public Line L1
        {
            get { return new Line(Pt10, pt11); }
        }

        public Line L2
        {
            get { return new Line(pt11, Pt01); }
        }

        public Line L3
        {
            get { return new Line(Pt01, pt00); }
        }

        public Vector Position
        {
            get
            {
                return this.pt00;
            }
            set
            {
                pt11.Offset(value - pt00);
                pt00.Copy(value);
            }
        }

        public float X
        {
            get
            {
                return pt00.x;
            }
            set
            {
                pt11.x += value - pt00.x;
                pt00.x = value;
            }
        }

        public float Y
        {
            get
            {
                return pt00.y;
            }
            set
            {
                pt11.y += value - pt00.y;
                pt00.y = value;
            }
        }

        public float Width
        {
            get
            {
                return pt11.x - pt00.x;
            }
            set
            {
                pt11.x = pt00.x + value;
            }
        }

        public float Height
        {
            get
            {
                return pt11.y - pt00.y;
            }
            set
            {
                pt11.y = pt00.y + value;
            }
        }

        public Vector Size
        {
            get
            {
                return pt11 - pt00;
            }
        }

        public Vector Center
        {
            get
            {
                return (pt00 + pt11) * 0.5f;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Width < EPS || Height < EPS;
            }
        }

        public Single AspectRatio()
        {
            return Width / Height;
        }

        public override bool Equals(object _obj)
        {
            if (_obj is Rect)
            {
                return (Rect)_obj == this;
            }
            return false;
        }

        public static Boolean Equals(Rect lhs, Rect rhs)
        {
            Boolean result =
                Vector.Equals(lhs.pt00, rhs.pt00) &&
                Vector.Equals(lhs.pt11, rhs.pt11);
            return result;
        }

        public override int GetHashCode()
        {
            return (pt00.GetHashCode() << 16) | (pt11.GetHashCode() & 0x0000FFFF);
        }

        public override string ToString()
        {
            return string.Format("{0};{1};{2};{3}", pt00.x, pt00.y, Width, Height);
        }

        public void Set(float _x, float _y, float _width, float _height)
        {
            pt00.x = _x;
            pt00.y = _y;
            pt11.x = _x + _width;
            pt11.y = _y + _height;
        }

        public void Copy(Rect _rect)
        {
            pt00 = _rect.pt00;
            pt11 = _rect.pt11;
        }

        public static Rect Scale(Rect rect, Single scale)
        {
            rect.pt00 *= scale;
            rect.pt11 *= scale;
            return rect;
        }

        public static Rect Scale(Rect rect, Single scale, Vector pivot)
        {
            rect = Rect.Translate(rect, -pivot);
            rect = Rect.Scale(rect, scale);
            rect = Rect.Translate(rect, pivot);
            return rect;
        }

        public Rect Inflate(float _dx, float _dy)
        {
            pt00.Offset(-_dx, -_dy);
            pt11.Offset(+_dx, +_dy);
            return this;
        }

        public Rect Inflate(Single left, Single right, Single up, Single down)
        {
            pt00.Offset(-left, -up);
            pt11.Offset(right, down);
            return this;
        }

        public Rect Deflate(float _dx, float _dy)
        {
            pt00.Offset(+_dx, +_dy);
            pt11.Offset(-_dx, -_dy);
            return this;
        }

        public void Normalize()
        {
            if (pt11.x < pt00.x)
            {
                float __t = pt00.x;
                pt00.x = pt11.x;
                pt11.x = __t;
            }
            if (pt11.y < pt00.y)
            {
                float __t = pt00.y;
                pt00.y = pt11.y;
                pt11.y = __t;
            }
        }

        public void Offset(float _x, float _y)
        {
            pt00.Offset(_x, _y);
            pt11.Offset(_x, _y);
        }

        public void Offset(Vector _v)
        {
            pt00.Offset(_v);
            pt11.Offset(_v);
        }

        public static Rect Translate(Rect rect, Vector translation)
        {
            rect.pt00 += translation;
            rect.pt11 += translation;
            return rect;
        }

        public static Rect Translate(Rect rect, Single x, Single y)
        {
            rect.Offset(x, y);
            return rect;
        }

        public void Union(Vector _v)
        {
            if (pt00.x > _v.x)
            {
                pt00.x = _v.x;
            }
            if (pt00.y > _v.y)
            {
                pt00.y = _v.y;
            }
            if (pt11.x < _v.x)
            {
                pt11.x = _v.x;
            }
            if (pt11.y < _v.y)
            {
                pt11.y = _v.y;
            }
        }

        public void Union(Rect _rect)
        {
            Union(_rect.pt00);
            Union(_rect.pt11);
            Union(_rect.Pt10);
            Union(_rect.Pt01);
        }

        public bool IsIntersectSegment(Geom2d.Line segment)
        {
            float x1 = segment.Start.x, x2 = segment.End.x;
            float y1 = segment.Start.y, y2 = segment.End.y;

            float minX = Pt00.x, minY = Pt00.y;
            float maxX = Pt11.x, maxY = Pt11.y;

            // Completely outside.
            if ((x1 <= minX && x2 <= minX) || (y1 <= minY && y2 <= minY) || (x1 >= maxX && x2 >= maxX) || (y1 >= maxY && y2 >= maxY))
                return false;

            float m = (y2 - y1) / (x2 - x1);

            float y = m * (minX - x1) + y1;
            if (y > minY && y < maxY) return true;

            y = m * (maxX - x1) + y1;
            if (y > minY && y < maxY) return true;

            float x = (minY - y1) / m + x1;
            if (x > minX && x < maxX) return true;

            x = (maxY - y1) / m + x1;
            if (x > minX && x < maxX) return true;

            return false;
        }

        public Rect Clamp(Rect _rect)
        {
            Rect __rc1 = new Rect(this);
            Rect __rc2 = _rect;

            // TopLeft
            if (__rc1.pt00.x < __rc2.pt00.x)
            {
                __rc1.pt00.x = __rc2.pt00.x;
                if (__rc1.pt00.x > __rc1.pt11.x)
                {
                    __rc1.pt11.x = __rc1.pt00.x;
                }
            }
            if (__rc1.pt00.y < __rc2.pt00.y)
            {
                __rc1.pt00.y = __rc2.pt00.y;
                if (__rc1.pt00.y > __rc1.pt11.y)
                {
                    __rc1.pt11.y = __rc1.pt00.y;
                }

            }

            // BottomRight
            if (__rc1.pt11.x > __rc2.pt11.x)
            {
                __rc1.pt11.x = __rc2.pt11.x;
                if (__rc1.pt00.x > __rc1.pt11.x)
                {
                    __rc1.pt00.x = __rc1.pt11.x;
                }
            }
            if (__rc1.pt11.y > __rc2.pt11.y)
            {
                __rc1.pt11.y = __rc2.pt11.y;
                if (__rc1.pt00.y > __rc1.pt11.y)
                {
                    __rc1.pt00.y = __rc1.pt11.y;
                }
            }

            this = __rc1;
            return this;
        }

        /// <summary>
        /// Точка находится внутри прямоугольника или касается его граней
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool Contains(float x, float y)
        {
            return x >= pt00.x &&
                   x <= pt11.x &&
                   y >= pt00.y &&
                   y <= pt11.y;
        }

        /// <summary>
        /// Точка находится внутри прямоугольника или касается его граней
        /// </summary>
        /// <param name="_pt"></param>
        /// <returns></returns>
        public bool Contains(Vector _pt)
        {
            return _pt.x >= pt00.x &&
                   _pt.x <= pt11.x &&
                   _pt.y >= pt00.y &&
                   _pt.y <= pt11.y;
        }

        /// <summary>
        /// Прямоугольник полностью находится внутри или касется гранями
        /// </summary>
        /// <param name="_rc"></param>
        /// <returns></returns>
        public bool Contains(Rect _rc)
        {
            return _rc.pt00.x >= pt00.x &&
                   _rc.pt00.y >= pt00.y &&
                   _rc.pt11.x <= pt11.x &&
                   _rc.pt11.y <= pt11.y;
        }

        /// <summary>
        /// Прямоугольник полностью включает в себя окружность или касается её
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public bool Contains(Geom2d.Circle c)
        {
            return c.x - c.r >= pt00.x &&
                   c.x + c.r <= pt11.x &&
                   c.y + c.r <= pt11.y &&
                   c.y - c.r >= pt00.y;
        }

        /// <summary>
        /// Может ли прямоугольник быть включён а окружность указанного радиуса
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public bool CanContainsInCircle(float r)
        {
            float w = Width;
            float h = Height;
            return w * w + h * h - 4 * r * r <= 0;
        }

        /// <summary>
        /// Пересечение, включение или касание с прямоугольником
        /// </summary>
        /// <param name="_rc"></param>
        /// <returns></returns>
        public bool Intersect(Rect _rc)
        {
            return pt00.x <= _rc.pt11.x &&
                   pt00.y <= _rc.pt11.y &&
                   pt11.x >= _rc.pt00.x &&
                   pt11.y >= _rc.pt00.y;
        }

        /// <summary>
        /// Пересечение, включение или касание с окружностью
        /// </summary>
        /// <param name="_c"></param>
        /// <returns></returns>
        public bool Intersect(Circle _c)
        {
            var p = _c.Position;
            bool interior = false;
            var dist = Distance(pt00 - p, pt11 - p, out interior);
            if (interior)
            {
                return true;
            }

            return dist.Magnitude2() <= _c.r * _c.r;
        }

        /// <summary>
        /// Пересечение, включение или касание с сектором
        /// </summary>
        /// <param name="_cs"></param>
        /// <returns></returns>
        public bool Intersect(CircleSector _cs)
        {
            var p = _cs.Position;
            bool interior = false;
            var dist = Distance(pt00 - p, pt11 - p, out interior);

            // Центр сектора лежит внутри прямоугольника
            if (interior)
            {
                //Log.e("Center at rect");
                return true;
            }

            // Пересекает ли потенциально окружность прямоугольник
            if (dist.Magnitude2() > _cs.Radius * _cs.Radius)
            {
                //Log.e("Rect too far");
                return false;
            }

            // Находится ли вектор в растре сектора - гарантированное пересечение минимум одной точкой дуги
            if (_cs.InsideAngle(dist))
            {
                //Log.e("Direction is between rays");
                return true;
            }

            // Пересечение лучами ближаших сторон

            Line lStart = _cs.StartSectorLine;
            Line lEnd = _cs.EndSectorLine;

            Line l;
            if (dist.x > 0)
            {
                l = L0;
                if (Line.Intersect(lStart, l) ||
                    Line.Intersect(lEnd, l))
                {
                    //Log.e("Intersect L0");
                    return true;
                }
            }
            else
            {
                l = L2;
                if (Line.Intersect(lStart, l) ||
                    Line.Intersect(lEnd, l))
                {
                    //Log.e("Intersect L2");
                    return true;
                }
            }

            if (dist.y > 0)
            {
                l = L3;
                if (Line.Intersect(lStart, l) ||
                    Line.Intersect(lEnd, l))
                {
                    //Log.e("Intersect L3");
                    return true;
                }
            }
            else
            {
                l = L1;
                if (Line.Intersect(lStart, l) ||
                    Line.Intersect(lEnd, l))
                {
                    //Log.e("Intersect L1");
                    return true;
                }
            }

            //Log.e("Non-intersecting mutual placement");
            return false;
        }

        /// <summary>
        /// Расстояние до прямоугольника из произвольной точки. == 0, если точка внутри прямоугольника
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public float Distance(Vector p)
        {
            bool interior = false;
            var dist = Distance(pt00 - p, pt11 - p, out interior);
            if (interior)
            {
                return 0f;
            }

            return dist.Magnitude();
        }

        /// <summary>
        /// Вектор до ближейшей точки периметра
        /// </summary>
        /// <param name="v00"></param>
        /// <param name="v11"></param>
        /// <param name="interior"></param>
        /// <returns></returns>
        private static Vector Distance(Vector v00, Vector v11, out bool interior)
        {
            interior = true;
            float x = 0;
            float y = 0;

            int sign = Math.Sign(v00.x);
            if (sign == Math.Sign(v11.x))
            {
                if (sign >= 0)
                {
                    x = Math.Min(v00.x, v11.x);
                }
                else
                {
                    x = Math.Max(v00.x, v11.x);
                }
                interior = false;
            }

            sign = Math.Sign(v00.y);
            if (sign == Math.Sign(v11.y))
            {
                if (sign >= 0)
                {
                    y = Math.Min(v00.y, v11.y);
                }
                else
                {
                    y = Math.Max(v00.y, v11.y);
                }
                interior = false;
            }

            return new Vector(x, y);
        }

        public void HorzFlip()
        {
            float tmp = pt00.x;
            pt00.x = pt11.x;
            pt11.x = tmp;
        }

        public void VertFlip()
        {
            float tmp = pt00.y;
            pt00.y = pt11.y;
            pt11.y = tmp;
        }

        public static bool operator ==(Rect _l, Rect _r)
        {
            return _l.pt00 == _r.pt00 && _l.pt11 == _r.pt11;
        }

        public static bool operator !=(Rect _l, Rect _r)
        {
            return _l.pt00 != _r.pt00 || _l.pt11 != _r.pt11;
        }

        public static Rect Lerp(Rect from, Rect to, Single t)
        {
            return new Rect(
                Vector.Lerp(from.pt00, to.pt00, t),
                Vector.Lerp(from.pt11, to.pt11, t));
        }
    }
}
