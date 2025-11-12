using System.Linq;
using Serializer.BinarySerializer;

namespace Shared.Protocol
{
    public class JoinBattleState : IDataStruct
    {
        public enum State : byte
        {
            Break = 0, // Процесс прерван по какой-то причине, бой не состоится
            WaitAcceptMatch, // Сервер ожидает от игроков подтверждения участия в бою
            WaitAcceptHeroes, // Сервер ожидает от игроков "подтверждения героев"
            WaitExtraTime, // Сервер ожидает "дополнительное время" (5-ти секундный таймер перед началом боя)
            WaitStartBattle, // Сервер ожидает инициализации боя
            ProcessBattle // В настоящий момент идет бой
        }

        /// <summary>
        /// Уникальный идентификатор боя
        /// </summary>
        public long battleId;

        /// <summary>
        /// Состояние
        /// </summary>
        public State state;

        /// <summary>
        /// Данные по таймаутам
        /// </summary>
        public Timeouts timeouts;

        /// <summary>
        /// Данные о состоянии сокомандников
        /// </summary>
        public TeamsDynamic.Team myTeam;

        /// <summary>
        /// Количество участников подтвердивших участие в бою
        /// </summary>
        public byte acceptedMatchCount;

        public JoinBattleState()
        {
        }

        public JoinBattleState(long battleId, State state, Timeouts timeouts, TeamsDynamic.Team myTeam, int acceptedMatchCount)
        {
            this.battleId = battleId;
            this.state = state;
            this.timeouts = timeouts;
            this.myTeam = myTeam;
            this.acceptedMatchCount = (byte)acceptedMatchCount;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref battleId);

            var t = (byte)state;
            dst.Add(ref t);
            state = (State)t;

            dst.Add(ref timeouts);
            dst.Add(ref myTeam);
            dst.Add(ref acceptedMatchCount);

            return true;
        }


        // don't want to override equals, because then I need to override hashcode and so on
        public bool ContentEquals(JoinBattleState otherState)
        {
            if (otherState == null) return false;

            bool result = (battleId == otherState.battleId) && (state == otherState.state) &&
                (acceptedMatchCount == otherState.acceptedMatchCount) && timeouts.ContentEquals(otherState.timeouts) && myTeam.ContentEquals(otherState.myTeam);
            return result;
        }


        public class Timeouts : IDataStruct
        {
            /// <summary>
            /// Время когда матче-мейкером были созданы группы для боя
            /// </summary>
            public long matchTimeMs;

            /// <summary>
            /// Время когда истекает таймер на подтверждение участия в бою
            /// (когда все игроки подтвердят участие в бою - значение поля изменится
            /// и будет указывать время когда последний из участников сделал accept)
            /// </summary>
            public long acceptMatchTimeMs;

            /// <summary>
            /// Время когда истекает таймер на accept героя
            /// (значение поля будет изменяться в зависимости от 'acceptTeamTimeMs'
            /// и в случае когда все участники заакцептят своих героев.
            /// Будет указывать время когда последний из участников сделал accept)
            /// </summary>
            public long acceptHeroesTimeMs;

            /// <summary>
            /// Время когда истекает дополниельный таймер
            /// (значение поля будет изменяться если меняются предыдущие поля)
            /// </summary>
            public long extraTimeMs;

            /// <summary>
            /// Время когда истекает таймаут на создание боя
            /// (значение поля будет изменяться если меняются предыдущие поля)
            /// </summary>
            public long startTimeMs;

            public Timeouts()
            {
            }

            public bool ContentEquals(Timeouts other)
            {
                if (other == null) return false;

                return (matchTimeMs == other.matchTimeMs) && (acceptMatchTimeMs == other.acceptMatchTimeMs) && (acceptHeroesTimeMs == other.acceptHeroesTimeMs) &&
                    (extraTimeMs == other.extraTimeMs) && (startTimeMs == other.startTimeMs);
            }

            public Timeouts(Timeouts timeouts)
            {
                matchTimeMs = timeouts.matchTimeMs;
                acceptMatchTimeMs = timeouts.acceptMatchTimeMs;
                acceptHeroesTimeMs = timeouts.acceptHeroesTimeMs;
                extraTimeMs = timeouts.extraTimeMs;
                startTimeMs = timeouts.startTimeMs;
            }

            public Timeouts(long matchTimeMs, long acceptMatchTimeMs, long acceptHeroesTimeMs, long extraTimeMs,
                long startTimeMs)
            {
                this.matchTimeMs = matchTimeMs;
                this.acceptMatchTimeMs = acceptMatchTimeMs;
                this.acceptHeroesTimeMs = acceptHeroesTimeMs;
                this.extraTimeMs = extraTimeMs;
                this.startTimeMs = startTimeMs;
            }

            public void changeAcceptMatchTime(long acceptMatchTimeMs, long acceptHeroesTimeMs, long extraTimeMs,
                long startTimeMs)
            {
                this.acceptMatchTimeMs = acceptMatchTimeMs;
                changeAcceptHeroesTime(acceptHeroesTimeMs, extraTimeMs, startTimeMs);
            }

            public void changeAcceptHeroesTime(long acceptHeroesTimeMs, long extraTimeMs, long startTimeMs)
            {
                this.acceptHeroesTimeMs = acceptHeroesTimeMs;
                this.extraTimeMs = extraTimeMs;
                this.startTimeMs = startTimeMs;
            }

            #region Implementation of IDataStruct

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref matchTimeMs);
                dst.Add(ref acceptMatchTimeMs);
                dst.Add(ref acceptHeroesTimeMs);
                dst.Add(ref extraTimeMs);
                dst.Add(ref startTimeMs);
                return true;
            }

            #endregion
        }

        public class TeamsStatic : IDataStruct
        {
            public Team[] teams;

            public TeamsStatic()
            {
            }

            public TeamsStatic(Team[] teams)
            {
                this.teams = teams;
            }

            #region Implementation of IDataStruct

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref teams);
                return true;
            }

            #endregion

            public Team.Group.Participant find(long id)
            {
                return teams.SelectMany(x => x.groups).SelectMany(x => x.participants).FirstOrDefault(x => x.id == id);
            }

            public Team TeamByID(int teamID)
            {
                for (int i = 0, c = teams.Length; i < c; ++i)
                {
                    if (teams[i].teamId == teamID)
                    {
                        return teams[i];
                    }
                }
                return null;
            }

            public Team.Group ParticipantGroup(long id)
            {
                return teams
                    .SelectMany(t => t.groups)
                    .FirstOrDefault(g => g.participants.FirstOrDefault(p => p.id == id) != null);
            }

            public class Team : IDataStruct
            {
                /// <summary>
                /// Идентификатор команды
                /// </summary>
                public byte teamId;

                /// <summary>
                /// Данные по участникам
                /// </summary>
                public Group[] groups;

                public int ParticipantsCount()
                {
                    return groups.SelectMany(g => g.participants).Count();
                }

                public Group ParticipantGroup(long id)
                {
                    return groups.FirstOrDefault(g => g.participants.FirstOrDefault(p => p.id == id) != null);
                }

                public Team()
                {
                }

                public Team(byte teamId, Group[] groups)
                {
                    this.teamId = teamId;
                    this.groups = groups;
                }

                #region Implementation of IDataStruct

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref teamId);
                    dst.Add(ref groups);
                    return true;
                }

                #endregion

                public Group.Participant find(long id)
                {
                    return groups.SelectMany(x => x.participants).FirstOrDefault(x => x.id == id);
                }

                public class Group : IDataStruct
                {
                    /// <summary>
                    /// Идентификатор лидера группы или 0 если это соло-игрок
                    /// </summary>
                    public long leaderId;

                    /// <summary>
                    /// Данные по участникам
                    /// </summary>
                    public Participant[] participants;

                    public bool isGroup
                    {
                        get { return leaderId > 0; }
                    }

                    public Group()
                    {
                    }

                    public Group(Participant participant) : this(0, new[] {participant})
                    {
                    }

                    public Group(long leaderId, Participant[] participants)
                    {
                        this.leaderId = leaderId;
                        this.participants = participants;
                    }

                    public Participant find(long id)
                    {
                        return participants.FirstOrDefault(x => x.id == id);
                    }

                    #region Implementation of IDataStruct

                    public bool Serialize(IBinarySerializer dst)
                    {
                        dst.Add(ref leaderId);
                        dst.Add(ref participants);

                        return true;
                    }

                    #endregion

                    public class Participant : IDataStruct
                    {
                        /// <summary>
                        /// Идентификатор участника (если больше нуля то это игрок)
                        /// </summary>
                        public long id;

                        /// <summary>
                        /// Имя участника
                        /// </summary>
                        public string name;

                        /// <summary>
                        /// Аватарка участника
                        /// </summary>
                        public short avatarDescId;

                        /// <summary>
                        /// Тип аккаунта (актуален только для игроков)
                        /// </summary>
                        public CommonData.AccountType accountType;

                        /// <summary>
                        /// Время когда заявка с этим участником встала в очередь в матчемейкер
                        /// </summary>
                        public long enterTime;

                        /// <summary>
                        /// Glicko rating мета-бота
                        /// </summary>
                        public double glickoRating;

                        public double glickoDeviation;
                        public double glickoVolatility;

                        /// <summary>
                        /// Список геонод игрока
                        /// </summary>
                        public byte[] geoNodes;

                        /// <summary>
                        /// Лига заявки с которой игрок попал в поиск
                        /// (в случае с группами, может отличаться от реальной лиги игрока)
                        /// </summary>
                        public string appLeagueName;

                        /// <summary>
                        /// Индекс лиги заявки с которым игрок попал в поиск
                        /// (в случае с группами, может отличаться от реальной лиги игрока)
                        /// </summary>
                        public int appLeagueIndex;

                        /// <summary>
                        /// Индекс дивизиона заявки с которым игрок попал в поиск
                        /// (в случае с группами, может отличаться от реального дивизиона игрока)
                        /// </summary>
                        public int appDivisionIndex;

                        /// <summary>
                        /// Лига заявки с которой игрок попал в поиск
                        /// </summary>
                        public string ownLeagueName;

                        /// <summary>
                        /// Индекс дивизиона заявки с которым игрок попал в поиск
                        /// </summary>
                        public int ownDivisionIndex;

                        public Participant()
                        {
                        }

                        private Participant(
                            long id,
                            string name,
                            CommonData.AccountType accountType,
                            short avatarDescId,
                            long enterTime,
                            byte[] geoNodes,
                            string appLeagueName,
                            int appLeagueIndex,
                            int appDivisionIndex,
                            string ownLeagueName,
                            int ownDivisionIndex)
                        {
                            this.id = id;
                            this.name = name;
                            this.accountType = accountType;
                            this.avatarDescId = avatarDescId;
                            this.enterTime = enterTime;
                            this.geoNodes = geoNodes;
                            this.appLeagueName = appLeagueName;
                            this.appLeagueIndex = appLeagueIndex;
                            this.appDivisionIndex = appDivisionIndex;
                            this.ownLeagueName = ownLeagueName;
                            this.ownDivisionIndex = ownDivisionIndex;
                        }

                        /// <summary>
                        /// Конструктор для игроков
                        /// </summary>
                        public Participant(
                            long id,
                            long enterTime,
                            byte[] geoNodes,
                            string appLeagueName,
                            int appLeagueIndex,
                            int appDivisionIndex,
                            string ownLeagueName,
                            int ownDivisionIndex)
                            : this(
                                id,
                                null,
                                CommonData.AccountType.Base,
                                0,
                                enterTime,
                                geoNodes,
                                appLeagueName,
                                appLeagueIndex,
                                appDivisionIndex,
                                ownLeagueName,
                                ownDivisionIndex)
                        {
                        }

                        /// <summary>
                        /// Конструктор для ботов
                        /// </summary>
                        public Participant(long id, string name, short avatarDescId)
                            : this(id, name, CommonData.AccountType.Base, avatarDescId, 0, null, null, -1, -1,
                                null, -1)
                        {
                        }

                        public bool isPlayer()
                        {
                            return id > 0;
                        }

                        public bool isBot()
                        {
                            return !isPlayer();
                        }

                        #region Implementation of IDataStruct

                        public bool Serialize(IBinarySerializer dst)
                        {
                            dst.Add(ref id);
                            dst.Add(ref name);
                            dst.Add(ref avatarDescId);
                            var t = (byte) accountType;
                            dst.Add(ref t);
                            accountType = (CommonData.AccountType) t;
                            dst.Add(ref enterTime);
                            dst.Add(ref glickoRating);
                            dst.Add(ref glickoDeviation);
                            dst.Add(ref glickoVolatility);
                            dst.Add(ref geoNodes);

                            dst.Add(ref appLeagueName);
                            dst.Add(ref appLeagueIndex);
                            dst.Add(ref appDivisionIndex);
                            dst.Add(ref ownLeagueName);
                            dst.Add(ref ownDivisionIndex);
                            return true;
                        }

                        #endregion
                    }
                }
            }
        }

        public class TeamsDynamic : IDataStruct
        {
            /// <summary>
            /// Данные по командам
            /// </summary>
            public Team[] teams;

            public TeamsDynamic()
            {
            }

            public TeamsDynamic(Team[] teams)
            {
                this.teams = teams;
            }

            #region Implementation of IDataStruct

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref teams);
                return true;
            }

            #endregion

            public class Team : IDataStruct
            {
                /// <summary>
                /// Идентификатор команды
                /// </summary>
                public byte teamId;

                /// <summary>
                /// Данные по участникам
                /// </summary>
                public Participant[] participants;

                public Team()
                {
                }

                public Team(byte teamId, Participant[] participants)
                {
                    this.teamId = teamId;
                    this.participants = participants;
                }

                public bool ContentEquals(Team other)
                {
                    if (other == null) return false;

                    bool result = teamId == other.teamId && ((participants != null && other.participants != null && participants.Length == other.participants.Length) || (participants == null && other.participants == null));

                    if (result && participants != null)
                    {
                        for (int i = 0, c = participants.Length; i < c; ++i)
                        {
                            result = result && participants[i].ContentEquals(other.participants[i]);
                            if (!result) break;
                        }
                    }

                    return result;
                }

                public Participant ParticipantByID(long id)
                {
                    for (int i = 0, c = participants.Length; i < c; ++i)
                    {
                        if (participants[i].id == id)
                        {
                            return participants[i];
                        }
                    }
                    return null;
                }

                #region Implementation of IDataStruct

                public bool Serialize(IBinarySerializer dst)
                {
                    dst.Add(ref teamId);
                    dst.Add(ref participants);
                    return true;
                }

                #endregion

                public class Participant : IDataStruct
                {
                    public const short NOT_SET = -1;

                    /// <summary>
                    /// Идентификатор участника
                    /// </summary>
                    public long id;

                    /// <summary>
                    /// Флаг указывает что этот участник заакцептил участие в бою
                    /// </summary>
                    public bool acceptMatch;

                    /// <summary>
                    /// Идентификатор выбранного (просматриваемого) героя
                    /// </summary>
                    public short selectedHero;

                    /// <summary>
                    /// Идентификатор подтвержденного героя
                    /// </summary>
                    public short acceptedHero;

                    /// <summary>
                    /// Выбранный скин
                    /// </summary>
                    public short selectedSkin;

                    /// <summary>
                    /// Выбранный spark
                    /// </summary>
                    public short selectedSpark;

                    /// <summary>
                    /// Выбраные руны
                    /// </summary>
                    public byte[] selectedRune;

                    /// <summary>
                    /// Выбранный пресет предметов
                    /// </summary>
                    public short[] battleItemsPreset;

                    /// <summary>
                    /// поле не сериализуется, используется только на сервере
                    /// </summary>
                    public int index;

                    public Participant()
                    {
                    }

                    public Participant(long id, bool acceptMatch, short selectedHero, short acceptedHero,
                        short selectedSkin, byte[] selectedRune, short[] battleItemsPreset, short selectedSpark)
                    {
                        this.id = id;
                        this.acceptMatch = acceptMatch;
                        this.selectedHero = selectedHero;
                        this.acceptedHero = acceptedHero;
                        this.selectedSkin = selectedSkin;
                        this.selectedSpark = selectedSpark;
                        this.selectedRune = selectedRune;
                        this.battleItemsPreset = battleItemsPreset;
                    }

                    public bool ContentEquals(Participant other)
                    {
                        if (other == null) return false;

                        return (id == other.id) && (acceptMatch == other.acceptMatch) &&
                               (selectedHero == other.selectedHero) && (acceptedHero == other.acceptedHero) &&
                               (selectedSkin == other.selectedSkin) && runesEqual(selectedRune, other.selectedRune) &&
                               BattleItemsEqual(battleItemsPreset, other.battleItemsPreset) &&
                               selectedSpark == other.selectedSpark;
                    }

                    static bool runesEqual(byte[] rune1, byte[] rune2)
                    {
                        if (rune1 != null && rune2 != null && rune1.Length == rune2.Length)
                        {
                            bool result = true;
                            for (int i = 0, c = rune1.Length; i < c; ++i)
                            {
                                result = result && rune1[i] == rune2[i];
                                if (!result)
                                {
                                    break;
                                }
                            }

                            return result;
                        }
                        else if (rune1 == null && rune2 == null)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    private static bool BattleItemsEqual(short[] battleItems1, short[] battleItems2)
                    {
                        if (battleItems1 == null && battleItems2 == null)
                        {
                            return true;
                        }

                        if (battleItems1 != null && battleItems2 != null && battleItems1.Length == battleItems2.Length)
                        {
                            for (int index = 0; index < battleItems1.Length; ++index)
                            {
                                if (battleItems1[index] != battleItems2[index])
                                {
                                    return false;
                                }
                            }

                            return true;
                        }

                        return false;
                    }

                    public Participant(long id)
                        : this(id, false, NOT_SET, NOT_SET, NOT_SET, null, null, NOT_SET)
                    {
                    }

                    public bool isPlayer()
                    {
                        return id > 0;
                    }

                    public bool isBot()
                    {
                        return !isPlayer();
                    }

                    public bool isHeroSelect()
                    {
                        return NOT_SET != selectedHero;
                    }

                    public bool isHeroAccept()
                    {
                        return NOT_SET != acceptedHero;
                    }

                    public bool isRuneSelect()
                    {
                        return null != selectedRune;
                    }

                    public bool IsBattleItemsPresetSelected()
                    {
                        return battleItemsPreset != null;
                    }

                    public bool isSkinSelect()
                    {
                        return NOT_SET != selectedSkin;
                    }

                    #region Implementation of IDataStruct

                    public bool Serialize(IBinarySerializer dst)
                    {
                        dst.Add(ref id);
                        dst.Add(ref acceptMatch);
                        dst.Add(ref selectedHero);
                        dst.Add(ref acceptedHero);
                        dst.Add(ref selectedSkin);
                        dst.Add(ref selectedRune);
                        dst.Add(ref battleItemsPreset);
                        dst.Add(ref selectedSpark);

                        return true;
                    }

                    #endregion
                }
            }
        }

    }

    public class JoinBattleMatchData : IDataStruct
    {
        public JoinBattleState.TeamsStatic teamsStatic;
        public JoinBattleState state;

        public JoinBattleMatchData()
        {
        }

        public JoinBattleMatchData(JoinBattleState.TeamsStatic teamsStatic, JoinBattleState state)
        {
            this.teamsStatic = teamsStatic;
            this.state = state;
        }

        #region Implementation of IDataStruct

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref teamsStatic);
            dst.Add(ref state);
            return true;
        }

        #endregion

        public bool DynamicDataEquals(JoinBattleMatchData otherData)
        {
            if (otherData == null) return false;
            return state.ContentEquals(otherData.state);
        }
    }

}
