using System;
using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.Battle
{
    public class BattleTeam : IEquatable<BattleTeam>
    {
        public enum TeamType
        {
            Void,
            Environment,
            God,
            Players
        }

        protected int mTeamId;
        protected IDLong<IClient>[] mPlayers;

        public TeamType Type { get; private set; }

        public BattleTopology Owner { get; private set; }

        protected BattleTeam(BattleTopology owner, TeamType type, int teamId, IDLong<IClient>[] players)
        {
            Owner = owner;
            Type = type;

            mTeamId = teamId;
            mPlayers = players;
        }

        public int Size
        {
            get
            {
                switch (Type)
                {
                    case TeamType.Environment:
                    case TeamType.God:
                        return 1;
                    case TeamType.Players:
                        return mPlayers.Length;
                    default:
                        return 0;
                }
            }
        }

        public Pool.CollectableEnumerable<PlayerRole> AllRoles()
        {
            Pool.CollectableEnumerable<PlayerRole> list = Pool.ObjectPool<Pool.CollectableEnumerable<PlayerRole>>.Allocate();
            AllRoles(list);
            return list;
        }

        public Pool.CollectableEnumerable<IDLong<IClient>> AllClients()
        {
            Pool.CollectableEnumerable<IDLong<IClient>> list = Pool.ObjectPool<Pool.CollectableEnumerable<IDLong<IClient>>>.Allocate();
            AllClients(list);
            return list;
        }

        public PlayerRole FirstRole
        {
            get
            {
                if (Type != TeamType.Void)
                {
                    return new PlayerRole(mTeamId, 0);
                }
                return PlayerRole.InvalidRole;
            }
        }

        public PlayerRole FindPlayer(IDLong<IClient> playerId)
        {
            switch (Type)
            {
                case TeamType.Players:
                    for (int i = 0; i < mPlayers.Length; ++i)
                    {
                        if (playerId == mPlayers[i])
                        {
                            return new PlayerRole(mTeamId, i);
                        }
                    }
                    return PlayerRole.InvalidRole;
                case TeamType.Environment:
                case TeamType.God:
                    if (!playerId.IsValid)
                    {
                        return new PlayerRole(mTeamId, 0);
                    }
                    return PlayerRole.InvalidRole;
                default:
                    return PlayerRole.InvalidRole;
            }
        }

        public IDLong<IClient> FindPlayer(PlayerRole role)
        {
            var data = role.Raw;
            if (data.TeamId != mTeamId || Type != TeamType.Players || (data.PlayerIdInTeam < 0 || data.PlayerIdInTeam >= mPlayers.Length))
            {
                return IDLong<IClient>.Invalid;
            }

            return mPlayers[data.PlayerIdInTeam];
        }

        public bool HasPlayer(PlayerRole role)
        {
            var data = role.Raw;
            return data.TeamId == mTeamId && Type == TeamType.Players && data.PlayerIdInTeam >= 0 && data.PlayerIdInTeam < mPlayers.Length;
        }

        public bool HasRole(PlayerRole role)
        {
            switch (Type)
            {
                case TeamType.Players:
                    return HasPlayer(role);
                case TeamType.Environment:
                case TeamType.God:
                    var data = role.Raw;
                    return data.TeamId == mTeamId && data.PlayerIdInTeam == 0;
                default:
                    return false;
            }
        }

        protected void AllRoles(Pool.CollectableEnumerable<PlayerRole> list)
        {
            switch (Type)
            {
                case TeamType.Players:
                    for (int i = 0; i < mPlayers.Length; ++i)
                    {
                        list.Add(new PlayerRole(mTeamId, i));
                    }
                    break;
                case TeamType.God:
                case TeamType.Environment:
                    list.Add(new PlayerRole(mTeamId, 0));
                    break;
            }
        }

        protected void AllClients(Pool.CollectableEnumerable<IDLong<IClient>> list)
        {
            switch (Type)
            {
                case TeamType.Players:
                    for (int i = 0; i < mPlayers.Length; ++i)
                    {
                        list.Add(mPlayers[i]);
                    }
                    break;
                case TeamType.God:
                case TeamType.Environment:
                    list.Add(IDLong<IClient>.Invalid);
                    break;
            }
        }

        protected void Serialize(IBinarySerializer dst)
        {
            byte type = (byte)Type;
            dst.Add(ref type);
            Type = (TeamType)type;

            dst.Add(ref mTeamId);

            if (dst.isReader)
            {
                byte count = 0;
                dst.Add(ref count);

                IDLong<IClient> role = new IDLong<IClient>();

                mPlayers = new IDLong<IClient>[count];
                for (int i = 0; i < count; ++i)
                {
                    dst.AddId(ref role);
                    mPlayers[i] = role;
                }
            }
            else
            {
                byte count = (byte)mPlayers.Length;
                dst.Add(ref count);

                for (int i = 0; i < count; ++i)
                {
                    dst.AddId(ref mPlayers[i]);
                }
            }
        }

        #region Equals

        public bool Equals(BattleTeam other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return mTeamId == other.mTeamId && Equals(Owner, other.Owner);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BattleTeam)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (mTeamId * 397) ^ (Owner != null ? Owner.GetHashCode() : 0);
            }
        }

        public static bool operator ==(BattleTeam left, BattleTeam right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BattleTeam left, BattleTeam right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}
