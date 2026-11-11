namespace Xeno.Core.Analytics
{
    using System;
    using System.Linq;
    using Xeno.Core.Chat;
    using Xeno.Core.Models;

    /// <summary>
    /// Maps server request history entries and chat turns to usage records, deriving their stages. Stage formulas are
    /// documented in USAGE_ANALYTICS.md. A stage with a missing input is left out; a negative result is clamped and
    /// counted. Thread safe (stateless).
    /// </summary>
    public static class UsageRecordMapper
    {
        /// <summary>
        /// Map a server request history entry (network requests dataset).
        /// </summary>
        /// <param name="entry">Entry; only typed fields and telemetry are read.</param>
        /// <param name="provider">Provider that answered on the client, when known.</param>
        /// <returns>Record.</returns>
        /// <exception cref="ArgumentNullException">entry is null.</exception>
        public static UsageRecord FromNetworkRequest(RequestHistoryEntry entry, string? provider = null)
        {
            ArgumentNullException.ThrowIfNull(entry);
            RequestHistoryTelemetry telemetry = entry.Telemetry ?? new RequestHistoryTelemetry();
            UsageRecord record = new UsageRecord
            {
                TimestampUtc = entry.CreatedUtc,
                Model = entry.Model,
                Provider = provider,
                Route = "network",
                ClientId = entry.SelectedClientId,
                TenantId = entry.TenantId,
                UserId = entry.UserId,
                CredentialId = entry.CredentialId,
                StatusCode = entry.StatusCode,
                Success = entry.StatusCode > 0 && entry.StatusCode < 400,
                Fallback = null,
                TotalMs = entry.DurationMs,
                TimeToFirstTokenMs = telemetry.TimeToFirstTokenMs,
                StreamingMs = telemetry.TotalStreamingTimeMs,
                PromptTokens = telemetry.PromptTokens,
                CompletionTokens = telemetry.CompletionTokens
            };

            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ServerOverhead, telemetry.ClientRoundTripMs.HasValue ? entry.DurationMs - telemetry.ClientRoundTripMs.Value : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.RelayAndClient, telemetry.ClientRoundTripMs.HasValue && telemetry.UpstreamTotalMs.HasValue ? telemetry.ClientRoundTripMs.Value - telemetry.UpstreamTotalMs.Value : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ProviderHeaders, telemetry.RequestToHeadersMs);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ProviderFirstToken, telemetry.TimeToFirstTokenMs.HasValue && telemetry.RequestToHeadersMs.HasValue ? telemetry.TimeToFirstTokenMs.Value - telemetry.RequestToHeadersMs.Value : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Streaming, telemetry.TotalStreamingTimeMs);
            return record;
        }

        /// <summary>
        /// Map a chat turn (chat dataset). Running turns should be left out by the caller.
        /// </summary>
        /// <param name="turn">Turn.</param>
        /// <param name="tenantId">Tenant (server), or null.</param>
        /// <param name="userId">User (server), or null.</param>
        /// <returns>Record.</returns>
        /// <exception cref="ArgumentNullException">turn is null.</exception>
        public static UsageRecord FromChatTurn(ChatTurn turn, string? tenantId = null, string? userId = null)
        {
            ArgumentNullException.ThrowIfNull(turn);
            UsageRecord record = new UsageRecord
            {
                TimestampUtc = turn.CreatedUtc,
                Model = turn.Model,
                Provider = String.IsNullOrWhiteSpace(turn.Provider) ? null : turn.Provider,
                Route = turn.Route,
                TenantId = tenantId,
                UserId = userId,
                StatusCode = turn.StatusCode,
                Success = turn.Status == ChatTurnStatusEnum.Success,
                Fallback = turn.Fallback,
                TotalMs = turn.TotalMs,
                TimeToFirstTokenMs = turn.TimeToFirstTokenMs,
                StreamingMs = turn.StreamingMs,
                PromptTokens = turn.PromptTokens,
                CachedPromptTokens = turn.CachedPromptTokens,
                CompletionTokens = turn.CompletionTokens,
                ToolCalls = Math.Max(0, turn.ToolCallCount - turn.ToolFailures - turn.ToolDenied),
                ToolFailures = turn.ToolFailures,
                ToolDenied = turn.ToolDenied,
                Rating = turn.FeedbackRating
            };

            if (turn.ToolCalls.Count > 0 && turn.ToolFailures == 0 && turn.ToolDenied == 0)
            {
                // Turns saved before failures and denials were counted: work them out from the tool calls.
                record.ToolDenied = turn.ToolCalls.Count(call => call.Approval == ChatToolApprovalEnum.Denied || call.Approval == ChatToolApprovalEnum.TimedOut);
                record.ToolFailures = turn.ToolCalls.Count(call => call.Approval != ChatToolApprovalEnum.Denied && call.Approval != ChatToolApprovalEnum.TimedOut && !call.Success);
                record.ToolCalls = Math.Max(0, turn.ToolCalls.Count - record.ToolDenied - record.ToolFailures);
            }

            double modelMs = turn.ModelMs;
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ModelFirstToken, turn.TimeToFirstTokenMs);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ModelStreaming, modelMs > 0 && turn.TimeToFirstTokenMs.HasValue ? modelMs - turn.TimeToFirstTokenMs.Value : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Tools, turn.ToolCallCount > 0 ? turn.ToolMs : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Approval, turn.ApprovalMs > 0 ? turn.ApprovalMs : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Other, modelMs > 0 ? turn.TotalMs - modelMs - turn.ToolMs - turn.ApprovalMs : null);
            return record;
        }

        /// <summary>
        /// Map a served request (network requests this machine answered).
        /// </summary>
        /// <param name="receivedUtc">Received.</param>
        /// <param name="model">Model.</param>
        /// <param name="provider">Provider.</param>
        /// <param name="statusCode">Status code, when known.</param>
        /// <param name="success">True when it succeeded.</param>
        /// <param name="durationMs">Total milliseconds.</param>
        /// <param name="timeToHeadersMs">Milliseconds to provider headers.</param>
        /// <param name="timeToFirstByteMs">Milliseconds to the first byte.</param>
        /// <param name="promptTokens">Prompt tokens.</param>
        /// <param name="completionTokens">Completion tokens.</param>
        /// <returns>Record.</returns>
        public static UsageRecord FromServedRequest(DateTime receivedUtc, string? model, string? provider, int? statusCode, bool success, double? durationMs, double? timeToHeadersMs, double? timeToFirstByteMs, long? promptTokens, long? completionTokens)
        {
            UsageRecord record = new UsageRecord
            {
                TimestampUtc = receivedUtc,
                Model = model,
                Provider = provider,
                Route = provider == null ? "local" : "local:" + provider,
                StatusCode = statusCode,
                Success = success,
                Fallback = null,
                TotalMs = durationMs,
                TimeToFirstTokenMs = timeToFirstByteMs,
                StreamingMs = durationMs.HasValue && timeToFirstByteMs.HasValue ? Math.Max(0, durationMs.Value - timeToFirstByteMs.Value) : null,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            };

            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ProviderHeaders, timeToHeadersMs);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ProviderFirstToken, timeToFirstByteMs.HasValue && timeToHeadersMs.HasValue ? timeToFirstByteMs.Value - timeToHeadersMs.Value : null);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Streaming, durationMs.HasValue && timeToFirstByteMs.HasValue ? durationMs.Value - timeToFirstByteMs.Value : null);
            return record;
        }

        /// <summary>
        /// Map a local endpoint request. The listener measures the first byte from when the provider call started, after
        /// any wait for a concurrency slot, and the latency from when the request arrived.
        /// </summary>
        /// <param name="receivedUtc">Received.</param>
        /// <param name="model">Model.</param>
        /// <param name="route">Route header value.</param>
        /// <param name="provider">Provider, or empty.</param>
        /// <param name="statusCode">Status code.</param>
        /// <param name="fallback">True when a network fallback answered.</param>
        /// <param name="latencyMs">Total milliseconds from arrival.</param>
        /// <param name="firstByteMs">Milliseconds from the provider call starting to its first byte.</param>
        /// <param name="queuedMs">Milliseconds waiting for a slot.</param>
        /// <param name="promptTokens">Prompt tokens.</param>
        /// <param name="cachedPromptTokens">Cached prompt tokens, when recorded.</param>
        /// <param name="completionTokens">Completion tokens.</param>
        /// <returns>Record.</returns>
        public static UsageRecord FromLocalEndpointRequest(DateTime receivedUtc, string? model, string? route, string? provider, int statusCode, bool fallback, double latencyMs, double? firstByteMs, double queuedMs, long? promptTokens, long? cachedPromptTokens, long? completionTokens)
        {
            double queue = Math.Max(0, queuedMs);
            double? firstToken = firstByteMs.HasValue ? queue + firstByteMs.Value : null;
            UsageRecord record = new UsageRecord
            {
                TimestampUtc = receivedUtc,
                Model = String.IsNullOrWhiteSpace(model) ? null : model,
                Provider = String.IsNullOrWhiteSpace(provider) ? null : provider,
                Route = route,
                StatusCode = statusCode,
                Success = statusCode > 0 && statusCode < 400,
                Fallback = fallback,
                TotalMs = latencyMs,
                TimeToFirstTokenMs = firstToken,
                StreamingMs = firstToken.HasValue ? Math.Max(0, latencyMs - firstToken.Value) : null,
                PromptTokens = promptTokens,
                CachedPromptTokens = cachedPromptTokens,
                CompletionTokens = completionTokens
            };

            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Queue, queue);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ProviderFirstToken, firstByteMs);
            UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Streaming, firstToken.HasValue ? latencyMs - firstToken.Value : null);
            return record;
        }
    }
}
