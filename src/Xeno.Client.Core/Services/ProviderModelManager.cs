namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Routing;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Reads, deletes, and imports the models on a local provider. Every front end uses these methods; pulls go through
    /// <see cref="ModelPullCoordinator"/>. Stateless and thread safe.
    /// Model management here is the local operator's own action and is not gated by
    /// <see cref="ClientRunnerSettings.AllowModelManagement"/>, which controls only whether the Xeno server may manage models remotely.
    /// </summary>
    public static class ProviderModelManager
    {
        private static readonly string[] _EmbeddingHints = new[] { "embed", "bge", "e5-", "gte-", "minilm", "nomic-bert", "bert" };

        /// <summary>
        /// Whether Xeno can pull and delete models on a provider (Ollama only).
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>True for Ollama providers.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        public static bool CanManageModels(ClientRunnerSettings provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            return ProviderConfiguration.IsOllamaApiType(provider.ApiType);
        }

        /// <summary>
        /// Best guess at whether a model is an embedding model, from its name and family.
        /// </summary>
        /// <param name="name">Model name.</param>
        /// <param name="family">Model family, if known.</param>
        /// <returns>True when the model looks like an embedding model.</returns>
        public static bool LooksLikeEmbeddingModel(string? name, string? family)
        {
            string text = ((name ?? String.Empty) + " " + (family ?? String.Empty)).ToLowerInvariant();
            return _EmbeddingHints.Any(hint => text.Contains(hint, StringComparison.Ordinal));
        }

        /// <summary>
        /// Read the models a provider has, live. Network failures are reported in the result rather than thrown.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="handler">Optional HTTP handler, for tests.</param>
        /// <returns>Inventory.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        public static async Task<ProviderModelInventory> GetInventoryAsync(ClientRunnerSettings provider, CancellationToken token, HttpMessageHandler? handler = null)
        {
            ArgumentNullException.ThrowIfNull(provider);
            bool ollama = ProviderConfiguration.IsOllamaApiType(provider.ApiType);
            ProviderModelInventory inventory = new ProviderModelInventory
            {
                Provider = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                CanPull = ollama,
                CanDelete = ollama,
                CheckedUtc = DateTime.UtcNow
            };

            string url = ProviderConfiguration.NormalizeEndpoint(provider.Endpoint) + (ollama ? "/api/tags" : "/v1/models");
            try
            {
                using (HttpClient client = CreateClient(handler, Math.Min(provider.TimeoutMs, 15000)))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    ApplyCredential(request, provider);
                    using (HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            inventory.Error = "The provider answered " + (int)response.StatusCode + " " + ProviderMessage(body, response.ReasonPhrase);
                            return inventory;
                        }

                        inventory.Reachable = true;
                        if (ollama)
                        {
                            LocalOllamaTagsResponse? tags = JsonUtil.TryDeserialize(body, out LocalOllamaTagsResponse? parsed) ? parsed : null;
                            foreach (LocalOllamaModel model in tags?.Models ?? new List<LocalOllamaModel>())
                            {
                                string name = String.IsNullOrWhiteSpace(model.Name) ? model.Model : model.Name;
                                if (String.IsNullOrWhiteSpace(name)) continue;
                                inventory.Models.Add(new ProviderInventoryModel
                                {
                                    Name = name,
                                    SizeBytes = model.Size > 0 ? model.Size : null,
                                    Modified = model.ModifiedAt,
                                    Family = model.Details?.Family,
                                    ParameterSize = model.Details?.ParameterSize,
                                    Quantization = model.Details?.QuantizationLevel,
                                    Digest = model.Digest,
                                    Remote = !String.IsNullOrWhiteSpace(model.RemoteHost),
                                    LooksLikeEmbeddings = LooksLikeEmbeddingModel(name, model.Details?.Family)
                                });
                            }
                        }
                        else
                        {
                            LocalOpenAiModelList? list = JsonUtil.TryDeserialize(body, out LocalOpenAiModelList? parsed) ? parsed : null;
                            foreach (LocalOpenAiModel model in list?.Data ?? new List<LocalOpenAiModel>())
                            {
                                if (String.IsNullOrWhiteSpace(model.Id)) continue;
                                inventory.Models.Add(new ProviderInventoryModel
                                {
                                    Name = model.Id,
                                    Family = String.IsNullOrWhiteSpace(model.OwnedBy) ? null : model.OwnedBy,
                                    LooksLikeEmbeddings = LooksLikeEmbeddingModel(model.Id, null)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException || ex is UriFormatException)
            {
                if (ex is TaskCanceledException && token.IsCancellationRequested) throw;
                inventory.Error = ex is TaskCanceledException ? "The provider did not answer in time." : ex.Message;
            }

            foreach (ProviderInventoryModel model in inventory.Models)
            {
                model.Advertised = IsAdvertised(provider, model.Name);
            }

            inventory.Models = inventory.Models.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
            return inventory;
        }

        /// <summary>
        /// Whether a provider's configuration already advertises a model. Ollama's implicit ":latest" tag is ignored.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="modelName">Model name.</param>
        /// <returns>True when advertised.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        public static bool IsAdvertised(ClientRunnerSettings provider, string modelName)
        {
            ArgumentNullException.ThrowIfNull(provider);
            bool ollama = ProviderConfiguration.IsOllamaApiType(provider.ApiType);
            return provider.Models.Any(item => ollama
                ? LocalModelNameNormalizer.AreEqual(item.Name, modelName)
                : String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Delete a model from an Ollama provider. Does not change the Xeno configuration; see <see cref="RemoveAdvertisement"/>.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="handler">Optional HTTP handler, for tests.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        /// <exception cref="ArgumentException">Model name is missing.</exception>
        /// <exception cref="NotSupportedException">The provider is not Ollama.</exception>
        /// <exception cref="InvalidOperationException">The provider refused or could not be reached.</exception>
        public static async Task DeleteModelAsync(ClientRunnerSettings provider, string modelName, CancellationToken token, HttpMessageHandler? handler = null)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            if (!CanManageModels(provider)) throw new NotSupportedException("Xeno can delete models only from Ollama providers. '" + provider.Name + "' is " + provider.ApiType + ".");
            string url = ProviderConfiguration.NormalizeEndpoint(provider.Endpoint) + "/api/delete";
            try
            {
                using (HttpClient client = CreateClient(handler, Math.Max(ProviderConfiguration.MinimumTimeoutMs, provider.TimeoutMs)))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, url))
                {
                    ApplyCredential(request, provider);
                    request.Content = new StringContent(JsonUtil.Serialize(new OllamaModelNameRequest { Model = modelName.Trim() }), Encoding.UTF8, "application/json");
                    using (HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode) return;
                        string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        throw new InvalidOperationException("Could not delete " + modelName + " from " + provider.Name + ": " + ProviderMessage(body, response.ReasonPhrase));
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !token.IsCancellationRequested))
            {
                throw new InvalidOperationException("Could not reach " + provider.Name + " at " + provider.Endpoint + ": " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Remove a model's advertisement from a provider's configuration. Does not save.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="providerName">Provider name.</param>
        /// <param name="modelName">Model name; Ollama's implicit ":latest" tag is ignored.</param>
        /// <returns>Number of advertisements removed.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static int RemoveAdvertisement(ClientSettings settings, string providerName, string modelName)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ClientRunnerSettings? provider = ProviderConfiguration.FindProvider(settings, providerName);
            if (provider == null) return 0;
            bool ollama = ProviderConfiguration.IsOllamaApiType(provider.ApiType);
            return provider.Models.RemoveAll(item => ollama
                ? LocalModelNameNormalizer.AreEqual(item.Name, modelName)
                : String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Advertise several provider models. Models already advertised are skipped; invalid ones are reported, not thrown. Does not save.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="items">Models to advertise.</param>
        /// <returns>What was added, skipped, and failed.</returns>
        /// <exception cref="ArgumentNullException">Settings or items is null.</exception>
        public static ModelImportResult ImportModels(ClientSettings settings, IEnumerable<ModelImportItem> items)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(items);
            ModelImportResult result = new ModelImportResult();
            foreach (ModelImportItem item in items)
            {
                if (item == null) continue;
                string label = (item.Provider ?? String.Empty) + "/" + (item.Name ?? String.Empty);
                ClientRunnerSettings? provider = ProviderConfiguration.FindProvider(settings, item.Provider);
                if (provider == null)
                {
                    result.Failed.Add(label + ": provider endpoint not found");
                    continue;
                }

                if (String.IsNullOrWhiteSpace(item.Name))
                {
                    result.Failed.Add(label + ": model name is required");
                    continue;
                }

                if (IsAdvertised(provider, item.Name))
                {
                    result.Skipped.Add(label);
                    continue;
                }

                try
                {
                    ProviderConfiguration.AddModel(settings, provider.Name, item.Name.Trim(), item.SupportsCompletions, item.SupportsEmbeddings);
                    result.Added.Add(label);
                }
                catch (ArgumentException ex)
                {
                    result.Failed.Add(label + ": " + ex.Message);
                }
            }

            return result;
        }

        internal static HttpClient CreateClient(HttpMessageHandler? handler, int timeoutMs)
        {
            HttpClient client = handler == null ? new HttpClient() : new HttpClient(handler, false);
            client.Timeout = timeoutMs <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(timeoutMs);
            return client;
        }

        internal static void ApplyCredential(HttpRequestMessage request, ClientRunnerSettings provider)
        {
            if (!String.IsNullOrWhiteSpace(provider.Credential))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.Credential);
            }
        }

        internal static string ProviderMessage(string? body, string? fallback)
        {
            if (!String.IsNullOrWhiteSpace(body) && JsonUtil.TryDeserialize(body, out OllamaPullLine? line) && !String.IsNullOrWhiteSpace(line?.Error)) return line!.Error!;
            string text = (body ?? String.Empty).Trim();
            if (text.Length > 300) text = text.Substring(0, 300) + "...";
            return text.Length > 0 ? text : (fallback ?? "unknown error");
        }
    }
}
