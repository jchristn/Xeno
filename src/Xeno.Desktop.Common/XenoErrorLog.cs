namespace Xeno.Desktop.Common
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using Avalonia.Threading;
    using Xeno.Client;

    /// <summary>
    /// Records unexpected errors in the desktop app and the tray agent to logs/&lt;app&gt;-errors.log in the client home.
    /// An error on the UI thread is logged and handled, so the app keeps running instead of exiting; the caller is told so
    /// it can say what happened. Only the exception (type, message, stack) is written, never chat or request content.
    /// </summary>
    public static class XenoErrorLog
    {
        #region Private-Members

        private static readonly object _Lock = new object();
        private static string _App = "xeno-desktop";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Path of the error log for an app.
        /// </summary>
        /// <param name="app">App name, for example "xeno-desktop".</param>
        /// <returns>Path.</returns>
        public static string FileFor(string app)
        {
            return Path.Combine(ClientSettings.SettingsDirectory, "logs", app + "-errors.log");
        }

        /// <summary>
        /// Start recording errors. Call once on the UI thread after the application is initialized.
        /// </summary>
        /// <param name="app">App name, for example "xeno-desktop".</param>
        /// <param name="report">Called on the UI thread with a one-line message after a UI error is handled, or null.</param>
        /// <exception cref="ArgumentException">app is empty.</exception>
        public static void Install(string app, Action<string>? report)
        {
            if (String.IsNullOrWhiteSpace(app)) throw new ArgumentException("An app name is required.", nameof(app));
            _App = app;
            Dispatcher.UIThread.UnhandledException += (sender, args) =>
            {
                Write("UI", args.Exception);
                args.Handled = true;
                report?.Invoke("Something went wrong (" + args.Exception.GetType().Name + "). Details are in " + FileFor(_App) + ".");
            };
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Write("Task", args.Exception);
                args.SetObserved();
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception exception) Write("Fatal", exception);
            };
        }

        /// <summary>
        /// Append an exception to the app's error log. Failures to write are ignored.
        /// </summary>
        /// <param name="kind">Where it came from, for example "UI".</param>
        /// <param name="exception">Exception.</param>
        public static void Write(string kind, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            try
            {
                string file = FileFor(_App);
                string? directory = Path.GetDirectoryName(file);
                if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                string entry = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " [" + kind + "] " + exception + Environment.NewLine + Environment.NewLine;
                lock (_Lock)
                {
                    File.AppendAllText(file, entry);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        #endregion
    }
}
