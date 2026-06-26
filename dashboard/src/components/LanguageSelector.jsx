import { useTranslation } from 'react-i18next';
import { supportedLocales } from '../i18n/localeRegistry.js';

export default function LanguageSelector() {
  const { t, i18n } = useTranslation();

  return (
    <label className="language-selector" title={t('tooltipLanguage')}>
      <span>{t('language')}</span>
      <select value={i18n.language} onChange={(event) => i18n.changeLanguage(event.target.value)} title={t('tooltipLanguage')}>
        {supportedLocales.map((locale) => (
          <option key={locale.code} value={locale.code}>{locale.nativeLabel}</option>
        ))}
      </select>
    </label>
  );
}
