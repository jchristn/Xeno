namespace Xeno.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// Compatibility DTO for client settings files that used the legacy Runners property.
    /// </summary>
    internal sealed class ClientSettingsLegacyRunners
    {
        /// <summary>
        /// Legacy provider endpoint list.
        /// </summary>
        public List<ClientRunnerSettings> Runners { get; set; } = new List<ClientRunnerSettings>();
    }
}
