namespace Xeno.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;

    /// <summary>
    /// Base database driver.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Database provider type.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Settings { get; private set; }

        /// <summary>
        /// Platform state.
        /// </summary>
        public XenoPlatform Platform { get; protected set; } = new XenoPlatform();

        /// <summary>
        /// Instantiate the database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        protected DatabaseDriverBase(DatabaseSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Initialize the database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public virtual async Task InitializeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            bool storageAvailable = await InitializeStorageAsync(token).ConfigureAwait(false);
            string? stateJson = storageAvailable ? await LoadStateJsonAsync(token).ConfigureAwait(false) : null;
            if (!String.IsNullOrWhiteSpace(stateJson))
            {
                PlatformState? state = JsonUtil.Deserialize<PlatformState>(stateJson);
                if (state != null) Platform.LoadState(state);
                if (storageAvailable) await SaveAsync(token).ConfigureAwait(false);
            }
            else
            {
                await Platform.InitializeAsync(token).ConfigureAwait(false);
                if (storageAvailable) await SaveAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Persist current platform state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task SaveAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            string stateJson = JsonUtil.Serialize(Platform.ToState());
            await SaveStateJsonAsync(stateJson, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose resources asynchronously.
        /// </summary>
        /// <returns>Value task.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Initialize provider storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if storage is available.</returns>
        protected abstract Task<bool> InitializeStorageAsync(CancellationToken token = default);

        /// <summary>
        /// Load serialized platform state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Serialized state, if available.</returns>
        protected abstract Task<string?> LoadStateJsonAsync(CancellationToken token = default);

        /// <summary>
        /// Save serialized platform state.
        /// </summary>
        /// <param name="stateJson">Serialized state.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        protected abstract Task SaveStateJsonAsync(string stateJson, CancellationToken token = default);
    }
}
