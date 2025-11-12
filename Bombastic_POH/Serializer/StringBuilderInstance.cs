using System;
using System.Text;

namespace Serializer
{
    // Copy of Shared.StringBuilderInstance
    internal static class StringBuilderInstance
    {
        [ThreadStatic]
        private static StringBuilder mSB;

        public static StringBuilder SB
        {
            get
            {
                var sb = mSB;
                if (sb == null)
                {
                    sb = new StringBuilder();
                    mSB = sb;
                }
                sb.Length = 0;
                return sb;
            }
        }
    }
}
