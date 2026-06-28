namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    internal sealed class SyntheticRequestFactory
    {
        private readonly DataLoaderOptions _Options;
        private readonly RandomSource _Random;
        private readonly List<SyntheticClientProfile> _Clients;

        public SyntheticRequestFactory(DataLoaderOptions options, RandomSource random, List<SyntheticClientProfile> clients)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _Random = random ?? throw new ArgumentNullException(nameof(random));
            _Clients = clients ?? throw new ArgumentNullException(nameof(clients));
        }

        public List<RequestHistoryEntry> CreateRequestHistory(CancellationToken token)
        {
            List<WeightedChoice<SyntheticClientModelSelection>> candidates = BuildCandidates();
            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            DateTime cursor = TruncateToHour(_Options.FromUtc);
            while (cursor < _Options.ToUtc)
            {
                token.ThrowIfCancellationRequested();
                double expected = RequestsPerHour(cursor);
                int count = _Random.Poisson(expected);
                for (int index = 0; index < count; index++)
                {
                    DateTime createdUtc = cursor.AddMinutes(_Random.NextInt32(0, 60)).AddSeconds(_Random.NextInt32(0, 60)).AddMilliseconds(_Random.NextInt32(0, 1000));
                    if (createdUtc < _Options.FromUtc || createdUtc >= _Options.ToUtc) continue;
                    SyntheticClientModelSelection selection = _Random.PickWeighted(candidates);
                    entries.Add(CreateEntry(selection.Client, selection.Model, createdUtc));
                }

                cursor = cursor.AddHours(1);
            }

            entries.Sort((left, right) => left.CreatedUtc.CompareTo(right.CreatedUtc));
            return entries;
        }

        private RequestHistoryEntry CreateEntry(SyntheticClientProfile client, SyntheticModelProfile model, DateTime createdUtc)
        {
            bool embedding = model.SupportsEmbeddings && _Random.Chance(0.82);
            int statusCode = ChooseStatusCode();
            bool success = statusCode < 400;
            long durationMs = EstimateDurationMs(client, model, embedding, statusCode, createdUtc);
            DateTime completedUtc = createdUtc.AddMilliseconds(durationMs);
            int promptTokens = embedding ? _Random.NextInt32(80, 2200) : _Random.NextInt32(100, 5200);
            int completionTokens = success && !embedding ? _Random.NextInt32(32, 1900) : 0;
            long requestBytes = embedding ? _Random.NextInt32(600, 14000) : _Random.NextInt32(900, 26000);
            long responseBytes = success ? (embedding ? _Random.NextInt32(2400, 42000) : _Random.NextInt32(900, 80000)) : _Random.NextInt32(120, 1800);
            string path = embedding ? "/api/v1/openai/v1/embeddings" : (_Random.Chance(0.82) ? "/api/v1/openai/v1/chat/completions" : "/api/v1/openai/v1/completions");
            long headersMs = Math.Max(15, (long)(durationMs * RandomSource.Clamp(_Random.Gaussian(0.2, 0.08), 0.06, 0.7)));
            long firstTokenMs = Math.Max(headersMs, (long)(durationMs * RandomSource.Clamp(_Random.Gaussian(0.34, 0.1), 0.12, 0.85)));
            long upstreamMs = Math.Max(20, durationMs - _Random.NextInt32(20, 120));
            string credentialId = ChooseCredential();

            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Id = SyntheticIdFactory.Create(SyntheticConstants.RequestIdPrefix, client.Id + model.Name + createdUtc.ToString("O") + _Random.NextDouble().ToString("R"), 32),
                TenantId = _Options.TenantId,
                RequestorTenantId = _Options.TenantId,
                Path = path,
                Method = "POST",
                Model = model.Name,
                SelectedClientId = client.Id,
                StatusCode = statusCode,
                DurationMs = durationMs,
                SourceIp = CreateSourceIp(createdUtc),
                UserId = "usr_admin",
                CredentialId = credentialId,
                PrincipalType = PrincipalTypeEnum.Credential,
                RequestorPrincipalType = PrincipalTypeEnum.Credential,
                RequestorPrincipalId = credentialId,
                AuthScheme = AuthSchemeEnum.Credential,
                CreatedUtc = createdUtc,
                CompletedUtc = completedUtc,
                RequestBodyLength = requestBytes,
                ResponseBodyLength = responseBytes,
                RequestBodyTruncated = false,
                ResponseBodyTruncated = false,
                Telemetry = CreateTelemetry(client, durationMs, upstreamMs, headersMs, firstTokenMs, promptTokens, completionTokens, requestBytes, responseBytes, embedding, success),
                RoutingDecision = CreateRoutingDecision(client, model, statusCode, success, durationMs, createdUtc),
                Timeline = CreateTimeline(createdUtc, durationMs, headersMs, firstTokenMs, success, embedding)
            };
            entry.RequestHeaders["content-type"] = "application/json";
            entry.RequestHeaders["user-agent"] = ChooseUserAgent();
            entry.ResponseHeaders["content-type"] = "application/json";
            return entry;
        }

        private RequestHistoryTelemetry CreateTelemetry(SyntheticClientProfile client, long durationMs, long upstreamMs, long headersMs, long firstTokenMs, int promptTokens, int completionTokens, long requestBytes, long responseBytes, bool embedding, bool success)
        {
            RequestHistoryTelemetry telemetry = new RequestHistoryTelemetry
            {
                TotalTimeMs = durationMs,
                ClientRoundTripMs = Math.Max(0, durationMs - upstreamMs),
                UpstreamTotalMs = upstreamMs,
                RequestToHeadersMs = success ? headersMs : null,
                TimeToFirstTokenMs = success && !embedding ? firstTokenMs : null,
                TotalStreamingTimeMs = success && !embedding ? Math.Max(0, durationMs - firstTokenMs) : null,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                StreamChunkCount = success && !embedding ? _Random.NextInt32(2, 120) : null,
                StreamEventCount = success && !embedding ? _Random.NextInt32(2, 120) : null,
                RequestBytes = requestBytes,
                ResponseBytes = responseBytes,
                TransferMode = success && !embedding && _Random.Chance(0.68) ? RequestTransferModeEnum.Stream : RequestTransferModeEnum.Standard,
                ProviderRequestId = success ? SyntheticIdFactory.Create("prv_", client.Id + durationMs.ToString() + _Random.NextDouble().ToString("R"), 28) : null
            };
            telemetry.Metadata["provider"] = client.ProviderName;
            telemetry.Metadata["source"] = client.Source;
            telemetry.Metadata["zone"] = client.Zone;
            telemetry.Metadata[SyntheticConstants.SyntheticMetadataKey] = SyntheticConstants.SyntheticMetadataValue;
            return telemetry;
        }

        private RoutingDecision CreateRoutingDecision(SyntheticClientProfile client, SyntheticModelProfile model, int statusCode, bool success, long durationMs, DateTime createdUtc)
        {
            string outcome = success ? "Completed" : statusCode == 429 ? "RateLimited" : statusCode == 504 ? "ClientTimeout" : "ProviderError";
            string message = success ? "Client completed the routed request." : statusCode == 429 ? "Provider returned rate limit." : statusCode == 504 ? "Selected client did not respond before timeout." : "Client upstream request failed.";
            RoutingDecision decision = new RoutingDecision
            {
                Success = success,
                OutcomeCode = outcome,
                Message = message,
                HttpStatusCode = statusCode,
                SelectedClientId = client.Id,
                SelectedClientName = client.Name,
                SelectedProvider = client.ProviderName,
                SelectedSource = client.Source,
                SelectedClientInFlightRequests = _Random.NextInt32(0, Math.Max(1, client.MaxParallelRequests)),
                SelectedClientMaxParallelRequests = client.MaxParallelRequests,
                SelectedClientWeight = client.Weight,
                SelectedClientLastHeartbeatUtc = createdUtc.AddSeconds(-_Random.NextInt32(3, 58)),
                EffectiveModel = model.Name,
                CandidateCount = Math.Max(1, _Random.NextInt32(1, Math.Min(_Clients.Count, 7) + 1)),
                SelectionReason = ChooseSelectionReason(),
                RoutingPolicyId = "rpol_default",
                RoutingPolicyName = "Default routing policy",
                RoutingPolicyModelName = "*",
                RoutingPolicyClientName = "*",
                RoutingPolicyReason = "Matched default model routing policy.",
                RoutingPolicyRequestTimeoutMs = 60000,
                LoadBalancingMode = ChooseLoadBalancingMode(),
                AdaptiveSampleCount = _Random.NextInt32(12, 120),
                AdaptiveScore = RandomSource.Clamp(_Random.Gaussian(0.78, 0.12), 0.2, 0.99).ToString("0.000"),
                ClientRoundTripMs = Math.Max(0, durationMs - (long)(durationMs * 0.92))
            };
            AddCandidateIds(decision, client);
            decision.Timeline.Add("ModelResolution");
            decision.Timeline.Add("ClientInventory");
            decision.Timeline.Add("ClientSelection");
            decision.Timeline.Add("ClientDispatch");
            return decision;
        }

        private List<RequestHistoryTimelineEvent> CreateTimeline(DateTime createdUtc, long durationMs, long headersMs, long firstTokenMs, bool success, bool embedding)
        {
            List<RequestHistoryTimelineEvent> timeline = new List<RequestHistoryTimelineEvent>();
            timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.Received, "Xeno server received the API request.", createdUtc, 0, true));
            timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.ModelResolution, "Resolved the requested model.", createdUtc.AddMilliseconds(12), 12, true));
            timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.ClientInventory, "Evaluated connected client inventory.", createdUtc.AddMilliseconds(24), 24, true));
            timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.ClientSelection, "Selected client by active routing policy.", createdUtc.AddMilliseconds(38), 38, true));
            timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.ClientDispatch, "Dispatched request to selected Xeno client.", createdUtc.AddMilliseconds(54), 54, true));
            if (success)
            {
                timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.ResponseHeaders, "Selected client received upstream response headers.", createdUtc.AddMilliseconds(headersMs), headersMs, true));
                if (!embedding) timeline.Add(CreateTimelineEvent(RequestHistoryTimelineStageEnum.FirstToken, "First response byte or token was received from the upstream provider.", createdUtc.AddMilliseconds(firstTokenMs), firstTokenMs, true));
            }

            timeline.Add(CreateTimelineEvent(success ? RequestHistoryTimelineStageEnum.Completed : RequestHistoryTimelineStageEnum.Failed, success ? "Client completed the routed request." : "Client upstream request failed.", createdUtc.AddMilliseconds(durationMs), durationMs, success));
            return timeline;
        }

        private static RequestHistoryTimelineEvent CreateTimelineEvent(RequestHistoryTimelineStageEnum stage, string message, DateTime timestampUtc, long elapsedMs, bool success)
        {
            return new RequestHistoryTimelineEvent
            {
                Stage = stage,
                Message = message,
                TimestampUtc = timestampUtc,
                ElapsedMs = elapsedMs,
                Success = success
            };
        }

        private List<WeightedChoice<SyntheticClientModelSelection>> BuildCandidates()
        {
            List<WeightedChoice<SyntheticClientModelSelection>> choices = new List<WeightedChoice<SyntheticClientModelSelection>>();
            foreach (SyntheticClientProfile client in _Clients)
            {
                foreach (SyntheticModelProfile model in client.Models)
                {
                    SyntheticClientModelSelection selection = new SyntheticClientModelSelection { Client = client, Model = model };
                    choices.Add(new WeightedChoice<SyntheticClientModelSelection>(selection, Math.Max(1, client.Weight * model.Weight)));
                }
            }

            return choices;
        }

        private double RequestsPerHour(DateTime hourUtc)
        {
            double baseRate = Math.Max(2, _Options.ClientCount * (1.8 + _Options.TelemetrySamplesPerHour / 14.0));
            double localHour = (hourUtc.Hour + 17) % 24;
            double business = localHour >= 8 && localHour <= 18 ? 1.0 : localHour >= 19 && localHour <= 22 ? 0.55 : 0.18;
            double weekend = hourUtc.DayOfWeek == DayOfWeek.Saturday || hourUtc.DayOfWeek == DayOfWeek.Sunday ? 0.52 : 1.0;
            double scenarioMultiplier = _Options.Scenario == "testing" ? 1.35 : _Options.Scenario == "mixed" ? 1.15 : 1.0;
            double randomMultiplier = RandomSource.Clamp(_Random.Gaussian(1.0, 0.05 + _Options.Randomness * 0.65), 0.05, 3.8);
            double spike = ShouldSpike(hourUtc) ? 1.8 + _Options.Randomness * 3.5 : 1.0;
            double gap = ShouldGap(hourUtc) ? 0.08 : 1.0;
            return baseRate * business * weekend * scenarioMultiplier * randomMultiplier * spike * gap;
        }

        private bool ShouldSpike(DateTime hourUtc)
        {
            if (_Options.Scenario == "screenshot") return _Random.Chance(0.006 + _Options.Randomness * 0.01);
            return _Random.Chance(0.015 + _Options.Randomness * 0.04);
        }

        private bool ShouldGap(DateTime hourUtc)
        {
            if (_Options.Scenario == "screenshot") return _Random.Chance(0.002 * _Options.Randomness);
            return _Random.Chance(0.01 + _Options.Randomness * 0.025);
        }

        private int ChooseStatusCode()
        {
            double errorRate = _Options.Scenario == "testing" ? 0.09 + _Options.Randomness * 0.08 : _Options.Scenario == "mixed" ? 0.055 + _Options.Randomness * 0.045 : 0.028 + _Options.Randomness * 0.025;
            if (!_Random.Chance(errorRate)) return 200;
            return _Random.PickWeighted(new List<WeightedChoice<int>>
            {
                new WeightedChoice<int>(429, 4),
                new WeightedChoice<int>(502, 3),
                new WeightedChoice<int>(504, 2),
                new WeightedChoice<int>(400, 1)
            });
        }

        private long EstimateDurationMs(SyntheticClientProfile client, SyntheticModelProfile model, bool embedding, int statusCode, DateTime createdUtc)
        {
            if (statusCode == 504) return (long)RandomSource.Clamp(_Random.Gaussian(30000, 7000), 9000, 60000);
            if (statusCode >= 400) return (long)RandomSource.Clamp(_Random.Gaussian(900, 550), 70, 7000);
            double localHour = (createdUtc.Hour + 17) % 24;
            double loadFactor = localHour >= 8 && localHour <= 18 ? 1.18 : 0.86;
            double scenarioNoise = 0.15 + _Options.Randomness * 0.65;
            double baseline = embedding ? client.BaseLatencyMs * 0.35 : client.BaseLatencyMs * model.SizeFactor;
            return (long)RandomSource.Clamp(_Random.Gaussian(baseline * loadFactor, baseline * scenarioNoise), 85, 45000);
        }

        private string CreateSourceIp(DateTime createdUtc)
        {
            int segment = Math.Abs(createdUtc.DayOfYear + _Random.NextInt32(0, 240)) % 240 + 1;
            return _Random.PickWeighted(new List<WeightedChoice<string>>
            {
                new WeightedChoice<string>("10.14." + segment.ToString() + "." + _Random.NextInt32(10, 240).ToString(), 5),
                new WeightedChoice<string>("172.22." + segment.ToString() + "." + _Random.NextInt32(10, 240).ToString(), 3),
                new WeightedChoice<string>("100.96." + segment.ToString() + "." + _Random.NextInt32(10, 240).ToString(), 1)
            });
        }

        private string ChooseCredential()
        {
            return _Random.PickWeighted(new List<WeightedChoice<string>>
            {
                new WeightedChoice<string>("cred_default", 7),
                new WeightedChoice<string>("cred_dashboard", 2),
                new WeightedChoice<string>("cred_service", 1)
            });
        }

        private string ChooseUserAgent()
        {
            return _Random.PickWeighted(new List<WeightedChoice<string>>
            {
                new WeightedChoice<string>("openai-python/1.82.0", 5),
                new WeightedChoice<string>("xeno-loadtest/0.4", 2),
                new WeightedChoice<string>("xeno-agentic-worker/1.1", 2),
                new WeightedChoice<string>("curl/8.8.0", 1)
            });
        }

        private string ChooseSelectionReason()
        {
            return _Random.PickWeighted(new List<WeightedChoice<string>>
            {
                new WeightedChoice<string>("weighted round-robin selected the next healthy client", 5),
                new WeightedChoice<string>("adaptive routing selected the lowest recent latency", 3),
                new WeightedChoice<string>("model policy matched client source and capacity", 2),
                new WeightedChoice<string>("least recently used routing avoided a hot client", 1)
            });
        }

        private LoadBalancingModeEnum ChooseLoadBalancingMode()
        {
            return _Random.PickWeighted(new List<WeightedChoice<LoadBalancingModeEnum>>
            {
                new WeightedChoice<LoadBalancingModeEnum>(LoadBalancingModeEnum.RoundRobin, 5),
                new WeightedChoice<LoadBalancingModeEnum>(LoadBalancingModeEnum.Adaptive, 3),
                new WeightedChoice<LoadBalancingModeEnum>(LoadBalancingModeEnum.LeastRecentlyUsed, 2),
                new WeightedChoice<LoadBalancingModeEnum>(LoadBalancingModeEnum.Random, 1)
            });
        }

        private void AddCandidateIds(RoutingDecision decision, SyntheticClientProfile selectedClient)
        {
            decision.CandidateClientIds.Add(selectedClient.Id);
            while (decision.CandidateClientIds.Count < decision.CandidateCount)
            {
                SyntheticClientProfile client = _Random.Pick(_Clients);
                if (!decision.CandidateClientIds.Contains(client.Id)) decision.CandidateClientIds.Add(client.Id);
            }
        }

        private static DateTime TruncateToHour(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
        }
    }
}
