using Serializer.BinarySerializer;
using System;

namespace Shared.Battle
{
    public struct PlayerRole : IEquatable<PlayerRole>, IConvertibleTo<byte>, IDataStruct
    {
        public struct RawData
        {
            public int TeamId;
            public int PlayerIdInTeam;
        }

        private int mTeamId;
        private int mPlayerIdInTeam;

        public static PlayerRole InvalidRole = new PlayerRole(-1, -1);

        public PlayerRole(int teamId, int playerInTeamId)
        {
            mTeamId = teamId;
            DBG.Diagnostics.Assert(teamId >= -1 && teamId < 8, "team id should fit into bit mask (0-8 range)");
            mPlayerIdInTeam = playerInTeamId;
        }

        public RawData Raw
        {
            get
            {
                return new RawData() { TeamId = mTeamId, PlayerIdInTeam = mPlayerIdInTeam };
            }
        }

        public bool IsValid
        {
            get { return this != InvalidRole; }
        }

        public bool IsSameTeam(PlayerRole other)
        {
            return mTeamId == other.mTeamId;
        }

        public override string ToString()
        {
            if (this == InvalidRole)
            {
                return "[role(invalid)]";
            }
            return String.Format("[role({0};{1})]", mTeamId, mPlayerIdInTeam);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            if (dst.isReader)
            {
                byte data = 0;
                dst.Add(ref data);
                ConvertFrom(data);
            }
            else
            {
                byte data = ConvertTo();
                dst.Add(ref data);
            }
            return true;
        }

        public override int GetHashCode()
        {
            return (mTeamId << 16) + mPlayerIdInTeam;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PlayerRole))
            {
                return false;
            }
            return Equals((PlayerRole)obj);
        }

        public bool Equals(PlayerRole role)
        {
            return mTeamId == role.mTeamId && mPlayerIdInTeam == role.mPlayerIdInTeam;
        }

        public static bool operator ==(PlayerRole role1, PlayerRole role2)
        {
            return role1.mTeamId == role2.mTeamId && role1.mPlayerIdInTeam == role2.mPlayerIdInTeam;
        }

        public static bool operator !=(PlayerRole role1, PlayerRole role2)
        {
            return role1.mTeamId != role2.mTeamId || role1.mPlayerIdInTeam != role2.mPlayerIdInTeam;
        }

        public static void Write(PlayerRole role, IBinarySerializer serializer)
        {
            DBG.Diagnostics.Assert(role.mTeamId < 16 && role.mPlayerIdInTeam < 16);
            var data = (byte)(role.mTeamId << 4 | role.mPlayerIdInTeam);
            serializer.Add(ref data);
        }

        public static PlayerRole Read(IBinarySerializer serializer)
        {
            byte rawData = 0;
            serializer.Add(ref rawData);

            int intRawData = rawData;
            if (intRawData == 0xFF)
            {
                return InvalidRole;
            }
            return new PlayerRole(intRawData >> 4, intRawData & 0x0F);
        }

        public byte ConvertTo()
        {
            return (byte)(mTeamId << 4 | mPlayerIdInTeam);
        }

        public void ConvertFrom(byte value)
        {
            mTeamId = value >> 4;
            mPlayerIdInTeam = value & 0x0F;
        }
    }
}