using System;

namespace Shared.Battle
{
    public interface ISerialKillingExpCoefficients
    {
        int GetSerialCoeffsCount { get; }
        float GetCoefficient(int index);
    }

    [System.Serializable]
    public class SerialKillingExpCoefficients : ISerialKillingExpCoefficients
    {
        public float[] Coefficients;

        public SerialKillingExpCoefficients() { }

        public int GetSerialCoeffsCount { get { return Coefficients != null ? Coefficients.Length : 0; } }

        public float GetCoefficient(int index)
        {
            if (Coefficients != null && index < Coefficients.Length && index >= 0)
            {
                return Coefficients[index];
            }
            return 0;
        }

        internal void Serialize(StorageFolder to)
        {
            if (Coefficients == null || Coefficients.Length == 0)
            {
                return;
            }

            for (int i = 0; i < Coefficients.Length; i++)
            {
                to.AddItem(new StorageFloat(i.ToString(), Coefficients[i]));
            }
        }

        internal void Deserialize(StorageFolder from)
        {
            if(from.Count==0)
            {
                return;
            }

            Coefficients = new float[from.Count];

            int i = 0;
            foreach(var item in from.Items.Items)
            {
                float coeff = item.asFloat();
                Coefficients[i] = coeff;
                i++;
            }
        }
    }


    public interface IHeroProgress
    {
        int MaximumExperience { get; }
        int MaxLevel { get; }
        int GetLevel(int experience, out int startLevelExperience, out int nextLevelExperience, out float progress, out float equipProgress);
        int GetMaxLevel(out float progress, out float equipProgress);
    }

    [System.Serializable]
    public class HeroProgress : IHeroProgress
    {
        [System.Serializable]
        public class LevelTransition
        {
            private const string FN_LEVEL = "level";
            private const string FN_EXPERIENCE = "experience";
            private const string FN_PROGRESS = "progress";
            private const string FN_EQUIP_PROGRESS = "equipProgress";

            public int Level;
            public int Experience;
            public float Progress;
            public float EquipProgress;

            public void Serialize(StorageFolder to)
            {
                to.AddItem(new StorageInt(FN_LEVEL, Level));
                to.AddItem(new StorageInt(FN_EXPERIENCE, Experience));
                to.AddItem(new StorageFloat(FN_PROGRESS, Progress));
                to.AddItem(new StorageFloat(FN_EQUIP_PROGRESS, EquipProgress));
            }

            public void Deserialize(StorageFolder from)
            {
                Level = from.GetItemAsInt(FN_LEVEL);
                Experience = from.GetItemAsInt(FN_EXPERIENCE);
                Progress = from.GetItemAsFloat(FN_PROGRESS);
                EquipProgress = from.GetItemAsFloat(FN_EQUIP_PROGRESS, (float)Level);
            }

            public bool IsGraterThan(LevelTransition previous)
            {
                if (previous == null)
                {
                    return true;
                }

                return Level > previous.Level && Experience > previous.Experience && Progress >= previous.Progress;
            }

            public override string ToString()
            {
                return string.Format("[Level = {0}, Experience = {1}, Progress = {2}, EquipProgress = {3}]", Level, Experience, Progress, EquipProgress);
            }
        }

        private const string FN_MAXIMUM_EXPERIENCE = "MaximumExperience";
        private const string FN_TRANSITIONS = "Transitions";

        // Максимальное значение опыта
        public int MaximumExperience = 100;
        int IHeroProgress.MaximumExperience
        {
            get { return MaximumExperience; }
        }

        // Максимальный опыт команды
        public int MaxLevel = 1;
        int IHeroProgress.MaxLevel
        {
            get { return MaxLevel; }
        }

        public LevelTransition[] LevelTransitions;

        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageInt(FN_MAXIMUM_EXPERIENCE, MaximumExperience));

            if (LevelTransitions == null || LevelTransitions.Length == 0)
            {
                return;
            }

            StorageFolder transitions = new StorageFolder(FN_TRANSITIONS);
            to.AddItem(transitions);

            int count = LevelTransitions.Length;
            for (int i = 0; i < count; i++)
            {
                LevelTransition transition = LevelTransitions[i];
                if (transition != null)
                {
                    StorageFolder item = new StorageFolder("Level " + transition.Level);
                    transitions.AddItem(item);
                    transition.Serialize(item);
                }
            }
        }

        public void Deserialize(StorageFolder from)
        {
            MaximumExperience = from.GetItemAsInt(FN_MAXIMUM_EXPERIENCE, MaximumExperience);

            StorageFolder transitions = from.GetFolder(FN_TRANSITIONS);
            if (transitions == null)
            {
                return;
            }

            LevelTransitions = new LevelTransition[transitions.Count];

            LevelTransition previous = null;
            int i = 0;
            foreach (var item in transitions.Items)
            {
                if(item is StorageFolder)
                {
                    LevelTransition transition = new LevelTransition();
                    transition.Deserialize(item as StorageFolder);

                    if (previous != null && !transition.IsGraterThan(previous))
                    {
                        Log.e("Wrong team level transition {0}, previous - {1}", transition, previous);
                        continue;
                    }

                    LevelTransitions[i] = transition;
                    previous = transition;

                    MaxLevel = Math.Max(transition.Level, MaxLevel);
                    i++;
                }
            }
        }

        public int GetMaxLevel(out float progress, out float equipProgress)
        {
            progress = 0;
            equipProgress = 0;

            int level = 0;
            if (LevelTransitions != null)
            {
                int count = LevelTransitions.Length;
                for (int i = 0; i < count; i++)
                {
                    LevelTransition transition = LevelTransitions[i];
                    if (transition == null)
                    {
                        break;
                    }

                    if (level < transition.Level)
                    {
                        level = transition.Level;
                        progress = transition.Progress;
                        equipProgress = transition.EquipProgress;
                    }
                }
            }
            return level;
        }

        public int GetLevel(int experience, out int startLevelExperience, out int nextLevelExperience, out float progress, out float equipProgress)
        {
            progress = 0;
            nextLevelExperience = 0;
            startLevelExperience = 0;
            equipProgress = 0;
            int level = 0;

            if (LevelTransitions != null)
            {
                int count = LevelTransitions.Length;

                LevelTransition activeTransition = null;
                for (int i = 0; i < count; i++)
                {
                    LevelTransition transition = LevelTransitions[i];
                    if (transition == null)
                    {
                        break;
                    }

                    nextLevelExperience = transition.Experience;
                    if (experience >= nextLevelExperience)
                    {
                        startLevelExperience = nextLevelExperience;
                        activeTransition = transition;
                    }
                    else
                    {
                        break;
                    }
                }

                if (activeTransition != null)
                {
                    level = activeTransition.Level;
                    progress = activeTransition.Progress;
                    equipProgress = activeTransition.EquipProgress;
                }
            }

            return level;
        }
    }

    public interface IPeriodicValueParams
    {
        DeltaTime StartDelay { get; }
        DeltaTime Period { get; }
        int StartValue { get; }
        int PeriodicValue { get; }
    }

    public interface IExperienceConstants
    {
        IPeriodicValueParams AutoCharge { get; }

        float CreepAssistRadius { get; }
        float NeutralAssistRadius { get; }

        float HeroKillBallanceCoefficient { get; }
        float HeroKillerShare { get; }
        float HeroAssistantShare { get; }

        int[] HeroKillCost { get; }
    }

    public interface IGoldBaseModifier
    {
        float Killer { get; }
    }

    public interface ISimpleGoldModifier : IGoldBaseModifier
    {
        float Passive { get; }
        float PassiveRange { get; }
    }

    public interface IActiveGoldModifier : IGoldBaseModifier
    {
        float Assister { get; }
    }

    public interface IHeroGoldModifier : IActiveGoldModifier
    {
        float[] KillStreakBreakMod { get; }
        float[] DeathStreakMod { get; }
    }

    public interface IGoldConstants
    {
        IPeriodicValueParams AutoCharge { get; }

        IHeroGoldModifier Hero { get; }
        IActiveGoldModifier Neutral { get; }
        ISimpleGoldModifier Creep { get; }
        ISimpleGoldModifier Tower { get; }
        ISimpleGoldModifier Boss { get; }

        int[] HeroKillCost { get; }
    }

    public interface ISurrenderConstants
    {
        bool Enabled { get; }

        DeltaTime StartDelay { get; }
        DeltaTime VotingPeriod { get; }
        DeltaTime Cooldown { get; }

        DeltaTime BotVotingDelay { get; }

        int AcceptVotesCount { get; }
    }

    public interface IMissionConstants
    {
        IExperienceConstants Experience { get; }
        IGoldConstants Gold { get; }

        DeltaTime MultiKillDelay { get; }
        DeltaTime AssistanceTimeToKill { get; }
        DeltaTime IndirectAssistanceTimeToKill { get; }
        float StandartSpeed { get; }

        MissionConstants.WeightTable HealthPriorityWeightTable { get; }
        MissionConstants.WeightTable DistancePriorityWeightTable { get; }

        DeltaTime AiActivationStartBatlleTime { get; }
        DeltaTime BotsAiActivationStartBatlleTime { get; }
        DeltaTime AiActivationDisconnectTime { get; }
        DeltaTime ShortDPSCounter { get; }
        DeltaTime LongDPSCounter { get; }
        DeltaTime PlayersCheckPeriod { get; }
        DeltaTime PlayersInactivityPeriod { get; }

        IHeroProgress HeroProgress { get; }
        ISerialKillingExpCoefficients SerialKilling { get; }

        int BattleItemsSlotsCount { get; }

        ISurrenderConstants Surrender { get; }
    }

    [Serializable]
    public class PeriodicValueParams : IPeriodicValueParams
    {
        private const string FN_START_DELAY = "StartDelay";
        private const string FN_PERIOD = "Period";
        private const string FN_START_VALUE = "StartValue";
        private const string FN_PERIODIC_VALUE = "PeriodicValue";

        // Начальная задержка
        public float StartDelay = 0.0f;
        // Таймаут регулярного события
        public float Period = 0.0f;
        // Стартовое значение начисляемой сущности
        public int StartValue = 0;
        // Периодически величина начисляемой сущности
        public int PeriodicValue = 0;

        DeltaTime IPeriodicValueParams.StartDelay
        {
            get { return DeltaTime.FromSeconds(StartDelay); }
        }

        DeltaTime IPeriodicValueParams.Period
        {
            get { return DeltaTime.FromSeconds(Period); }
        }

        int IPeriodicValueParams.StartValue
        {
            get { return StartValue; }
        }

        int IPeriodicValueParams.PeriodicValue
        {
            get { return PeriodicValue; }
        }

        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageFloat(FN_START_DELAY, StartDelay));
            to.AddItem(new StorageFloat(FN_PERIOD, Period));
            to.AddItem(new StorageInt(FN_START_VALUE, StartValue));
            to.AddItem(new StorageInt(FN_PERIODIC_VALUE, PeriodicValue));
        }

        public void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            StartDelay = from.GetItemAsFloat(FN_START_DELAY, StartDelay);
            Period = from.GetItemAsFloat(FN_PERIOD, Period);
            StartValue = from.GetItemAsInt(FN_START_VALUE, StartValue);
            PeriodicValue = from.GetItemAsInt(FN_PERIODIC_VALUE, PeriodicValue);
        }

        public void Init(float period, int value)
        {
            Period = period;
            PeriodicValue = value;
        }
    }

    [Serializable]
    public class ExperienceConstants : IExperienceConstants
    {
        private const string FN_AUTO_CHARGE = "AutoCharge";
        private const string FN_CREEP_ASSIST_RADIUS = "CreepAssistRadius";
        private const string FN_NEUTRAL_ASSIST_RADIUS = "NeutralAssistRadius";
        private const string FN_HERO_KILL_BALANCE_COEFFICIENT = "HeroKillBallanceCoefficient";
        private const string FN_HERO_KILLER_SHARE = "HeroKillerShare";
        private const string FN_HERO_ASSISTANT_SHARE = "HeroAssistantShare";
        private const string FN_HERO_KILL_COST = "HeroKillCost";

        // Параметры переодического начисления опыта
        public PeriodicValueParams AutoCharge = new PeriodicValueParams();
        // Радиус получения опыта за ассист (крипы)
        public float CreepAssistRadius = 8.0f;
        // Радиус получения опыта за ассист (нейтралы)
        public float NeutralAssistRadius = 8.0f;
        // Балансный коеффициент для расчета получаемого опыта за убийство вражесеого героя
        public float HeroKillBallanceCoefficient = 1.0f;
        // Модификатор опыта убийце за убийство героя
        public float HeroKillerShare = 1.0f;
        // Модификатор опыта асситсенту за убийство героя
        public float HeroAssistantShare = 1.0f;
        // Стоимость убийства героя (от его уровня). Переопределяет значение из Description
        public int[] HeroKillCost;

        IPeriodicValueParams IExperienceConstants.AutoCharge
        {
            get { return AutoCharge; }
        }

        float IExperienceConstants.CreepAssistRadius
        {
            get { return CreepAssistRadius; }
        }

        float IExperienceConstants.NeutralAssistRadius
        {
            get { return NeutralAssistRadius; }
        }

        float IExperienceConstants.HeroKillBallanceCoefficient
        {
            get { return HeroKillBallanceCoefficient; }
        }

        float IExperienceConstants.HeroKillerShare
        {
            get { return HeroKillerShare; }
        }

        float IExperienceConstants.HeroAssistantShare
        {
            get { return HeroAssistantShare; }
        }

        int[] IExperienceConstants.HeroKillCost
        {
            get { return HeroKillCost; }
        }

        public void Serialize(StorageFolder to)
        {
            var autoChargeFld = new StorageFolder(FN_AUTO_CHARGE);
            AutoCharge.Serialize(autoChargeFld);
            to.AddItem(autoChargeFld);

            to.AddItem(new StorageFloat(FN_CREEP_ASSIST_RADIUS, CreepAssistRadius));
            to.AddItem(new StorageFloat(FN_NEUTRAL_ASSIST_RADIUS, NeutralAssistRadius));
            to.AddItem(new StorageFloat(FN_HERO_KILL_BALANCE_COEFFICIENT, HeroKillBallanceCoefficient));
            to.AddItem(new StorageFloat(FN_HERO_KILLER_SHARE, HeroKillerShare));
            to.AddItem(new StorageFloat(FN_HERO_ASSISTANT_SHARE, HeroAssistantShare));

            if (HeroKillCost != null && HeroKillCost.Length > 0)
            {
                var heroKillCostFld = new StorageFolder(FN_HERO_KILL_COST);
                to.AddItem(heroKillCostFld);
                foreach (int killCost in HeroKillCost)
                {
                    heroKillCostFld.AddItem(new StorageInt(killCost));
                }
            }
        }

        public void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            var autoChargeFld = from.GetFolder(FN_AUTO_CHARGE);
            AutoCharge.Deserialize(autoChargeFld);

            CreepAssistRadius = from.GetItemAsFloat(FN_CREEP_ASSIST_RADIUS, CreepAssistRadius);
            NeutralAssistRadius = from.GetItemAsFloat(FN_NEUTRAL_ASSIST_RADIUS, NeutralAssistRadius);
            HeroKillBallanceCoefficient = from.GetItemAsFloat(FN_HERO_KILL_BALANCE_COEFFICIENT, HeroKillBallanceCoefficient);
            HeroKillerShare = from.GetItemAsFloat(FN_HERO_KILLER_SHARE, HeroKillerShare);
            HeroAssistantShare = from.GetItemAsFloat(FN_HERO_ASSISTANT_SHARE, HeroAssistantShare);

            var heroKillCostFld = from.GetFolder(FN_HERO_KILL_COST);
            if (heroKillCostFld != null && heroKillCostFld.Count > 0)
            {
                HeroKillCost = new int[heroKillCostFld.Count];

                for (int i = 0; i < HeroKillCost.Length; i++)
                {
                    StorageItem itm = heroKillCostFld.GetItem(i);
                    HeroKillCost[i] = itm.asInt();
                }
            }
        }

        public void Init(float period, int value, float creepAssistRadius, float neutralAssistRadius, float heroKillBallanceCoefficient, float heroKillerShare, float heroAssistantShare)
        {
            AutoCharge.Init(period, value);

            CreepAssistRadius = creepAssistRadius;
            NeutralAssistRadius = neutralAssistRadius;
            HeroKillBallanceCoefficient = heroKillBallanceCoefficient;
            HeroKillerShare = heroKillerShare;
            HeroAssistantShare = heroAssistantShare;
        }
    }

    [Serializable]
    public abstract class GoldBaseModifier : IGoldBaseModifier
    {
        private const string FN_PRIMARY = "Primary";

        public float KillerMod = 1.0f;
        float IGoldBaseModifier.Killer
        {
            get { return KillerMod; }
        }
                
        public virtual void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageFloat(FN_PRIMARY, KillerMod));
        }

        public virtual void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            KillerMod = from.GetItemAsFloat(FN_PRIMARY, KillerMod);
        }
    }

    [Serializable]
    public class SimpleGoldModifier : GoldBaseModifier, ISimpleGoldModifier
    {
        private const string FN_SECONDARY = "Secondary";
        private const string FN_SECONDARY_RANGE = "SecondaryRange";

        public float PassiveMod = 1.0f;
        public float PassiveRange = 10.0f;

        float ISimpleGoldModifier.Passive
        {
            get { return PassiveMod; }
        }

        float ISimpleGoldModifier.PassiveRange
        {
            get { return PassiveRange; }
        }
                
        public override void Serialize(StorageFolder to)
        {
            base.Serialize(to);

            to.AddItem(new StorageFloat(FN_SECONDARY, PassiveMod));
            to.AddItem(new StorageFloat(FN_SECONDARY_RANGE, PassiveRange));
        }

        public override void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            base.Deserialize(from);
            PassiveMod = from.GetItemAsFloat(FN_SECONDARY, PassiveMod);
            PassiveRange = from.GetItemAsFloat(FN_SECONDARY_RANGE, PassiveRange);
        }
    }

    [Serializable]
    public class ActiveGoldModifier : GoldBaseModifier, IActiveGoldModifier
    {
        private const string FN_SECONDARY = "Secondary";

        public float AssisterMod = 1.0f;

        float IActiveGoldModifier.Assister
        {
            get { return AssisterMod; }
        }
                
        public override void Serialize(StorageFolder to)
        {
            base.Serialize(to);

            to.AddItem(new StorageFloat(FN_SECONDARY, AssisterMod));
        }

        public override void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            base.Deserialize(from);
            AssisterMod = from.GetItemAsFloat(FN_SECONDARY, AssisterMod);
        }
    }

    [Serializable]
    public class HeroGoldModifier : ActiveGoldModifier, IHeroGoldModifier
    {
        private const string FN_KILL_STREAK_BREAK = "KillStreakBreak";
        private const string FN_DEATH_STREAK = "DeathStreak";

        public float[] KillStreakBreakMod;
        public float[] DeathStreakMod;

        float[] IHeroGoldModifier.KillStreakBreakMod
        {
            get { return KillStreakBreakMod; }
        }

        float[] IHeroGoldModifier.DeathStreakMod
        {
            get { return DeathStreakMod; }
        }

        public override void Serialize(StorageFolder to)
        {
            base.Serialize(to);

            SerializeArray(KillStreakBreakMod, to, FN_KILL_STREAK_BREAK);
            SerializeArray(DeathStreakMod, to, FN_DEATH_STREAK);
        }

        public override void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            base.Deserialize(from);

            DeserializeArray(ref KillStreakBreakMod, from, FN_KILL_STREAK_BREAK);
            DeserializeArray(ref DeathStreakMod, from, FN_DEATH_STREAK);
        }

        private void DeserializeArray(ref float[] array, StorageFolder from, string fldName)
        {
            var arrayFld = from.GetFolder(fldName);
            if (arrayFld != null && arrayFld.Count > 0)
            {
                array = new float[arrayFld.Count];
                for (int i = 0; i < array.Length; i++)
                {
                    StorageItem itm = arrayFld.GetItem(i);
                    array[i] = itm.asFloat();
                }
            }
        }

        private void SerializeArray(float[] array, StorageFolder to, string fldName)
        {
            if (array != null && array.Length > 0)
            {
                var arrayFld = new StorageFolder(fldName);
                to.AddItem(arrayFld);
                foreach (float val in array)
                {
                    arrayFld.AddItem(new StorageFloat(val));
                }
            }
        }
    }

    [Serializable]
    public class GoldConstants : IGoldConstants
    {
        private const string FN_AUTO_CHARGE = "AutoCharge";
        private const string FN_HERO = "Hero";
        private const string FN_NEUTRAL = "Neutral";
        private const string FN_CREEP = "Creep";
        private const string FN_TOWER = "Tower";
        private const string FN_BOSS = "Boss";
        private const string FN_HERO_KILL_COST = "HeroKillCost";

        // Параметры переодического начисления золота
        public PeriodicValueParams AutoCharge = new PeriodicValueParams();
        // Модификаторы золота за убийство героя
        public HeroGoldModifier Hero = new HeroGoldModifier();
        // Модификаторы золота за убийство монстров
        public ActiveGoldModifier Neutral = new ActiveGoldModifier();
        // Модификаторы золота за убийство крипов
        public SimpleGoldModifier Creep = new SimpleGoldModifier();
        // Модификаторы золота за убийство башен
        public SimpleGoldModifier Tower = new SimpleGoldModifier();
        // Модификаторы золота за убийство боссов
        public SimpleGoldModifier Boss = new SimpleGoldModifier();

        // Стоимость убийства героя (от его уровня). Переопределяет значение из Description
        public int[] HeroKillCost;

        IPeriodicValueParams IGoldConstants.AutoCharge
        {
            get { return AutoCharge; }
        }

        IHeroGoldModifier IGoldConstants.Hero
        {
            get { return Hero; }
        }

        IActiveGoldModifier IGoldConstants.Neutral
        {
            get { return Neutral; }
        }

        ISimpleGoldModifier IGoldConstants.Creep
        {
            get { return Creep; }
        }

        ISimpleGoldModifier IGoldConstants.Tower
        {
            get { return Tower; }
        }

        ISimpleGoldModifier IGoldConstants.Boss
        {
            get { return Boss; }
        }

        int[] IGoldConstants.HeroKillCost
        {
            get { return HeroKillCost; }
        }

        public void Serialize(StorageFolder to)
        {
            var autoChargeFld = new StorageFolder(FN_AUTO_CHARGE);
            AutoCharge.Serialize(autoChargeFld);
            to.AddItem(autoChargeFld);

            StorageFolder fld = null;

            fld = new StorageFolder(FN_HERO);
            to.AddItem(fld);
            Hero.Serialize(fld);

            fld = new StorageFolder(FN_NEUTRAL);
            to.AddItem(fld);
            Neutral.Serialize(fld);

            fld = new StorageFolder(FN_CREEP);
            to.AddItem(fld);
            Creep.Serialize(fld);

            fld = new StorageFolder(FN_TOWER);
            to.AddItem(fld);
            Tower.Serialize(fld);

            fld = new StorageFolder(FN_BOSS);
            to.AddItem(fld);
            Boss.Serialize(fld);

            if (HeroKillCost != null && HeroKillCost.Length > 0)
            {
                var heroKillCostFld = new StorageFolder(FN_HERO_KILL_COST);
                to.AddItem(heroKillCostFld);
                foreach (int killCost in HeroKillCost)
                {
                    heroKillCostFld.AddItem(new StorageInt(killCost));
                }
            }
        }

        public void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            var autoChargeFld = from.GetFolder(FN_AUTO_CHARGE);
            AutoCharge.Deserialize(autoChargeFld);

            StorageFolder fld = null;

            fld = from.GetFolder(FN_HERO);
            Hero.Deserialize(fld);
            fld = from.GetFolder(FN_NEUTRAL);
            Neutral.Deserialize(fld);
            fld = from.GetFolder(FN_CREEP);
            Creep.Deserialize(fld);
            fld = from.GetFolder(FN_TOWER);
            Tower.Deserialize(fld);
            fld = from.GetFolder(FN_BOSS);
            Boss.Deserialize(fld);

            var heroKillCostFld = from.GetFolder(FN_HERO_KILL_COST);
            if (heroKillCostFld != null && heroKillCostFld.Count > 0)
            {
                HeroKillCost = new int[heroKillCostFld.Count];

                for (int i = 0; i < HeroKillCost.Length; i++)
                {
                    StorageItem itm = heroKillCostFld.GetItem(i);
                    HeroKillCost[i] = itm.asInt();
                }
            }
        }
    }

    [Serializable]
    public class SurrenderConstants : ISurrenderConstants
    {
        private const string FN_ENABLED = "Enabled";
        private const string FN_START_DELAY = "StartDelay";
        private const string FN_VOTING_PERIOD = "VotingPeriod";
        private const string FN_COOLDOWN = "Cooldown";
        private const string FN_BOT_VOTING_DELAY = "BotVotingDelay";
        private const string FN_ACCEPT_VOTES_COUNT = "AcceptVotesCount";

        // Активация функционала сдачи
        public bool Enabled = false;
        // Время с начала боя, в течении которого нельзя сдаться
        public float StartDelay;
        // Временной промежуток, в течении которого идет голосование
        public float VotingPeriod;
        // Время блокировки до следующего голосования
        public float Cooldown;
        // Время с начала голосования, в которое начинают голосовать боты
        public float BotVotingDelay;
        // Количество голосов, необходимых для сдачи
        public int AcceptVotesCount;

        bool ISurrenderConstants.Enabled
        {
            get { return Enabled; }
        }

        DeltaTime ISurrenderConstants.StartDelay
        {
            get { return DeltaTime.FromSeconds(StartDelay); }
        }

        DeltaTime ISurrenderConstants.VotingPeriod
        {
            get { return DeltaTime.FromSeconds(VotingPeriod); }
        }

        DeltaTime ISurrenderConstants.Cooldown
        {
            get { return DeltaTime.FromSeconds(Cooldown); }
        }

        DeltaTime ISurrenderConstants.BotVotingDelay
        {
            get { return DeltaTime.FromSeconds(BotVotingDelay); }
        }

        int ISurrenderConstants.AcceptVotesCount
        {
            get { return AcceptVotesCount; }
        }

        public void Serialize(StorageFolder to)
        {
            to.AddItem(new StorageBool(FN_ENABLED, Enabled));
            to.AddItem(new StorageFloat(FN_START_DELAY, StartDelay));
            to.AddItem(new StorageFloat(FN_VOTING_PERIOD, VotingPeriod));
            to.AddItem(new StorageFloat(FN_COOLDOWN, Cooldown));
            to.AddItem(new StorageFloat(FN_BOT_VOTING_DELAY, BotVotingDelay));
            to.AddItem(new StorageFloat(FN_ACCEPT_VOTES_COUNT, AcceptVotesCount));
        }

        public void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                return;
            }

            Enabled = from.GetItemAsBool(FN_ENABLED, Enabled);
            StartDelay = from.GetItemAsFloat(FN_START_DELAY, StartDelay);
            VotingPeriod = from.GetItemAsFloat(FN_VOTING_PERIOD, VotingPeriod);
            Cooldown = from.GetItemAsFloat(FN_COOLDOWN, Cooldown);
            BotVotingDelay = from.GetItemAsFloat(FN_BOT_VOTING_DELAY, BotVotingDelay);
            AcceptVotesCount = from.GetItemAsInt(FN_ACCEPT_VOTES_COUNT, AcceptVotesCount);
        }

        public bool IsEmpty
        {
            get
            {
                return !Enabled &&
                       StartDelay == 0 &&
                       VotingPeriod == 0 &&
                       Cooldown == 0 &&
                       BotVotingDelay == 0 &&
                       AcceptVotesCount == 0;
            }
        }
    }

    [Serializable]
    public class MissionConstants : IMissionConstants
    {
        #region Const Fields

        // [TODO]: Выпилить
        private const string FN_TIMED_EXPERIENCE_TIMEOUT = "TimedExperienceTimeout";
        private const string FN_TIMED_EXPERIENCE_VALUE = "TimedExperienceValue";
        private const string FN_HERO_KILL_BALLANCE_COEFFICIENT = "HeroKillBallanceCoefficient";
        private const string FN_ASSISTANCE_RADIUS_TO_GIVE_EXP_CREEP = "AssistanceRadiusToGiveExpCreep";
        private const string FN_ASSISTANCE_RADIUS_TO_GIVE_EXP_NEUTRAL = "AssistanceRadiusToGiveExpNeutral";
        private const string FN_HERO_KILLER_EXP_SHARE = "HeroKillerExpShare";
        private const string FN_HERO_ASSISTANT_EXP_SHARE = "HeroAssistantExpShare";

        private const string FN_EXPERIENCE_CONSTANTS = "ExperienceConstants";
        private const string FN_GOLD_CONSTANTS = "GoldConstants";
        private const string FN_HERO_PROGRESS = "HeroProgress";
        private const string FN_SERIAL_KILLING = "SerialKillingExpCoefficients";
        private const string FN_MULTI_KILL_DELAY = "MultiKillDelay";
        private const string FN_ASSISTANCE_TIME_TO_KILL = "AssistanceTimeToKill";
        private const string FN_INDIRECT_ASSISTANCE_TIME_TO_KILL = "IndirectAssistanceTimeToKill";
        private const string FN_STANDART_SPEED = "StandartSpeed";
        private const string FN_HEALTH_PRIORITY_WEIGHTS = "HealthPriorityWeights";
        private const string FN_DISTANCE_PRIORITY_WEIGHTS = "DistancePriorityWeights";
        private const string FN_AI_START_BATTLE_ACTIVATION_DELAY = "AiActivationStartBatlleTime";
        private const string FN_BOTS_AI_START_BATTLE_ACTIVATION_DELAY = "BotsAiActivationStartBatlleTime";
        private const string FN_AI_DISCONNECT_ACTIVATION_DELAY = "AiActivationDisconnectTime";

        private const string FN_BATTLE_ITEMS_SLOTS_COUNT = "BattleItemsSlotsCount";

        private const string FN_SURRENDER = "Surrender";

        #endregion

        [Serializable]
        public struct WeightTable
        {
            [Serializable]
            public struct Item
            {
                public float Value;
                public float Weight;

                public void Serialize(StorageFolder to)
                {
                    var item = new StorageFolder("item");
                    item.AddItem(new StorageFloat("Value", Value));
                    item.AddItem(new StorageFloat("Weight", Weight));

                    to.AddItem(item);
                }

                public void Deserialize(StorageFolder from)
                {
                    Value = from.GetItemAsFloat("Value");
                    Weight = from.GetItemAsFloat("Weight");
                }
            }

            public Item[] Items;

            public WeightTable(Item[] items)
            {
                Items = items;
            }

            public float Interpolate(float v)
            {
                // assume values are sorted in increasing orders
                int c = Items != null ? Items.Length : 0;
                if (c > 0)
                {
                    for (int i = 0; i < c; ++i)
                    {
                        if (v < Items[i].Value)
                        {
                            if (i == 0)
                            {
                                return Items[0].Weight; // v < any of values
                            }
                            else
                            {
                                float lerpCoeff = (v - Items[i - 1].Value) / (Items[i].Value - Items[i - 1].Value);
                                return Items[i - 1].Weight * (1 - lerpCoeff) + lerpCoeff * Items[i].Weight;
                            }
                        }
                    }
                    return Items[c - 1].Weight; // v > any of values
                }
                else
                {
                    return 1; // just default value for safety reasons
                }
            }

            public void Serialize(StorageFolder to)
            {
                int count = Items != null ? Items.Length : 0;
                for (int i = 0; i < count; ++i)
                {
                    Items[i].Serialize(to);
                }
            }

            public static WeightTable Deserialize(StorageFolder folder)
            {
                if (folder.Items.Count == 0)
                {
                    Log.e("WeightTable is empty.");
                }
                int count = folder.Items.Count;

                Item[] items = new Item[count];
                for (int i = 0; i < count; ++i)
                {
                    var it = folder.GetItem(i) as StorageFolder;
                    Item item = new Item();
                    item.Deserialize(it);
                    items[i] = item;
                }

                Array.Sort(items, (it1, it2) => it1.Value.CompareTo(it2.Value));

                return new WeightTable(items);
            }
        }

#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        // Параметры начисления опыта
        public ExperienceConstants Experience = new ExperienceConstants();


#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        // Параметры начисления золота
        public GoldConstants Gold = new GoldConstants();

        // [TODO]: Выпилить
        // Таймаут регулярного события, в конце которого начисляется опыт всем командам
        private float TimedExperienceTimeout = 5.0f;
        // Величина регулярно начисляемого опыта
        private int TimedExperienceValue = 1;
        // Балансный коеффициент для расчета получаемого опыта за убийство вражесеого героя
        private float HeroKillBallanceCoefficient = 1.0f;
        private float AssistanceRadiusToGiveExpCreep = 8.0f;
        private float AssistanceRadiusToGiveExpNeutral = 8.0f;
        private float HeroKillerExpShare = 1.0f;
        private float HeroAssistantExpShare = 1.0f;


#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        // Временной промежуток от предыдущего убийства, в рамках которого продолжается учет серии убийств
        public float MultiKillDelay = 10.0f;
        // Временной промежуток для расчета ассистов в убийстве
        public float AssistanceTimeToKill = 5.0f;
        // Временной промежуток для расчета непрямых ассистов в убийстве
        public float IndirectAssistanceTimeToKill = 5.0f;

        // Базовая скорость, по которой считаются относительные скорости юнитов
        public float StandartSpeed = 1.0f;

#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        // Таблица для вычисления веса по здоровью
        public WeightTable HealthPriorityWeightTable;

#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        // Таблица для вычисления веса по расстоянию
        public WeightTable DistancePriorityWeightTable;

#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        // Время от начала боя, в течении которого Ai не будет перехватывать управление
        public float AiActivationStartBatlleTime = 30.0f;
        // Время от начала боя, в течении которого Ai не будет перехватывать управление
        public float BotsAiActivationStartBatlleTime = 2.0f;
        // Время неактивности игрока, после которого управление перехватывает AI
        public float AiActivationDisconnectTime = 15.0f;
        // Короткий счетчик DPS, время для вычисления
        public float ShortDPSCounter = 3.0f;
        // Длинный счетчик DPS, время для вычисления
        public float LongDPSCounter = 10.0f;
        // MYM-2822 
        public float PlayersCheckPeriod = 10.0f;
        public float PlayersInactivityPeriod = 60.0f;

#if UNITY_EDITOR
        [UnityEngine.Space]
#endif
        public SerialKillingExpCoefficients SerialKilling = new SerialKillingExpCoefficients();
        public HeroProgress HeroProgress = new HeroProgress();

        public int BattleItemsSlotsCount = 0;

        public SurrenderConstants Surrender = new SurrenderConstants();

        IExperienceConstants IMissionConstants.Experience
        {
            get { return Experience; }
        }

        IGoldConstants IMissionConstants.Gold
        {
            get { return Gold; }
        }

        DeltaTime IMissionConstants.MultiKillDelay
        {
            get { return DeltaTime.FromSeconds(MultiKillDelay); }
        }

        DeltaTime IMissionConstants.AssistanceTimeToKill
        {
            get { return DeltaTime.FromSeconds(AssistanceTimeToKill); }
        }

        DeltaTime IMissionConstants.IndirectAssistanceTimeToKill
        {
            get { return DeltaTime.FromSeconds(IndirectAssistanceTimeToKill); }
        }

        float IMissionConstants.StandartSpeed
        {
            get { return StandartSpeed; }
        }

        WeightTable IMissionConstants.HealthPriorityWeightTable
        {
            get { return HealthPriorityWeightTable; }
        }

        WeightTable IMissionConstants.DistancePriorityWeightTable
        {
            get { return DistancePriorityWeightTable; }
        }

        DeltaTime IMissionConstants.AiActivationStartBatlleTime
        {
            get { return DeltaTime.FromSeconds(AiActivationStartBatlleTime); }
        }

        DeltaTime IMissionConstants.BotsAiActivationStartBatlleTime
        {
            get { return DeltaTime.FromSeconds(BotsAiActivationStartBatlleTime); }
        }
        DeltaTime IMissionConstants.AiActivationDisconnectTime
        {
            get { return DeltaTime.FromSeconds(AiActivationDisconnectTime); }
        }

        DeltaTime IMissionConstants.ShortDPSCounter
        {
            get { return DeltaTime.FromSeconds(ShortDPSCounter); }
        }

        DeltaTime IMissionConstants.LongDPSCounter
        {
            get { return DeltaTime.FromSeconds(LongDPSCounter); }
        }

        DeltaTime IMissionConstants.PlayersCheckPeriod
        {
            get { return DeltaTime.FromSeconds(PlayersCheckPeriod); }
        }

        DeltaTime IMissionConstants.PlayersInactivityPeriod
        {
            get { return DeltaTime.FromSeconds(PlayersInactivityPeriod); }
        }

        ISerialKillingExpCoefficients IMissionConstants.SerialKilling
        {
            get { return SerialKilling; }
        }

        IHeroProgress IMissionConstants.HeroProgress
        {
            get { return HeroProgress; }
        }

        int IMissionConstants.BattleItemsSlotsCount
        {
            get { return BattleItemsSlotsCount; }
        }

        ISurrenderConstants IMissionConstants.Surrender
        {
            get { return Surrender; }
        }

        public void Serialize(StorageFolder to)
        {
            var expFolder = new StorageFolder(FN_EXPERIENCE_CONSTANTS);
            Experience.Serialize(expFolder);
            to.AddItem(expFolder);

            var goldFolder = new StorageFolder(FN_GOLD_CONSTANTS);
            Gold.Serialize(goldFolder);
            to.AddItem(goldFolder);

            to.AddItem(new StorageFloat(FN_MULTI_KILL_DELAY, MultiKillDelay));
            to.AddItem(new StorageFloat(FN_ASSISTANCE_TIME_TO_KILL, AssistanceTimeToKill));
            to.AddItem(new StorageFloat(FN_INDIRECT_ASSISTANCE_TIME_TO_KILL, IndirectAssistanceTimeToKill));
            to.AddItem(new StorageFloat(FN_STANDART_SPEED, StandartSpeed));

            var serialKilling = new StorageFolder(FN_SERIAL_KILLING);
            to.AddItem(serialKilling);
            SerialKilling.Serialize(serialKilling);

            var heroProgress = new StorageFolder(FN_HERO_PROGRESS);
            to.AddItem(heroProgress);
            HeroProgress.Serialize(heroProgress);

            var healthFolder = new StorageFolder(FN_HEALTH_PRIORITY_WEIGHTS);
            HealthPriorityWeightTable.Serialize(healthFolder);
            to.AddItem(healthFolder);

            var distanceFolder = new StorageFolder(FN_DISTANCE_PRIORITY_WEIGHTS);
            DistancePriorityWeightTable.Serialize(distanceFolder);
            to.AddItem(distanceFolder);

            to.AddItem(new StorageFloat(FN_AI_START_BATTLE_ACTIVATION_DELAY, AiActivationStartBatlleTime));
            to.AddItem(new StorageFloat(FN_AI_DISCONNECT_ACTIVATION_DELAY, AiActivationDisconnectTime));
            to.AddItem(new StorageFloat(FN_BOTS_AI_START_BATTLE_ACTIVATION_DELAY, BotsAiActivationStartBatlleTime));

            to.AddItem(new StorageInt(FN_BATTLE_ITEMS_SLOTS_COUNT, BattleItemsSlotsCount));

            if (!Surrender.IsEmpty)
            {
                var surrenderFld = new StorageFolder(FN_SURRENDER);
                Surrender.Serialize(surrenderFld);
                to.AddItem(surrenderFld);
            }
        }

        public void Deserialize(StorageFolder from)
        {
            if (from == null)
            {
                Log.e("GlobalConstants not inited");
                return;
            }

            TimedExperienceTimeout = from.GetItemAsFloat(FN_TIMED_EXPERIENCE_TIMEOUT, TimedExperienceTimeout);
            TimedExperienceValue = from.GetItemAsInt(FN_TIMED_EXPERIENCE_VALUE, TimedExperienceValue);
            HeroKillBallanceCoefficient = from.GetItemAsFloat(FN_HERO_KILL_BALLANCE_COEFFICIENT, HeroKillBallanceCoefficient);
            AssistanceRadiusToGiveExpCreep = from.GetItemAsFloat(FN_ASSISTANCE_RADIUS_TO_GIVE_EXP_CREEP, AssistanceRadiusToGiveExpCreep);
            AssistanceRadiusToGiveExpNeutral = from.GetItemAsFloat(FN_ASSISTANCE_RADIUS_TO_GIVE_EXP_NEUTRAL, AssistanceRadiusToGiveExpNeutral);
            HeroKillerExpShare = from.GetItemAsFloat(FN_HERO_KILLER_EXP_SHARE, HeroKillerExpShare);
            HeroAssistantExpShare = from.GetItemAsFloat(FN_HERO_ASSISTANT_EXP_SHARE, HeroAssistantExpShare);

            var expFolder = from.GetFolder(FN_EXPERIENCE_CONSTANTS);
            if (expFolder != null)
            {
                Experience.Deserialize(expFolder);

            }
            else
            {
                Experience.Init(
                    TimedExperienceTimeout,
                    TimedExperienceValue,
                    AssistanceRadiusToGiveExpCreep,
                    AssistanceRadiusToGiveExpNeutral,
                    HeroKillBallanceCoefficient,
                    HeroKillerExpShare,
                    HeroAssistantExpShare);
            }

            var goldFolder = from.GetFolder(FN_GOLD_CONSTANTS);
            if (goldFolder != null)
            {
                Gold.Deserialize(goldFolder);
            }

            MultiKillDelay = from.GetItemAsFloat(FN_MULTI_KILL_DELAY, MultiKillDelay);
            AssistanceTimeToKill = from.GetItemAsFloat(FN_ASSISTANCE_TIME_TO_KILL, AssistanceTimeToKill);
            IndirectAssistanceTimeToKill = from.GetItemAsFloat(FN_INDIRECT_ASSISTANCE_TIME_TO_KILL, IndirectAssistanceTimeToKill);
            StandartSpeed = from.GetItemAsFloat(FN_STANDART_SPEED, StandartSpeed);


            var healthFolder = from.GetFolder(FN_HEALTH_PRIORITY_WEIGHTS);
            if (healthFolder != null)
            {
                HealthPriorityWeightTable = WeightTable.Deserialize(healthFolder);
            }
            else
            {
                Log.e("Missing health weights table in Descriptions.xml");
            }

            var serialKilling = from.GetFolder(FN_SERIAL_KILLING);
            if (serialKilling != null)
            {
                SerialKilling.Deserialize(serialKilling);
            }

            var heroProgress = from.GetFolder(FN_HERO_PROGRESS);
            if (heroProgress != null)
            {
                HeroProgress.Deserialize(heroProgress);
            }

            var distanceFolder = from.GetFolder(FN_DISTANCE_PRIORITY_WEIGHTS);
            if (distanceFolder != null)
            {
                DistancePriorityWeightTable = WeightTable.Deserialize(distanceFolder);
            }
            else
            {
                Log.e("Missing distance weights table in Descriptions.xml");
            }

            AiActivationStartBatlleTime = from.GetItemAsFloat(FN_AI_START_BATTLE_ACTIVATION_DELAY, AiActivationStartBatlleTime);
            AiActivationDisconnectTime = from.GetItemAsFloat(FN_AI_DISCONNECT_ACTIVATION_DELAY, AiActivationDisconnectTime);
            BotsAiActivationStartBatlleTime = from.GetItemAsFloat(FN_BOTS_AI_START_BATTLE_ACTIVATION_DELAY, BotsAiActivationStartBatlleTime);

            BattleItemsSlotsCount = from.GetItemAsInt(FN_BATTLE_ITEMS_SLOTS_COUNT, BattleItemsSlotsCount);

            var surrenderFld = from.GetFolder(FN_SURRENDER);
            if (distanceFolder != null)
            {
                Surrender.Deserialize(surrenderFld);
            }
        }
    }
}