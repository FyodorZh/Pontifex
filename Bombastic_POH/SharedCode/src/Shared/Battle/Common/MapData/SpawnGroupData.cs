using System;
using System.Collections.Generic;

namespace Shared.Battle
{
    public interface ISpawnGroupData : IMapObjectData
    {
        string Name { get; }

        int WavesCountLimit { get; }
        float SpawnDelay { get; }
        bool RandomSpawn { get; }
        bool Respawnable { get; }
        bool UseWaveDelayRespawnTime { get; }
        bool IsPresetRotation { get; }

        int GenerationLimit { get; }
        float GenerationUpTimeout { get; }

        float UnitLifeTime { get; }
        bool AlternateDeathAnim { get; }
        bool CanGenerationUpInDisabledState { get; }

        bool SpawnAllUnitsInSomePlace { get; }
        bool LiveWhenAllDead { get; }

        bool Enabled { get; }

        List<UnitPreset> Presets { get; }
        List<WayPointData> WayPoints { get; }
    }

    [Serializable]
    public class SpawnGroupData : MapObjectData, ISpawnGroupData
    {
        private const string FN_WAY_POINTS = "WayPoints";
        private const string FN_SPAWN_INFO = "SpawnInfo";
        private const string UNIT_PRESET = "UnitPreset";

        private const string FN_SPAWN_GROUP_NAME = "SpawnGroupName";

        private const string FN_IS_PRESET_ROTATION = "IsPresetRotation";
        private const string FN_WAVES_COUNT_LIMIT = "WavesCountLimit";
        private const string FN_RANDOM_SPAWN = "RandomSpawn";
        private const string FN_RESPAWNABLE = "Respawnable";
        private const string FN_SPAWN_DELAY = "SpawnDelay";

        private const string FN_GENERATION_LIMIT = "GenerationLimit";
        private const string FN_GENERATION_UP_TIMEOUT = "GenerationUpTimeout";
        private const string FN_CAN_GENERATION_UP_IN_DISABLED_STATE = "CanGenerationUpInDisabledState";

        private const string FN_SPAWN_ALL_UNITS_IN_SOME_PLACE = "SpawnAllUnitsInSomePlace";
        private const string FN_USE_WAVE_DELAY_RESPAWNTIME = "UseWaveDelayRespawnTime";

        private const string FN_UNIT_LIFE_TIME = "UnitLifeTime";
        private const string FN_ALTERNATE_DEATH = "AlternateDeath";
        private const string FN_LIVE_WHEN_ALL_DEAD = "LiveWhenAllDead";

        private const string FN_ENABLED = "Enabled";

        public List<WayPointData> WayPointsProp;
        public List<WayPointData> WayPoints { get { return WayPointsProp; } set { WayPointsProp = value; } }

        public List<UnitPreset> PresetsProp;
        public List<UnitPreset> Presets { get { return PresetsProp; } set { PresetsProp = value; } }

        public string NameProp;
        public string Name { get { return NameProp; } set { NameProp = value; } }

        public bool IsPresetRotationProp = false;
        public bool IsPresetRotation { get { return IsPresetRotationProp; } set { IsPresetRotationProp = value; } }
        public int WavesCountLimitProp = 1;
        public int WavesCountLimit { get { return WavesCountLimitProp; } set { WavesCountLimitProp = value; } }
        public bool RandomSpawnProp;
        public bool RandomSpawn { get { return RandomSpawnProp; } set { RandomSpawnProp = value; } }
        public bool RespawnableProp;
        public bool Respawnable { get { return RespawnableProp; } set { RespawnableProp = value; } }

        public float SpawnDelayProp;
        public float SpawnDelay { get { return SpawnDelayProp; } set { SpawnDelayProp = value; } }

        public int GenerationLimitProp;
        public int GenerationLimit { get { return GenerationLimitProp; } set { GenerationLimitProp = value; } }
        public float GenerationUpTimeoutProp;
        public float GenerationUpTimeout { get { return GenerationUpTimeoutProp; } set { GenerationUpTimeoutProp = value; } }
        public bool CanGenerationUpInDisabledStateProp;
        public bool CanGenerationUpInDisabledState { get { return CanGenerationUpInDisabledStateProp; } set { CanGenerationUpInDisabledStateProp = value; } }

        public bool SpawnAllUnitsInSomePlaceProp;
        public bool SpawnAllUnitsInSomePlace { get { return SpawnAllUnitsInSomePlaceProp; } set { SpawnAllUnitsInSomePlaceProp = value; } }
        public bool UseWaveDelayRespawnTimeProp;
        public bool UseWaveDelayRespawnTime { get { return UseWaveDelayRespawnTimeProp; } set { UseWaveDelayRespawnTimeProp = value; } }

        public float UnitLifeTimeProp;
        public float UnitLifeTime { get { return UnitLifeTimeProp; } set { UnitLifeTimeProp = value; } }
        public bool AlternateDeathAnimProp;
        public bool AlternateDeathAnim { get { return AlternateDeathAnimProp; } set { AlternateDeathAnimProp = value; } }

        public bool LiveWhenAllDeadProp;
        public bool LiveWhenAllDead { get { return LiveWhenAllDeadProp; } set { LiveWhenAllDeadProp = value; } }

        public bool EnabledProp;
        public bool Enabled { get { return EnabledProp; } set { EnabledProp = value; } }

        public SpawnGroupData()
        {
            WayPointsProp = new List<WayPointData>();
            Presets = new List<UnitPreset>();
        }

        public void Deserialize(StorageFolder from, string defName)
        {
            base.Deserialize(from);
            Name = from.GetItemAsString(FN_SPAWN_GROUP_NAME, defName);

            GenerationLimit = from.GetItemAsInt(FN_GENERATION_LIMIT);
            GenerationUpTimeout = from.GetItemAsFloat(FN_GENERATION_UP_TIMEOUT);
            CanGenerationUpInDisabledState = from.GetItemAsBool(FN_CAN_GENERATION_UP_IN_DISABLED_STATE);

            IsPresetRotation = from.GetItemAsBool(FN_IS_PRESET_ROTATION, IsPresetRotation);

            WavesCountLimit = from.GetItemAsInt(FN_WAVES_COUNT_LIMIT, WavesCountLimit);
            RandomSpawn = from.GetItemAsBool(FN_RANDOM_SPAWN);
            Respawnable = from.GetItemAsBool(FN_RESPAWNABLE);
            SpawnDelay = from.GetItemAsFloat(FN_SPAWN_DELAY);

            SpawnAllUnitsInSomePlace = from.GetItemAsBool(FN_SPAWN_ALL_UNITS_IN_SOME_PLACE);
            UseWaveDelayRespawnTime = from.GetItemAsBool(FN_USE_WAVE_DELAY_RESPAWNTIME);

            UnitLifeTime = from.GetItemAsFloat(FN_UNIT_LIFE_TIME);
            AlternateDeathAnim = from.GetItemAsBool(FN_ALTERNATE_DEATH);

            LiveWhenAllDeadProp = from.GetItemAsBool(FN_LIVE_WHEN_ALL_DEAD);
            Enabled = from.GetItemAsBool(FN_ENABLED);

            DeserializeWaypoints(from);
            DeserializeUnitPreset(from);
        }

        public override void Serialize(StorageFolder to)
        {
            base.Serialize(to);

            SerializeWaypoints(to);
            SerializeUnitPreset(to);

            to.AddItem(new StorageString(FN_SPAWN_GROUP_NAME, Name));

            to.AddItem(new StorageInt(FN_GENERATION_LIMIT, GenerationLimit));
            to.AddItem(new StorageFloat(FN_GENERATION_UP_TIMEOUT, GenerationUpTimeout));
            to.AddItem(new StorageBool(FN_CAN_GENERATION_UP_IN_DISABLED_STATE, CanGenerationUpInDisabledState));

            if (IsPresetRotation)
            {
                to.AddItem(new StorageBool(FN_IS_PRESET_ROTATION, IsPresetRotation));
            }

            to.AddItem(new StorageInt(FN_WAVES_COUNT_LIMIT, WavesCountLimit));
            to.AddItem(new StorageBool(FN_RANDOM_SPAWN, RandomSpawn));
            to.AddItem(new StorageBool(FN_RESPAWNABLE, Respawnable));
            to.AddItem(new StorageFloat(FN_SPAWN_DELAY, SpawnDelay));

            to.AddItem(new StorageBool(FN_SPAWN_ALL_UNITS_IN_SOME_PLACE, SpawnAllUnitsInSomePlace));
            to.AddItem(new StorageBool(FN_USE_WAVE_DELAY_RESPAWNTIME, UseWaveDelayRespawnTime));

            to.AddItem(new StorageBool(FN_LIVE_WHEN_ALL_DEAD, LiveWhenAllDeadProp));
            to.AddItem(new StorageBool(FN_ENABLED, Enabled));

            if (UnitLifeTime > 0)
            {
                to.AddItem(new StorageFloat(FN_UNIT_LIFE_TIME, UnitLifeTime));
                to.AddItem(new StorageBool(FN_ALTERNATE_DEATH, AlternateDeathAnim));
            }
        }

        private void DeserializeWaypoints(StorageFolder from)
        {
            StorageFolder wayPointsStorageFolder = from.GetFolder(FN_WAY_POINTS);
            if (wayPointsStorageFolder == null)
            {
                return;
            }

            WayPointsProp = new List<WayPointData>();
            foreach (StorageFolder waypoint in wayPointsStorageFolder.Items)
            {
                var point = new WayPointData();
                point.Deserialize(waypoint);
                WayPointsProp.Add(point);
            }
        }

        private void DeserializeUnitPreset(StorageFolder from)
        {
            Presets.Clear();
            StorageFolder spawnInfoStorageFolder = from.GetFolder(FN_SPAWN_INFO);
            if (spawnInfoStorageFolder == null)
            {
                Log.e("MapData.Deserialize: spawnInfoStorageFolder is null");
                return;
            }

            foreach (StorageFolder spawnData in spawnInfoStorageFolder.Items)
            {
                var presetFolder = spawnData;
                //Убрать
                if (presetFolder.Name != UNIT_PRESET)
                {
                    presetFolder = presetFolder.GetFolder(UNIT_PRESET);
                }

                if (presetFolder != null)
                {
                    var preset = new UnitPreset();
                    Presets.Add(preset);
                    preset.Deserialize(presetFolder);

                    //Если у юнита пустой тег, то он наследует тег группы
                    if (string.IsNullOrEmpty(preset.Tag))
                    {
                        preset.Tag = Name;
                    }
                }
            }
        }

        private void SerializeWaypoints(StorageFolder to)
        {
            if (WayPointsProp != null && WayPointsProp.Count > 0)
            {
                StorageFolder fld = new StorageFolder(FN_WAY_POINTS);
                for (int i = 0; i < WayPointsProp.Count; ++i)
                {
                    StorageFolder f = new StorageFolder(i.ToString());
                    fld.AddItem(f);
                    WayPointsProp[i].Serialize(f);
                }

                to.AddItem(fld);
            }
        }

        private void SerializeUnitPreset(StorageFolder to)
        {
            if (Presets != null && Presets.Count > 0)
            {
                StorageFolder fld = new StorageFolder(FN_SPAWN_INFO);
                for (int i = 0; i < Presets.Count; ++i)
                {
                    StorageFolder f = new StorageFolder(UNIT_PRESET);
                    fld.AddItem(f);
                    Presets[i].Serialize(f);
                }

                to.AddItem(fld);
            }
        }

    }

}
