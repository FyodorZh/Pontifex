namespace Shared.CommonData.Plt
{
    public static class ItemLevelExtensions
    {
        public static short MaxLevel(this ItemLevel[] levels)
        {
            return (short) (levels.Length - 1);
        }

        public static bool IsMax(this ItemLevel[] levels, short level)
        {
            var maxLevel = levels.MaxLevel();

            return level >= maxLevel;
        }

        public static T Find<T>(this T[] levels, int level)
            where T : ItemLevel
        {
            int index = LevelToIndex(level);
            if (0 <= index && index < levels.Length)
            {
                return levels[index];
            }

            return null;
        }

        public static ItemLevel FindNext(this ItemLevel[] levels, short level)
        {
            return levels.Find(level + 1);
        }

        public static int LevelToIndex(int level)
        {
            return level;
        }

        public static void AccumulateRpgParams(this ItemLevel[] levels, ref System.Collections.Generic.Dictionary<short, float> result, short targetLevel)
        {
            for (int i = 0, il = System.Math.Min(levels.Length, targetLevel + 1); i < il; ++i)
            {
                var rpgParams = levels[i].RpgParamsChange;
                for (int j = 0, jl = rpgParams.Length; j < jl; ++j)
                {
                    float paramValue = 0f;
                    var paramDescId = rpgParams[j].RpgParamDescId;
                    if (result.TryGetValue(paramDescId, out paramValue))
                    {
                        result[rpgParams[j].RpgParamDescId] = rpgParams[j].Value + paramValue;
                    }
                    else
                    {
                        result[rpgParams[j].RpgParamDescId] = rpgParams[j].Value;
                    }
                }
            }
        }

        public static float GetRpgParamValue(this ItemLevel[] levels, short paramId, short targetLevel)
        {
            float result = 0;

            for (int i = 0, il = System.Math.Min(levels.Length, targetLevel + 1); i < il; ++i)
            {
                var rpgParams = levels[i].RpgParamsChange;
                for (int j = 0, jl = rpgParams.Length; j < jl; ++j)
                {
                    if (rpgParams[j].RpgParamDescId == paramId)
                    {
                        result += rpgParams[j].Value;
                    }
                }
            }

            return result;
        }
    }
}
