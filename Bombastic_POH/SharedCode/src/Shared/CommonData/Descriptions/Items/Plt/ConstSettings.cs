namespace Shared.CommonData.Plt
{
    public static class ConstSettings
    {
        public static int INVENTORY_MAX_ITEM_COUNT = 5000;

        public static Version CurrentVersion = new Version(1, 2, 3);
        
        public class Version
        {
            public int MajorMajorVersion { get; private set; }
            public int MinorVersion { get; private set; }
            public int HotfixVersion { get; private set; }

            public Version(int majorVersion, int minorVersion, int hotfixVersion)
            {
                MajorMajorVersion = majorVersion;
                MinorVersion = minorVersion;
                HotfixVersion = hotfixVersion;
            }
        }
    }
}