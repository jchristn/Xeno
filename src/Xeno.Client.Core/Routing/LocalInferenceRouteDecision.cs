namespace Xeno.Client.Routing
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The router's answer for one model: where the request goes and why.
    /// </summary>
    public class LocalInferenceRouteDecision
    {
        /// <summary>
        /// Model as the caller sent it. Never null.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Model after Ollama-style normalization (":latest" added when untagged). Never null.
        /// </summary>
        public string NormalizedModel { get; set; } = String.Empty;

        /// <summary>
        /// Target kind.
        /// </summary>
        public LocalInferenceRouteTargetKindEnum Target { get; set; } = LocalInferenceRouteTargetKindEnum.Network;

        /// <summary>
        /// Chosen local provider, when <see cref="Target"/> is <see cref="LocalInferenceRouteTargetKindEnum.Local"/>. Null otherwise.
        /// </summary>
        [JsonIgnore]
        public ClientRunnerSettings? Provider { get; set; } = null;

        /// <summary>
        /// Chosen local provider name, or empty. Never null.
        /// </summary>
        public string ProviderName
        {
            get
            {
                return Provider?.Name ?? String.Empty;
            }
        }

        /// <summary>
        /// Stable reason code: "rule", "preferLocal", "notLocal", "localUnhealthy", "networkOnlyMode", "localOnlyMode",
        /// "ruleProviderUnavailable", or "noModel". Front ends translate the code. Never null.
        /// </summary>
        public string Reason { get; set; } = "notLocal";

        /// <summary>
        /// Whether the matched rule allows falling back to the Xeno network when the local provider fails before any response byte.
        /// </summary>
        public bool FallbackToNetwork { get; set; } = false;

        /// <summary>
        /// True when the router already chose the network because the rule's local provider is down or missing and the rule allows fallback.
        /// Such requests carry X-Xeno-Fallback and ask the server not to route back to this client.
        /// </summary>
        public bool IsFallback { get; set; } = false;

        /// <summary>
        /// Whether the chosen local provider speaks the caller's API shape. Always true for network targets.
        /// </summary>
        public bool ShapeSupported { get; set; } = true;

        /// <summary>
        /// Value for the X-Xeno-Route response header: "local:&lt;provider&gt;", "network", or "unavailable". Never null.
        /// </summary>
        public string RouteHeader
        {
            get
            {
                switch (Target)
                {
                    case LocalInferenceRouteTargetKindEnum.Local:
                        return "local:" + ProviderName;
                    case LocalInferenceRouteTargetKindEnum.Network:
                        return "network";
                    default:
                        return "unavailable";
                }
            }
        }
    }
}
