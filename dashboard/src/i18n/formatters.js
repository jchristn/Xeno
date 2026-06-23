export function formatNumber(value, locale) {
  return new Intl.NumberFormat(locale).format(value ?? 0);
}

export function formatDateTime(value, locale) {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
}

export function formatDuration(value, locale) {
  return `${formatNumber(value ?? 0, locale)} ms`;
}
