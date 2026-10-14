namespace Xeno.Desktop
{
    using System;
    using System.Threading.Tasks;
    using Avalonia.Threading;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// State shared by the desktop app's windows: the workspace, the latest client status (refreshed on a timer),
    /// preferences, and a status message line. Members are accessed on the UI thread.
    /// </summary>
    public sealed class DesktopSession : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Workspace used for every operation.
        /// </summary>
        public XenoWorkspace Workspace { get; } = new XenoWorkspace();

        /// <summary>
        /// Latest client status, or null before the first refresh.
        /// </summary>
        public ClientStatusSnapshot? Status { get; private set; } = null;

        /// <summary>
        /// Front-end preferences.
        /// </summary>
        public XenoFrontEndPreferences Preferences { get; } = XenoFrontEndPreferences.Load();

        /// <summary>
        /// Latest message shown in the status bar.
        /// </summary>
        public string Message { get; private set; } = String.Empty;

        /// <summary>
        /// True while a long-running client operation (start, stop, restart) is in progress.
        /// </summary>
        public bool Busy { get; private set; } = false;

        /// <summary>
        /// Raised on the UI thread after the status, message, or busy state changes.
        /// </summary>
        public event EventHandler? Changed;

        #endregion

        #region Private-Members

        private readonly DispatcherTimer _Timer;
        private bool _Refreshing = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the session and start refreshing status.
        /// </summary>
        public DesktopSession()
        {
            _Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Preferences.RefreshSeconds) };
            _Timer.Tick += (sender, args) => _ = RefreshStatusAsync();
            _Timer.Start();
            _ = RefreshStatusAsync();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Refresh the client status now.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task RefreshStatusAsync()
        {
            if (_Refreshing) return;
            _Refreshing = true;
            try
            {
                ClientStatusSnapshot snapshot = await Task.Run(() => Workspace.GetStatus()).ConfigureAwait(true);
                Status = snapshot;
                Changed?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _Refreshing = false;
            }
        }

        /// <summary>
        /// Show a status bar message.
        /// </summary>
        /// <param name="message">Message.</param>
        public void Report(string message)
        {
            Message = message ?? String.Empty;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Run a client operation with the busy flag set, reporting progress and refreshing status afterward.
        /// </summary>
        /// <param name="progress">Message while running.</param>
        /// <param name="operation">Operation returning the completion message.</param>
        /// <returns>Completion message, or the error message when the operation failed.</returns>
        public async Task<string> RunBusyAsync(string progress, Func<Task<string>> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (Busy) return "Another client operation is in progress.";
            Busy = true;
            Report(progress);
            string message;
            try
            {
                message = await operation().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                message = ex.Message;
            }
            finally
            {
                Busy = false;
            }

            Report(message);
            await RefreshStatusAsync().ConfigureAwait(true);
            return message;
        }

        /// <summary>
        /// Save preferences.
        /// </summary>
        public void SavePreferences()
        {
            Preferences.Save();
        }

        /// <summary>
        /// Stop refreshing and release the workspace.
        /// </summary>
        public void Dispose()
        {
            _Timer.Stop();
            Workspace.Dispose();
        }

        #endregion
    }
}
