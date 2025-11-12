using System;
namespace Geom2d
{
    public struct Ray
    {
        Vector origin;
        Vector direction;

        public Vector Origin
        {
            get { return origin; }
        }

        public Vector Direction
        {
            get { return direction; }
        }

        public Ray(Vector _origin, Vector _direction, bool shouldNormalize = true)
        {
            origin = _origin;
            if (shouldNormalize)
            {
                direction = _direction.Normalized();
            }
            else
            {
                direction = _direction;
            }
            
        }

        public float Cast(Vector lineOrigin, Vector lineNormal)
        {
            float d = -Vector.Dot(lineNormal, lineOrigin);
            float numer = Vector.Dot(lineNormal, origin) + d;
            float denom = Vector.Dot(lineNormal, direction);

            if (denom == 0)
            {
                float nonZeroNumSign = numer < 0 ? -1 : 1;
                return nonZeroNumSign * float.PositiveInfinity;
            }
            else
            {
                return -numer / denom;
            }
        }
    }
}