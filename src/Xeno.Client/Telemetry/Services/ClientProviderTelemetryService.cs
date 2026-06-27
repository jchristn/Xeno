namespace Xeno.Client.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Core.Helpers;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Default provider telemetry service.
    /// </summary>
    public class ClientProviderTelemetryService : IClientProviderTelemetryService
    {
        private readonly IPrometheusMetricsParser _PrometheusParser;

        /// <summary>
        /// Instantiate provider telemetry service.
        /// </summary>
        /// <param name="prometheusParser">Prometheus parser.</param>
        public ClientProviderTelemetryService(IPrometheusMetricsParser prometheusParser)
        {
            _PrometheusParser = prometheusParser ?? throw new ArgumentNullException(nameof(prometheusParser));
        }

        /// <inheritdoc />
        public async Task<ClientProviderTelemetrySnapshot> CollectAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            Stopwatch stopwatch = Stopwatch.StartNew();
            ClientProviderTelemetrySnapshot snapshot = new ClientProviderTelemetrySnapshot
            {
                Metadata = new ClientProviderTelemetryMetadata
                {
                    Id = IdGenerator.NewId("ptel_"),
                    ClientId = settings.ClientId,
                    MachineName = settings.Name,
                    CollectedUtc = DateTime.UtcNow
                }
            };

            if (!settings.Telemetry.ProviderTelemetry.Enabled)
            {
                stopwatch.Stop();
                snapshot.Metadata.CollectionElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
                return snapshot;
            }

            SemaphoreSlim semaphore = new SemaphoreSlim(settings.Telemetry.ProviderTelemetry.ConcurrentProviderLimit);
            List<Task<ClientProviderTelemetryProviderStatus>> tasks = new List<Task<ClientProviderTelemetryProviderStatus>>();
            foreach (ClientRunnerSettings runner in settings.Runners)
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        return await CollectProviderSafeAsync(settings, runner, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            ClientProviderTelemetryProviderStatus[] statuses = await Task.WhenAll(tasks).ConfigureAwait(false);
            snapshot.Providers.AddRange(statuses);
            snapshot.Metadata.ProviderCount = snapshot.Providers.Count;
            stopwatch.Stop();
            snapshot.Metadata.CollectionElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            return snapshot;
        }

        private async Task<ClientProviderTelemetryProviderStatus> CollectProviderSafeAsync(ClientSettings settings, ClientRunnerSettings runner, CancellationToken token)
        {
            ClientProviderTelemetryEndpointOverride? endpointOverride = FindOverride(settings, runner.Name);
            ClientProviderTelemetryProviderStatus status = BuildInitialStatus(runner, endpointOverride);
            if (endpointOverride != null && !endpointOverride.Enabled)
            {
                status.Status = ClientProviderTelemetryStatusEnum.Disabled;
                status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Unknown;
                return status;
            }

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(settings.Telemetry.ProviderTelemetry.RequestTimeoutMs);
                    if (!String.IsNullOrWhiteSpace(runner.Credential))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.Credential);
                    }

                    if (status.Endpoint.Kind == ClientProviderTelemetryKindEnum.Ollama)
                    {
                        await CollectOllamaAsync(httpClient, settings, runner, status, token).ConfigureAwait(false);
                    }
                    else
                    {
                        await CollectOpenAiCompatibleAsync(httpClient, settings, runner, status, token).ConfigureAwait(false);
                    }

                    if (settings.Telemetry.ProviderTelemetry.CollectProcessCorrelation)
                    {
                        ApplyProcessCorrelation(status);
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                status.Status = ClientProviderTelemetryStatusEnum.Unavailable;
                status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Unhealthy;
                status.LastError = "Provider telemetry request timed out.";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                status.Status = ClientProviderTelemetryStatusEnum.Failed;
                status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Unhealthy;
                status.LastError = ex.Message;
            }

            NormalizeStatus(status);
            return status;
        }

        private async Task CollectOllamaAsync(HttpClient httpClient, ClientSettings settings, ClientRunnerSettings runner, ClientProviderTelemetryProviderStatus status, CancellationToken token)
        {
            OllamaVersionResponse? version = await TryGetJsonAsync<OllamaVersionResponse>(httpClient, CombineUrl(runner.Endpoint, "/api/version"), token).ConfigureAwait(false);
            if (version != null)
            {
                status.Version = version.Version;
                status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Healthy;
                status.Status = ClientProviderTelemetryStatusEnum.Available;
            }

            if (settings.Telemetry.ProviderTelemetry.CollectModelDetails)
            {
                OllamaTagsResponse? tags = await TryGetJsonAsync<OllamaTagsResponse>(httpClient, CombineUrl(runner.Endpoint, "/api/tags"), token).ConfigureAwait(false);
                if (tags != null)
                {
                    foreach (OllamaTagModelResponse model in tags.Models)
                    {
                        status.Models.Add(new ClientProviderTelemetryModel
                        {
                            Name = model.Name,
                            Family = model.Details?.Family,
                            Quantization = model.Details?.QuantizationLevel,
                            ParameterSize = model.Details?.ParameterSize,
                            SizeBytes = model.Size
                        });
                    }
                }

                OllamaPsResponse? loaded = await TryGetJsonAsync<OllamaPsResponse>(httpClient, CombineUrl(runner.Endpoint, "/api/ps"), token).ConfigureAwait(false);
                if (loaded != null)
                {
                    foreach (OllamaLoadedModelResponse loadedModel in loaded.Models)
                    {
                        ClientProviderTelemetryModel? existing = status.Models.FirstOrDefault(item => String.Equals(item.Name, loadedModel.Name, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                        {
                            existing = new ClientProviderTelemetryModel { Name = loadedModel.Name };
                            status.Models.Add(existing);
                        }

                        existing.Loaded = true;
                        existing.MemoryBytes = loadedModel.SizeVram > 0 ? loadedModel.SizeVram : loadedModel.Size;
                        existing.ExpiresUtc = loadedModel.ExpiresAt;
                        existing.Family ??= loadedModel.Details?.Family;
                        existing.Quantization ??= loadedModel.Details?.QuantizationLevel;
                        existing.ParameterSize ??= loadedModel.Details?.ParameterSize;
                    }
                }
            }

            status.AvailableModelCount = status.Models.Count;
            status.LoadedModelCount = status.Models.Count(item => item.Loaded);
            status.Metrics.Add(new ClientProviderTelemetryMetric { Name = "ollama_available_models", Kind = "gauge", Value = status.AvailableModelCount, Source = ClientProviderTelemetryMetricSourceEnum.Models });
            status.Metrics.Add(new ClientProviderTelemetryMetric { Name = "ollama_loaded_models", Kind = "gauge", Value = status.LoadedModelCount, Source = ClientProviderTelemetryMetricSourceEnum.Models });
        }

        private async Task CollectOpenAiCompatibleAsync(HttpClient httpClient, ClientSettings settings, ClientRunnerSettings runner, ClientProviderTelemetryProviderStatus status, CancellationToken token)
        {
            string modelsUrl = status.Endpoint.ModelsUrl ?? CombineUrl(runner.Endpoint, "/v1/models");
            OpenAiModelsResponse? models = await TryGetJsonAsync<OpenAiModelsResponse>(httpClient, modelsUrl, token).ConfigureAwait(false);
            if (models != null)
            {
                status.Status = ClientProviderTelemetryStatusEnum.Available;
                status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Healthy;
                foreach (OpenAiModelResponse model in models.Data)
                {
                    status.Models.Add(new ClientProviderTelemetryModel { Name = model.Id });
                }
            }

            status.AvailableModelCount = status.Models.Count;
            if (settings.Telemetry.ProviderTelemetry.DiscoverMetricsEndpoints)
            {
                string metricsUrl = status.Endpoint.MetricsUrl ?? CombineUrl(runner.Endpoint, "/metrics");
                string? rawMetrics = await TryGetStringAsync(httpClient, metricsUrl, token).ConfigureAwait(false);
                if (!String.IsNullOrWhiteSpace(rawMetrics))
                {
                    ApplyPrometheusMetrics(settings, status, rawMetrics, token);
                    status.Status = status.Models.Count > 0 ? ClientProviderTelemetryStatusEnum.Available : ClientProviderTelemetryStatusEnum.Partial;
                    status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Healthy;
                }
            }
        }

        private void ApplyPrometheusMetrics(ClientSettings settings, ClientProviderTelemetryProviderStatus status, string rawMetrics, CancellationToken token)
        {
            byte[] rawBytes = Encoding.UTF8.GetBytes(rawMetrics);
            status.RawMetricsBytes = rawBytes.Length;
            if (settings.Telemetry.ProviderTelemetry.PersistRawMetrics)
            {
                int maxBytes = settings.Telemetry.ProviderTelemetry.RawMetricsMaxBytes;
                if (rawBytes.Length > maxBytes)
                {
                    status.RawMetricsTruncated = true;
                    status.RawMetrics = Encoding.UTF8.GetString(rawBytes, 0, Math.Max(0, maxBytes));
                }
                else
                {
                    status.RawMetrics = rawMetrics;
                }
            }

            PrometheusMetricDocument document = _PrometheusParser.Parse(rawMetrics, token);
            foreach (PrometheusMetricSample sample in document.Samples)
            {
                if (!IsRelevantProviderMetric(sample.Name)) continue;
                ClientProviderTelemetryMetric metric = new ClientProviderTelemetryMetric
                {
                    Name = sample.Name,
                    Kind = "gauge",
                    Value = sample.Value,
                    Source = ClientProviderTelemetryMetricSourceEnum.Prometheus,
                    ModelName = LabelValue(sample, "model_name") ?? LabelValue(sample, "model"),
                    Labels = sample.Labels
                };
                status.Metrics.Add(metric);
            }

            status.ActiveRequests = SumMetric(document, "num_requests_running");
            status.WaitingRequests = SumMetric(document, "num_requests_waiting");
            status.QueuedRequests = status.WaitingRequests;
            status.SwappedRequests = SumMetric(document, "num_requests_swapped");
            status.GpuCacheUsagePercent = SumMetric(document, "gpu_cache_usage_perc");
            status.CpuCacheUsagePercent = SumMetric(document, "cpu_cache_usage_perc");
            status.CacheUsagePercent = status.GpuCacheUsagePercent ?? status.CpuCacheUsagePercent;
            status.PromptTokensPerSecond = SumMetric(document, "prompt_tokens_total");
            status.GenerationTokensPerSecond = SumMetric(document, "generation_tokens_total");
            if (status.PromptTokensPerSecond.HasValue || status.GenerationTokensPerSecond.HasValue)
            {
                status.TotalTokensPerSecond = (status.PromptTokensPerSecond ?? 0) + (status.GenerationTokensPerSecond ?? 0);
            }

            status.RequestRatePerSecond = SumMetric(document, "request_success_total") ?? SumMetric(document, "requests_total");
            status.ErrorRatePerSecond = SumMetric(document, "request_failure_total") ?? SumMetric(document, "errors_total");
            status.LatencyP95Ms = HistogramBucketApproximation(document, "e2e_request_latency_seconds", 0.95);
            status.TimeToFirstTokenP95Ms = HistogramBucketApproximation(document, "time_to_first_token_seconds", 0.95);
        }

        private static bool IsRelevantProviderMetric(string name)
        {
            return name.IndexOf("vllm", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("tgi", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("llamacpp", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("localai", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("request", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double? SumMetric(PrometheusMetricDocument document, string contains)
        {
            List<PrometheusMetricSample> samples = document.Samples.Where(item => item.Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0 && item.Name.IndexOf("_bucket", StringComparison.OrdinalIgnoreCase) < 0).ToList();
            if (samples.Count < 1) return null;
            return samples.Sum(item => item.Value);
        }

        private static double? HistogramBucketApproximation(PrometheusMetricDocument document, string contains, double percentile)
        {
            List<PrometheusMetricSample> buckets = document.Samples.Where(item => item.Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0 && item.Name.EndsWith("_bucket", StringComparison.OrdinalIgnoreCase)).OrderBy(item => BucketLe(item)).ToList();
            if (buckets.Count < 1) return null;
            double total = buckets[buckets.Count - 1].Value;
            if (total <= 0) return null;
            double target = total * percentile;
            foreach (PrometheusMetricSample bucket in buckets)
            {
                if (bucket.Value >= target)
                {
                    double seconds = BucketLe(bucket);
                    if (Double.IsInfinity(seconds)) return null;
                    return seconds * 1000;
                }
            }

            return null;
        }

        private static double BucketLe(PrometheusMetricSample sample)
        {
            string? value = LabelValue(sample, "le");
            if (String.IsNullOrWhiteSpace(value)) return Double.PositiveInfinity;
            if (String.Equals(value, "+Inf", StringComparison.OrdinalIgnoreCase)) return Double.PositiveInfinity;
            if (Double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)) return parsed;
            return Double.PositiveInfinity;
        }

        private static string? LabelValue(PrometheusMetricSample sample, string name)
        {
            ClientProviderTelemetryLabel? label = sample.Labels.FirstOrDefault(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            return label?.Value;
        }

        private static void NormalizeStatus(ClientProviderTelemetryProviderStatus status)
        {
            if (status.Status == ClientProviderTelemetryStatusEnum.Unavailable && status.Models.Count > 0)
            {
                status.Status = ClientProviderTelemetryStatusEnum.Partial;
            }

            if (status.HealthStatus == ClientProviderTelemetryHealthStatusEnum.Unknown && (status.Status == ClientProviderTelemetryStatusEnum.Available || status.Status == ClientProviderTelemetryStatusEnum.Partial))
            {
                status.HealthStatus = ClientProviderTelemetryHealthStatusEnum.Healthy;
            }
        }

        private static void ApplyProcessCorrelation(ClientProviderTelemetryProviderStatus status)
        {
            string processNeedle = ProcessNeedle(status.Endpoint.Kind);
            if (String.IsNullOrWhiteSpace(processNeedle)) return;
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.ProcessName.IndexOf(processNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        status.ProcessId = process.Id;
                        status.ProcessName = process.ProcessName;
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private static string ProcessNeedle(ClientProviderTelemetryKindEnum kind)
        {
            if (kind == ClientProviderTelemetryKindEnum.Ollama) return "ollama";
            if (kind == ClientProviderTelemetryKindEnum.Vllm) return "vllm";
            if (kind == ClientProviderTelemetryKindEnum.LocalAI) return "localai";
            if (kind == ClientProviderTelemetryKindEnum.LlamaCpp) return "llama";
            if (kind == ClientProviderTelemetryKindEnum.LmStudio) return "studio";
            if (kind == ClientProviderTelemetryKindEnum.TextGenerationInference) return "text-generation";
            return string.Empty;
        }

        private static ClientProviderTelemetryEndpointOverride? FindOverride(ClientSettings settings, string providerName)
        {
            return settings.Telemetry.ProviderTelemetry.EndpointOverrides.FirstOrDefault(item => String.Equals(item.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        }

        private static ClientProviderTelemetryProviderStatus BuildInitialStatus(ClientRunnerSettings runner, ClientProviderTelemetryEndpointOverride? endpointOverride)
        {
            ClientProviderTelemetryKindEnum kind = DetectKind(runner.ApiType, endpointOverride?.ProviderKind);
            return new ClientProviderTelemetryProviderStatus
            {
                Endpoint = new ClientProviderTelemetryEndpoint
                {
                    ProviderName = runner.Name,
                    ApiType = runner.ApiType,
                    Kind = kind,
                    EndpointDisplay = RedactUrl(runner.Endpoint),
                    EndpointHash = EndpointHash(runner.Endpoint),
                    MetricsUrl = endpointOverride?.MetricsUrl,
                    HealthUrl = endpointOverride?.HealthUrl,
                    ModelsUrl = endpointOverride?.ModelsUrl
                },
                Status = ClientProviderTelemetryStatusEnum.Unavailable,
                HealthStatus = ClientProviderTelemetryHealthStatusEnum.Unknown,
                CollectedUtc = DateTime.UtcNow
            };
        }

        private static ClientProviderTelemetryKindEnum DetectKind(string apiType, string? overrideValue)
        {
            string input = String.IsNullOrWhiteSpace(overrideValue) ? apiType : overrideValue;
            if (input.IndexOf("ollama", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.Ollama;
            if (input.IndexOf("vllm", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.Vllm;
            if (input.IndexOf("gemini", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.Gemini;
            if (input.IndexOf("openai", StringComparison.OrdinalIgnoreCase) >= 0 && input.IndexOf("compatible", StringComparison.OrdinalIgnoreCase) < 0) return ClientProviderTelemetryKindEnum.OpenAI;
            if (input.IndexOf("localai", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.LocalAI;
            if (input.IndexOf("tgi", StringComparison.OrdinalIgnoreCase) >= 0 || input.IndexOf("generation", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.TextGenerationInference;
            if (input.IndexOf("llama", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.LlamaCpp;
            if (input.IndexOf("lmstudio", StringComparison.OrdinalIgnoreCase) >= 0 || input.IndexOf("lm studio", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.LmStudio;
            if (input.IndexOf("compatible", StringComparison.OrdinalIgnoreCase) >= 0) return ClientProviderTelemetryKindEnum.OpenAICompatible;
            return ClientProviderTelemetryKindEnum.OpenAICompatible;
        }

        private static async Task<T?> TryGetJsonAsync<T>(HttpClient httpClient, string url, CancellationToken token) where T : class
        {
            string? payload = await TryGetStringAsync(httpClient, url, token).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(payload)) return null;
            try
            {
                return JsonUtil.Deserialize<T>(payload);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.InvalidDataException)
            {
                return null;
            }
        }

        private static async Task<string?> TryGetStringAsync(HttpClient httpClient, string url, CancellationToken token)
        {
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                using (HttpResponseMessage response = await httpClient.SendAsync(request, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException || ex is UriFormatException)
            {
                return null;
            }
        }

        private static string CombineUrl(string baseUrl, string relativePath)
        {
            return (baseUrl ?? string.Empty).TrimEnd('/') + "/" + (relativePath ?? string.Empty).TrimStart('/');
        }

        private static string RedactUrl(string url)
        {
            if (String.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                UriBuilder builder = new UriBuilder(url);
                if (!String.IsNullOrWhiteSpace(builder.UserName)) builder.UserName = "redacted";
                if (!String.IsNullOrWhiteSpace(builder.Password)) builder.Password = "redacted";
                builder.Query = string.Empty;
                return builder.Uri.ToString().TrimEnd('/');
            }
            catch (UriFormatException)
            {
                return url;
            }
        }

        private static string EndpointHash(string endpoint)
        {
            byte[] input = Encoding.UTF8.GetBytes((endpoint ?? string.Empty).Trim().ToLowerInvariant());
            byte[] hash = SHA256.HashData(input);
            return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }
    }
}
