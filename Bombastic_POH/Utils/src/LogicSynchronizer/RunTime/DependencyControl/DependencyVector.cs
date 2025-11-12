using Shared.Buffer;

namespace Shared.LogicSynchronizer
{
    public class DependencyVector<TRole>
        where TRole : struct, IConvertibleTo<byte>, System.IEquatable<TRole>
    {
        private readonly TinyDictionary<TRole, int> mGenerations = new TinyDictionary<TRole, int>();
        private TRole mRole = default(TRole);

        public TRole Role { get { return mRole; } }

        public DependencyVector(TRole role)
        {
            mRole = role;
            mGenerations[mRole] = 0;
        }

        public void IncDependency()
        {
            mGenerations[mRole] += 1;
        }

        public int GetGeneration(TRole role)
        {
            int generation;
            mGenerations.TryGetValue(role, out generation);
            return generation;
        }

        public bool ContainsDependencies(DependencyVector<TRole> other)
        {
            foreach (var kv in other.mGenerations)
            {
                if (!kv.Key.Equals(other.mRole))
                {
                    int thisValue;
                    mGenerations.TryGetValue(kv.Key, out thisValue);
                    if (kv.Value > thisValue)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void CombineWith(DependencyVector<TRole> other)
        {
            TRole remoteObserver = other.mRole;

            foreach (var kv in other.mGenerations)
            {
                TRole otherRole = kv.Key;
                int generation = kv.Value;

                int oldOtherGeneration;
                if (mGenerations.TryGetValue(otherRole, out oldOtherGeneration))
                {
                    if (!otherRole.Equals(mRole))
                    {// Если отчёт не обо мне

                        if (remoteObserver.Equals(otherRole)) // если мессадж от владельца роли
                        {
                            if (generation != oldOtherGeneration + 1)
                            {
                                Log.e("Wrong message generation. '{0}' sent about self. Received '{1}', expected '{2}'", remoteObserver, generation, oldOtherGeneration + 1);
                            }
                        }
                        else // если мессадж не от владельца роли
                        {
                            if (oldOtherGeneration < generation)
                            {
                                Log.e("Wrong message generation. Received from '{0}'. About '{1}'. Received '{2}', expected no more than '{3}'", remoteObserver, otherRole, generation, oldOtherGeneration + 1);
                            }
                        }
                        mGenerations[otherRole] = System.Math.Max(oldOtherGeneration, generation);
                    }
                    else
                    {// Если отчёт обо мне
                        if (oldOtherGeneration < generation)
                        {
                            Log.e("Wrong message generation. Received from '{0}' about me '{1}'.", remoteObserver, otherRole);
                        }
                    }
                }
                else
                {
                    if (!otherRole.Equals(remoteObserver) || generation != 1)
                    {
                        Log.e("Wrong first message. It must be sent by owner with zero generation. Received from '{0}' about '{1}' with generation '{2}'", remoteObserver, otherRole, generation);
                    }
                    mGenerations.Add(otherRole, generation);
                }
            }
        }

        public void WriteTo(IMemoryBufferHolder dst)
        {
            using (var bufferAccessor = dst.Acquire().ExposeAccessorOnce())
            {
                var buffer = bufferAccessor.Buffer;

                foreach (var kv in mGenerations)
                {
                    buffer.PushInt32(kv.Value);
                    buffer.PushByte(kv.Key.ConvertTo());
                }

                buffer.PushByte((byte)mGenerations.Count);
                buffer.PushByte(mRole.ConvertTo());
            }
        }

        public void ReadFrom(IMemoryBufferHolder src)
        {
            using (var bufferAccessor = src.Acquire().ExposeAccessorOnce())
            {
                var buffer = bufferAccessor.Buffer;

                byte roleByte;
                if (!buffer.PopFirst().AsByte(out roleByte))
                {
                    throw new System.Exception("mRole");
                }
                mRole.ConvertFrom(roleByte);

                byte count;
                if (!buffer.PopFirst().AsByte(out count))
                {
                    throw new System.Exception("count");
                }

                mGenerations.Clear();

                for (int i = 0; i < count; ++i)
                {
                    if (!buffer.PopFirst().AsByte(out roleByte))
                    {
                        throw new System.Exception("role");
                    }
                    TRole role = default(TRole);
                    role.ConvertFrom(roleByte);

                    int gen;
                    if (!buffer.PopFirst().AsInt32(out gen))
                    {
                        throw new System.Exception("gen");
                    }

                    mGenerations.Add(role, gen);
                }
            }
        }
    }
}
