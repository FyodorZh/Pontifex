using System;

namespace Shared.Meta
{
    [Flags]
    public enum BattleType : byte
    {
        SyncPVP = 1 << 0,
        AsyncPVP = 1 << 1,
        AsyncPVE = 1 << 2
    }

    public static class BattleTypeHelper
    {
        public static bool IsPVPScenario(MissionType missionType)
        {
            return missionType == MissionType.AsyncPVP;
        }
    }

    public enum DifficultyLevel
    {
        Undefined = 0,
        Easy = 10,
        Normal = 20,
        Hard = 30,
    }

    public enum MissionType
    {
        BasePVE = 0,
        AlternativePVE = 10,
        DailyPVE = 20,
        TowerPVE = 30,
        AsyncPVP = 40,
        SyncPVE = 50,
    }
    
    public enum BattleCompetitionType : short
    {
        Undefined = 0,
        Simple = 1,
        League = 2
    }
}
