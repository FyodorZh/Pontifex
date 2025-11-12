using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions
{
    public static class ColorExtentions
    {
        public static void AddColor(this IBinarySerializer serializer, ref Color32 color)
        {
            if (serializer.isReader)
            {
                // чтобы не вносить сайд эфектов в переданную структуру
                byte r = 0, g = 0, b = 0, a = 0;
                serializer.Add(ref r);
                serializer.Add(ref g);
                serializer.Add(ref b);
                serializer.Add(ref a);
                color = new Color32(r, g, b, a);
            }
            else
            {
                serializer.Add(ref color.R);
                serializer.Add(ref color.G);
                serializer.Add(ref color.B);
                serializer.Add(ref color.A);
            }
        }
    }
}
