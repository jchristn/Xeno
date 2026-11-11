namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Base class for desktop app pages. A page builds its view in code (rebuilt when the theme changes), offers
    /// header actions, and refreshes its data from the shared <see cref="XenoWorkspace"/>. Members run on the UI thread.
    /// </summary>
    public abstract class DesktopPage
    {
        #region Public-Members

        /// <summary>
        /// Page this view renders.
        /// </summary>
        public abstract XenoPageEnum Page { get; }

        /// <summary>
        /// True when the page's charts follow the auto-refresh choice right now.
        /// </summary>
        public virtual bool AutoRefreshes
        {
            get { return false; }
        }

        /// <summary>
        /// True when the view should be wrapped in a scroll viewer; false for views that size to the window.
        /// </summary>
        public virtual bool Scrolls
        {
            get { return true; }
        }

        #endregion

        #region Protected-Members

        /// <summary>
        /// Session.
        /// </summary>
        protected DesktopSession Session { get; }

        /// <summary>
        /// Main window.
        /// </summary>
        protected MainWindow Window { get; }

        /// <summary>
        /// Workspace.
        /// </summary>
        protected XenoWorkspace Workspace
        {
            get { return Session.Workspace; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        /// <exception cref="ArgumentNullException">Session or window is null.</exception>
        protected DesktopPage(DesktopSession session, MainWindow window)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the page view.
        /// </summary>
        /// <returns>View.</returns>
        public abstract Control Build();

        /// <summary>
        /// Header actions shown at the right of the page title.
        /// </summary>
        /// <returns>Controls.</returns>
        public virtual IReadOnlyList<Control> Actions()
        {
            return new List<Control>();
        }

        /// <summary>
        /// Reload the page's data.
        /// </summary>
        /// <returns>Task.</returns>
        public virtual Task RefreshAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the client status changes.
        /// </summary>
        public virtual void OnStatusChanged()
        {
        }

        /// <summary>
        /// Called when the page is hidden.
        /// </summary>
        public virtual void OnDeactivated()
        {
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Run an operation, reporting known failures in an error dialog instead of crashing.
        /// </summary>
        /// <param name="title">Error dialog title.</param>
        /// <param name="operation">Operation.</param>
        /// <returns>True when the operation completed without a reported failure.</returns>
        protected async Task<bool> GuardAsync(string title, Func<Task> operation)
        {
            try
            {
                await operation().ConfigureAwait(true);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is Microsoft.Data.Sqlite.SqliteException || ex is OperationCanceledException)
            {
                await XenoDialogs.MessageAsync(Window, title, ex.Message, XenoToneEnum.Danger).ConfigureAwait(true);
                return false;
            }
        }

        /// <summary>
        /// Show a status bar message.
        /// </summary>
        /// <param name="message">Message.</param>
        protected void Report(string message)
        {
            Session.Report(message);
        }

        /// <summary>
        /// A cancellation token that never cancels, for UI-initiated work.
        /// </summary>
        protected static CancellationToken None
        {
            get { return CancellationToken.None; }
        }

        #endregion
    }
}
