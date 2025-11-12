using Geom2d;
using Serializer.BinarySerializer;

namespace Serializer.Extensions
{
    public static class Geom2dTypesExtensions
    {
        public static void AddGeom(this IBinarySerializer serializer, ref Vector vector)
        {
            if (serializer.isReader)
            {
                float x = 0, y = 0;
                serializer.Add(ref x);
                serializer.Add(ref y);
                vector = new Vector(x, y);
            }
            else
            {
                serializer.Add(ref vector.x);
                serializer.Add(ref vector.y);
            }
        }

        public static void AddGeom(this IBinarySerializer serializer, ref Rect rect)
        {
            float x = 0, y = 0, width = 0, height = 0;
            if (serializer.isReader)
            {
                serializer.Add(ref x);
                serializer.Add(ref y);
                serializer.Add(ref width);
                serializer.Add(ref height);
                rect = new Rect(x, y, width, height);
            }
            else
            {
                x = rect.X;
                y = rect.Y;
                width = rect.Width;
                height = rect.Height;

                serializer.Add(ref x);
                serializer.Add(ref y);
                serializer.Add(ref width);
                serializer.Add(ref height);
            }
        }

        public static void AddGeom(this IBinarySerializer serializer, ref Rotation rotation)
        {
            float angle = 0;
            if (serializer.isReader)
            {
                serializer.Add(ref angle);
                rotation = new Rotation(angle);
            }
            else
            {
                angle = rotation.Angle;
                serializer.Add(ref angle);
            }
        }
    }
}
