using System.Collections.Generic;
using System.Xml.Linq;
using System;
using System.Linq;
using Shared.Battle.Collisions;

public enum ScenarioType
{
    None,
    PVP,
    PVE,
    Universal
}

namespace Shared.Battle
{
    public interface IAbilitySlotUpgradeSettings
    {
        List<int> SlotsLevels { get; }
        List<int> UltimateAvailabilityLevels { get; }
        bool HasUpgrades { get; }
    }

    [Serializable]
    public struct ReferencePrefabGuids
    {
        public List<string> Guids;

        private const string PREFAB_ASSET_GUID = "prefabAssetGuid";

        public static ReferencePrefabGuids Deserialize(StorageFolder from)
        {
            List<string> data = new List<string>();
            if (from != null)
            {
                for (int i = 0; i < from.Items.Items.Count; ++i)
                {
                    StorageFolder folder = from.Items.Items[i] as StorageFolder;
                    if (folder == null)
                        continue;

                    string guid = folder.GetItemAsString(PREFAB_ASSET_GUID);
                    data.Add(guid);
                }
            }

            return new ReferencePrefabGuids
            {
                Guids = data
            };
        }

        public void Serialize(StorageFolder to)
        {
            if (to != null && Guids != null)
            {
                for (int i = 0; i < Guids.Count; i++)
                {
                    StorageFolder pairStorageFolder = new StorageFolder("" + i);
                    pairStorageFolder.AddItem(new StorageString(PREFAB_ASSET_GUID, Guids[i]));
                    to.AddItem(pairStorageFolder);
                }
            }
            else
            {
                Log.e("Storage folder for reference prefab guids is null.");
            }
        }
    }

    public interface IMapData
    {
        ICameraSettings Camera { get; }
        IMissionConstants MissionConstants { get; }
        IAbilitySlotUpgradeSettings AbilitySlotUpgradeSettings { get; }

        ArrayEnumerable<ISpawnPointData> SpawnPoints { get; }
        StorageFolder TriggersData { get; }
        StorageFolder QuestsData { get; }

        CollisionData Collisions { get; }
    }

    public class MapData : IMapData, ISerializable, IDSource<ISpawnPointData>
    {

        public StorageFolder TriggersData { get; set; }
        public StorageFolder QuestsData { get; set; }

        public StorageFolder TriggerAreasData { get; set; }

        public CollisionData Collisions { get; set; }

        public SpawnPointData[] mSpawnPoints;
        public ArrayEnumerable<ISpawnPointData> SpawnPoints { get { return new ArrayEnumerable<ISpawnPointData>(mSpawnPoints); } }

        public MissionConstants mMissionConstants;
        public IMissionConstants MissionConstants { get { return mMissionConstants; } }

        public AbiliTySlotUpgradeSettings mAbilitySlotsSettings;
        public IAbilitySlotUpgradeSettings AbilitySlotUpgradeSettings { get { return mAbilitySlotsSettings; } }

        public CameraSettings mCamera;
        public ICameraSettings Camera { get { return mCamera; } }

        public ReferencePrefabGuids ReferencePrefabGuids;

        private const string FN_SPAWN_POINTS = "SpawnPoints";
        private const string FN_ABILITY_SLOTS_SETTING_FOLDER = "AbilitySlotsSettings";
        private const string FN_REFERENCE_PREFAB_GUIDS = "ReferencePrefabGuids";
        private const string FN_CONSTANTS = "Constants";
        private const string FN_TRIGGERS = "Triggers";
        private const string FN_QUESTS = "Quests";
        private const string FN_TRIGGER_AREAS = "TriggerAreas";
        private const string FN_FOG_OF_WAR_TYPE = "FogOfWarType";

        private const string FN_SCENARIO_TYPE = "ScenarioType";

        private const string FN_CAMERA = "Camera";
        private const string FN_COLLISION_DATA = "CollisionData";

        //TODO: Очень не нравится необходимость вообще создавать StorageFolder в этом случае, надо переписать! 
        public static MapData Create(string path)
        {
            StorageFolder storageFolder = new StorageFolder();
            CStorageSerializer.loadFromFile(path, storageFolder, false, true);

            MapData mapData = new MapData(storageFolder);

            return mapData;
        }
        //TODO: Полная хрень на клиенте и на сервере данные должны собираться одиннаково!!!! 
        public static MapData CreateFromXDoc(XDocument xDoc)
        {
            StorageFolder storageFolder = new StorageFolder();
            CStorageSerializer.loadFromXDoc(xDoc, storageFolder, false);
            MapData newMapData = new MapData(storageFolder);
            return newMapData;
        }

        public bool Save(string path)
        {
            StorageFolder storageFolder = new StorageFolder("MapData");
            bool result = true;
            try
            {
                Serialize(storageFolder);
            } catch(Exception e)
            {
                Log.e("Exception during save of mapdata {0}", e);
                result = false;
            }
            if (result)
            {
                CStorageSerializer.saveStorageToFile(path, storageFolder);
            }
            return result;
        }

        public MapData() { }

        public MapData(StorageFolder folder)
        {
            Deserialize(folder);
        }

        public void Deserialize(StorageFolder from)
        {
            DeserializeSpawnPoints(from);
            mMissionConstants = new MissionConstants();
            mMissionConstants.Deserialize(from.GetFolder(FN_CONSTANTS)); // TODO GlobalConstants is part of mapdata by its sense. 
            // It should be removed and merged into MapData and be accessible through world in battle code
            mAbilitySlotsSettings = Battle.AbiliTySlotUpgradeSettings.Deserialize(from.GetFolder(FN_ABILITY_SLOTS_SETTING_FOLDER));
            ReferencePrefabGuids = ReferencePrefabGuids.Deserialize(from.GetFolder(FN_REFERENCE_PREFAB_GUIDS));
            TriggersData = from.GetFolder(FN_TRIGGERS);
            QuestsData = from.GetFolder(FN_QUESTS);
            TriggerAreasData = from.GetFolder(FN_TRIGGER_AREAS);
            mCamera = CameraSettings.Deserialize(from.GetFolder(FN_CAMERA));

            var collisionFolder = from.GetFolder(FN_COLLISION_DATA);
            if (collisionFolder != null)
            {
                Collisions = new CollisionData();
                Collisions.Deserialize(collisionFolder);
            }
        }

        private void DeserializeSpawnPoints(StorageFolder from)
        {
            StorageFolder spawnPointsStorageFolder = from.GetFolder(FN_SPAWN_POINTS);
            if (spawnPointsStorageFolder == null)
            {
                Log.e("MapData.Deserialize: spawnPointsStorageFolder is null");
                return;
            }

            var spl = new List<SpawnPointData>();
            foreach (StorageFolder spawnPoint in spawnPointsStorageFolder.Items)
            {
                var sp = new SpawnPointData();
                sp.Deserialize(spawnPoint);
                spl.Add(sp);
            }

            mSpawnPoints = spl.ToArray();

            _lastSpawnPointId = FindLastSpawnPointId(mSpawnPoints);

            //Проверка на то что спавнеры в xml имеют id
            for (int i = 0; i < mSpawnPoints.Length; ++i)
            {
                if (!mSpawnPoints[i].ID.IsValid)
                {
                    mSpawnPoints[i].ID = new ID<ISpawnPointData>(this);
                }
            }
        }

        public void Serialize(StorageFolder to)
        {
            var constantsFolder = new StorageFolder(FN_CONSTANTS);
            mMissionConstants.Serialize(constantsFolder);
            to.AddItem(constantsFolder);

            var abilitySlotsSettingsFolder = new StorageFolder(FN_ABILITY_SLOTS_SETTING_FOLDER);
            mAbilitySlotsSettings.Serialize(abilitySlotsSettingsFolder);
            to.AddItem(abilitySlotsSettingsFolder);

            if (mSpawnPoints != null && mSpawnPoints.Length > 0)
            {
                StorageFolder fld = new StorageFolder(FN_SPAWN_POINTS);
                for (int i = 0; i < mSpawnPoints.Length; ++i)
                {
                    StorageFolder f = new StorageFolder(i.ToString());
                    fld.AddItem(f);
                    mSpawnPoints[i].Serialize(f);
                }

                to.AddItem(fld);
            }

            SerializeCollisionData(to);

            if (TriggersData != null)
            {
                TriggersData.Name = FN_TRIGGERS;
                to.AddItem(TriggersData);
            }

            if (QuestsData != null)
            {
                QuestsData.Name = FN_QUESTS;
                to.AddItem(QuestsData);
            }

            if (TriggerAreasData != null)
            {
                TriggerAreasData.Name = FN_TRIGGER_AREAS;
                to.AddItem(TriggerAreasData);
            }

            var cameraFolder = new StorageFolder(FN_CAMERA);
            mCamera.Serialize(cameraFolder);
            to.AddItem(cameraFolder);
        }

        private void SerializeCollisionData(StorageFolder to)
        {
            if (Collisions != null)
            {
                var folder = new StorageFolder(FN_COLLISION_DATA);
                Collisions.Serialize(folder);
                to.AddItem(folder);
            }
        }

        #region IDSource<SpawnPointData>
        private int _lastSpawnPointId;
        int IDSource<ISpawnPointData>.GenNewId()
        {
            return ++_lastSpawnPointId;
        }

        private int FindLastSpawnPointId(SpawnPointData[] spawnPoints)
        {
            int result = 0;
            for (int i = 0; i < spawnPoints.Length; ++i)
            {
                if (spawnPoints[i].ID.SerializeTo() > result)
                {
                    result = spawnPoints[i].ID.SerializeTo();
                }
            }

            return result;
        }

        #endregion
    }

    public enum FogOfWarType
    {
        DistanceAndBrush,
        BrushOnly
    }

    public interface ICameraSettings
    {
        float PrAlpha { get; }
        float PrBeta { get; }
        float PrDistance { get; }
        float PrFOV { get; }
        bool PrFlipForOtherTeam { get; }
    }

    [Serializable]
    public struct CameraSettings : ICameraSettings
    {
        public float Alpha;
        public float PrAlpha { get { return Alpha; } }
        public float Beta;
        public float PrBeta { get { return Beta; } }
        public float Distance;
        public float PrDistance { get { return Distance; } }

        public float FOV;
        public float PrFOV { get { return FOV; } }

        public bool FlipForOtherTeam;
        public bool PrFlipForOtherTeam { get { return FlipForOtherTeam; } }

        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageFloat("Alpha", Alpha));
            to.AddItem(new StorageFloat("Beta", Beta));
            to.AddItem(new StorageFloat("Distance", Distance));
            to.AddItem(new StorageFloat("FOV", FOV));
            if (FlipForOtherTeam)
            {
                to.AddItem(new StorageBool("FlipForOtherTeam", FlipForOtherTeam));
            }
        }

        public static CameraSettings Deserialize(StorageFolder from)
        {
            if (from != null)
            {
                return new CameraSettings
                {
                    Alpha = from.GetItemAsFloat("Alpha", 50),
                    Beta = from.GetItemAsFloat("Beta", 0),
                    Distance = from.GetItemAsFloat("Distance", 20),
                    FOV = from.GetItemAsFloat("FOV", 26.9f),
                    FlipForOtherTeam = from.GetItemAsBool("FlipForOtherTeam")
                };
            }
            else
            {
                return new CameraSettings { Alpha = 50, Beta = 0, Distance = 20, FOV = 26.9f, FlipForOtherTeam = false };
            }
        }
    }

    [System.Serializable]
    public struct AbiliTySlotUpgradeSettings : IAbilitySlotUpgradeSettings
    {
        public List<int> slotsLevels;
        public List<int> SlotsLevels { get { return slotsLevels; } set { slotsLevels = value; } }

        public List<int> ultimateAvailabilityLevels;
        public List<int> UltimateAvailabilityLevels { get { return ultimateAvailabilityLevels; } set { ultimateAvailabilityLevels = value; } }

        public bool HasUpgrades
        {
            get { return slotsLevels != null && slotsLevels.Count > 0; }
        }

        private const string SLOTS_LEVELS = "SlotsLevels";
        private const string ULTIMATE_AVALIABILITY_LEVELS = "UltimateAvailabilityLevels";

        public static AbiliTySlotUpgradeSettings Deserialize(StorageFolder from)
        {
            var slotslevels = new List<int>();
            var ultlevels = new List<int>();

            StorageFolder slotsLevelsfld = from == null ? null : from.GetFolder(SLOTS_LEVELS);
            if (slotsLevelsfld != null)
            {
                foreach (StorageItem itm in slotsLevelsfld.Items)
                {
                    slotslevels.Add(itm.asInt());
                }
            }

            StorageFolder ultLevelsfld = from == null ? null : from.GetFolder(ULTIMATE_AVALIABILITY_LEVELS);
            if (ultLevelsfld != null)
            {
                foreach (StorageItem itm in ultLevelsfld.Items)
                {
                    ultlevels.Add(itm.asInt());
                }
            }
            ultlevels = ultlevels.Distinct().Where(x => x > 0).OrderBy((x) => x).ToList();
            ultlevels.Sort();

            return new AbiliTySlotUpgradeSettings
            {
                SlotsLevels = slotslevels,
                UltimateAvailabilityLevels = ultlevels
            };
        }

        internal void Serialize(StorageFolder to)
        {
            UltimateAvailabilityLevels = UltimateAvailabilityLevels.Distinct().Where(x => x > 0).OrderBy((x) => x).ToList();
            UltimateAvailabilityLevels.Sort();

            StorageFolder slotsLevelsfld = new StorageFolder(SLOTS_LEVELS);
            to.AddItem(slotsLevelsfld);
            foreach (var lvl in SlotsLevels)
            {
                slotsLevelsfld.AddItem(new StorageInt(lvl));
            }

            StorageFolder ultLevelsfld = new StorageFolder(ULTIMATE_AVALIABILITY_LEVELS);
            to.AddItem(ultLevelsfld);
            foreach (var lvl in UltimateAvailabilityLevels)
            {
                ultLevelsfld.AddItem(new StorageInt(lvl));
            }
        }
    }
}