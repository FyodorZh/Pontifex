using System.Collections.Generic;
using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.Battle;
using Shared.Protocol;

namespace Shared.Meta
{
    public struct TeamResult : IDataStruct
    {
        public Team TeamID;
        public PlayerResultType Result;

        public TeamResult(Team teamID, PlayerResultType result)
        {
            TeamID = teamID;
            Result = result;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            int id = (int)TeamID;
            dst.Add(ref id);
            TeamID = (Team)id;

            int v = (int)Result;
            dst.Add(ref v);
            Result = (PlayerResultType)v;
            return true;
        }
    }

    public enum PlayerResultType : byte
    {
        Unknown = 0,
        Win,
        DefeatHeroDeath,
        DefeatTimeout,
        DefeatLeave
    }

    public enum BattleFinishState : byte
    {
        Normal = 0,
        NoPlayers,
        Surrender,
        Cheat,
    }

    public class BattleResult : IDataStruct
    {
        public BattleResult()
        {

        }

        private long _battleId;
        private byte _battleType;
        private byte _battleCompetitionType;
        private byte _finishState;
        private string _host;
        private int _basketType;
        private Team _winnerTeamId;
        private BattlePlayerResult[] _playerResults;
        private BattleTeamResult[] _teamResults;
        private long _startTime;
        private long _durationMs;
        private bool _rating;
        private bool _fake;

        public long BattleId
        {
            get { return _battleId; }
            set { _battleId = value; }
        }

        public BattleType BattleType
        {
            get { return (BattleType)_battleType; }
            set { _battleType = (byte)value; }
        }

        public BattleCompetitionType BattleCompetitionType
        {
            get { return (BattleCompetitionType)_battleCompetitionType; }
            set { _battleCompetitionType = (byte)value; }
        }

        public BattleFinishState FinishState
        {
            get { return (BattleFinishState) _finishState; }
            set { _finishState = (byte) value; }
        }


        public string Host
        {
            get { return _host; }
            set { _host = value; }
        }

        public MatchMakerBasketType BasketType
        {
            get { return (MatchMakerBasketType)_basketType; }
            set { _basketType = (int)value; }
        }

        public Team WinnerTeamId
        {
            get { return _winnerTeamId; }
            set { _winnerTeamId = value; }
        }

        public BattlePlayerResult[] PlayerResults
        {
            get { return _playerResults; }
            set { _playerResults = value; }
        }

        public BattleTeamResult[] TeamResults
        {
            get { return _teamResults; }
            set { _teamResults = value; }
        }

        public System.DateTime StartTime
        {
            get { return Shared.UnixTime.UnixTimeToDateTime(_startTime); }
            set { _startTime = Shared.UnixTime.DateTimeToUnixTime(value); }
        }

        public long Duration
        {
            get { return _durationMs; }
            set { _durationMs = value; }
        }

        public bool IsRatingBattle
        {
            get { return _rating; }
            set { _rating = value; }
        }

        public bool IsFakeBattle
        {
            get { return _fake; }
            set { _fake = value; }
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _battleId);
            dst.Add(ref _battleType);
            dst.Add(ref _host);
            dst.Add(ref _basketType);
            dst.Add(ref _playerResults);
            dst.Add(ref _teamResults);
            dst.Add(ref _startTime);
            dst.Add(ref _durationMs);
            dst.Add(ref _rating);
            byte teamId = (byte)_winnerTeamId;
            dst.Add(ref teamId);
            _winnerTeamId = (Team)teamId;
            dst.Add(ref _fake);
            dst.Add(ref _battleCompetitionType);
            dst.Add(ref _finishState);

            return true;
        }

        public BattlePlayerResult[] PlayerResultsByID(long playerID)
        {
            int playerResultsRealCount = 0;
            for (int i = 0, c = PlayerResults.Length; i < c; ++i)
            {
                if (PlayerResults[i].PlayerId == playerID)
                {
                    playerResultsRealCount += 1;
                }
            }

            BattlePlayerResult[] results = new BattlePlayerResult[playerResultsRealCount];
            if (playerResultsRealCount > 0)
            {

                for (int i = 0, k = 0, c = PlayerResults.Length; i < c; ++i)
                {
                    if (PlayerResults[i].PlayerId == playerID)
                    {
                        results[k++] = PlayerResults[i];
                    }
                }
            }
            return results;
        }

        public JsonFactory.IExternalJson PrintAsJson()
        {
            var json = new JsonFactory.ExternalJsons.JsonObjectAsExternalJson();
            var root = json.Root;
            root.AddElement("battleId", BattleId);
            root.AddElement("battleType", BattleType.ToString());
            root.AddElement("battleCompetitionType", BattleCompetitionType.ToString());
            root.AddElement("finishState", FinishState.ToString());
            //root.AddElement("host", Host);
            root.AddElement("basketType", BasketType.ToString());
            root.AddElement("winnerTeamId", WinnerTeamId.ToString());
            var playerResults = root.AddArray("playerResults");
            for (int i = 0; i < (_playerResults != null ? _playerResults.Length : 0); ++i)
            {
                _playerResults[i].PrintToJson(playerResults.AddObject());
            }
            root.AddElement("teamResults_count", _teamResults != null ? _teamResults.Length : 0);
            root.AddElement("startTime", StartTime.ToString());
            root.AddElement("durationMs", Duration.ToString());
            root.AddElement("rating", _rating);
            root.AddElement("fake", _fake);
            return json;
        }
    }

    public class BattlePlayerResult : Serializer.BinarySerializer.IDataStruct
    {
        public BattlePlayerResult()
        {

        }

        private byte _teamId;
        private long _playerId;
        private string _playerName;
        private int _fragsHero;
        private int _fragsHeroAssist;
        private int _fragsMinion;
        private int _fragsBuilding;
        private int _fragsNeutral;
        private int _fragsRoshan;
        private int _killedByHero;
        private int _killedByCreep;
        private int _killedByBuilding;
        private int _killedByNeutral;
        private byte _battleResultType;
        private short _heroId;
        private short _skinId;
        private int _groupId;
        private string _facebookId;
        private string _facebookEmail;
        private bool _processed;
        private short _summonersPowerId;
        private byte[] _equipLevels;
        private byte[] _runes;
        private bool _mvp;
        private float? _mvpCoeff;
        private float? _mvpPercent;
        private BattlePlayerStats _extraData;
        private byte _finishState;

        public byte TeamId { get { return _teamId; } set { _teamId = value; } }
        public long PlayerId { get { return _playerId; } set { _playerId = value; } }
        public string PlayerName { get { return _playerName; } set { _playerName = value; } }

        public int FragsHero { get { return _fragsHero; } set { _fragsHero = value; } }
        public int FragsHeroAssist { get { return _fragsHeroAssist; } set { _fragsHeroAssist = value; } }
        public int FragsMinion { get { return _fragsMinion; } set { _fragsMinion = value; } }
        public int FragsBuilding { get { return _fragsBuilding; } set { _fragsBuilding = value; } }
        public int FragsNeutral { get { return _fragsNeutral; } set { _fragsNeutral = value; } }
        public int FragsRoshan { get { return _fragsRoshan; } set { _fragsRoshan = value; } }

        public int KilledByHero { get { return _killedByHero; } set { _killedByHero = value; } }
        public int KilledByCreep { get { return _killedByCreep; } set { _killedByCreep = value; } }
        public int KilledByBuilding { get { return _killedByBuilding; } set { _killedByBuilding = value; } }
        public int KilledByNeutral { get { return _killedByNeutral; } set { _killedByNeutral = value; } }

        public PlayerResultType ResultType { get { return (PlayerResultType)_battleResultType; } set { _battleResultType = (byte)value; } }
        public BattleFinishState FinishState { get { return (BattleFinishState) _finishState; } set { _finishState = (byte) value; } }
        public short HeroDescriptionId { get { return _heroId; } set { _heroId = value; } }
        public short HeroSkinId { get { return _skinId; } set { _skinId = value; } }
        public int GroupId { get { return _groupId; } set { _groupId = value; } }
        public string FacebookId { get { return _facebookId; } set { _facebookId = value; } }
        public string FacebookEmail { get { return _facebookEmail; } set { _facebookEmail = value; } }

        public byte[] EquipLevels { get { return _equipLevels; } set { _equipLevels = value; } }
        public byte[] Runes { get { return _runes; } set { _runes = value; } }
        public bool IsProcessed { get { return _processed; } set { _processed = value; } }

        public byte EquipLevel1 { get { return GetEquipLevel(0); } }
        public byte EquipLevel2 { get { return GetEquipLevel(1); } }
        public byte EquipLevel3 { get { return GetEquipLevel(2); } }

        public byte Rune1 { get { return GetRune(0); } }
        public byte Rune2 { get { return GetRune(1); } }
        public byte Rune3 { get { return GetRune(2); } }
        public byte Rune4 { get { return GetRune(3); } }
        public byte Rune5 { get { return GetRune(4); } }
        public byte Rune6 { get { return GetRune(5); } }

        public bool IsMvp
        {
            get { return _mvp; }
            set { _mvp = value; }
        }

        public float? MvpCoeff
        {
            get { return _mvpCoeff; }
            set { _mvpCoeff = value; }
        }

        public float? MvpPercent
        {
            get { return _mvpPercent; }
            set { _mvpPercent = value; }
        }

        public BattlePlayerStats ExtraData
        {
            get { return _extraData; }
            set { _extraData = value; }
        }

        public bool IsBot()
        {
            return PlayerId <= 0;
        }

        private byte GetRune(int index)
        {
            return GetArrayVal(_runes, index);
        }

        private byte GetEquipLevel(int index)
        {
            return GetArrayVal(_equipLevels, index);
        }

        private byte GetArrayVal(byte[] array, int index)
        {
            return (array != null && array.Length > index) ? array[index] : (byte)0;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _teamId);
            dst.Add(ref _playerId);
            dst.Add(ref _fragsHero);
            dst.Add(ref _fragsHeroAssist);
            dst.Add(ref _fragsMinion);
            dst.Add(ref _fragsBuilding);
            dst.Add(ref _fragsNeutral);
            dst.Add(ref _fragsRoshan);
            dst.Add(ref _killedByHero);
            dst.Add(ref _killedByCreep);
            dst.Add(ref _killedByBuilding);
            dst.Add(ref _killedByNeutral);
            dst.Add(ref _battleResultType);
            dst.Add(ref _heroId);
            dst.Add(ref _groupId);
            dst.Add(ref _facebookId);
            dst.Add(ref _facebookEmail);
            dst.Add(ref _processed);
            dst.Add(ref _summonersPowerId);
            dst.Add(ref _equipLevels);
            dst.Add(ref _runes);
            dst.Add(ref _mvp);
            dst.AddNullable(ref _mvpCoeff);
            dst.AddNullable(ref _mvpPercent);
            dst.Add(ref _extraData);
            dst.Add(ref _skinId);
            dst.Add(ref _finishState);

            return true;
        }

        public JsonFactory.IJsonObject PrintToJson(JsonFactory.IJsonObject json)
        {
            if (json == null)
            {
                json = JsonFactory.JsonObject.Construct();
            }

            json.AddElement("TeamId", TeamId);
            json.AddElement("PlayerId", PlayerId);
            json.AddElement("PlayerName", PlayerName);
            json.AddElement("ResultType", ResultType.ToString());
            json.AddElement("FinishState", FinishState.ToString());

            return json;
        }
    }

    public class BattleTeamResult : Serializer.BinarySerializer.IDataStruct
    {
        private byte _teamId;
        private int _fragsHero;
        private BattleTeamStats _extraData;

        public byte TeamId { get { return _teamId; } set { _teamId = value; } }
        public int FragsHero { get { return _fragsHero; } set { _fragsHero = value; } }

        public BattleTeamStats ExtraData
        {
            get { return _extraData; }
            set { _extraData = value; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _teamId);
            dst.Add(ref _fragsHero);
            dst.Add(ref _extraData);

            return true;
        }
    }
}
