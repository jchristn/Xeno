import i18next from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';
import { resources } from './resources.js';
import { defaultLocale, normalizeLocale } from './localeRegistry.js';

const stored = window.localStorage.getItem('xeno.locale');
const initialLocale = normalizeLocale(stored || navigator.language || defaultLocale);

i18next
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    lng: initialLocale,
    fallbackLng: defaultLocale,
    interpolation: { escapeValue: false }
  });

document.documentElement.lang = initialLocale;
document.documentElement.dir = initialLocale === 'ar' || initialLocale === 'qps-rtl' ? 'rtl' : 'ltr';

i18next.on('languageChanged', (locale) => {
  const normalized = normalizeLocale(locale);
  window.localStorage.setItem('xeno.locale', normalized);
  document.documentElement.lang = normalized;
  document.documentElement.dir = normalized === 'ar' || normalized === 'qps-rtl' ? 'rtl' : 'ltr';
});

export default i18next;
