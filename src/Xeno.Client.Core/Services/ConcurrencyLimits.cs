namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Effective concurrency limits for one model on one provider.
    /// </summary>
    public class ConcurrencyLimits
    {
        /// <summary>
        /// Provider name. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model name, or empty when only the provider limit was resolved. Never null.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Limit for all models on the provider combined. Minimum 1.
        /// </summary>
        public int ProviderLimit { get; set; } = 4;

        /// <summary>
        /// Where the provider limit came from: "provider" or "global". Never null.
        /// </summary>
        public string ProviderLimitSource { get; set; } = "global";

        /// <summary>
        /// Limit for this model on this provider. Null when the model inherits the provider limit.
        /// </summary>
        public int? ModelLimit { get; set; } = null;

        /// <summary>
        /// The limit a single model can actually reach: the smaller of the model and provider limits.
        /// </summary>
        public int EffectiveModelLimit
        {
            get
            {
                return ModelLimit.HasValue ? Math.Min(ModelLimit.Value, ProviderLimit) : ProviderLimit;
            }
        }

        /// <summary>
        /// Whether the model limit is above the provider limit and therefore has no effect.
        /// </summary>
        public bool ModelLimitExceedsProvider
        {
            get
            {
                return ModelLimit.HasValue && ModelLimit.Value > ProviderLimit;
            }
        }
    }
}
