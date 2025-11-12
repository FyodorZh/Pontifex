using Shared.Meta;
using System;
using System.Collections.Generic;

public enum MobStat
{
    Health = 0,
    AttackDamage = 1,
    AbilityPower = 2,
}

[Serializable]
public struct PowerPair
{
    public int MissionPowerAhead;
    public float Multiplier;
}

[Serializable]
public struct StatCoefMap
{
    public MobStat Stat;
    public float BalanceCoef;
    public float BaseValue;
    public List<PowerPair> Map;

    public float GetCoeficent(int deltaPower)
    {
        float powerCoef = BaseValue;

        for (int i = 0, l = Map.Count; i < l; ++i)
        {
            PowerPair targetMap = Map[i];
            if (deltaPower > targetMap.MissionPowerAhead)
            {
                powerCoef = targetMap.Multiplier;
                break;
            }
        }

        return powerCoef;
    }
}

[Serializable]
public struct MissionTypeCoeficients
{
    public MissionType MissionType;
    public int BaseHeroPower;
    public List<StatCoefMap> StatMultipliers;

    public float GetCoeficent(int targetPower, int missionPower, MobStat targetStat)
    {
        int deltaPower = missionPower - targetPower;
        float powerCoef = 1.0f;
        float balanceCoef = 1.0f;

        for (int i = 0, l = StatMultipliers.Count; i < l; ++i)
        {
            StatCoefMap targetMap = StatMultipliers[i];
            if (targetMap.Stat == targetStat)
            {
                powerCoef = targetMap.GetCoeficent(deltaPower);
                balanceCoef = targetMap.BalanceCoef;
                break;
            }
        }

        return (1f + balanceCoef * (targetPower - BaseHeroPower) / 1000.0f) * powerCoef - 1f;
    }
}

public sealed partial class BalancePowerCoeficientHelper
{
    private static List<MissionTypeCoeficients> Table = new List<MissionTypeCoeficients>();

    public static float GetCoeficent(int targetPower, int missionPower, MissionType missionType, MobStat targetStat)
    {
        float result = 1.0f;

        for (int i = 0, l = Table.Count; i < l; ++i)
        {
            MissionTypeCoeficients targetTable = Table[i];
            if (targetTable.MissionType == missionType)
            {
                result = targetTable.GetCoeficent(targetPower, missionPower, targetStat);
                break;
            }
        }

        return result;
    }
}
