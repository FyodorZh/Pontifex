using System.Collections.Generic;
using System.IO;
using Serializer.Tools;
using Shared.Meta;

namespace Shared
{
    namespace CommonData
    {
        public interface IMissionsData
        {
            MissionsDifficultyInfo GetDifficulty(DifficultyLevel level);
            ChapterInfo GetChapter(string uid);
            MissionInfo GetMission(string uid);
            Dictionary<string, MissionInfo> GetAllMissions();

            IEnumerable<MissionsDifficultyInfo> DifficultiesList { get; }
        }

        public class MissionsData : CommonDataContainer, IMissionsData
        {
            public const string BLOB_PATH = "Assets/LogicResources/Runtime/Data/MissionsData.bytes";
            public const string FileName = "MissionsData";

            public readonly DifficultyLevel[] DifficultyLevels = {
                DifficultyLevel.Easy,
                DifficultyLevel.Hard,
            };

            private MissionsDifficultyInfo[] Difficulties;

            private readonly Dictionary<int, MissionsDifficultyInfo> AllDifficulties = new Dictionary<int, MissionsDifficultyInfo>();
            private readonly Dictionary<string, ChapterInfo> AllChapters = new Dictionary<string, ChapterInfo>();
            private readonly Dictionary<string, MissionInfo> AllMissions = new Dictionary<string, MissionInfo>();

            public Dictionary<string, MissionInfo> GetAllMissions()
            {
                return AllMissions;
            }

            public IEnumerable<MissionsDifficultyInfo> DifficultiesList { get { return Difficulties; } }
            public MissionsDifficultyInfo GetDifficulty(DifficultyLevel level)
            {
                MissionsDifficultyInfo info = null;
                AllDifficulties.TryGetValue((int)level, out info);
                return info;
            }

            public int GetDifficultyIndex(DifficultyLevel difficultyLevel)
            {
                for (int i = 0, n = DifficultyLevels.Length; i < n; ++i)
                {
                    if (difficultyLevel == DifficultyLevels[i])
                    {
                        return i;
                    }
                }
                return -1;
            }


            ChapterInfo IMissionsData.GetChapter(string uid)
            {
                return GetChapter(uid);
            }

            MissionInfo IMissionsData.GetMission(string uid)
            {
                return GetMission(uid);
            }

            public ChapterInfo GetChapter(string uid)
            {
                ChapterInfo info = null;
                AllChapters.TryGetValue(uid, out info);
                return info;
            }

            public MissionInfo GetMission(string uid)
            {
                MissionInfo info = null;
                AllMissions.TryGetValue(uid, out info);
                return info;
            }

            public bool GetChapterNumberAndMissionName(string missionUid, out int chapterNumber, out int missionNumber, out string missionNameLocalizationKey)
            {
                var difficultyLevels = DifficultyLevels;
                for (int difficultyLevelIndex = 0, difficultyLevelCount = difficultyLevels.Length; difficultyLevelIndex < difficultyLevelCount; difficultyLevelIndex++)
                {
                    var difficultyLevel = difficultyLevels[difficultyLevelIndex];
                    var difficultyInfo = GetDifficulty(difficultyLevel);
                    var chapters = difficultyInfo.ChaptersInfoList;
                    for (int i = 0, n = chapters.Length; i < n; ++i)
                    {
                        var chapterInfo = chapters[i];
                        for (int j = 0, m = chapterInfo.MissionsInfoCount; j < m; ++j)
                        {
                            var missionInfo = chapterInfo.GetMission(j);
                            if (missionInfo.Uid == missionUid)
                            {
                                chapterNumber = i + 1;
                                missionNumber = j + 1;
                                missionNameLocalizationKey = missionInfo.DisplayName;
                                return true;
                            }
                        }
                    }
                }
                chapterNumber = 0;
                missionNumber = 0;
                missionNameLocalizationKey = null;
                return false;
            }

            private void InitInternal(MissionsDataContainer container)
            {
                if (container == null)
                {
                    return;
                }

                Difficulties = container.Difficulties;

                ProcessMissionsData();
            }

            private void ProcessMissionsData()
            {
                if (Difficulties == null || Difficulties.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < Difficulties.Length; i++)
                {
                    MissionsDifficultyInfo difficulty = Difficulties[i];
                    AllDifficulties[(int)difficulty.Level] = difficulty;

                    for (int j = 0; j < difficulty.ChaptersInfoList.Length; ++j)
                    {
                        ChapterInfo chapter = difficulty.ChaptersInfoList[j];
                        AllChapters[chapter.Uid] = chapter;

                        chapter.ParentDifficulty = difficulty;

                        for (int k = 0; k < chapter.MissionsInfo.Length; ++k)
                        {
                            MissionInfo mission = chapter.MissionsInfo[k];
                            mission.MissionIndexInAct = k;
                            AllMissions[mission.Uid] = mission;

                            mission.Init(k, chapter);
                        }
                    }
                }
            }

            #region Init
            protected override string GetBlobPath(string dataPath)
            {
                return Path.Combine(dataPath, BLOB_PATH);
            }

            protected override void InitFromRawData(byte[] bytes)
            {
                InitInternal(CommonDataConstructor<MissionsDataContainer, CommonResoucesFactory>.CreateFromBytes(bytes, true));
            }
            #endregion
        }
    }
}
