namespace Xeno.Core.Enums
{

    /// <summary>
    /// Client routing state.
    /// </summary>
    public enum ClientServiceStateEnum
    {
        /// <summary>
        /// Client is eligible for new routing.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Client only serves existing work.
        /// </summary>
        Draining = 1,

        /// <summary>
        /// Client is excluded from routing.
        /// </summary>
        Quarantined = 2
    }
}
