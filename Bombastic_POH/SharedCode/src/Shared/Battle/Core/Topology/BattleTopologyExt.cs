using System;
using System.Collections.Generic;
using Shared.Meta;

namespace Shared.Battle
{
    public class BattleTopologyExt
    {
        public const int PVP3x3_HEROES_IN_TEAM = 3;
        public const int PVP5x5_HEROES_IN_TEAM = 5;

        public static IDLong<IClient> GetAsyncPlayerID(bool ally, int index)
        {
            if (index < PVP5x5_HEROES_IN_TEAM)
            {
                return IDLong<IClient>.DeserializeFrom((ally ? -10 : -20) - index - 1); // allies: -11, -12, -13... | opponents: -21, -21, -23...
            }
            throw new ArgumentException("index should be less than team size - 1");
        }

        public static BattleTopology AsyncPVETopology(IDLong<IClient> playerID)
        {
            return new BattleTopology(true, true, new[]{playerID}, new[]{IDLong<IClient>.Invalid} );
        }

        private static BattleTopology AsyncPVPTopology(IDLong<IClient> playerID, int teamSize, bool toRightTeam)
        {
            var playerTeam = new List<IDLong<IClient>>();
            playerTeam.Add(playerID);
            var opponentsTeam = new List<IDLong<IClient>>();
            for(int i = 1; i < teamSize; i++)
            {
                playerTeam.Add(GetAsyncPlayerID(true, i));
            }
            for (int i = 0; i < teamSize; i++)
            {
                opponentsTeam.Add(GetAsyncPlayerID(false, i));
            }

            if (toRightTeam)
            {
                return new BattleTopology(true, true, opponentsTeam.ToArray(), playerTeam.ToArray());
            }
            return new BattleTopology(true, true, playerTeam.ToArray(), opponentsTeam.ToArray());
        }

        public static BattleTopology AsyncTopology(IDLong<IClient> playerID, MissionType missionType, int teamSize, bool toRightTeam)
        {
            if (BattleTypeHelper.IsPVPScenario(missionType))
            {
                return AsyncPVPTopology(playerID, teamSize, toRightTeam);
            }
            return AsyncPVETopology(playerID);
        }

        public static int GetAllyCount(MissionType missionType)
        {
            int result;

            switch (missionType)
            {
                case MissionType.AsyncPVP:
                case MissionType.TowerPVE:
                    result = 3;
                    break;
                default:
                    result = 1;
                    break;
            }

            return result;
        }

        public static int GetEnemyCount(MissionType missionType)
        {
            int result;

            switch (missionType)
            {
                case MissionType.AsyncPVP:
                    result = 3;
                    break;
                default:
                    result = 0;
                    break;
            }

            return result;
        }
    }
}