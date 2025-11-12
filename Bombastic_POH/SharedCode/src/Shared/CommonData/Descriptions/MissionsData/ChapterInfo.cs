using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        [System.Diagnostics.DebuggerDisplay("Name = {Name}, Type = {Type}")]
        public class ChapterInfo : IDataStruct
        {
            public string Name;
            public string PathToBackSprite;
            public string Description;
            public string Uid;

            public MissionInfo[] MissionsInfo;

            public MissionsDifficultyInfo ParentDifficulty;

            #region IDataStruct Members
            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Name);
                dst.Add(ref PathToBackSprite);
                dst.Add(ref Description);

                dst.Add(ref Uid);

                dst.Add(ref MissionsInfo);

                return true;
            }
            #endregion

            public int MissionsInfoCount { get { return MissionsInfo != null ? MissionsInfo.Length : 0; } }

            public MissionInfo GetMission(int index)
            {
                if (index < 0 || index >= MissionsInfoCount)
                {
                    return null;
                }
                return MissionsInfo[index];
            }
        }

    }
}
