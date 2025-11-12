using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public class MissionsDataContainer :  IDataStruct
        {
            public MissionsDifficultyInfo[] Difficulties;

            #region IDataStruct Members

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Difficulties);
                return true;
            }

            #endregion
        }
    }
}
