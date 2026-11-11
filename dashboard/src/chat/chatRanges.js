// Range and bucket size for every chat time range picker, the same presets every Xeno chart uses: 60 buckets of 1 minute
// for the last hour, 96 of 15 minutes for the last day, 84 of 2 hours for the last week, and 120 of 6 hours for the last
// month (HistoryRanges.ChatBucketSeconds, UsageRanges).
export const chatRanges = {
  lastHour: { hours: 1, bucketSeconds: 60 },
  lastDay: { hours: 24, bucketSeconds: 15 * 60 },
  lastWeek: { hours: 24 * 7, bucketSeconds: 2 * 3600 },
  lastMonth: { hours: 24 * 30, bucketSeconds: 6 * 3600 }
};
