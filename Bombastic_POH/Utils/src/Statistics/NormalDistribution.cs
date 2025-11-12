using System;

namespace Shared.Statistics
{
    public class NormalDistribution
    {
        private readonly double mExpectation;

        private readonly double mA;
        private readonly double mB;

        /// <summary>
        /// Нормальное распределение
        /// https://en.wikipedia.org/wiki/Normal_distribution
        /// </summary>
        /// <param name="expectation"> Математическое ожидание </param>
        /// <param name="variance"> Дисперсия </param>
        public NormalDistribution(double expectation, double variance)
        {
            mExpectation = expectation;

            mA = 1.0 / Math.Sqrt(2.0 * Math.PI * variance);
            mB = -1.0 / (2.0 * variance);
        }

        public double F(double x)
        {
            double d = x - mExpectation;
            return mA * Math.Exp(mB * d * d);
        }
    }
}
