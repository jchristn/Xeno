namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// Provider endpoint and model configuration operations shared by the CLI, the local dashboard, the desktop app,
    /// and the terminal app. Operations mutate the supplied <see cref="ClientSettings"/> instance; call
    /// <see cref="SaveInventory(ClientSettings)"/> to persist the change and ask a running worker to re-advertise.
    /// Not thread-safe: callers serialize access to a settings instance.
    /// </summary>
    public static class ProviderConfiguration
    {
        #region Public-Members

        /// <summary>
        /// API type name for Ollama providers.
        /// </summary>
        public const string OllamaApiType = "Ollama";

        /// <summary>
        /// API types offered by front ends when adding a provider.
        /// </summary>
        public static readonly IReadOnlyList<string> KnownApiTypes = new List<string> { "Ollama", "OpenAICompatible", "vLLM" };

        /// <summary>
        /// Minimum provider timeout in milliseconds.
        /// </summary>
        public const int MinimumTimeoutMs = 1000;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Find a provider by name (case-insensitive).
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="name">Provider name.</param>
        /// <returns>Provider, or null when not found.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static ClientRunnerSettings? FindProvider(ClientSettings settings, string? name)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(name)) return null;
            return settings.Runners.FirstOrDefault(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Find an existing provider that has the same name or the same endpoint as a candidate.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="candidate">Candidate provider.</param>
        /// <returns>Conflicting provider, or null.</returns>
        /// <exception cref="ArgumentNullException">Settings or candidate is null.</exception>
        public static ClientRunnerSettings? FindDuplicateProvider(ClientSettings settings, ClientRunnerSettings candidate)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(candidate);
            string candidateEndpoint = NormalizeEndpoint(candidate.Endpoint);
            foreach (ClientRunnerSettings existing in settings.Runners)
            {
                if (String.Equals(existing.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)) return existing;
                if (!String.IsNullOrWhiteSpace(candidateEndpoint) &&
                    String.Equals(NormalizeEndpoint(existing.Endpoint), candidateEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            return null;
        }

        /// <summary>
        /// Add a provider endpoint unless one with the same name or endpoint already exists.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="provider">Provider to add. Name, API type, and endpoint are required.</param>
        /// <returns>Add result.</returns>
        /// <exception cref="ArgumentNullException">Settings or provider is null.</exception>
        /// <exception cref="ArgumentException">A required field is missing, or model management was requested for a non-Ollama provider.</exception>
        public static ProviderAddResult AddProvider(ClientSettings settings, ClientRunnerSettings provider)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(provider);
            if (String.IsNullOrWhiteSpace(provider.Name)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(provider.ApiType)) throw new ArgumentException("Provider API type is required.");
            if (String.IsNullOrWhiteSpace(provider.Endpoint)) throw new ArgumentException("Provider endpoint is required.");
            if (provider.AllowModelManagement && !IsOllamaApiType(provider.ApiType))
            {
                throw new ArgumentException("Model management can only be enabled for Ollama providers.");
            }

            ClientRunnerSettings? duplicate = FindDuplicateProvider(settings, provider);
            if (duplicate != null)
            {
                return new ProviderAddResult
                {
                    Added = false,
                    Provider = duplicate,
                    NameConflict = String.Equals(duplicate.Name, provider.Name, StringComparison.OrdinalIgnoreCase)
                };
            }

            if (provider.Models == null) provider.Models = new List<ClientModelSettings>();
            settings.Runners.Add(provider);
            return new ProviderAddResult { Added = true, Provider = provider };
        }

        /// <summary>
        /// Apply a partial update to an existing provider.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="name">Name of the provider to update.</param>
        /// <param name="update">Changes to apply.</param>
        /// <returns>Updated provider.</returns>
        /// <exception cref="ArgumentNullException">Settings or update is null.</exception>
        /// <exception cref="ArgumentException">Name is missing, the new name is taken, the timeout is too small, or model management was requested for a non-Ollama provider.</exception>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        public static ClientRunnerSettings UpdateProvider(ClientSettings settings, string name, ProviderUpdateRequest update)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(update);
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Provider endpoint name is required.");
            ClientRunnerSettings runner = FindProvider(settings, name) ?? throw new KeyNotFoundException("Provider endpoint not found: " + name);
            if (!String.IsNullOrWhiteSpace(update.NewName)
                && !String.Equals(update.NewName, runner.Name, StringComparison.OrdinalIgnoreCase)
                && FindProvider(settings, update.NewName) != null)
            {
                throw new ArgumentException("Provider endpoint already exists: " + update.NewName);
            }

            if (update.TimeoutMs != null && update.TimeoutMs.Value < MinimumTimeoutMs)
            {
                throw new ArgumentException("Provider timeout must be at least " + MinimumTimeoutMs + " milliseconds.");
            }

            ValidateConcurrencyLimit(update.MaxConcurrentRequests, "Provider");

            string apiType = String.IsNullOrWhiteSpace(update.ApiType) ? runner.ApiType : update.ApiType;
            bool allowModelManagement = update.AllowModelManagement ?? runner.AllowModelManagement;
            if (allowModelManagement && !IsOllamaApiType(apiType))
            {
                throw new ArgumentException("Model management can only be enabled for Ollama providers.");
            }

            if (!String.IsNullOrWhiteSpace(update.NewName)) runner.Name = update.NewName;
            runner.ApiType = apiType;
            if (!String.IsNullOrWhiteSpace(update.Endpoint)) runner.Endpoint = update.Endpoint;
            if (update.TimeoutMs != null) runner.TimeoutMs = update.TimeoutMs.Value;
            if (update.ClearCredential) runner.Credential = null;
            else if (update.Credential != null) runner.Credential = update.Credential;
            runner.AllowModelManagement = IsOllamaApiType(runner.ApiType) && allowModelManagement;
            if (update.ShareWithNetwork.HasValue) runner.ShareWithNetwork = update.ShareWithNetwork.Value;
            if (update.RouteLocalRequests.HasValue) runner.RouteLocalRequests = update.RouteLocalRequests.Value;
            if (update.InheritMaxConcurrentRequests) runner.MaxConcurrentRequests = null;
            else if (update.MaxConcurrentRequests.HasValue) runner.MaxConcurrentRequests = update.MaxConcurrentRequests.Value;
            return runner;
        }

        /// <summary>
        /// Create or update a provider from a local dashboard request.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="request">Upsert request.</param>
        /// <param name="routeName">Provider name from the route, which takes precedence over the request name.</param>
        /// <returns>Created or updated provider.</returns>
        /// <exception cref="ArgumentNullException">Settings or request is null.</exception>
        /// <exception cref="ArgumentException">Name or endpoint is missing, the endpoint loops back to the local inference server, or model management was requested for a non-Ollama provider.</exception>
        public static ClientRunnerSettings UpsertProvider(ClientSettings settings, LocalProviderUpsertRequest request, string? routeName)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(request);
            string name = String.IsNullOrWhiteSpace(routeName) ? request.Name : routeName;
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(request.Endpoint)) throw new ArgumentException("Provider endpoint is required.");
            settings.ValidateProviderEndpointAgainstLocalInference(name, request.Endpoint);
            ClientRunnerSettings? provider = FindProvider(settings, name);
            if (provider == null)
            {
                provider = new ClientRunnerSettings { Name = name, Models = new List<ClientModelSettings>() };
                settings.Runners.Add(provider);
            }

            provider.ApiType = String.IsNullOrWhiteSpace(request.ApiType) ? provider.ApiType : request.ApiType;
            provider.Endpoint = request.Endpoint;
            provider.TimeoutMs = Math.Max(MinimumTimeoutMs, request.TimeoutMs);
            if (request.ClearCredential) provider.Credential = null;
            else if (!String.IsNullOrWhiteSpace(request.Credential)) provider.Credential = request.Credential;
            bool requestedModelManagement = request.AllowModelManagement ?? provider.AllowModelManagement;
            if (requestedModelManagement && !IsOllamaApiType(provider.ApiType))
            {
                throw new ArgumentException("Model management can only be enabled for Ollama providers.");
            }

            provider.AllowModelManagement = IsOllamaApiType(provider.ApiType) && requestedModelManagement;
            ValidateConcurrencyLimit(request.MaxConcurrentRequests, "Provider");
            if (request.ShareWithNetwork.HasValue) provider.ShareWithNetwork = request.ShareWithNetwork.Value;
            if (request.RouteLocalRequests.HasValue) provider.RouteLocalRequests = request.RouteLocalRequests.Value;
            provider.MaxConcurrentRequests = request.MaxConcurrentRequests;
            return provider;
        }

        /// <summary>
        /// Delete a provider and its models.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="name">Provider name.</param>
        /// <returns>Number of providers removed.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static int DeleteProvider(ClientSettings settings, string name)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(name)) return 0;
            return settings.Runners.RemoveAll(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Add a model to a provider.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="providerName">Provider name.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="supportsCompletions">Whether the model serves completions.</param>
        /// <param name="supportsEmbeddings">Whether the model serves embeddings.</param>
        /// <returns>Provider the model was added to.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="ArgumentException">A name is missing, the model already exists on the provider, or the model serves neither completions nor embeddings.</exception>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        public static ClientRunnerSettings AddModel(ClientSettings settings, string providerName, string modelName, bool supportsCompletions, bool supportsEmbeddings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            ClientRunnerSettings runner = FindProvider(settings, providerName) ?? throw new KeyNotFoundException("Provider endpoint not found: " + providerName);
            if (!supportsCompletions && !supportsEmbeddings) throw new ArgumentException("Model must support completions, embeddings, or both.");
            runner.Models.Add(new ClientModelSettings
            {
                Name = modelName,
                SupportsCompletions = supportsCompletions,
                SupportsEmbeddings = supportsEmbeddings
            });
            return runner;
        }

        /// <summary>
        /// Apply a partial update to a model, optionally renaming it or moving it to another provider.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="update">Changes to apply.</param>
        /// <returns>Update result.</returns>
        /// <exception cref="ArgumentNullException">Settings or update is null.</exception>
        /// <exception cref="ArgumentException">Model name is missing or ambiguous, the target name is taken, or the model would serve neither completions nor embeddings.</exception>
        /// <exception cref="KeyNotFoundException">The model or target provider does not exist.</exception>
        public static ModelUpdateResult UpdateModel(ClientSettings settings, ModelUpdateRequest update)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(update);
            if (String.IsNullOrWhiteSpace(update.Name)) throw new ArgumentException("Model name is required.");

            ClientRunnerSettings? sourceRunner = null;
            ClientModelSettings? model = null;
            int matches = 0;
            foreach (ClientRunnerSettings runner in settings.Runners)
            {
                if (!String.IsNullOrWhiteSpace(update.Provider) && !String.Equals(runner.Name, update.Provider, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (ClientModelSettings candidate in runner.Models)
                {
                    if (!String.Equals(candidate.Name, update.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    sourceRunner = runner;
                    model = candidate;
                    matches++;
                }
            }

            if (matches == 0 || sourceRunner == null || model == null) throw new KeyNotFoundException("Model not found: " + update.Name);
            if (matches > 1 && String.IsNullOrWhiteSpace(update.Provider)) throw new ArgumentException("Model name exists in multiple providers. Specify --provider.");

            ClientRunnerSettings targetRunner = sourceRunner;
            if (!String.IsNullOrWhiteSpace(update.NewProvider))
            {
                targetRunner = FindProvider(settings, update.NewProvider) ?? throw new KeyNotFoundException("Provider endpoint not found: " + update.NewProvider);
            }

            string targetModelName = String.IsNullOrWhiteSpace(update.NewName) ? model.Name : update.NewName;
            bool moving = !Object.ReferenceEquals(sourceRunner, targetRunner);
            bool renaming = !String.Equals(model.Name, targetModelName, StringComparison.OrdinalIgnoreCase);
            if ((moving || renaming) && targetRunner.Models.Exists(item => String.Equals(item.Name, targetModelName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Model already exists in provider endpoint '" + targetRunner.Name + "': " + targetModelName);
            }

            ValidateConcurrencyLimit(update.MaxConcurrentRequests, "Model");
            bool finalSupportsCompletions = update.SupportsCompletions ?? model.SupportsCompletions;
            bool finalSupportsEmbeddings = update.SupportsEmbeddings ?? model.SupportsEmbeddings;
            if (!finalSupportsCompletions && !finalSupportsEmbeddings) throw new ArgumentException("Model must support completions, embeddings, or both.");

            if (moving)
            {
                sourceRunner.Models.Remove(model);
                targetRunner.Models.Add(model);
            }

            model.Name = targetModelName;
            model.SupportsCompletions = finalSupportsCompletions;
            model.SupportsEmbeddings = finalSupportsEmbeddings;
            if (update.InheritMaxConcurrentRequests) model.MaxConcurrentRequests = null;
            else if (update.MaxConcurrentRequests.HasValue) model.MaxConcurrentRequests = update.MaxConcurrentRequests.Value;
            return new ModelUpdateResult { Provider = targetRunner, Model = model };
        }

        /// <summary>
        /// Create or update a model from a local dashboard request.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="request">Upsert request.</param>
        /// <param name="routeProvider">Provider name from the route, which takes precedence over the request.</param>
        /// <param name="routeModel">Model name from the route, which takes precedence over the request.</param>
        /// <returns>Model entry.</returns>
        /// <exception cref="ArgumentNullException">Settings or request is null.</exception>
        /// <exception cref="ArgumentException">Provider or model name is missing.</exception>
        /// <exception cref="KeyNotFoundException">The provider does not exist.</exception>
        public static LocalModelEntry UpsertModel(ClientSettings settings, LocalModelUpsertRequest request, string? routeProvider, string? routeModel)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(request);
            string providerName = String.IsNullOrWhiteSpace(routeProvider) ? request.Provider : routeProvider;
            string modelName = String.IsNullOrWhiteSpace(routeModel) ? request.Name : routeModel;
            if (String.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.");
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            ClientRunnerSettings provider = FindProvider(settings, providerName) ?? throw new KeyNotFoundException("Provider not found.");
            ClientModelSettings? model = provider.Models.FirstOrDefault(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
            if (model == null)
            {
                model = new ClientModelSettings { Name = modelName };
                provider.Models.Add(model);
            }

            ValidateConcurrencyLimit(request.MaxConcurrentRequests, "Model");
            model.SupportsCompletions = request.SupportsCompletions;
            model.SupportsEmbeddings = request.SupportsEmbeddings;
            model.MaxConcurrentRequests = request.MaxConcurrentRequests;
            return ToModelEntry(provider, model, settings.DefaultMaxConcurrentRequests);
        }

        /// <summary>
        /// Delete a model from one provider, or from every provider when no provider is named.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="providerName">Provider name, or null for every provider.</param>
        /// <param name="modelName">Model name.</param>
        /// <returns>Number of model entries removed.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="ArgumentException">Model name is missing.</exception>
        /// <exception cref="KeyNotFoundException">A provider was named and does not exist.</exception>
        public static int DeleteModel(ClientSettings settings, string? providerName, string modelName)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            if (!String.IsNullOrWhiteSpace(providerName) && FindProvider(settings, providerName) == null)
            {
                throw new KeyNotFoundException("Provider endpoint not found: " + providerName);
            }

            int removed = 0;
            foreach (ClientRunnerSettings runner in settings.Runners)
            {
                if (!String.IsNullOrWhiteSpace(providerName) && !String.Equals(runner.Name, providerName, StringComparison.OrdinalIgnoreCase)) continue;
                removed += runner.Models.RemoveAll(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
            }

            return removed;
        }

        /// <summary>
        /// Build the flat list of configured models.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Model list.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static LocalModelListResponse BuildModelList(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            LocalModelListResponse response = new LocalModelListResponse();
            foreach (ClientRunnerSettings provider in settings.Runners)
            {
                foreach (ClientModelSettings model in provider.Models)
                {
                    response.Models.Add(ToModelEntry(provider, model, settings.DefaultMaxConcurrentRequests));
                }
            }

            return response;
        }

        /// <summary>
        /// Convert a provider and model into a model entry.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="model">Model.</param>
        /// <param name="defaultMaxConcurrentRequests">Global default limit used when the provider sets none. Default is 4.</param>
        /// <returns>Model entry.</returns>
        /// <exception cref="ArgumentNullException">Provider or model is null.</exception>
        public static LocalModelEntry ToModelEntry(ClientRunnerSettings provider, ClientModelSettings model, int defaultMaxConcurrentRequests = 4)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(model);
            return new LocalModelEntry
            {
                Provider = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                Name = model.Name,
                SupportsCompletions = model.SupportsCompletions,
                SupportsEmbeddings = model.SupportsEmbeddings,
                ModelManagementAllowed = IsModelManagementAllowed(provider),
                MaxConcurrentRequests = model.MaxConcurrentRequests,
                EffectiveMaxConcurrentRequests = Math.Min(model.MaxConcurrentRequests ?? Int32.MaxValue, provider.MaxConcurrentRequests ?? defaultMaxConcurrentRequests),
                ShareWithNetwork = provider.ShareWithNetwork
            };
        }

        /// <summary>
        /// Return a copy of the settings with secrets removed, suitable for display.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Redacted copy.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static ClientSettings Redact(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ClientSettings copy = new ClientSettings
            {
                ClientId = settings.ClientId,
                TenantId = settings.TenantId,
                Name = settings.Name,
                UseColors = settings.UseColors,
                Server = new ClientServerSettings
                {
                    Url = settings.Server.Url,
                    RestUrl = settings.Server.RestUrl,
                    AccessKey = settings.Server.AccessKey,
                    SecretKey = null,
                    Token = settings.Server.Token
                },
                LocalDashboard = settings.LocalDashboard,
                LocalInferenceServer = settings.LocalInferenceServer,
                DefaultMaxConcurrentRequests = settings.DefaultMaxConcurrentRequests,
                Telemetry = settings.Telemetry,
                Providers = settings.Runners.Select(LocalSecretRedactor.RedactProvider).ToList(),
                Chat = ChatSettingsSecrets.Redact(settings.Chat),
                Analytics = settings.Analytics,
                Mcp = settings.Mcp
            };
            copy.Server.Token = String.IsNullOrWhiteSpace(copy.Server.Token) ? null : "configured";
            return copy;
        }

        /// <summary>
        /// Validate the provider endpoints, save the settings, and ask a running worker to re-advertise its inventory
        /// on the next heartbeat.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        /// <exception cref="ArgumentException">A provider endpoint loops back to the local inference server.</exception>
        public static void SaveInventory(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.ValidateLocalInferenceProviderEndpoints();
            settings.Save();
            ClientConnectionState.MarkInventoryUpdateRequested();
        }

        /// <summary>
        /// Probe a provider's model-list endpoint (/api/tags for Ollama, /v1/models otherwise) with its credential.
        /// </summary>
        /// <param name="provider">Provider to test.</param>
        /// <param name="requireSuccessStatus">True to treat only 2xx responses as success; false to treat any response below 500 as reachable.</param>
        /// <param name="timeoutMs">Probe timeout in milliseconds, or null to use the provider timeout. Minimum 1000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Test result. Network failures are reported in the result rather than thrown.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        public static async Task<LocalProviderTestResponse> TestProviderAsync(ClientRunnerSettings provider, bool requireSuccessStatus, int? timeoutMs, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(provider);
            LocalProviderTestResponse response = new LocalProviderTestResponse
            {
                Name = provider.Name,
                Endpoint = provider.Endpoint
            };

            string relativePath = IsOllamaApiType(provider.ApiType) ? "/api/tags" : "/v1/models";
            string endpoint = provider.Endpoint.TrimEnd('/') + relativePath;
            int effectiveTimeoutMs = Math.Max(MinimumTimeoutMs, timeoutMs ?? provider.TimeoutMs);
            try
            {
                using (HttpClient httpClient = new HttpClient())
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(effectiveTimeoutMs);
                    if (!String.IsNullOrWhiteSpace(provider.Credential))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.Credential);
                    }

                    using (HttpResponseMessage httpResponse = await httpClient.SendAsync(request, token).ConfigureAwait(false))
                    {
                        int statusCode = (int)httpResponse.StatusCode;
                        response.StatusCode = statusCode;
                        response.Success = requireSuccessStatus
                            ? httpResponse.IsSuccessStatusCode
                            : statusCode >= 200 && statusCode < 500;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException || ex is UriFormatException)
            {
                if (ex is TaskCanceledException && token.IsCancellationRequested) throw;
                response.Success = false;
                response.Error = ex.Message;
            }

            return response;
        }

        private static void ValidateConcurrencyLimit(int? value, string scope)
        {
            if (value.HasValue && (value.Value < 1 || value.Value > 256))
            {
                throw new ArgumentException(scope + " max concurrent requests must be between 1 and 256.");
            }
        }

        /// <summary>
        /// Whether the API type is Ollama.
        /// </summary>
        /// <param name="apiType">API type.</param>
        /// <returns>True for Ollama.</returns>
        public static bool IsOllamaApiType(string? apiType)
        {
            return String.Equals(apiType, OllamaApiType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determine whether the Xeno server may manage models on a provider.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>True when the provider is Ollama and model management is enabled.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        public static bool IsModelManagementAllowed(ClientRunnerSettings provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            return IsOllamaApiType(provider.ApiType) && provider.AllowModelManagement;
        }

        /// <summary>
        /// Normalize an endpoint for comparison by trimming whitespace and trailing slashes.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <returns>Normalized endpoint, or an empty string.</returns>
        public static string NormalizeEndpoint(string? endpoint)
        {
            return String.IsNullOrWhiteSpace(endpoint) ? String.Empty : endpoint.Trim().TrimEnd('/');
        }

        #endregion
    }
}
