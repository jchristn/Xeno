namespace Xeno.Core.Enums
{

    /// <summary>
    /// Model access enforcement mode.
    /// </summary>
    public enum ModelAccessModeEnum
    {
        /// <summary>
        /// Skip model access policies.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Evaluate policies without blocking.
        /// </summary>
        Monitor = 1,

        /// <summary>
        /// Block denied requests.
        /// </summary>
        Enforce = 2
    }
}
