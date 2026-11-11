import { useEffect, useRef } from 'react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// The chart refresh choices in every Xeno app: off, 15 seconds, or 60 seconds.
export const refreshIntervals = [0, 15, 60];

function dialogOpen() {
  return document.querySelector('[role="dialog"]') !== null;
}

// Calls back on the chosen interval. An open dialog pauses refreshing so nothing moves underneath it.
export function useAutoRefresh(seconds, callback) {
  const callbackRef = useRef(callback);

  useEffect(() => {
    callbackRef.current = callback;
  }, [callback]);

  useEffect(() => {
    if (!seconds) return undefined;
    const interval = window.setInterval(() => {
      if (!dialogOpen()) callbackRef.current?.();
    }, seconds * 1000);
    return () => window.clearInterval(interval);
  }, [seconds]);
}

export function AutoRefreshSelect({ disabled = false, onChange, onRefresh, seconds }) {
  const { t } = useTranslation();
  return (
    <label className="chart-refresh" title={t('tooltipAutoRefresh')}>
      <span>{t('autoRefresh')}</span>
      <select disabled={disabled} onChange={(event) => onChange(Number(event.target.value))} title={t('tooltipAutoRefresh')} value={seconds}>
        {refreshIntervals.map((item) => <option key={item} value={item}>{item === 0 ? t('none') : `${item}s`}</option>)}
      </select>
      {onRefresh ? <button className="pagination-button" disabled={disabled} onClick={onRefresh} title={t('tooltipManualRefresh')} type="button"><RefreshCw size={14} /></button> : null}
    </label>
  );
}
