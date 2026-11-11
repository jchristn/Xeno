// A usage analytics response for Playwright: 60 one-minute buckets ending now, with one busy bucket.
export function usageAnalytics(dataset, overrides = {}) {
  const to = Date.now();
  const from = to - 60 * 60000;
  const distribution = (value) => ({ count: 2, average: value, p50: value, p95: value * 2, p99: value * 3, maximum: value * 4 });
  const empty = { count: 0 };
  const buckets = Array.from({ length: 60 }, (_, index) => {
    const busy = index === 30;
    const stages = busy ? [{ stage: 'Queue', count: 2, averageMs: 20, p95Ms: 40 }, { stage: 'Streaming', count: 2, averageMs: 900, p95Ms: 1200 }] : [];
    return {
      bucketStartUtc: new Date(from + index * 60000).toISOString(),
      bucketEndUtc: new Date(from + (index + 1) * 60000).toISOString(),
      requests: busy ? 3 : 0,
      succeeded: busy ? 2 : 0,
      failed: busy ? 1 : 0,
      fallbacks: dataset === 'NetworkRequests' ? undefined : busy ? 1 : 0,
      latency: busy ? distribution(1000) : empty,
      timeToFirstToken: busy ? distribution(200) : empty,
      streaming: busy ? distribution(800) : empty,
      throughput: busy ? distribution(40) : empty,
      promptTokens: busy ? 120 : 0,
      cachedPromptTokens: dataset === 'Chat' ? (busy ? 30 : 0) : undefined,
      completionTokens: busy ? 80 : 0,
      groups: [{ key: 'llama3', requests: busy ? 2 : 0, tokens: busy ? 150 : 0 }, { key: 'Other', requests: busy ? 1 : 0, tokens: busy ? 50 : 0 }],
      stages,
      thumbsUp: busy ? 1 : 0,
      thumbsDown: busy ? 1 : 0,
      toolCalls: busy ? 2 : 0,
      toolFailures: busy ? 1 : 0,
      toolDenied: 0
    };
  });
  const cells = [];
  for (let row = 0; row < 6; row += 1) {
    for (let column = 0; column < 10; column += 1) {
      cells.push({ row, column, startUtc: new Date(from + (row * 10 + column) * 60000).toISOString(), tokens: row === 3 && column === 0 ? 200 : 0, requests: row === 3 && column === 0 ? 3 : 0 });
    }
  }
  return {
    dataset,
    fromUtc: new Date(from).toISOString(),
    toUtc: new Date(to).toISOString(),
    bucketMinutes: 1,
    totals: { requests: 3, succeeded: 2, failed: 1, errorRate: 0.3333, fallbacks: dataset === 'NetworkRequests' ? undefined : 1, latency: distribution(1000), timeToFirstToken: distribution(200), streaming: distribution(800), throughput: distribution(40), promptTokens: 150, cachedPromptTokens: dataset === 'Chat' ? 30 : undefined, completionTokens: 80, thumbsUp: 1, thumbsDown: 1, toolCalls: 2, toolFailures: 1, toolDenied: 0 },
    buckets,
    groupBy: 'Model',
    groupMetric: 'Requests',
    groupKeys: ['llama3', 'Other'],
    groupTotals: [{ key: 'llama3', requests: 2, failed: 0, promptTokens: 100, completionTokens: 50, latency: distribution(900) }, { key: 'Other', requests: 1, failed: 1, promptTokens: 50, completionTokens: 30, latency: distribution(1100) }],
    heatmap: { layout: 'HourByMinute', rows: 6, columns: 10, utcOffsetMinutes: 0, cells, maximumTokens: 200 },
    stageOrder: ['Queue', 'ProviderFirstToken', 'Streaming'],
    stageAverages: [{ stage: 'Queue', count: 2, averageMs: 20, p95Ms: 40 }, { stage: 'Streaming', count: 2, averageMs: 900, p95Ms: 1200 }],
    recordsScanned: 3,
    truncated: false,
    ...overrides
  };
}

export function emptyUsage(dataset) {
  const analytics = usageAnalytics(dataset);
  return {
    ...analytics,
    totals: { requests: 0, succeeded: 0, failed: 0, latency: { count: 0 }, timeToFirstToken: { count: 0 }, streaming: { count: 0 }, throughput: { count: 0 }, promptTokens: 0, completionTokens: 0, thumbsUp: 0, thumbsDown: 0, toolCalls: 0, toolFailures: 0, toolDenied: 0 },
    buckets: analytics.buckets.map((item) => ({ bucketStartUtc: item.bucketStartUtc, bucketEndUtc: item.bucketEndUtc, requests: 0, succeeded: 0, failed: 0, latency: { count: 0 }, timeToFirstToken: { count: 0 }, streaming: { count: 0 }, throughput: { count: 0 }, promptTokens: 0, completionTokens: 0, groups: [], stages: [], thumbsUp: 0, thumbsDown: 0, toolCalls: 0, toolFailures: 0, toolDenied: 0 })),
    groupKeys: [],
    groupTotals: [],
    heatmap: { ...analytics.heatmap, cells: analytics.heatmap.cells.map((cell) => ({ ...cell, tokens: 0, requests: 0 })), maximumTokens: 0 },
    stageAverages: []
  };
}

export function healthStrip(bucketMinutes = 15) {
  const to = Date.now();
  const statuses = ['Healthy', 'Degraded', 'Unhealthy', 'NoData'];
  return {
    bucketMinutes,
    providers: [{ providerName: 'local-ollama', uptimePercent: 75, consecutiveFailures: 0, cells: statuses.map((status, index) => ({ startUtc: new Date(to - (4 - index) * bucketMinutes * 60000).toISOString(), status, samples: status === 'NoData' ? 0 : 4, failures: status === 'Unhealthy' ? 4 : status === 'Degraded' ? 1 : 0 })) }]
  };
}
