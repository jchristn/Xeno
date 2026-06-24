export const defaultLocale = 'en';

export const supportedLocales = [
  { code: 'en', label: 'English', nativeLabel: 'English', dir: 'ltr' },
  { code: 'es', label: 'Spanish', nativeLabel: 'Español', dir: 'ltr' },
  { code: 'de', label: 'German', nativeLabel: 'Deutsch', dir: 'ltr' },
  { code: 'ja', label: 'Japanese', nativeLabel: '日本語', dir: 'ltr' },
  { code: 'qps-rtl', label: 'RTL pseudo', nativeLabel: 'RTL pseudo', dir: 'rtl' },
  { code: 'qps-ploc', label: 'Expansion pseudo', nativeLabel: 'Expansion pseudo', dir: 'ltr' }
];

export function normalizeLocale(locale) {
  if (!locale) return defaultLocale;
  const lower = locale.toLowerCase();
  if (lower.startsWith('es')) return 'es';
  if (lower.startsWith('de')) return 'de';
  if (lower.startsWith('ja')) return 'ja';
  if (lower === 'qps-rtl') return 'qps-rtl';
  if (lower === 'qps-ploc') return 'qps-ploc';
  return 'en';
}
