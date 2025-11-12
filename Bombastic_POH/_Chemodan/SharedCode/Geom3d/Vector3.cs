using System;
using Serializer.BinarySerializer;

namespace Geom3d
{
    public struct Vector3 : IDataStruct
    {
        public static Vector3 zero
        {
            get
            {
                return new Vector3(0, 0, 0);
            }
        }

        public static Vector3 one
        {
            get
            {
                return new Vector3(1, 1, 1);
            }
        }

        public float x, y, z;

        public Vector3(float x, float y, float z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator Vector3(Geom2d.Vector v) {
            return new Vector3(v.x, v.y, 0);
        }

        public override string ToString()
        {
            return string.Format("({0}, {1}, {2})", x, y, z);
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3 && Equals((Vector3)obj);
        }

        public bool Equals(Vector3 other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + x.GetHashCode();
                hash = hash * 23 + y.GetHashCode();
                hash = hash * 23 + z.GetHashCode();
                return hash;
            }
        }

        #region IDataStruct Members

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref x);
            dst.Add(ref y);
            dst.Add(ref z);
            return true;
        }

        #endregion

        public static Vector3 operator +(Vector3 lhs, Vector3 rhs)
        {
            lhs.x += rhs.x;
            lhs.y += rhs.y;
            lhs.z += rhs.z;
            return lhs;
        }

        public static Vector3 operator -(Vector3 lhs, Vector3 rhs)
        {
            lhs.x -= rhs.x;
            lhs.y -= rhs.y;
            lhs.z -= rhs.z;
            return lhs;
        }

        public static Vector3 operator -(Vector3 vector)
        {
            vector.x = -vector.x;
            vector.y = -vector.y;
            vector.z = -vector.z;
            return vector;
        }

        public static Vector3 operator *(Vector3 vector, float factor)
        {
            vector.x *= factor;
            vector.y *= factor;
            vector.z *= factor;
            return vector;
        }

        public static Vector3 operator *(float factor, Vector3 vector)
        {
            vector.x *= factor;
            vector.y *= factor;
            vector.z *= factor;
            return vector;
        }

        public static Vector3 operator *(Vector3 v1, Vector3 v2)
        {
            v1.x *= v2.x;
            v1.y *= v2.y;
            v1.z *= v2.z;
            return v1;
        }

        public static Vector3 operator /(Vector3 vector, float factor)
        {
            vector.x /= factor;
            vector.y /= factor;
            vector.z /= factor;
            return vector;
        }

        public static Vector3 operator /(float factor, Vector3 vector)
        {
            vector.x /= factor;
            vector.y /= factor;
            vector.z /= factor;
            return vector;
        }

        public static Vector3 Max(Vector3 v1, Vector3 v2)
        {
            v1.x = Math.Max(v1.x, v2.x);
            v1.y = Math.Max(v1.y, v2.y);
            v1.z = Math.Max(v1.z, v2.z);
            return v1;
        }

        public static Vector3 Min(Vector3 v1, Vector3 v2)
        {
            v1.x = Math.Min(v1.x, v2.x);
            v1.y = Math.Min(v1.y, v2.y);
            v1.z = Math.Min(v1.z, v2.z);
            return v1;
        }

        public static bool operator ==(Vector3 _l, Vector3 _r)
        {
            return _l.x == _r.x && _l.y == _r.y && _l.z == _r.z;
        }

        public static bool operator !=(Vector3 _l, Vector3 _r)
        {
            return _l.x != _r.x || _l.y != _r.y || _l.z != _r.z;
        }
    }
}
