namespace Xeno.Client
{
    using System;

    /// <summary>
    /// Per-model routing override for the local inference listener.
    /// Not thread safe; settings objects are loaded, edited, and saved by one caller at a time.
    /// </summary>
    public class LocalInferenceRouteRule
    {
        private string _Model = String.Empty;

        /// <summary>
        /// Model name the rule applies to. Matching is exact after normalization, so "llama3.1" and "llama3.1:latest" are the same model. Never null.
        /// </summary>
        public string Model
        {
            get
            {
                return _Model;
            }
            set
            {
                _Model = (value ?? String.Empty).Trim();
            }
        }

        /// <summary>
        /// Target for the model. Default is <see cref="LocalInferenceRouteTargetEnum.Local"/>.
        /// </summary>
        public LocalInferenceRouteTargetEnum Target { get; set; } = LocalInferenceRouteTargetEnum.Local;

        /// <summary>
        /// Provider name when <see cref="Target"/> is <see cref="LocalInferenceRouteTargetEnum.Local"/>. Ignored for network targets. Null when unset.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Whether a request may fall back to the Xeno network when the local provider is unavailable before any response byte is sent. Default is false.
        /// </summary>
        public bool FallbackToNetwork { get; set; } = false;
    }
}
