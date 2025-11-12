using System.Collections.Generic;
using System.IO;
using Serializer.BinarySerializer;
using Shared.Meta;

namespace Shared
{
    namespace CommonData
    {
        [System.Diagnostics.DebuggerDisplay("Name = {Name}, ArenaID = {ArenaID}")]
        public class MissionInfo : IDataStruct
        {
            public string Name;
            public string DisplayName;
            public string DisplayDescription;
            public string PathToMapData;
            public string PathToVisualScene;
            public string PathToUnitDescription;
            public Geom2d.Vector PositionAtMap;
            public string Uid;

            public MissionType Type;

            public string SceneName;
            public int IndexInChapter;

            public string[] MissionObjectiveDescriptions;

            public short[] PredefinedAllyHeroesIds;
            public short[] PredefinedAllySkinHeroesIds;

            public short[] PredefinedEnemyHeroesIds;

            public ChapterInfo ParentChapter;
            public MissionInfo[] ParentMissions;

            public int MissionIndexInAct = -1;

            public string[] CachedAssets;

            public DifficultyLevel DifficultyLevel
            {
                get { return this.ParentChapter.ParentDifficulty.Level; }
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref this.Name);
                dst.Add(ref this.DisplayName);
                dst.Add(ref this.DisplayDescription);
                dst.Add(ref this.PathToMapData);
                dst.Add(ref this.PathToVisualScene);
                dst.Add(ref this.PathToUnitDescription);
                dst.Add(ref this.PositionAtMap);
                dst.Add(ref this.Uid);
                dst.Add(ref this.MissionObjectiveDescriptions);

                var type = (int) this.Type;
                dst.Add(ref type);
                this.Type = (MissionType) type;

                dst.Add(ref this.PredefinedAllyHeroesIds);
                dst.Add(ref this.PredefinedAllySkinHeroesIds);

                dst.Add(ref this.PredefinedEnemyHeroesIds);

                dst.Add(ref this.CachedAssets);
                return true;
            }

            public void Init(int index, ChapterInfo parent)
            {
                this.IndexInChapter = index;
                this.SceneName = Path.GetFileNameWithoutExtension(this.PathToVisualScene);
                this.ParentChapter = parent;
            }
        }
    }
}