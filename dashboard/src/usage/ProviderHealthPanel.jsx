import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import StatusStrip from '../charts/StatusStrip.jsx';
import { useUsageApi } from './UsageApiContext.js';
import { usageWindow } from './usageRanges.js';

// Provider health over a range, for the provider telemetry pages.
export default function ProviderHealthPanel({ range, providerName, clientId, onCell }) {
  const { t } = useTranslation();
  const api = useUsageApi();
  const [strip, setStrip] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    api.getProviderHealth({ ...usageWindow(range), providerName, clientId })
      .then((value) => {
        if (!active) return;
        setError('');
        setStrip(value);
      })
      .catch((err) => {
        if (active) setError(err.message);
      });
    return () => {
      active = false;
    };
  }, [range, providerName, clientId]);

  return (
    <section className="workspace-card usage-chart-card" data-chart="usage.health">
      <div className="workspace-card-header"><div><h2>{t('usageChartHealth')}</h2></div></div>
      <div className="workspace-card-body">
        {error ? <div className="usage-empty">{error}</div> : strip ? <StatusStrip onCell={onCell} strip={strip} /> : <div className="usage-empty">{t('usageLoading')}</div>}
      </div>
    </section>
  );
}
