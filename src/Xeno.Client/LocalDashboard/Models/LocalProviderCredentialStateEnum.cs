namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local provider credential display state.
    /// </summary>
    public enum LocalProviderCredentialStateEnum
    {
        /// <summary>
        /// No credential is configured.
        /// </summary>
        NotConfigured = 0,

        /// <summary>
        /// A credential is configured but not exposed.
        /// </summary>
        Configured = 1,

        /// <summary>
        /// A credential value was redacted from output.
        /// </summary>
        Redacted = 2
    }
}
