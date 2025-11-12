using System;
using System.Collections.Generic;
using System.Text;
using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData;
using Shared.CommonData.Plt;

namespace Shared.Battle
{
    public class PlayerItem : IDataStruct
    {
        public short ItemId;

        public PlayerItem() { }

        public PlayerItem(short itemId)
        {
            ItemId = itemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ItemId);

            return true;
        }
    }

    public class MetaRpgParam : IDataStruct
    {
        public RpgParameter RpgParameter;
        public float Value;

        public MetaRpgParam() { }

        public MetaRpgParam(short rpgParameter, float value)
        {
            RpgParameter = (RpgParameter)rpgParameter;
            Value = value;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            short tmpRpgParameter = (short)RpgParameter;
            dst.Add(ref tmpRpgParameter);
            RpgParameter = (RpgParameter)tmpRpgParameter;

            dst.Add(ref Value);

            return true;
        }

        public static MetaRpgParam[] Map(Dictionary<short, float> rpgParams)
        {
            List<MetaRpgParam> resultRpgParams = new List<MetaRpgParam>();
            foreach (var elem in rpgParams)
            {
                short battleRpgParam;
                if (RpgParam.BATTLE_PARAMS_MAP.TryGetValue(elem.Key, out battleRpgParam))
                {
                    float rpgParamValue = elem.Value;

                    if (elem.Key == RpgParam.MAX_HEALTH)
                    {
                        float multiplier;
                        if (rpgParams.TryGetValue(RpgParam.HEALTH_PERCENT, out multiplier))
                        {
                            rpgParamValue *= 1 + multiplier;
                        }
                    }
                    else if (elem.Key == RpgParam.ATTACK_DAMAGE)
                    {
                        float multiplier;
                        if (rpgParams.TryGetValue(RpgParam.ATTACK_DAMAGE_PERCENT, out multiplier))
                        {
                            rpgParamValue *= 1 + multiplier;
                        }
                    }

                    resultRpgParams.Add(new MetaRpgParam(battleRpgParam, rpgParamValue));
                }
            }

            return resultRpgParams.ToArray();
        }
    }

    public class PlayerItems : IDataStruct
    {
        public PlayerItem Hero;
        public PlayerItem WeaponFirst;
        public PlayerItem WeaponSecond;
        public PlayerItem Special;
        public MetaRpgParam[] RpgParams;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Hero);
            dst.Add(ref WeaponFirst);
            dst.Add(ref WeaponSecond);
            dst.Add(ref Special);
            dst.Add(ref RpgParams);

            return true;
        }

        public void GetInfo(StringBuilder strb)
        {
            strb.AppendFormat("Hero item desc id - '{0}'\n", Hero != null ? Hero.ItemId : -1);
            strb.AppendFormat("WeaponFirst item desc id - '{0}'\n", WeaponFirst != null ? WeaponFirst.ItemId : -1);
            strb.AppendFormat("WeaponSecond item desc id - '{0}'\n", WeaponSecond != null ? WeaponSecond.ItemId : -1);
            strb.AppendFormat("Special item desc id - '{0}'\n", Special != null ? Special.ItemId : -1);

            int cnt = RpgParams != null ? RpgParams.Length : 0;
            for (int i = 0; i < cnt; ++i)
            {
                MetaRpgParam rpgParam = RpgParams[i];
                strb.AppendFormat("RpgParam  - '{0}'(value:{1})\n", rpgParam.RpgParameter, rpgParam.Value);

            }
        }
    }

    public class PlayerHero : IDataStruct
    {
        public PlayerItems PlayerItems;

        private short mHeroLevel;
        private short mHeroId;
        private short mSkinId;
        private byte[] mRunes;
        private byte[] mEquipLevels;
        private short[] mBattleItemsPreset;
        private Spark mSpark;
        private float mHealthCoeff;
        private float mHealthCoeffAtBattleEnd;

        private AiConfiguration mAiConfig; //нужно сериализовать только для риплеев, надо бы разделить клиентский и трастовый PlayerDataSet

        //only for pool, tmp solution
        public short WeaponItemId
        {
            get
            {
                return (PlayerItems != null && PlayerItems.WeaponFirst != null) ? PlayerItems.WeaponFirst.ItemId : (short)0; // see NeoLobbyState.GetPlayerItems()
            }
        }

        //only for pool, tmp solution
        public short SpecialItemId
        {
            get
            {
                return (PlayerItems != null && PlayerItems.Special != null) ? PlayerItems.Special.ItemId : (short)0; // see NeoLobbyState.GetPlayerItems()
            }
        }

        public short HeroItemId
        {
            get
            {
                return (PlayerItems != null && PlayerItems.Hero != null) ? PlayerItems.Hero.ItemId : (short)0; // see NeoLobbyState.GetPlayerItems()
            }
        }

        public PlayerHero() { }

        public PlayerHero(short heroId, short skinId, short level, byte[] runes, byte[] equipLevels, short[] battleItemsPreset,
            Spark spark, AiConfiguration aiConfig, PlayerItems playerItems, float healthCoeff = -1)
        {
            DBG.Diagnostics.Assert(heroId > 0, "Invalid HeroId");
            PlayerItems = playerItems;

            mHeroLevel = level;
            mHeroId = heroId;
            mSkinId = skinId;
            mRunes = runes;
            mEquipLevels = equipLevels;
            mBattleItemsPreset = battleItemsPreset;
            mSpark = spark;
            mAiConfig = aiConfig;
            mHealthCoeff = Math.Max(-1, Math.Min(1, healthCoeff));
            mHealthCoeffAtBattleEnd = mHealthCoeff;
        }

        public PlayerHero Clone()
        {
            return new PlayerHero(
                mHeroId,
                mSkinId,
                mHeroLevel,
                mRunes!=null ? (byte[])mRunes.Clone() : null,
                mEquipLevels != null ? (byte[])mEquipLevels.Clone() : null,
                mBattleItemsPreset != null ? (short[])mBattleItemsPreset.Clone() : null,
                mSpark!=null ? mSpark.Clone() : null,
                mAiConfig,
                PlayerItems,
                mHealthCoeff);
        }

        public short HeroLevel { get { return mHeroLevel; } set { mHeroLevel = value; }  }
        public short HeroId { get { return mHeroId; } set { mHeroId = value; } }
        public short SkinId { get { return mSkinId; } set { mSkinId = value; } }
        public byte[] Runes { get { return mRunes; } set { mRunes = value; } }
        public byte[] EquipLevels { get { return mEquipLevels; } set { mEquipLevels = value; }}
        public short[] BattleItemsPreset { get { return mBattleItemsPreset; } set { mBattleItemsPreset = value; } }
        public AiConfiguration AiConfig { get { return mAiConfig; } }
        public Spark Spark { get { return mSpark; } set { mSpark = value; } }
        public float HealthCoeff { get { return mHealthCoeff; } set { mHealthCoeff = value; } }
        public float HealthCoeffAtBattleEnd { get { return mHealthCoeffAtBattleEnd; } set { mHealthCoeffAtBattleEnd = value; } }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref PlayerItems);

            dst.Add(ref mHeroLevel);
            dst.Add(ref mHeroId);
            dst.Add(ref mSkinId);
            dst.Add(ref mRunes);
            dst.Add(ref mEquipLevels);
            dst.Add(ref mBattleItemsPreset);
            dst.Add(ref mSpark);
            dst.Add(ref mHealthCoeff);

            if (dst.isReader)
            {
                mHealthCoeffAtBattleEnd = mHealthCoeff;
                bool isAiConfig = false;
                dst.Add(ref isAiConfig);
                if (isAiConfig)
                {
                    mAiConfig = new AiConfiguration();
                    mAiConfig.Serialize(dst);
                }
            }
            else
            {
                bool isAiConfig = mAiConfig != null;
                dst.Add(ref isAiConfig);
                if (mAiConfig != null)
                {
                    mAiConfig.Serialize(dst);
                }
            }

            return true;
        }

        public string GetInfo(CommonData.Descriptions descriptions)
        {
            var desc = descriptions == null ? null : descriptions.GetUnitDescription(mHeroId);

            StringBuilder strb = new StringBuilder();

            GetHeroInfo(desc, strb);
            string head = strb.ToString();
            strb.Length = 0;

            RunesInfo(desc, strb);
            string runes = strb.ToString();
            strb.Length = 0;

            PlayerItems.GetInfo(strb);
            string playerItems = strb.ToString();
            strb.Length = 0;

            strb.AppendFormat("({0}, Runes: <{1}>)\n", head, runes);
            strb.AppendLine(playerItems);
            return strb.ToString();
        }

        private void GetHeroInfo(CommonData.UnitDescription desc, StringBuilder strb)
        {
            if (desc != null)
            {
                strb.Append(desc.Name).Append(", ");
            }
            strb.AppendFormat("Id: {0}, Level: {1}", mHeroId, mHeroLevel);
            if (mHealthCoeff >= 0)
            {
                strb.AppendFormat(", Health: {0}%", mHealthCoeff * 100);
            }
        }

        private void RunesInfo(CommonData.UnitDescription desc, StringBuilder strb)
        {
            if (mRunes == null || mRunes.Length == 0)
            {
                strb.Append("Empty");
                return;
            }
            System.Collections.ObjectModel.ReadOnlyCollection<CommonData.IBaseUnitRuneItem> runesDesc = desc == null ? null : desc.UnitRunes.Runes;

            for (int i = 0; i < mRunes.Length; i++)
            {
                if (i > 0)
                {
                    strb.Append(", ");
                }
                byte id = mRunes[i];
                if (runesDesc != null)
                {
                    strb.Append(runesDesc[id].Tag);
                }
                else
                {
                    strb.Append(id);
                }
            }
        }

        public override string ToString()
        {
            return GetInfo(null);
        }
    }

    public class PlayerData : IDataStruct
    {
        private string mName;
        private short mPlayerLevel;
        private short mSelectedHero = -1;
        private short mHeroSkinId = -1;
        private readonly List<PlayerHero> mHeroes = new List<PlayerHero>();
        private int mGroupId;
        private int mGroupSize;
        private string mFacebookId;
        private string mFacebookEmail;
        private int mSessionOrderId;
        private int mSyncPvpWins;
        private int mSyncPvpDefeats;
        private int mSyncPvpLeaves;
        private CommonData.AccountType mAccountType;
        private int? mDivisionIndex;
        private string mLeague;

        public string Name { get { return mName; } }
        public short PlayerLevel { get { return mPlayerLevel; } }
        public short SelectedHero { get { return mSelectedHero; } }
        public short HeroSkinId { get { return mHeroSkinId; } }
        public List<PlayerHero> Heroes { get { return mHeroes; } }
        public int GroupId { get { return mGroupId; } }
        public int GroupSize { get { return mGroupSize; } }
        public string FacebookId { get { return mFacebookId; } }
        public string FacebookEmail { get { return mFacebookEmail; } }
        public int SessionOrderId { get { return mSessionOrderId; } }
        public int SyncPvpWins { get { return mSyncPvpWins; } }
        public int SyncPvpDefeats { get { return mSyncPvpDefeats; } }
        public int SyncPvpLeaves { get { return mSyncPvpLeaves; } }
        public CommonData.AccountType AccountType { get { return mAccountType; } }
        public int? DivisionIndex { get { return mDivisionIndex; } }
        public string League { get { return mLeague; } }

        public static PlayerData CreatePlayerData(List<PlayerHero> selectedHeroes, string playerName = "")
        {
            return new PlayerData(
                playerLevel: 1,
                name: playerName,
                accountType: AccountType.Base,
                heroes: selectedHeroes,
                groupId: 0,
                groupSize: 0,
                facebookId: null,
                facebookEmail: string.Empty,
                sessionOrderId: 0,
                wins: 0,
                defeats: 0,
                leaves: 0,
                divisionIndex: null,
                league: null);
        }

        public PlayerData() { mHeroes = new List<PlayerHero>(); }

        public PlayerData(short playerLevel, string name, CommonData.AccountType accountType, PlayerHero hero,
            int groupId, int groupSize, string facebookId, string facebookEmail, int sessionOrderId, int wins,
            int defeats, int leaves, int? divisionIndex, string league) :
                this(
                playerLevel, name, accountType, toList(hero), groupId, groupSize, facebookId, facebookEmail,
                sessionOrderId, wins, defeats, leaves, divisionIndex, league)
        { }

        public PlayerData(short playerLevel, string name, CommonData.AccountType accountType,
            List<PlayerHero> heroes, int groupId, int groupSize, string facebookId, string facebookEmail,
            int sessionOrderId, int wins, int defeats, int leaves,
            int? divisionIndex, string league)
        {
            mName = name;
            mPlayerLevel = playerLevel;

            mGroupId = groupId;
            mGroupSize = groupSize;
            mFacebookId = facebookId;
            mFacebookEmail = facebookEmail;
            mSessionOrderId = sessionOrderId;
            mSyncPvpWins = wins;
            mSyncPvpDefeats = defeats;
            mSyncPvpLeaves = leaves;
            mAccountType = accountType;

            if (heroes != null && heroes.Count > 0)
            {
                mHeroes = heroes;
                mSelectedHero = mHeroes[0].HeroId;
                mHeroSkinId = mHeroes[0].SkinId;
            }
            else
            {
                mHeroes = new List<PlayerHero>();
            }

            mDivisionIndex = divisionIndex;
            mLeague = league;
        }

        public void AddHero(PlayerHero hero)
        {
            mHeroes.Add(hero);
            if (mSelectedHero == -1)
            {
                mSelectedHero = hero.HeroId;
                mHeroSkinId = hero.SkinId;
            }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mName);
            dst.Add(ref mSelectedHero);
            dst.Add(ref mHeroSkinId);
            dst.Add(ref mPlayerLevel);
            dst.Add(ref mGroupId);
            dst.Add(ref mFacebookId);
            dst.Add(ref mFacebookEmail);
            dst.Add(ref mSessionOrderId);
            dst.Add(ref mSyncPvpWins);
            dst.Add(ref mSyncPvpDefeats);
            dst.Add(ref mSyncPvpLeaves);
            var t = (byte)mAccountType;
            dst.Add(ref t);
            mAccountType = (Shared.CommonData.AccountType)t;

            if (dst.isReader)
            {
                int nCount = 0;
                dst.Add(ref nCount);
                mHeroes.Clear();
                for (int i = 0; i < nCount; ++i)
                {
                    var heroInfo = new PlayerHero();
                    heroInfo.Serialize(dst);
                    mHeroes.Add(heroInfo);
                }
            }
            else
            {
                int nCount = mHeroes.Count;
                dst.Add(ref nCount);
                for (int i = 0; i < nCount; ++i)
                {
                    mHeroes[i].Serialize(dst);
                }
            }

            dst.AddNullable(ref mDivisionIndex);
            dst.Add(ref mLeague);

            return true;
        }

        public override string ToString()
        {
            return GetInfo(null);
        }

        public string GetInfo(CommonData.Descriptions descriptions)
        {
            StringBuilder strb = new StringBuilder();

            HeroesInfo(descriptions, strb);
            string heroes = strb.ToString();
            strb.Length = 0;

            strb.AppendFormat("[Name: {0}, Lvl: {1}, Acc: {2}, Heroes: {3}]", mName, mPlayerLevel, mAccountType, heroes);
            return strb.ToString();
        }

        private void HeroesInfo(CommonData.Descriptions descriptions, StringBuilder strb)
        {
            if (Heroes == null || Heroes.Count == 0)
            {
                strb.Append("Empty");
                return;
            }
            for (int i = 0; i < Heroes.Count; i++)
            {
                if (i > 0)
                {
                    strb.Append(", ");
                }
                strb.Append(Heroes[i].GetInfo(descriptions));
            }
        }

        public PlayerHero GetPlayerHero(short heroId)
        {
            for (int i = 0; i < mHeroes.Count; i++)
            {
                PlayerHero ph = mHeroes[i];
                if (ph.HeroId == heroId)
                {
                    return ph;
                }
            }
            return null;
        }

        public string GetLeagueDivision()
        {
            return null != mLeague && mDivisionIndex.HasValue ? mLeague + mDivisionIndex.Value : "";
        }

        static List<T> toList<T>(T obj)
        {
            List<T> result = new List<T>();
            result.Add(obj);
            return result;
        }
    }

    public class PlayerDataSet : IDataStruct
    {
        public readonly Dictionary<PlayerRole, PlayerData> PlayerHeroes = new Dictionary<PlayerRole, PlayerData>();

        public void Add(PlayerRole role, PlayerData dataSet)
        {
            PlayerHeroes.Add(role, dataSet);
        }

        public PlayerData GetPlayerRoleHeroes(PlayerRole role)
        {
            PlayerData data;
            PlayerHeroes.TryGetValue(role, out data);
            return data;
        }

        public List<KeyValuePair<PlayerRole, PlayerHero>> GetHeroes()
        {
            List<KeyValuePair<PlayerRole, PlayerHero>> teamHeroes = new List<KeyValuePair<PlayerRole, PlayerHero>>();

            foreach (var playerData in PlayerHeroes)
            {
                foreach (var heroData in playerData.Value.Heroes)
                {
                    teamHeroes.Add(new KeyValuePair<PlayerRole, PlayerHero>(playerData.Key, heroData));
                }
            }
            return teamHeroes;
        }

        public List<PlayerHero> GetTeamHeroes(PlayerRole role)
        {
            List<PlayerHero> heroes = new   List<PlayerHero>();
            foreach (KeyValuePair<PlayerRole, PlayerData> pair in PlayerHeroes)
            {
                if (pair.Key.IsSameTeam(role))
                {
                    heroes.AddRange(pair.Value.Heroes);
                }
            }
            return heroes;
        }

        public IEnumerable<PlayerRole> PlayerRoles
        {
            get
            {
                return PlayerHeroes.Keys;
            }
        }

        public IEnumerable<PlayerData> Players
        {
            get
            {
                return PlayerHeroes.Values;
            }
        }

        public string GetInfo(CommonData.Descriptions descriptions)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("HeroesSet:");
            foreach (var kv in PlayerHeroes)
            {
                sb.AppendLine(kv.Key + " : " + kv.Value.GetInfo(descriptions));
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return GetInfo(null);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            if (dst.isReader)
            {
                PlayerHeroes.Clear();
                int nCount = 0;
                dst.Add(ref nCount);
                for (int i = 0; i < nCount; ++i)
                {
                    PlayerRole role = new PlayerRole();
                    PlayerData playerInfo = new PlayerData();
                    dst.Add(ref role);
                    playerInfo.Serialize(dst);
                    PlayerHeroes.Add(role, playerInfo);
                }
            }
            else
            {
                int nCount = PlayerHeroes.Count;
                dst.Add(ref nCount);
                foreach (var kv in PlayerHeroes)
                {
                    var role = kv.Key;
                    var heroInfo = kv.Value;
                    dst.Add(ref role);
                    heroInfo.Serialize(dst);
                }
            }
            return true;
        }
    }
}
