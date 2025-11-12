using System;
using System.Globalization;

namespace Shared
{
    public struct Color32
    {
        public static readonly Color32 DEFAULT = new Color32(0xFFFFFFFF);
        public static readonly Color32 EMPTY = new Color32(0);

        public byte R, G, B, A;

        public Color32(byte r, byte g, byte b, byte a)
        {
            R = r; G = g; B = b; A = a;
        }

        public Color32(byte r, byte g, byte b) : this(r, g, b, 255) { }

        public Color32(UInt32 value)
        {
            R = (byte)((value & 0xFF000000) >> 24);
            G = (byte)((value & 0xFF0000) >> 16);
            B = (byte)((value & 0xFF00) >> 8);
            A = (byte)((value & 0xFF) >> 0);
        }

        public UInt32 AsUInt32()
        {
            UInt32 value = R;
            value = (value << 8) + G;
            value = (value << 8) + B;
            value = (value << 8) + A;
            return value;
        }

        public override string ToString()
        {
            return string.Format("Color32({0}, {1}, {2}, {3})", R, G, B, A);
        }

        public string ToHex()
        {
            return AsUInt32().ToString("X8");
        }

        public static Color32 FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return DEFAULT;
            }
            
            hex = hex.Replace("#", "");
            if (hex.Length == 8)
            {
                return InternalFromHexArgb(hex);
            }

            if (hex.Length == 6)
            {
                return InternalFromHexRgb(hex);
            }

            return DEFAULT;
        }

        private static Color32 InternalFromHexArgb(string hex)
        {
            int rgba = Int32.Parse(hex, NumberStyles.HexNumber);
            return new Color32((byte)((rgba & 0xff000000) >> 24), 
                (byte)((rgba & 0xff0000) >> 16),
                (byte)((rgba & 0xff00) >> 8),
                (byte)(rgba & 0xff));
        }

        private static Color32 InternalFromHexRgb(string hex)
        {
            var rgb = Int32.Parse(hex, NumberStyles.HexNumber);
            return new Color32((byte)((rgb & 0xff0000) >> 16),
                (byte)((rgb & 0xff00) >> 8),
                (byte)(rgb & 0xff));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Color32))
            {
                return false;
            }

            var color = (Color32)obj;
            return R == color.R &&
                   G == color.G &&
                   B == color.B &&
                   A == color.A;
        }

        public override int GetHashCode()
        {
            var hashCode = 1960784236;
            hashCode = hashCode * -1521134295 + R.GetHashCode();
            hashCode = hashCode * -1521134295 + G.GetHashCode();
            hashCode = hashCode * -1521134295 + B.GetHashCode();
            hashCode = hashCode * -1521134295 + A.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Color32 _l, Color32 _r)
        {
            return _l.R == _r.R && _l.G == _r.G && _l.B == _r.B && _l.A == _r.A;
        }

        public static bool operator !=(Color32 _l, Color32 _r)
        {
            return _l.R != _r.R || _l.G != _r.G || _l.B != _r.B || _l.A != _r.A;
        }

        public bool IsEmpty
        {
            get { return this == EMPTY; }
        }
    }
}
