import { useTranslation } from 'react-i18next';
import { formatChartValue } from './usageCharts.js';

// One request's timing: a bar per stage with milliseconds and its share of the total.
export default function TimingBars({ stages }) {
  const { i18n, t } = useTranslation();
  const locale = i18n.resolvedLanguage || i18n.language;
  if (!stages?.length) return <div className="usage-empty">{t('usageEmptyRequestTiming')}</div>;
  const total = stages.reduce((sum, item) => sum + item.ms, 0);
  return (
    <div aria-label={t('usageChartRequestTiming')} className="usage-bars usage-timing" role="img">
      {stages.map((item) => (
        <div className="usage-bar-row" key={item.stage}>
          <span className="usage-bar-label">{t(`usageStage${item.stage}`)}</span>
          <div className="usage-bar-track"><i style={{ background: 'var(--color-info)', width: `${total > 0 ? (item.ms / total) * 100 : 0}%` }} /></div>
          <span className="usage-bar-value">{formatChartValue(item.ms, ' ms', locale)} ({total > 0 ? Math.round((item.ms / total) * 100) : 0}%)</span>
        </div>
      ))}
    </div>
  );
}

function stage(stages, name, value) {
  if (value === undefined || value === null || Number.isNaN(value)) return;
  stages.push({ stage: name, ms: Math.max(0, value) });
}

// Stages of a chat turn, matching UsageRecordMapper.FromChatTurn.
export function chatTurnStages(turn) {
  const stages = [];
  const modelMs = turn?.modelMs || 0;
  stage(stages, 'ModelFirstToken', turn?.timeToFirstTokenMs);
  if (modelMs > 0 && turn?.timeToFirstTokenMs != null) stage(stages, 'ModelStreaming', modelMs - turn.timeToFirstTokenMs);
  if (turn?.toolCallCount > 0) stage(stages, 'Tools', turn.toolMs || 0);
  if (turn?.approvalMs > 0) stage(stages, 'Approval', turn.approvalMs);
  if (modelMs > 0) stage(stages, 'Other', (turn.totalMs || 0) - modelMs - (turn.toolMs || 0) - (turn.approvalMs || 0));
  return stages;
}

// Stages of a request this machine served, matching UsageRecordMapper.FromServedRequest.
export function servedRequestStages(telemetry) {
  const stages = [];
  const headers = telemetry?.timeToHeadersMs;
  const firstByte = telemetry?.timeToFirstByteMs;
  const duration = telemetry?.durationMs;
  stage(stages, 'ProviderHeaders', headers);
  if (firstByte != null && headers != null) stage(stages, 'ProviderFirstToken', firstByte - headers);
  if (duration != null && firstByte != null) stage(stages, 'Streaming', duration - firstByte);
  return stages;
}

// Stages of a request the server relayed, matching UsageRecordMapper.FromNetworkRequest.
export function networkRequestStages(entry) {
  const stages = [];
  const telemetry = entry?.telemetry || {};
  const roundTrip = telemetry.clientRoundTripMs;
  if (roundTrip != null && entry?.durationMs != null) stage(stages, 'ServerOverhead', entry.durationMs - roundTrip);
  if (roundTrip != null && telemetry.upstreamTotalMs != null) stage(stages, 'RelayAndClient', roundTrip - telemetry.upstreamTotalMs);
  stage(stages, 'ProviderHeaders', telemetry.requestToHeadersMs);
  if (telemetry.timeToFirstTokenMs != null && telemetry.requestToHeadersMs != null) stage(stages, 'ProviderFirstToken', telemetry.timeToFirstTokenMs - telemetry.requestToHeadersMs);
  stage(stages, 'Streaming', telemetry.totalStreamingTimeMs);
  return stages;
}
