namespace Xeno.Client.Services
{
    /// <summary>
    /// Result of a settings mutation inside <see cref="XenoWorkspace"/>: the value to return and whether the provider
    /// and model inventory changed (which asks a running worker to re-advertise).
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    internal sealed class MutationResult<T>
    {
        /// <summary>
        /// Value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// True when the inventory changed.
        /// </summary>
        public bool InventoryChanged { get; }

        /// <summary>
        /// Create a result.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="inventoryChanged">True when the inventory changed.</param>
        public MutationResult(T value, bool inventoryChanged)
        {
            Value = value;
            InventoryChanged = inventoryChanged;
        }
    }
}
