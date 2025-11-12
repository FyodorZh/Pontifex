namespace Shared.Battle
{
    public enum Team
    {
        Unknown = -1,
        Environment = 0,
        Blue1 = 1,
        Red2 = 2,
        PvePlayer = Blue1,
        PveEnemy = Red2
    }

    public static class TeamExt
    {
        public static Team GetTeamId(this BattleTeam team)
        {
            if (team.Type == BattleTeam.TeamType.Players && team.Owner.PlayerTeamCount > 0)
            {
                BattleTeam firstPlayerTeam = team.Owner.GetPlayerTeamAt(0);
                return Team.Blue1 + (team.FirstRole.Raw.TeamId - firstPlayerTeam.FirstRole.Raw.TeamId);
            }
            if (team.Type == BattleTeam.TeamType.Environment)
            {
                return Team.Environment;
            }
            return Team.Unknown;
        }

        public static Team GetTeamId(this BattleTopology topology, PlayerRole role)
        {
            return topology.FindTeam(role).GetTeamId();
        }

        public static BattleTeam FindTeam(this BattleTopology topology, Team teamId)
        {
            if (teamId == Team.Environment)
            {
                return topology.Environment;
            }
            return topology.GetPlayerTeamAt((int)teamId - 1);
        }

        public static Team GetOpponentTeam(Team team)
        {
            if (team == Team.Blue1)
            {
                return Team.Red2;
            }
            if (team == Team.Red2)
            {
                return Team.Blue1;
            }
            Log.e("Да чёрт побери, я понятия не имею как можно реализовать эту функцию в этом случае!!");
            return Team.Unknown;
        }
    }
}