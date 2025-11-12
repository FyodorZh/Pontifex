using Serializer.BinarySerializer;
namespace Shared
{
    namespace CommonData
    {
        public interface IMVPCoefficients
        {
            float Hero { get; }
            float Creep { get; }
            float Tower { get; }
            float Neutral { get; }
        }

        public class MVPCoefficients : IMVPCoefficients, IDataStruct
        {
            public float Hero = 1;
            public float Creep = 1;
            public float Tower = 1;
            public float Neutral = 1;

            float IMVPCoefficients.Hero { get { return Hero; } }
            float IMVPCoefficients.Creep { get { return Creep; } }
            float IMVPCoefficients.Tower { get { return Tower; } }
            float IMVPCoefficients.Neutral { get { return Neutral; } }

            public virtual bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref Hero);
                dst.Add(ref Creep);
                dst.Add(ref Tower);
                dst.Add(ref Neutral);

                return true;
            }
        }

        public interface IMVPBalanceCoefficients : IMVPCoefficients
        {
            float Balance { get; }
            float KillModifier { get; }
            float DeathModifier { get; }
            float AssistModifier { get; }
            bool UseHeroDamage { get; }
            float KDAMaximum { get; }
            float MinValueMVP { get; }
        }

        public class MVPBalanceCoefficients : MVPCoefficients, IMVPBalanceCoefficients
        {
            public bool UseHeroDamage = true;
            public float Balance = 1;
            public float KillModifier = 1;
            public float DeathModifier = 1;
            public float AssistModifier = 1;
            public float KDAMaximum = 1;
            public float MinValueMVP = 1;

            float IMVPBalanceCoefficients.Balance { get { return Balance; } }
            float IMVPBalanceCoefficients.KillModifier { get { return KillModifier; } }
            float IMVPBalanceCoefficients.DeathModifier { get { return DeathModifier; } }
            float IMVPBalanceCoefficients.AssistModifier { get { return AssistModifier; } }
            float IMVPBalanceCoefficients.KDAMaximum { get { return KDAMaximum; } }
            float IMVPBalanceCoefficients.MinValueMVP { get { return MinValueMVP; } }
            bool IMVPBalanceCoefficients.UseHeroDamage { get { return UseHeroDamage; } }

            public override bool Serialize(IBinarySerializer dst)
            {
                base.Serialize(dst);

                dst.Add(ref UseHeroDamage);
                dst.Add(ref Balance);
                dst.Add(ref KillModifier);
                dst.Add(ref DeathModifier);
                dst.Add(ref AssistModifier);
                dst.Add(ref KDAMaximum);
                dst.Add(ref MinValueMVP);

                return true;
            }
        }
    }
}