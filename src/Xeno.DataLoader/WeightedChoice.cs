namespace Xeno.DataLoader
{
    internal sealed class WeightedChoice<T>
    {
        public WeightedChoice(T value, double weight)
        {
            Value = value;
            Weight = weight;
        }

        public T Value { get; }

        public double Weight { get; }
    }
}
