using System;
using System.Collections.Generic;

namespace Shared.Battle
{
    public interface ISpawnPointData : IMapObjectData
    {
        ID<ISpawnPointData> ID { get; }
        SpawnPointType SpawnPointType { get; }
        int SubType { get; }

        string Name { get; }

        Team Team { get; }

        float StartDelay { get; }
        float WaveDelay { get; }
        bool NewWaveWhenNoOneLive { get; }
        bool NotifyOnNewWaveStarts { get; }

        int GroupsCount { get; }
        List<SpawnGroupData> GroupsData { get; }
        ISpawnGroupData GetGroup(string name);

        List<int> DependencyIDs { get; }
        string HierarсhyPath { get; }
    }

    /// <summary>
    /// <Prop> fields needed for right work editor drawer in unity3d
    /// </summary>
    [Serializable]
    public class SpawnPointData : MapObjectData, ISpawnPointData
    {
        public List<SpawnGroupData> GroupsDataProp;
        public List<SpawnGroupData> GroupsData { get { return GroupsDataProp; } set { GroupsDataProp = value; } }
        public int GroupsCount { get { return GroupsData != null ? GroupsData.Count : 0; } }

        public ISpawnGroupData GetGroup(string name)
        {
            if (GroupsData != null)
            {
                foreach (var group in GroupsData)
                {
                    if (group.Name == name)
                    {
                        return group;
                    }
                }
            }
            return null;
        }

        public string NameProp;
        public string Name { get { return NameProp; } set { NameProp = value; } }
        public int TeamProp;
        public Team Team { get { return (Team)TeamProp; }  }
        public float StartDelayProp;
        public float StartDelay { get { return StartDelayProp; } set { StartDelayProp = value; } }
        public float WaveDelayProp;
        public float WaveDelay { get { return WaveDelayProp; } set { WaveDelayProp = value; } }
        public bool NewWaveWhenNoOneLiveProp;
        public bool NewWaveWhenNoOneLive { get { return NewWaveWhenNoOneLiveProp; } set { NewWaveWhenNoOneLiveProp = value; } }
        public bool NotifyOnNewWaveStartsProp;
        public bool NotifyOnNewWaveStarts { get { return NotifyOnNewWaveStartsProp; } set { NotifyOnNewWaveStartsProp = value; } }

        public SpawnPointType SpawnPointTypeProp;
        public SpawnPointType SpawnPointType { get { return SpawnPointTypeProp; } set { SpawnPointTypeProp = value; } }
        public int SubTypeProp;
        public int SubType { get { return SubTypeProp; } set { SubTypeProp = value; } }

        public int IDProp;
        public ID<ISpawnPointData> ID { get { return ID<ISpawnPointData>.DeserializeFrom(IDProp); } set { IDProp = value.SerializeTo(); } }
        public List<int> DependencyIDProps = new List<int>(0);
        public List<int> DependencyIDs { get { return DependencyIDProps; } set { DependencyIDProps = value; } }

        public string HierarhyPathProp;
        public string HierarсhyPath { get { return HierarhyPathProp; } set { HierarhyPathProp = value; } }

        private const string FN_SPAWN_POINT_ID = "SpawnPointId";

        private const string FN_SPAWN_POINT_NAME = "SpawnPointName";
        private const string FN_GROUPS_INFO = "GroupsInfo";

        private const string FN_TEAM = "Team";
        private const string FN_SPAWN_POINT_TYPE = "SpawnPointType";
        private const string FN_SUB_TYPE = "SubType";
        private const string FN_START_DELAY = "StartDelay";
        private const string FN_WAVE_DELAY = "WaveDelay";
        private const string FN_NEW_WAVE_WHEN_NO_ONE_LIVE = "NewWaveWhenNoOneLive";
        private const string FN_NOTIFY_ON_NEW_WAVE_STARTS = "NotifyOnNewWaveStarts";

        private const string FN_HIERARCHY_PATH = "HierarhyPath";

        public SpawnPointData()
        {
            GroupsData = new List<SpawnGroupData>();
            IDProp = -1;
        }

        public override void Deserialize(StorageFolder from)
        {
            base.Deserialize(from);

            IDProp = from.GetItemAsInt(FN_SPAWN_POINT_ID);
            Name = from.GetItemAsString(FN_SPAWN_POINT_NAME);
            TeamProp = from.GetItemAsInt(FN_TEAM);

            var pointTypeStr = from.GetItemAsString(FN_SPAWN_POINT_TYPE);
            if (Enum.IsDefined(typeof(SpawnPointType), pointTypeStr))
            {
                SpawnPointType = (SpawnPointType)Enum.Parse(typeof(SpawnPointType), pointTypeStr);
            }
            else
            {
                Log.i("SpawnPointData.Deserialize cant parse SpawnPointType by name:  " + pointTypeStr);
            }

            SubType = from.GetItemAsInt(FN_SUB_TYPE);

            StartDelay = from.GetItemAsFloat(FN_START_DELAY);
            WaveDelay = from.GetItemAsFloat(FN_WAVE_DELAY);
            NewWaveWhenNoOneLive = from.GetItemAsBool(FN_NEW_WAVE_WHEN_NO_ONE_LIVE);
            NotifyOnNewWaveStarts= from.GetItemAsBool(FN_NOTIFY_ON_NEW_WAVE_STARTS);

            DependencyIDProps.Clear();
            HierarсhyPath = from.GetItemAsString(FN_HIERARCHY_PATH);

            DeserializeGroupsData(from);
        }

        public override void Serialize(StorageFolder to)
        {
            base.Serialize(to);
            SerializeGroupsData(to);

            to.AddItem(new StorageInt(FN_SPAWN_POINT_ID, IDProp));
            to.AddItem(new StorageString(FN_SPAWN_POINT_NAME, Name));
            to.AddItem(new StorageInt(FN_TEAM, TeamProp));
            to.AddItem(new StorageString(FN_SPAWN_POINT_TYPE, SpawnPointType.ToString()));

            if (SubType > 0)
            {
                to.AddItem(new StorageInt(FN_SUB_TYPE, SubType));
            }

            to.AddItem(new StorageFloat(FN_START_DELAY, StartDelay));
            to.AddItem(new StorageFloat(FN_WAVE_DELAY, WaveDelay));
            to.AddItem(new StorageBool(FN_NEW_WAVE_WHEN_NO_ONE_LIVE, NewWaveWhenNoOneLive));
            to.AddItem(new StorageBool(FN_NOTIFY_ON_NEW_WAVE_STARTS, NotifyOnNewWaveStarts));

            to.AddItem(new StorageString(FN_HIERARCHY_PATH, HierarсhyPath));
        }

        private void DeserializeGroupsData(StorageFolder from)
        {
            GroupsData.Clear();
            StorageFolder groupsInfoStorageFolder = from.GetFolder(FN_GROUPS_INFO);
            if (groupsInfoStorageFolder != null)
            {
                foreach (StorageFolder groupData in groupsInfoStorageFolder.Items)
                {
                    DeserializeGroup(groupData);
                }
            }
            else
            {
                ConvertToGroup(from);
            }
        }

        private void ConvertToGroup(StorageFolder from)
        {
            SpawnGroupData info = DeserializeGroup(from);

            NewWaveWhenNoOneLive = SpawnPointType == SpawnPointType.Glyph;

            info.Enabled = true;
            info.RandomSpawn = SpawnPointType == SpawnPointType.Glyph;
            info.Respawnable = SpawnPointType == SpawnPointType.Hero;

            switch (SpawnPointType)
            {
                case SpawnPointType.Common:
                case SpawnPointType.Glyph:
                    {
                        info.WavesCountLimit = -1;
                    }
                    break;
                case SpawnPointType.Hero:
                    {
                        info.WavesCountLimit = 1;
                    }
                    break;
                default:
                    //nothing
                    break;
            }
        }

        private SpawnGroupData DeserializeGroup(StorageFolder from)
        {
            var info = new SpawnGroupData();
            GroupsData.Add(info);
            info.Deserialize(from, Name);
            return info;
        }

        private void SerializeGroupsData(StorageFolder to)
        {
            if (GroupsData != null && GroupsData.Count > 0)
            {
                StorageFolder fld = new StorageFolder(FN_GROUPS_INFO);
                for (int i = 0; i < GroupsData.Count; ++i)
                {
                    StorageFolder f = new StorageFolder(i.ToString());
                    fld.AddItem(f);
                    GroupsData[i].Serialize(f);
                }

                to.AddItem(fld);
            }
        }
    }
}