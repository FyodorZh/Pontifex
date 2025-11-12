using Shared.Battle;

namespace MetaGame
{
    public class Consts
    {
        /// <summary>
        /// Ограничение на количество одновременно приглашенных в группу игроков
        /// </summary>
        public const int FRIEND_GROUP_MAX_INVITED = 15;
        /// <summary>
        /// Ограничение на минимальное количество игроков в группе
        /// </summary>
        public const int FRIEND_GROUP_MIN_SIZE = 2;

        public const char FRIEND_GROUP_INVITED = 'i';
        public const char FRIEND_GROUP_REJECTED = 'r';
        public const int FRIEND_GROUP_LEADER_INDEX = 0;
        public const char FRIEND_GROUP_BATTLE_COMPETITION_TYPE = 'c';
        public const char FRIEND_GROUP_BATTLE_SCRIPT_TYPE = 's';
        public const int PLAYER_INFO_DATA_TOKEN_LENGTH = 32;

        public const byte DEFAULT_GEO_NODE_ID = 255;

        /// <summary>
        /// Максимальный размер команды для боя
        /// </summary>
        public static int GetMaxTeamSize(BattleScriptType battleType)
        {
            if (battleType == BattleScriptType.Basic_5x5)
            {
                return Group5x5Size;
            }

            return Group3x3Size;
        }

        /// <summary>
        /// Максимальный размер группы
        /// </summary>
        public static int GetMaxGroupSize(BattleScriptType battleType)
        {
            if (battleType == BattleScriptType.Basic_5x5)
            {
                return Group5x5Size;
            }

            return Group3x3Size;
        }

        private const int Group5x5Size = 5;
        private const int Group3x3Size = 3;
    }
}