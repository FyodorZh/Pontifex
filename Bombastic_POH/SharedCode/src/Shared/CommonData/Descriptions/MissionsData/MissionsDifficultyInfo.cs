using Serializer.BinarySerializer;
using Shared.Meta;

namespace Shared
{
    namespace CommonData
    {
        public class MissionsDifficultyInfo : IDataStruct
        {
            public DifficultyLevel Level;
            public ChapterInfo[] ChaptersInfoList;

            public string GetFirstMissionUid()
            {
                string result = null;

                if (ChaptersInfoList != null && ChaptersInfoList.Length > 0)
                {
                    result = ChaptersInfoList[0].Uid;
                }

                return result;
            }

            #region IDataStruct Members
            public bool Serialize(IBinarySerializer dst)
            {
                int level = (int)Level;
                dst.Add(ref level);
                Level = (DifficultyLevel)level;

                dst.Add(ref ChaptersInfoList);

                return true;
            }
            #endregion
        }

    }
}
