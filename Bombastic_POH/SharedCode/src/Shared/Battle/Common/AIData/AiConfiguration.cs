using Serializer.BinarySerializer;

namespace Shared.Battle
{
    public sealed class AiConfiguration : IDataStruct // Sealed for P2P synchronizer
    {
        public static AiConfiguration TeplateWithCustomWP(IMapObjectData[] customWP)
        {
            return new AiConfiguration("TeplateWithCustomWP", customWP);
        }

        private string mName;

        public Geom2d.Vector[] CustomWp;

        public AiConfiguration(string name, IMapObjectData[] customWP)
        {
            mName = name;

            if (customWP != null)
            {
                CustomWp = new Geom2d.Vector[customWP.Length];
                for (int i = 0, l = customWP.Length; i < l; ++i)
                {
                    CustomWp[i] = customWP[i].MapPosition;
                }
            }
        }

        public AiConfiguration() {}

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mName);
            dst.Add(ref CustomWp);

            return true;
        }
    }
}
