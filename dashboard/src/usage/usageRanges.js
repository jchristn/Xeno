// Range presets for every Usage chart: 60 buckets of 1 minute, 96 of 15 minutes, 84 of 2 hours, and 120 of 6 hours
// (UsageRanges in Xeno.Core).
export const usageRanges = {
  lastHour: { hours: 1, bucketMinutes: 1 },
  lastDay: { hours: 24, bucketMinutes: 15 },
  lastWeek: { hours: 24 * 7, bucketMinutes: 120 },
  lastMonth: { hours: 24 * 30, bucketMinutes: 360 }
};

export function usageWindow(range) {
  const { hours, bucketMinutes } = usageRanges[range] || usageRanges.lastDay;
  const to = new Date();
  return { fromUtc: new Date(to.getTime() - hours * 3600 * 1000).toISOString(), toUtc: to.toISOString(), bucketMinutes };
}

export function usageQueryString(values) {
  const parts = Object.entries(values || {})
    .filter(([, value]) => value !== undefined && value !== null && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
  return parts.length ? `?${parts.join('&')}` : '';
}
