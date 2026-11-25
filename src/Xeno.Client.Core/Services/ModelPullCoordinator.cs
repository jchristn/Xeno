namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Runs model pulls onto Ollama providers in the background and tracks their progress. A pull keeps running after the
    /// caller that started it goes away (a closed dialog, a page change) until it finishes, fails, or is cancelled.
    /// Jobs live in the process that started them: the local dashboard's pulls live in the client worker, the desktop and
    /// terminal apps' pulls in those apps. Thread safe.
    /// </summary>
    public sealed class ModelPullCoordinator : IDisposable
    {
        /// <summary>
        /// Most finished jobs kept for display. Older finished jobs are dropped first.
        /// </summary>
        public const int MaxFinishedJobs = 50;

        private static readonly Lazy<ModelPullCoordinator> _Shared = new Lazy<ModelPullCoordinator>(() => new ModelPullCoordinator());

        private readonly object _Lock = new object();
        private readonly Dictionary<string, JobEntry> _Jobs = new Dictionary<string, JobEntry>(StringComparer.Ordinal);
        private readonly HttpMessageHandler? _Handler;
        private bool _Disposed;

        /// <summary>
        /// Create a coordinator.
        /// </summary>
        /// <param name="handler">Optional HTTP handler, for tests.</param>
        public ModelPullCoordinator(HttpMessageHandler? handler = null)
        {
            _Handler = handler;
        }

        /// <summary>
        /// The process-wide coordinator front ends share.
        /// </summary>
        public static ModelPullCoordinator Shared => _Shared.Value;

        /// <summary>
        /// Raised (on a background thread) whenever a job changes. Handlers must not block.
        /// </summary>
        public event EventHandler<ModelPullJob>? JobChanged;

        /// <summary>
        /// Start pulling a model. When a pull of the same model onto the same provider is already active, that job is returned instead.
        /// </summary>
        /// <param name="provider">Ollama provider.</param>
        /// <param name="modelName">Model to pull.</param>
        /// <param name="advertiseOnSuccess">Advertise the model once the pull succeeds; <paramref name="onSucceeded"/> does the advertising.</param>
        /// <param name="onSucceeded">Called after a successful pull, for example to advertise the model and save. Its failure is recorded on the job but does not fail the pull.</param>
        /// <returns>Snapshot of the job.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        /// <exception cref="ArgumentException">Model name is missing.</exception>
        /// <exception cref="NotSupportedException">The provider is not Ollama.</exception>
        /// <exception cref="ObjectDisposedException">The coordinator was disposed.</exception>
        public ModelPullJob Start(ClientRunnerSettings provider, string modelName, bool advertiseOnSuccess, Func<ModelPullJob, CancellationToken, Task>? onSucceeded)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (String.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.");
            if (!ProviderModelManager.CanManageModels(provider)) throw new NotSupportedException("Xeno can pull models only onto Ollama providers. '" + provider.Name + "' is " + provider.ApiType + ".");
            string model = modelName.Trim();
            ClientRunnerSettings snapshot = new ClientRunnerSettings
            {
                Name = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                Credential = provider.Credential,
                TimeoutMs = provider.TimeoutMs
            };

            JobEntry entry;
            lock (_Lock)
            {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                JobEntry? existing = _Jobs.Values.FirstOrDefault(item => item.Job.Active
                    && String.Equals(item.Job.Provider, provider.Name, StringComparison.OrdinalIgnoreCase)
                    && LocalModelNameNormalizer.AreEqual(item.Job.Model, model));
                if (existing != null) return existing.Job.Clone();

                entry = new JobEntry(new ModelPullJob
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Provider = provider.Name,
                    Model = model,
                    AdvertiseOnSuccess = advertiseOnSuccess,
                    StartedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                });
                _Jobs[entry.Job.Id] = entry;
                TrimFinished();
            }

            entry.Task = Task.Run(() => RunAsync(entry, snapshot, onSucceeded));
            Raise(entry);
            return Snapshot(entry);
        }

        /// <summary>
        /// Every job, newest first.
        /// </summary>
        /// <returns>Snapshots.</returns>
        public List<ModelPullJob> List()
        {
            lock (_Lock)
            {
                return _Jobs.Values.Select(item => item.Job.Clone()).OrderByDescending(item => item.StartedUtc).ToList();
            }
        }

        /// <summary>
        /// One job.
        /// </summary>
        /// <param name="id">Job id.</param>
        /// <returns>Snapshot, or null when unknown.</returns>
        public ModelPullJob? Get(string id)
        {
            lock (_Lock)
            {
                return _Jobs.TryGetValue(id ?? String.Empty, out JobEntry? entry) ? entry.Job.Clone() : null;
            }
        }

        /// <summary>
        /// Wait for a job to end.
        /// </summary>
        /// <param name="id">Job id.</param>
        /// <param name="token">Cancellation token; cancelling stops the wait, not the pull.</param>
        /// <returns>Final snapshot, or null when unknown.</returns>
        public async Task<ModelPullJob?> WaitAsync(string id, CancellationToken token)
        {
            Task? task;
            lock (_Lock)
            {
                task = _Jobs.TryGetValue(id ?? String.Empty, out JobEntry? entry) ? entry.Task : null;
            }

            if (task == null) return Get(id ?? String.Empty);
            await task.WaitAsync(token).ConfigureAwait(false);
            return Get(id ?? String.Empty);
        }

        /// <summary>
        /// Cancel an active job.
        /// </summary>
        /// <param name="id">Job id.</param>
        /// <returns>True when an active job was cancelled.</returns>
        public bool Cancel(string id)
        {
            JobEntry? entry;
            lock (_Lock)
            {
                if (!_Jobs.TryGetValue(id ?? String.Empty, out entry) || !entry.Job.Active) return false;
            }

            entry.Cancellation.Cancel();
            return true;
        }

        /// <summary>
        /// Forget finished jobs.
        /// </summary>
        /// <returns>Number removed.</returns>
        public int ClearFinished()
        {
            lock (_Lock)
            {
                List<string> finished = _Jobs.Values.Where(item => !item.Job.Active).Select(item => item.Job.Id).ToList();
                foreach (string id in finished)
                {
                    _Jobs[id].Cancellation.Dispose();
                    _Jobs.Remove(id);
                }

                return finished.Count;
            }
        }

        /// <summary>
        /// Cancel every active job and release resources.
        /// </summary>
        public void Dispose()
        {
            List<JobEntry> entries;
            lock (_Lock)
            {
                if (_Disposed) return;
                _Disposed = true;
                entries = _Jobs.Values.ToList();
            }

            foreach (JobEntry entry in entries)
            {
                try
                {
                    entry.Cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private async Task RunAsync(JobEntry entry, ClientRunnerSettings provider, Func<ModelPullJob, CancellationToken, Task>? onSucceeded)
        {
            CancellationToken token = entry.Cancellation.Token;
            Dictionary<string, long[]> layers = new Dictionary<string, long[]>(StringComparer.Ordinal);
            Update(entry, job =>
            {
                job.State = ModelPullStateEnum.Running;
                job.Status = "starting";
            });

            try
            {
                string url = ProviderConfiguration.NormalizeEndpoint(provider.Endpoint) + "/api/pull";
                using (HttpClient client = ProviderModelManager.CreateClient(_Handler, 0))
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    ProviderModelManager.ApplyCredential(request, provider);
                    request.Content = new StringContent(JsonUtil.Serialize(new OllamaModelNameRequest { Model = entry.Job.Model, Stream = true }), Encoding.UTF8, "application/json");
                    using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                            Fail(entry, provider.Name + " answered " + (int)response.StatusCode + ": " + ProviderModelManager.ProviderMessage(body, response.ReasonPhrase));
                            return;
                        }

                        bool succeeded = false;
                        using (Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            while (true)
                            {
                                string? text = await reader.ReadLineAsync(token).ConfigureAwait(false);
                                if (text == null) break;
                                if (String.IsNullOrWhiteSpace(text)) continue;
                                if (!JsonUtil.TryDeserialize(text, out OllamaPullLine? line) || line == null) continue;
                                if (!String.IsNullOrWhiteSpace(line.Error))
                                {
                                    Fail(entry, line.Error!);
                                    return;
                                }

                                if (!String.IsNullOrWhiteSpace(line.Digest) && line.Total.HasValue && line.Total.Value > 0)
                                {
                                    layers[line.Digest!] = new long[] { Math.Min(line.Completed ?? 0, line.Total.Value), line.Total.Value };
                                }

                                long completed = layers.Values.Sum(item => item[0]);
                                long total = layers.Values.Sum(item => item[1]);
                                string status = line.Status ?? String.Empty;
                                if (String.Equals(status, "success", StringComparison.OrdinalIgnoreCase)) succeeded = true;
                                Update(entry, job =>
                                {
                                    if (status.Length > 0) job.Status = status;
                                    job.CompletedBytes = completed;
                                    job.TotalBytes = total;
                                    job.Percent = total > 0 ? Math.Round(completed * 100.0 / total, 1) : null;
                                });
                            }
                        }

                        if (!succeeded)
                        {
                            Fail(entry, "The provider ended the pull without reporting success.");
                            return;
                        }
                    }
                }

                string? advertiseError = null;
                if (onSucceeded != null)
                {
                    try
                    {
                        await onSucceeded(Snapshot(entry), CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        advertiseError = "Pulled, but could not update the Xeno configuration: " + ex.Message;
                    }
                }

                Update(entry, job =>
                {
                    job.State = ModelPullStateEnum.Succeeded;
                    job.Status = "success";
                    job.Percent = 100;
                    if (job.TotalBytes > 0) job.CompletedBytes = job.TotalBytes;
                    job.Advertised = job.AdvertiseOnSuccess && advertiseError == null;
                    job.Error = advertiseError;
                    job.FinishedUtc = DateTime.UtcNow;
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Update(entry, job =>
                {
                    job.State = ModelPullStateEnum.Cancelled;
                    job.Status = "cancelled";
                    job.FinishedUtc = DateTime.UtcNow;
                });
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is InvalidOperationException || ex is OperationCanceledException)
            {
                Fail(entry, "Could not reach " + provider.Name + " at " + provider.Endpoint + ": " + ex.Message);
            }
        }

        private void Fail(JobEntry entry, string message)
        {
            Update(entry, job =>
            {
                job.State = ModelPullStateEnum.Failed;
                job.Status = "failed";
                job.Error = message;
                job.FinishedUtc = DateTime.UtcNow;
            });
        }

        private void Update(JobEntry entry, Action<ModelPullJob> change)
        {
            lock (_Lock)
            {
                change(entry.Job);
                entry.Job.UpdatedUtc = DateTime.UtcNow;
            }

            Raise(entry);
        }

        private ModelPullJob Snapshot(JobEntry entry)
        {
            lock (_Lock)
            {
                return entry.Job.Clone();
            }
        }

        private void Raise(JobEntry entry)
        {
            EventHandler<ModelPullJob>? handler = JobChanged;
            if (handler == null) return;
            try
            {
                handler(this, Snapshot(entry));
            }
            catch (Exception)
            {
                // A front end's progress handler must never break a pull.
            }
        }

        private void TrimFinished()
        {
            List<JobEntry> finished = _Jobs.Values.Where(item => !item.Job.Active).OrderByDescending(item => item.Job.StartedUtc).ToList();
            foreach (JobEntry stale in finished.Skip(MaxFinishedJobs))
            {
                stale.Cancellation.Dispose();
                _Jobs.Remove(stale.Job.Id);
            }
        }

        private sealed class JobEntry
        {
            internal JobEntry(ModelPullJob job)
            {
                Job = job;
            }

            internal ModelPullJob Job { get; }

            internal CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

            internal Task? Task { get; set; }
        }
    }
}
