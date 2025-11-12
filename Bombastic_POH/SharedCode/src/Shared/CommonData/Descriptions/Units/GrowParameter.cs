using System;
using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {

        public interface IGrowParameter<T> : IDataStruct
        {
            T BaseValue { get; }
            T LevelGrowth { get; }
            T TeamGrowth { get; }

            T Evaluate(float progress, int generation, Shared.Battle.UnitClassValue unitType);
        }

        public sealed class GrowParameterConstants
        {
            public const string FN_GROW_PARAM_BASEVALUE = "BaseValue";
            public const string FN_GROW_PARAM_LEVEL_GROWTH = "LevelGrowth";
            public const string FN_GROW_PARAM_TEAM_GROWTH = "TeamGrowth";
        }
        
        public class IntGrowParameter : IGrowParameter<int>
        {
            protected int mBaseValue;
            protected int mLevelGrowth;
            protected int mTeamGrowth;

            public int BaseValue { get { return mBaseValue; } set { mBaseValue = value; } }
            public int LevelGrowth { get { return mLevelGrowth; } set { mLevelGrowth = value; } }
            public int TeamGrowth { get { return mTeamGrowth; } set { mTeamGrowth = value; } }

            // please check that IntGrowParameter does the same logic below
            public int Evaluate(float progress, int generation, Shared.Battle.UnitClassValue unitType)
            {
                // PVP logic assumed for now
                int result = BaseValue;

                if (unitType.Matches(Shared.Battle.UnitClassType.Hero))
                {
                    result += (int)Math.Ceiling(TeamGrowth * progress);
                }
                else if (unitType.Matches(Shared.Battle.UnitClassType.Minion) || unitType.Matches(Shared.Battle.UnitClassType.Neutral))
                {
                    result += generation * LevelGrowth;
                }

                return result;
            }

            #region IDataStruct Members

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mBaseValue);
                dst.Add(ref mTeamGrowth);
                dst.Add(ref mLevelGrowth);
                return true;
            }

            #endregion
        }
        
        public class FloatGrowParameter : IGrowParameter<float>
        {
            protected float mBaseValue;
            protected float mLevelGrowth;
            protected float mTeamGrowth;

            public float BaseValue { get { return mBaseValue; } set { mBaseValue = value; } }
            public float LevelGrowth { get { return mLevelGrowth; } set { mLevelGrowth = value; } }
            public float TeamGrowth { get { return mTeamGrowth; } set { mTeamGrowth = value; } }
            
            // please check that FloatGrowParameter does the same logic below
            public float Evaluate(float progress, int generation, Shared.Battle.UnitClassValue unitType)
            {
                // PVP logic assumed for now
                float result = BaseValue;

                if (unitType.Matches(Shared.Battle.UnitClassType.Hero))
                {
                    result += TeamGrowth * progress;
                }
                else if (unitType.Matches(Shared.Battle.UnitClassType.Minion) || unitType.Matches(Shared.Battle.UnitClassType.Neutral))
                {
                    result += (float)generation * LevelGrowth;
                }

                return result;
            }

            #region IDataStruct Members

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mBaseValue);
                dst.Add(ref mTeamGrowth);
                dst.Add(ref mLevelGrowth);
                return true;
            }

            #endregion
        }
    }
}