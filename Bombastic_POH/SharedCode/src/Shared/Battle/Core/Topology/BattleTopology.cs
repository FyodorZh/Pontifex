using Serializer.BinarySerializer;
using Shared.Battle.Common;

namespace Shared
{
    public interface IClient
    {
    }
}

namespace Shared.Battle
{
    public class BattleTopology : IDataStruct
    {
        private class BattleTeamImpl : BattleTeam
        {
            public static BattleTeamImpl Environment(BattleTopology owner, int teamId)
            {
                return new BattleTeamImpl(owner, TeamType.Environment, teamId, new IDLong<IClient>[0]);
            }

            public static BattleTeamImpl God(BattleTopology owner, int teamId)
            {
                return new BattleTeamImpl(owner, TeamType.God, teamId, new IDLong<IClient>[0]);
            }

            public BattleTeamImpl(BattleTopology owner)
                : base(owner, TeamType.Void, -1, new IDLong<IClient>[0])
            {
            }

            public BattleTeamImpl(BattleTopology owner, TeamType type, int teamId, IDLong<IClient>[] players)
                : base(owner, type, teamId, players)
            {
            }

            public void EnumerateRoles(Pool.CollectableEnumerable<PlayerRole> list)
            {
                AllRoles(list);
            }

            public void EnumerateClients(Pool.CollectableEnumerable<IDLong<IClient>> list)
            {
                AllClients(list);
            }

            public new void Serialize(IBinarySerializer dst)
            {
                base.Serialize(dst);
            }

            public int TeamId
            {
                get { return mTeamId; }
            }
        }

        public readonly BattleTeam VoidTeam;

        private readonly BattleTeamImpl mEnvironment;
        private readonly BattleTeamImpl mGod;
        private BattleTeamImpl[] mPlayers;

        public BattleTopology()
        {
            VoidTeam = new BattleTeamImpl(this);
            mEnvironment = new BattleTeamImpl(this);
            mGod = new BattleTeamImpl(this);
            mPlayers = new BattleTeamImpl[0];
        }

        public BattleTopology(bool hasEnvironment, bool hasGod, params IDLong<IClient>[][] teams)
        {
            VoidTeam = new BattleTeamImpl(this);

            int id = 0;

            mEnvironment = hasEnvironment ? BattleTeamImpl.Environment(this, id++) : new BattleTeamImpl(this);
            mGod = hasGod ? BattleTeamImpl.God(this, id++) : new BattleTeamImpl(this);

            mPlayers = new BattleTeamImpl[teams.Length];
            for (int i = 0; i < teams.Length; ++i)
            {
                mPlayers[i] = new BattleTeamImpl(this, BattleTeam.TeamType.Players, id++, teams[i]);
            }
        }

        public int TeamCount
        {
            get
            {
                int count = mPlayers.Length;
                if (mGod.Type != BattleTeam.TeamType.Void)
                {
                    count += 1;
                }
                if (mEnvironment.Type != BattleTeam.TeamType.Void)
                {
                    count += 1;
                }
                return count;
            }
        }

        public int PlayerTeamCount
        {
            get { return mPlayers.Length; }
        }

        public BattleTeam GetPlayerTeamAt(int id)
        {
            if (id >= 0 && id < mPlayers.Length)
            {
                return mPlayers[id];
            }
            return VoidTeam;
        }

        public BattleTeam Environment
        {
            get { return mEnvironment; }
        }

        public BattleTeam God
        {
            get { return mGod; }
        }

        public Pool.CollectableEnumerable<BattleTeam> EnumerateAllTeams()
        {
            Pool.CollectableEnumerable<BattleTeam> list = Pool.ObjectPool<Pool.CollectableEnumerable<BattleTeam>>.Allocate();

            if (mEnvironment.Type != BattleTeam.TeamType.Void)
            {
                list.Add(mEnvironment);
            }
            if (mGod.Type != BattleTeam.TeamType.Void)
            {
                list.Add(mGod);
            }

            for (int i = 0; i < mPlayers.Length; ++i)
            {
                list.Add(mPlayers[i]);
            }

            return list;
        }

        public int GetPlayersCount()
        {
            int count = 0;

            if (mEnvironment.Type != BattleTeam.TeamType.Void)
            {
                count += mEnvironment.Size;
            }
            if (mGod.Type != BattleTeam.TeamType.Void)
            {
                count += mGod.Size;
            }

            for (int i = 0; i < mPlayers.Length; ++i)
            {
                count += mPlayers[i].Size;
            }

            return count;
        }

        public Pool.CollectableEnumerable<BattleTeam> EnumeratePlayerTeams()
        {
            Pool.CollectableEnumerable<BattleTeam> list = Pool.ObjectPool<Pool.CollectableEnumerable<BattleTeam>>.Allocate();

            for (int i = 0; i < mPlayers.Length; ++i)
            {
                list.Add(mPlayers[i]);
            }

            return list;
        }

        public BattleTeam FindTeam(PlayerRole role)
        {
            int id = role.Raw.TeamId;
            if (mEnvironment.Type != BattleTeam.TeamType.Void && mEnvironment.TeamId == id)
            {
                return mEnvironment;
            }

            if (mGod.Type != BattleTeam.TeamType.Void && mGod.TeamId == id)
            {
                return mGod;
            }

            if (mPlayers.Length > 0)
            {
                id -= mPlayers[0].TeamId;
                if (id >= 0 && id < mPlayers.Length)
                {
                    return mPlayers[id];
                }
            }

            return VoidTeam;
        }

        public BattleTeam FindTeam(IDLong<IClient> playerId)
        {
            for (int i = 0; i < mPlayers.Length; ++i)
            {
                if (mPlayers[i].FindPlayer(playerId).IsValid)
                {
                    return mPlayers[i];
                }
            }
            return VoidTeam;
        }

        public PlayerRole FindPlayer(IDLong<IClient> playerId)
        {
            foreach (var team in EnumeratePlayerTeams())
            {
                PlayerRole role = team.FindPlayer(playerId);
                if (role.IsValid)
                {
                    return role;
                }
            }
            return PlayerRole.InvalidRole;
        }

        public IDLong<IClient> FindPlayer(PlayerRole role)
        {
            foreach (var team in EnumeratePlayerTeams())
            {
                IDLong<IClient> id = team.FindPlayer(role);
                if (role.IsValid)
                {
                    return id;
                }
            }
            return IDLong<IClient>.Invalid;
        }

        public int GetRoleIndex(PlayerRole role)
        {
            int index = -1;
            foreach (var r in AllRoles())
            {
                index += 1;
                if (r == role)
                {
                    break;
                }
            }

            return index;
        }

        public Pool.CollectableEnumerable<PlayerRole> AllRoles()
        {
            Pool.CollectableEnumerable<PlayerRole> list = Pool.ObjectPool<Pool.CollectableEnumerable<PlayerRole>>.Allocate();
            mEnvironment.EnumerateRoles(list);
            mGod.EnumerateRoles(list);
            for (int i = 0; i < mPlayers.Length; ++i)
            {
                mPlayers[i].EnumerateRoles(list);
            }
            return list;
        }

        public Pool.CollectableEnumerable<PlayerRole> AllPlayerRoles()
        {
            Pool.CollectableEnumerable<PlayerRole> list = Pool.ObjectPool<Pool.CollectableEnumerable<PlayerRole>>.Allocate();
            for (int i = 0; i < mPlayers.Length; ++i)
            {
                mPlayers[i].EnumerateRoles(list);
            }
            return list;
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("BattleTopology:\n");
            sb.AppendFormat("Environment: {0}\n", mEnvironment.Type != BattleTeam.TeamType.Void ? "on" : "off");
            sb.AppendFormat("God: {0}\n", mGod.Type != BattleTeam.TeamType.Void ? "on" : "off");
            sb.AppendFormat("Monsters coef's hp:'{0}' ad:'{1}' ap:'{2}'\n", mHpCoef, mAdCoef, mApCoef);

            for (int i = 0; i < mPlayers.Length; ++i)
            {
                sb.Append("\nTeam#" + (i + 1) + ": ");
                foreach (var player in mPlayers[i].AllClients())
                {
                    sb.Append(player.SerializeTo() + " ");
                }
            }

            return sb.ToString();
        }

        // r1 going to attack r2
        public bool IsEnemy(PlayerRole r1, PlayerRole r2)
        {
            return r1.Raw.TeamId != r2.Raw.TeamId;
        }

        public bool IsAlly(PlayerRole r1, PlayerRole r2)
        {
            return r1.Raw.TeamId == r2.Raw.TeamId;
        }

        public bool IsNeutral(PlayerRole r1, PlayerRole r2)
        {
            return false;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            mEnvironment.Serialize(dst);
            mGod.Serialize(dst);

            if (dst.isReader)
            {
                byte count = 0;
                dst.Add(ref count);

                mPlayers = new BattleTeamImpl[count];

                for (int i = 0; i < count; ++i)
                {
                    mPlayers[i] = new BattleTeamImpl(this);
                    mPlayers[i].Serialize(dst);
                }
            }
            else
            {
                byte count = (byte)mPlayers.Length;
                dst.Add(ref count);

                for (int i = 0; i < count; ++i)
                {
                    mPlayers[i].Serialize(dst);
                }
            }

            dst.Add(ref mHpCoef);
            dst.Add(ref mAdCoef);
            dst.Add(ref mApCoef);

            dst.Add(ref EventUnitId);
            dst.Add(ref EventUnitSkinId);

            byte tmp = (byte)EventStrategyType;
            dst.Add(ref tmp);
            EventStrategyType = (StrategyType)tmp;

            dst.Add(ref EventStrategyTemplateId);

            return true;
        }

        #region PLT battle params
        private float mHpCoef;
        private float mAdCoef;
        private float mApCoef;

        public void SetMobHpCoef(float hpCoef)
        {
            mHpCoef = hpCoef;
        }

        public void SetMobAdCoef(float adCoef)
        {
            mAdCoef = adCoef;
        }

        public void SetMobApCoef(float apCoef)
        {
            mApCoef = apCoef;
        }

        public float GetMobHpCoef()
        {
            return mHpCoef;
        }

        public float GetMobAdCoef()
        {
            return mAdCoef;
        }

        public float GetMobApCoef()
        {
            return mApCoef;
        }
        #endregion //PLT battle params

        #region PLT event unit
        public short EventUnitId;
        public short EventUnitSkinId;
        public StrategyType EventStrategyType;
        public int EventStrategyTemplateId;
        #endregion //PLT event unit
    }
}