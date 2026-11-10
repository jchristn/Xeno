namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Resolves concurrency limits from the global, provider, and model settings.
    /// A model's limit caps that model on its provider; the provider limit caps all of the provider's models together; the global default applies where a provider sets no limit.
    /// Thread safe; the class is stateless.
    /// </summary>
    public static class ConcurrencyLimitResolver
    {
        /// <summary>
        /// Resolve limits for a model on a provider.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="provider">Provider settings.</param>
        /// <param name="model">Model name. Null or empty resolves only the provider limit.</param>
        /// <returns>Resolved limits.</returns>
        /// <exception cref="ArgumentNullException">settings or provider is null.</exception>
        public static ConcurrencyLimits Resolve(ClientSettings settings, ClientRunnerSettings provider, string? model)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(provider);
            ConcurrencyLimits limits = new ConcurrencyLimits
            {
                Provider = provider.Name ?? String.Empty,
                Model = model ?? String.Empty,
                ProviderLimit = provider.MaxConcurrentRequests ?? settings.DefaultMaxConcurrentRequests,
                ProviderLimitSource = provider.MaxConcurrentRequests.HasValue ? "provider" : "global"
            };

            if (!String.IsNullOrWhiteSpace(model))
            {
                ClientModelSettings? configured = (provider.Models ?? new List<ClientModelSettings>())
                    .FirstOrDefault(item => item != null && LocalModelNameNormalizer.AreEqual(item.Name, model));
                limits.ModelLimit = configured?.MaxConcurrentRequests;
            }

            return limits;
        }

        /// <summary>
        /// Total capacity this machine offers the Xeno network: the sum of provider limits for providers shared with the network.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Capacity, zero when no provider is shared.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static int NetworkCapacity(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return settings.Providers
                .Where(item => item != null && item.ShareWithNetwork)
                .Sum(item => item.MaxConcurrentRequests ?? settings.DefaultMaxConcurrentRequests);
        }
    }
}
