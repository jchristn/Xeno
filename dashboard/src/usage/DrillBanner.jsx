import { useTranslation } from 'react-i18next';

// Says which Usage bucket a list is showing, with a way back to everything.
export default function DrillBanner({ drill, onClear }) {
  const { i18n, t } = useTranslation();
  if (!drill) return null;
  const locale = i18n.resolvedLanguage || i18n.language;
  const format = (value) => new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
  return (
    <div className="usage-drill-banner" role="status">
      <span>{t('usageDrillShowing', { from: format(drill.fromUtc), to: format(drill.toUtc) })}</span>
      {drill.model ? <span>{t('usageDrillModel', { model: drill.model })}</span> : null}
      <button className="action-button secondary" onClick={onClear} type="button">{t('usageDrillClear')}</button>
    </div>
  );
}

// True when a record at a time and with a model is in the drill-down window.
export function matchesDrill(drill, when, model) {
  if (!drill) return true;
  const time = new Date(when).getTime();
  if (Number.isNaN(time) || time < new Date(drill.fromUtc).getTime() || time >= new Date(drill.toUtc).getTime()) return false;
  return !drill.model || drill.model === model;
}
