namespace Xeno.Client.Services
{
    /// <summary>
    /// Result of adding a provider endpoint.
    /// </summary>
    public class ProviderAddResult
    {
        /// <summary>
        /// True when the provider was added; false when an existing provider already used the same name or endpoint.
        /// </summary>
        public bool Added { get; set; } = false;

        /// <summary>
        /// The added provider, or the existing provider that blocked the add.
        /// </summary>
        public ClientRunnerSettings Provider { get; set; } = new ClientRunnerSettings();

        /// <summary>
        /// True when an existing provider has the same name; false when only the endpoint matched.
        /// </summary>
        public bool NameConflict { get; set; } = false;
    }
}
