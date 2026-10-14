namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Capability advertised for a local model.
    /// </summary>
    public enum LocalModelCapabilityEnum
    {
        /// <summary>
        /// Completion capability.
        /// </summary>
        Completion = 0,

        /// <summary>
        /// Embedding capability.
        /// </summary>
        Embedding = 1,

        /// <summary>
        /// Completion and embedding capabilities.
        /// </summary>
        Both = 2
    }
}
