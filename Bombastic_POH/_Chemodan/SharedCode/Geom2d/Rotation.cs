using System;
using Serializer.BinarySerializer;

namespace Geom2d
{
    public struct Rotation : IDataStruct
    {
        public const float Pi = 3.141592f;
        public const float Pi2 = Pi * 2;
        public const float PiHalf = Pi * 0.5f;
        public const float Rad2Grad = 180.0f / Pi;
        public const float Grad2Rad = Pi / 180.0f;
        public const float TinyAngle = 0.01f;

        private float mAngle; // (-pi; pi]

        public static readonly Rotation Identity = new Rotation(0);
        public static readonly Rotation NegativeIdentity = new Rotation(-Vector.Abscissa);

        public Rotation(float angleRadians)
        {
            mAngle = Normalize(angleRadians);
        }

        public Rotation(Vector direction)
        {
            mAngle = 0;
            Direction = direction;
        }

        public static Rotation FromDegree(float degree)
        {
            return new Rotation(ToRadians(degree));
        }

        public static Rotation FromToRotation(Vector from, Vector to)
        {
            return new Rotation(to) - new Rotation(from);
        }

        public float Angle
        {
            get
            {
                return mAngle;
            }
            set
            {
                mAngle = Normalize(value);
            }
        }

        public float AngleDegrees
        {
            get
            {
                return mAngle * Rad2Grad;
            }
        }

        public float TangageAngle
        {
            get
            {
                if (mAngle > PiHalf)
                {
                    return Pi - mAngle;
                }
                if (mAngle < -PiHalf)
                {
                    return -Pi - mAngle;
                }
                return mAngle;
            }
        }

        public float TangageAngleDegrees
        {
            get
            {
                return TangageAngle * Rad2Grad;
            }
        }

        /// <summary>
        /// true - right, false - left
        /// </summary>
        public bool OrientationRightLeft
        {
            get { return mAngle <= PiHalf && mAngle > -PiHalf; }
        }

        /// <summary>
        /// true - up, false - down
        /// </summary>
        public bool OrientationUpDown
        {
            get { return mAngle <= Pi && mAngle > 0; }
        }

        public Vector Direction
        {
            get
            {
                float sin = (float)Math.Sin(mAngle);
                float cos = (float)Math.Cos(mAngle);
                return new Vector(cos, sin);
            }
            set
            {
                DBG.Diagnostics.Assert(value.Magnitude2() >= C.EPS2);
                if (value.Magnitude2() >= C.EPS2)
                {
                    mAngle = (float)Math.Atan2(value.y, value.x);
                }
            }
        }

        public override string ToString()
        {
            return ((int)(AngleDegrees * 10) * 0.1) + " grad";
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mAngle);
            return true;
        }

        public static float ToRadians(float degree)
        {
            return degree * Grad2Rad;
        }

        public static float ToRadiansNormalize(float degree)
        {
            return Normalize(ToRadians(degree));
        }

        public static float ToDegrees(float radians)
        {
            return radians * Rad2Grad;
        }

        public static float ToDegreesNormalize(float radians)
        {
            return Normalize(radians) * Rad2Grad;
        }

        public static float ToRadians(Vector lhs, Vector rhs)
        {
            return Normalize(lhs.ToRadiansAngle - rhs.ToRadiansAngle);
        }

        public static float ToDegrees(Vector lhs, Vector rhs)
        {
            return ToDegrees(ToRadians(lhs, rhs));
        }

        public static bool IsUpDownDegree(float degree)
        {
            return IsUpDownRadians(ToRadians(degree));
        }

        public static bool IsUpDownRadians(float radians)
        {
            radians = Normalize(radians);
            return Math.Abs((Math.Abs(radians) - PiHalf)) < C.EPS;
        }

        public static bool operator ==(Rotation rot1, Rotation rot2)
        {
            return rot1.mAngle == rot2.mAngle;
        }

        public static bool operator !=(Rotation rot1, Rotation rot2)
        {
            return rot1.mAngle != rot2.mAngle;
        }

        public static Rotation Lerp(Rotation rot1, Rotation rot2, float t)
        {
            if (t <= 0) return rot1;
            if (t >= 1) return rot2;

            float angle1 = rot1.Angle;
            float angle2 = rot2.Angle;

            if (angle1 == angle2)
                return rot1;

            float path1 = (angle2 + Pi2) - angle1; // [0; 4pi]
            float path2 = angle1 - (angle2 - Pi2); // [0; 4pi]

            if (path1 >= Pi2)
                path1 -= Pi2;
            if (path2 >= Pi2)
                path2 -= Pi2;

            Rotation rotation;
            if (path1 < path2)
                rotation = new Rotation(angle1 + path1 * t);
            else
                rotation = new Rotation(angle1 - path2 * t);
            return rotation;
        }

        public static Rotation operator +(Rotation rot, float delta)
        {
            return new Rotation(rot.mAngle + delta);
        }

        public static Rotation operator -(Rotation rot, float delta)
        {
            return new Rotation(rot.mAngle - delta);
        }

        public static Rotation operator +(Rotation rot1, Rotation rot2)
        {
            return new Rotation(rot1.mAngle + rot2.mAngle);
        }

        public static Rotation operator -(Rotation rot1, Rotation rot2)
        {
            return new Rotation(rot1.mAngle - rot2.mAngle);
        }

        public static Rotation operator *(Rotation rot, float factor)
        {
            return new Rotation(rot.mAngle * factor);
        }

        public static Rotation operator *(float factor, Rotation rot)
        {
            return new Rotation(rot.mAngle * factor);
        }

        public static Rotation operator -(Rotation rot)
        {
            return new Rotation(-rot.mAngle);
        }

        public static float Normalize(float angle)
        {
            angle = angle % Pi2;
            if (angle > Pi)
            {
                angle -= Pi2;
            }
            else if (angle <= -Pi)
            {
                angle += Pi2;
            }
            return angle;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Rotation))
            {
                return false;
            }

            var rotation = (Rotation)obj;
            return mAngle == rotation.mAngle;
        }

        public bool IsNearZero
        {
            get { return Math.Abs(mAngle) <= TinyAngle; }
        }

        public override int GetHashCode()
        {
            return -1308167405 + mAngle.GetHashCode();
        }
    }
}
