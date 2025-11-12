using System;
using Serializer.BinarySerializer;
using Serializer.Extensions;

namespace Shared.Battle
{
    namespace Common
    {
        public interface IInitRestrictions : IDataStruct
        {
            bool NoSelfMove { get; }
            bool NoSelfRotate { get; }
            bool NoForceMove { get; }
            bool Uncontrollable { get; }
            bool Invulnerable { get; }
            bool NoCast { get; }
            bool Untargetable { get; }
            bool Unaffectable { get; }

            bool HasRestrictions { get; }

            DeltaTime LifeTime { get; }
        }

        public class InitRestrictions : IInitRestrictions
        {
            public enum ParamFlags : byte
            {
                NoSelfMove,
                NoSelfRotate,
                NoForceMove,
                Invulnerable,
                NoCast,
                Uncontrollable,
                Untargetable,
                Unaffectable,

                _COUNT
            }

            private uint mFlags;
            private DeltaTime mLifeTime;

            public uint Flags { get { return mFlags; } }
            public bool HasRestrictions { get { return mFlags != 0; } }
            public DeltaTime LifeTime { get { return mLifeTime; } set { mLifeTime = value; } }

            public void Init(InitRestrictions other)
            {
                if (other != null)
                {
                    Init(other.mFlags, other.mLifeTime);
                }
                else
                {
                    Clear();
                }
            }

            public void Init(uint flags, DeltaTime lifetime)
            {
                mFlags = flags;
                mLifeTime = lifetime;
            }

            public void Clear()
            {
                Init(0, DeltaTime.Zero);
            }

            public void SetGodRestrictionsInfinity()
            {
                Init(0xFFFFFFFF, DeltaTime.Infinity);
                NoCast = false;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mFlags);
                dst.AddDeltaTime(ref mLifeTime);
                return true;
            }

            public bool NoSelfMove
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.NoSelfMove);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.NoSelfMove, value);
                }
            }

            public bool NoSelfRotate
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.NoSelfRotate);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.NoSelfRotate, value);
                }
            }

            public bool NoForceMove
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.NoForceMove);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.NoForceMove, value);
                }
            }

            public bool Invulnerable
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.Invulnerable);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.Invulnerable, value);
                }
            }

            public bool NoCast
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.NoCast);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.NoCast, value);
                }
            }

            public bool Uncontrollable
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.Uncontrollable);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.Uncontrollable, value);
                }
            }

            public bool Untargetable
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.Untargetable);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.Untargetable, value);
                }
            }

            public bool Unaffectable
            {
                get
                {
                    return CFlags.IsBit(mFlags, (byte)ParamFlags.Unaffectable);
                }
                set
                {
                    CFlags.ChangeBit(ref mFlags, (byte)ParamFlags.Unaffectable, value);
                }
            }
        }
    }
}
