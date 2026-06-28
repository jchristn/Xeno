namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;

    internal sealed class RandomSource
    {
        private readonly Random _Random;

        public RandomSource(int seed)
        {
            _Random = new Random(seed);
        }

        public double NextDouble()
        {
            return _Random.NextDouble();
        }

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            return _Random.Next(minimumInclusive, maximumExclusive);
        }

        public bool Chance(double probability)
        {
            return _Random.NextDouble() < Clamp(probability, 0, 1);
        }

        public double Gaussian(double mean, double standardDeviation)
        {
            double u1 = 1.0 - _Random.NextDouble();
            double u2 = 1.0 - _Random.NextDouble();
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + normal * standardDeviation;
        }

        public int Poisson(double lambda)
        {
            if (lambda <= 0) return 0;
            if (lambda > 30)
            {
                return Math.Max(0, (int)Math.Round(Gaussian(lambda, Math.Sqrt(lambda))));
            }

            double limit = Math.Exp(-lambda);
            int k = 0;
            double product = 1.0;
            do
            {
                k++;
                product *= _Random.NextDouble();
            }
            while (product > limit);

            return k - 1;
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            return items[_Random.Next(items.Count)];
        }

        public T PickWeighted<T>(IReadOnlyList<WeightedChoice<T>> choices)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            if (choices.Count == 0) throw new ArgumentException("Cannot pick from an empty weighted-choice list.", nameof(choices));

            double total = 0;
            foreach (WeightedChoice<T> choice in choices)
            {
                total += Math.Max(0, choice.Weight);
            }

            if (total <= 0) return choices[0].Value;

            double pick = _Random.NextDouble() * total;
            double cursor = 0;
            foreach (WeightedChoice<T> choice in choices)
            {
                cursor += Math.Max(0, choice.Weight);
                if (pick <= cursor) return choice.Value;
            }

            return choices[choices.Count - 1].Value;
        }

        public static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
