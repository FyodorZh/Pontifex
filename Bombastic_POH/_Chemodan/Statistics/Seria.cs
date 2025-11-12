using System;
using System.Collections.Generic;

namespace Shared.Statistics
{
    public class Seria
    {
        private int mCount;
        private double mSum;
        private double mSumOfSquares;

        private double mMin = double.MaxValue;
        private double mMax = double.MinValue;

        private readonly List<double> mValues;

        public Seria(bool collectValues)
        {
            if (collectValues)
            {
                mValues = new List<double>();
            }
        }

        public void Add(double value)
        {
            mCount += 1;
            mSum += value;
            mSumOfSquares += value * value;

            mMin = Math.Min(mMin, value);
            mMax = Math.Max(mMax, value);

            if (mValues != null)
            {
                mValues.Add(value);
            }
        }

        public int Count
        {
            get { return mCount; }
        }

        /// <summary>
        /// Математическое ожидание
        /// </summary>
        public double Expectation
        {
            get
            {
                if (mCount > 0)
                {
                    return mSum / mCount;
                }
                return 0;
            }
        }

        /// <summary>
        /// Дисперсия
        /// https://en.wikipedia.org/wiki/Variance
        /// </summary>
        public double Variance
        {
            get
            {
                if (mCount > 0)
                {
                    double expectation = Expectation;
                    return mSumOfSquares / mCount - expectation * expectation;
                }
                return 0;
            }
        }

        /// <summary>
        /// Среднеквадратическое отклонение (без поправки n/(n-1))
        /// https://en.wikipedia.org/wiki/Standard_deviation
        /// </summary>
        public double Deviation
        {
            get
            {
                return Math.Sqrt(Variance);
            }
        }

        public IEnumerable<double> Values
        {
            get
            {
                if (mValues == null)
                {
                    throw new InvalidOperationException("Values were not collected, use Seria(true) constructor");
                }

                return mValues;
            }
        }

        public struct HistogramElement
        {
            public readonly double From;
            public readonly double Till;
            public readonly int Count;

            internal HistogramElement(double from, double till, int count)
            {
                From = from;
                Till = till;
                Count = count;
            }
        }

        public IEnumerable<HistogramElement> Histogram(int elementNumber)
        {
            if (mValues == null)
            {
                throw new InvalidOperationException("Values were not collected, use Seria(true) constructor");
            }

            double[] values = mValues.ToArray();

            Array.Sort(values);

            int pos = 0;
            for (int i = 0; i < elementNumber; ++i)
            {
                double from = mMin + (mMax - mMin) * i / elementNumber;
                double till = mMin + (mMax - mMin) * (i + 1) / elementNumber;

                int count = 0;
                while (pos < values.Length && values[pos] <= till)
                {
                    ++count;
                    ++pos;
                }

                yield return new HistogramElement(from, till, count);
            }
        }
    }
}
