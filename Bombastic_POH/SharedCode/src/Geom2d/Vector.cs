using System;
using Serializer.BinarySerializer;

namespace Geom2d
{
    public static class C
    {
        public const float EPS = 1e-5f;
        public const float EPS2 = EPS * EPS;
    }

    [Serializable]
    public struct Vector : IDataStruct
        , IEquatable<Vector>
    {
        public float x;
        public float y;

        public static readonly Vector Abscissa = new Vector(1, 0);
        public static readonly Vector AbscissaNegative = new Vector(-1, 0);
        public static readonly Vector Ordinate = new Vector(0, 1);
        public static readonly Vector OrdinateNegative = new Vector(0, -1);

        public static readonly Vector Zero = new Vector(0, 0);
        public static readonly Vector One = new Vector(1, 1);

        public Vector(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public override bool Equals(object _c)
        {
            if (_c is Vector)
            {
                return this == (Vector)_c;
            }
            return false;
        }

        public bool Equals(Vector other) // IEquatable<Vector>
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode();
        }

        public void Set(float _x, float _y)
        {
            x = _x;
            y = _y;
        }

        public void Copy(Vector _pt)
        {
            x = _pt.x;
            y = _pt.y;
        }

        public void Offset(float _x, float _y)
        {
            x += _x;
            y += _y;
        }

        public void Offset(Vector _pt)
        {
            x += _pt.x;
            y += _pt.y;
        }

        public void Flip()
        {
            FlipX();
            FlipY();
        }

        public void FlipX()
        {
            x = -x;
        }

        public void FlipY()
        {
            y = -y;
        }

        public Vector RotateDegrees(float degrees)
        {
            return RotateRadians(Rotation.ToRadians(degrees));
        }

        public Vector RotateRadians(float radians)
        {
            float sin = (float)Math.Sin(radians);
            float cos = (float)Math.Cos(radians);
            float x = this.x * cos - this.y * sin;
            float y = this.x * sin + this.y * cos;
            return new Vector(x, y);
        }

        public Vector Rotate(Vector direction)
        {
            return RotateNormalized(direction.Normalized());
        }

        public Vector RotateNormalized(Vector ox)
        {
            return new Vector(x * ox.x - y * ox.y, x * ox.y + y * ox.x);
        }

        public float Magnitude()
        {
            return (float)Math.Sqrt(x * x + y * y);
        }

        public float Magnitude2()
        {
            return x * x + y * y;
        }

        public float ToRadiansAngle
        {
            get
            {
                return (float)Math.Atan2(y, x);
            }
        }

        public float ToDegreesAngle
        {
            get
            {
                return Rotation.ToDegrees(ToRadiansAngle);
            }
        }

        public static Vector operator +(Vector lhs, Vector rhs)
        {
            lhs.x += rhs.x;
            lhs.y += rhs.y;
            return lhs;
        }

        public static Vector operator -(Vector lhs, Vector rhs)
        {
            lhs.x -= rhs.x;
            lhs.y -= rhs.y;
            return lhs;
        }

        public static Vector operator -(Vector vector)
        {
            vector.x = -vector.x;
            vector.y = -vector.y;
            return vector;
        }

        public static Vector operator *(Vector vector, float factor)
        {
            vector.x *= factor;
            vector.y *= factor;
            return vector;
        }

        public static Vector operator *(float factor, Vector vector)
        {
            vector.x *= factor;
            vector.y *= factor;
            return vector;
        }

        public static Vector operator *(Vector v1, Vector v2)
        {
            v1.x *= v2.x;
            v1.y *= v2.y;
            return v1;
        }

        public static Vector operator /(Vector vector, float factor)
        {
            vector.x /= factor;
            vector.y /= factor;
            return vector;
        }

        public static Vector operator /(float factor, Vector vector)
        {
            vector.x /= factor;
            vector.y /= factor;
            return vector;
        }

        public static bool operator ==(Vector _l, Vector _r)
        {
            return _l.x == _r.x && _l.y == _r.y;
        }

        public static bool operator !=(Vector _l, Vector _r)
        {
            return _l.x != _r.x || _l.y != _r.y;
        }

        public static Vector Normalize(Vector vector)
        {
            if (vector.NearZero())
            {
                DBG.Diagnostics.Assert(false, "Zero vector normalization! {0}", vector);
                return vector;
            }
            return vector / vector.Magnitude();
        }

        public Vector Normalized()
        {
            return Normalize(this);
        }

        public static float Dot(Vector _v0, Vector _v1)
        {
            return _v0.x * _v1.x + _v0.y * _v1.y;
        }

        public Vector Project(Vector other)
        {
            return Dot(this, other) * other.Normalized();
        }

        public static float Cross(Vector _v0, Vector _v1)
        {
            return _v0.x * _v1.y - _v0.y * _v1.x;
        }

        /// <summary>
        /// Находится ли вектор v2 справа или на одной прямой от v1
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static bool AreClockwise(Vector v1, Vector v2)
        {
            return Cross(v1, v2) <= 0;
        }

        public static Vector Lerp(Vector from, Vector to, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            float x = from.x * (1 - t) + to.x * t;
            float y = from.y * (1 - t) + to.y * t;
            return new Vector(x, y);
        }

        public static float Distance(Vector from, Vector to)
        {
            float dx = from.x - to.x;
            float dy = from.y - to.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static float Distance2(Vector from, Vector to)
        {
            float dx = from.x - to.x;
            float dy = from.y - to.y;
            return dx * dx + dy * dy;
        }

        public override string ToString()
        {
            return string.Format("{{{0};{1}}}", x, y);
        }

        public string ToString(IFormatProvider formatProvider)
        {
            return string.Format(formatProvider, "{{{0};{1}}}", x, y);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref x);
            dst.Add(ref y);
            return true;
        }

        public static Vector FromDegreesAngle(float degreea)
        {
            return Abscissa.RotateDegrees(degreea);
        }

        public static Vector FromRadiansAngle(float radians)
        {
            return Abscissa.RotateRadians(radians);
        }

        public bool NearZero()
        {
            return Math.Abs(x) <= C.EPS && Math.Abs(y) <= C.EPS;
        }

#region From Vector2D
        public static bool NearEquals(Vector v1, Vector v2, float eps = C.EPS)
        {
            v1 = v1 - v2;
            return v1.x >= -eps && v1.x <= eps && v1.y >= -eps && v1.y <= eps;
        }

        public void RotateImpure(Vector angle)
        {
            float newX = angle.x * x - angle.y * y;
            float newY = angle.y * x + angle.x * y;
            x = newX;
            y = newY;
        }

        public void RotateBackImpure(Vector angle)
        {
            float newX = angle.x * x + angle.y * y;
            float newY = angle.x * y - angle.y * x;
            x = newX;
            y = newY;
        }
        #endregion
    }
}
