// Telemetry metrics both web dashboards chart, with the units XenoCharts uses in the desktop and terminal apps.
export const hostMetrics = ['CpuUtilizationPercent', 'MemoryUtilizationPercent', 'DiskUtilizationPercent', 'GpuUtilizationPercent', 'GpuMemoryUtilizationPercent', 'GpuTemperatureCelsius', 'GpuPowerWatts'];
export const providerMetrics = ['ActiveRequests', 'QueuedRequests', 'WaitingRequests', 'RequestRatePerSecond', 'ErrorRatePerSecond', 'PromptTokensPerSecond', 'GenerationTokensPerSecond', 'TotalTokensPerSecond', 'LatencyP95Ms', 'TimeToFirstTokenP95Ms', 'CacheUsagePercent', 'LoadedModelCount', 'AvailableModelCount'];

export function hostMetricUnit(metric) {
  if (metric === 'GpuTemperatureCelsius') return ' C';
  if (metric === 'GpuPowerWatts') return ' W';
  return '%';
}

export function providerMetricUnit(metric) {
  if (metric.endsWith('Percent')) return '%';
  if (metric.endsWith('PerSecond')) return '/s';
  if (metric.endsWith('Ms')) return ' ms';
  return '';
}
