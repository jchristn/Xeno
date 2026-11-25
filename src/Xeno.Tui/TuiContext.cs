namespace Xeno.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Modals;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Services shared by the terminal app's shell and pages: the TUIKit host, the workspace, the latest status, and
    /// helpers for background work, modals, and notifications. UI state is touched only on the loop thread; background
    /// results return through <see cref="Post(Action)"/>.
    /// </summary>
    internal sealed class TuiContext
    {
        #region Public-Members

        /// <summary>
        /// TUIKit host.
        /// </summary>
        public TuiApplication App { get; }

        /// <summary>
        /// Workspace.
        /// </summary>
        public XenoWorkspace Workspace { get; }

        /// <summary>
        /// Preferences.
        /// </summary>
        public XenoFrontEndPreferences Preferences { get; }

        /// <summary>
        /// Preferences file, or null for the default.
        /// </summary>
        public string? PreferencesPath { get; }

        /// <summary>
        /// Latest status, or null before the first refresh.
        /// </summary>
        public ClientStatusSnapshot? Status { get; set; } = null;

        /// <summary>
        /// True while a client operation runs.
        /// </summary>
        public bool Busy { get; set; } = false;

        /// <summary>
        /// Latest message shown on the message line.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Severity of <see cref="Message"/>.
        /// </summary>
        public NotificationSeverity MessageSeverity { get; private set; } = NotificationSeverity.Info;

        /// <summary>
        /// When <see cref="Message"/> was set (UTC), or null before the first message.
        /// </summary>
        public DateTime? MessageUtc { get; private set; } = null;

        /// <summary>
        /// True while the work described by <see cref="Message"/> is still running.
        /// </summary>
        public bool Working { get; private set; } = false;

        /// <summary>
        /// Active theme.
        /// </summary>
        public Theme Theme
        {
            get { return App.Theme; }
        }

        /// <summary>
        /// Executes shared menu commands.
        /// </summary>
        public Action<XenoCommandEnum> Execute { get; set; } = command => { };

        /// <summary>
        /// Navigates to a page.
        /// </summary>
        public Action<XenoPageEnum> Navigate { get; set; } = page => { };

        /// <summary>
        /// Apply the appearance and colors in <see cref="Preferences"/> again, after either changes.
        /// </summary>
        public Action Restyle { get; set; } = () => { };

        /// <summary>
        /// Background work in flight, for tests that wait for quiet.
        /// </summary>
        public int Pending
        {
            get { return Volatile.Read(ref _Pending); }
        }

        #endregion

        #region Private-Members

        private int _Pending = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a context.
        /// </summary>
        /// <param name="app">TUIKit host.</param>
        /// <param name="workspace">Workspace.</param>
        /// <param name="preferences">Preferences.</param>
        /// <param name="preferencesPath">Preferences file, or null.</param>
        public TuiContext(TuiApplication app, XenoWorkspace workspace, XenoFrontEndPreferences preferences, string? preferencesPath)
        {
            App = app ?? throw new ArgumentNullException(nameof(app));
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            PreferencesPath = preferencesPath;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run an action on the loop thread.
        /// </summary>
        /// <param name="action">Action.</param>
        public void Post(Action action)
        {
            App.Post(action);
        }

        /// <summary>
        /// Show an informational message on the message line.
        /// </summary>
        /// <param name="message">Message.</param>
        public void Report(string message)
        {
            SetMessage(message, NotificationSeverity.Info, false);
        }

        /// <summary>
        /// Show work in progress on the message line, with a spinner, until the next message replaces it.
        /// </summary>
        /// <param name="message">What is happening, for example "Starting the client...".</param>
        public void Progress(string message)
        {
            SetMessage(message, NotificationSeverity.Info, true);
        }

        /// <summary>
        /// Show the outcome of an action on the message line, colored by severity. Errors also raise a toast.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="severity">Severity.</param>
        public void Notify(string message, NotificationSeverity severity)
        {
            SetMessage(message, severity, false);
            if (severity == NotificationSeverity.Error) App.Notify(message ?? String.Empty, severity);
        }

        /// <summary>
        /// Severity of an outcome message from a shared action: errors for messages that report a failure, success
        /// otherwise.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Severity.</returns>
        public static NotificationSeverity SeverityOf(string? message)
        {
            if (String.IsNullOrWhiteSpace(message)) return NotificationSeverity.Info;
            if (message.StartsWith("Could not", StringComparison.Ordinal)
                || message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("did not", StringComparison.OrdinalIgnoreCase)) return NotificationSeverity.Error;
            return NotificationSeverity.Success;
        }

        /// <summary>
        /// Fetch data in the background, then apply it on the loop thread. Known failures become an error toast.
        /// </summary>
        /// <typeparam name="T">Data type.</typeparam>
        /// <param name="fetch">Background fetch.</param>
        /// <param name="apply">Applies the result on the loop thread.</param>
        /// <param name="failed">Optional, called on the loop thread with the error message after a known failure.</param>
        /// <returns>Task that completes after the result is posted.</returns>
        public async Task LoadAsync<T>(Func<CancellationToken, Task<T>> fetch, Action<T> apply, Action<string>? failed = null)
        {
            Interlocked.Increment(ref _Pending);
            try
            {
                T result = await Task.Run(() => fetch(CancellationToken.None)).ConfigureAwait(false);
                Post(() => apply(result));
            }
            catch (Exception ex) when (IsReportable(ex))
            {
                Post(() =>
                {
                    Notify(ex.Message, NotificationSeverity.Error);
                    failed?.Invoke(ex.Message);
                });
            }
            finally
            {
                Interlocked.Decrement(ref _Pending);
            }
        }

        /// <summary>
        /// Run background work that returns a message, then report it.
        /// </summary>
        /// <param name="work">Work.</param>
        /// <param name="after">Optional follow-up on the loop thread.</param>
        /// <returns>Task.</returns>
        public Task RunAsync(Func<CancellationToken, Task<string>> work, Action? after = null)
        {
            return LoadAsync(work, message =>
            {
                Notify(message, SeverityOf(message));
                after?.Invoke();
            });
        }

        /// <summary>
        /// Show a form dialog and return the values, or null when cancelled.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="intro">Intro text.</param>
        /// <param name="fields">Fields.</param>
        /// <param name="validator">Optional validator run in the background.</param>
        /// <returns>Values, or null.</returns>
        /// <param name="submitText">Submit action, for the footer hint.</param>
        /// <param name="progressText">Shown while the validator runs.</param>
        public async Task<Dictionary<string, string>?> FormAsync(string title, string intro, IEnumerable<XenoFormFieldSpec> fields, Func<Dictionary<string, string>, Task<string?>>? validator, string submitText = "Save", string progressText = "Saving...")
        {
            FormDialog dialog = new FormDialog(title, intro, fields, validator, Post, submitText, progressText);
            dialog.ApplyTheme(Theme);
            object? result = await App.ShowAsync(dialog).ConfigureAwait(false);
            return result as Dictionary<string, string>;
        }

        /// <summary>
        /// Show a shared form, run its submit action (keeping the form open on errors), and report success.
        /// </summary>
        /// <param name="spec">Form, or null to do nothing.</param>
        /// <param name="after">Optional follow-up on the loop thread after success.</param>
        /// <returns>Task.</returns>
        public async Task FormAsync(XenoFormSpec? spec, Action? after = null)
        {
            if (spec == null) return;
            FormDialog dialog = new FormDialog(spec.Title, spec.Intro, spec.Fields, form => spec.SubmitAsync(form, CancellationToken.None), Post, spec.SubmitText, spec.ProgressText, spec.CanAddAnother ? spec.AddAnotherText : null)
            {
                NextValues = form => spec.NextValues(form),
                AddedMessage = spec.SuccessMessage,
                Added = message =>
                {
                    // "Save and add another" keeps the form open: confirm on the message line and refresh the page underneath.
                    Notify(message, NotificationSeverity.Success);
                    after?.Invoke();
                }
            };
            dialog.ApplyTheme(Theme);
            object? result = await App.ShowAsync(dialog).ConfigureAwait(false);
            Dictionary<string, string>? values = result as Dictionary<string, string>;
            if (values == null)
            {
                if (dialog.AddedCount == 0) Post(() => Report(spec.Title + ": cancelled, nothing changed."));
                return;
            }

            string message = spec.SuccessMessage(values);
            Post(() =>
            {
                Notify(message, NotificationSeverity.Success);
                after?.Invoke();
            });
        }

        /// <summary>
        /// Sign in through the shared form, then confirm the result in a dialog that offers the next step (start the
        /// client, or restart it when it is running with the previous credential).
        /// </summary>
        /// <param name="after">Optional follow-up on the loop thread after a successful sign-in, before the dialog.</param>
        /// <returns>Task that completes when the confirmation closes.</returns>
        public async Task SignInAsync(Action? after = null)
        {
            XenoFormSpec spec = XenoForms.SignIn(Workspace);
            Dictionary<string, string>? values = await FormAsync(spec.Title, spec.Intro, spec.Fields, form => spec.SubmitAsync(form, CancellationToken.None), spec.SubmitText, spec.ProgressText).ConfigureAwait(false);
            if (values == null)
            {
                Post(() => Report("Sign-in cancelled. Nothing changed."));
                return;
            }

            ClientStatusSnapshot status = await Task.Run(() => Workspace.GetStatus()).ConfigureAwait(false);
            XenoFollowUp followUp = XenoClientActions.AfterSignIn(status);
            TaskCompletionSource<bool> shown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(() =>
            {
                Status = status;
                Notify(spec.SuccessMessage(values), NotificationSeverity.Success);
                after?.Invoke();
                _ = ShowFollowUpAsync(followUp).ContinueWith(task => shown.TrySetResult(true), TaskScheduler.Default);
            });
            await shown.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Show a follow-up dialog and run its next step when chosen.
        /// </summary>
        /// <param name="followUp">Follow-up.</param>
        /// <returns>Task that completes when the dialog closes.</returns>
        public async Task ShowFollowUpAsync(XenoFollowUp followUp)
        {
            ArgumentNullException.ThrowIfNull(followUp);
            List<string> buttons = new List<string>();
            if (followUp.Command != null && !String.IsNullOrEmpty(followUp.ActionText)) buttons.Add(followUp.ActionText);
            buttons.Add(buttons.Count == 0 ? "OK" : "Not Now");
            ChoiceDialog modal = new ChoiceDialog(followUp.Title, followUp.Message, buttons);
            modal.ApplyTheme(Theme);
            object? result = await App.ShowAsync(modal).ConfigureAwait(false);
            if (followUp.Command != null && buttons.Count > 1 && result is int index && index == 0)
            {
                XenoCommandEnum command = followUp.Command.Value;
                Post(() => Execute(command));
            }
        }

        /// <summary>
        /// Ask for confirmation.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="message">Message.</param>
        /// <param name="confirmLabel">Confirm button label.</param>
        /// <returns>True when confirmed.</returns>
        public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
        {
            ChoiceDialog modal = new ChoiceDialog(title, message, new List<string> { confirmLabel, "Cancel" });
            modal.ApplyTheme(Theme);
            object? result = await App.ShowAsync(modal).ConfigureAwait(false);
            return result is int index && index == 0;
        }

        /// <summary>
        /// Finds where a widget was last drawn on screen, or null. Set by the app.
        /// </summary>
        public Func<IWidget, Rect?>? Locate { get; set; } = null;

        /// <summary>
        /// Show a page dropdown's choices as a popup under it, marking the current choice. Picking one (with Enter or
        /// a click) selects it; Esc or a click outside leaves the selection alone.
        /// </summary>
        /// <param name="picker">Picker.</param>
        /// <returns>Task that completes when the popup closes.</returns>
        public Task ChooseAsync(DropdownPicker picker)
        {
            ArgumentNullException.ThrowIfNull(picker);
            Dropdown<string> dropdown = picker.Dropdown;
            List<MenuItem> items = new List<MenuItem>();
            for (int i = 0; i < dropdown.Items.Count; i++)
            {
                int index = i;
                string marker = i == dropdown.SelectedIndex ? "• " : "  ";
                items.Add(new MenuItem(marker + dropdown.Items[i] + " ", () => dropdown.SelectedIndex = index));
            }

            if (items.Count == 0) return Task.CompletedTask;
            Rect? at = Locate?.Invoke(picker);
            int x = at == null ? 30 : Math.Max(0, at.Value.X - 1);
            int y = at == null ? 6 : at.Value.Y + 1;
            ContextMenu menu = new ContextMenu(items, x, y);
            menu.ApplyTheme(Theme);
            return App.ShowAsync(menu);
        }

        /// <summary>
        /// Show Markdown in a scrollable viewer, laid out at the viewer's width.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="markdown">Markdown. Build it with <see cref="Xeno.Client.Presentation.XenoMarkdown"/> so values render verbatim.</param>
        /// <returns>Task that completes when the viewer closes.</returns>
        public Task ShowMarkdownAsync(string title, string markdown)
        {
            MarkdownViewerModal modal = new MarkdownViewerModal(title, markdown);
            modal.ApplyTheme(Theme);
            return App.ShowAsync(modal);
        }

        /// <summary>
        /// Let the user choose a table's columns in a checklist, then show and remember the choice.
        /// </summary>
        /// <param name="table">Table.</param>
        /// <returns>Task that completes when the chooser closes.</returns>
        public async Task ChooseColumnsAsync(Pages.ITuiTableColumns table)
        {
            ArgumentNullException.ThrowIfNull(table);
            IReadOnlyList<string> visible = table.Visible;
            List<CheckListItem> items = table.Columns.Select(column => new CheckListItem { Label = column, Checked = visible.Contains(column, StringComparer.Ordinal) }).ToList();
            string title = String.IsNullOrWhiteSpace(table.Title) ? "Columns" : "Columns: " + table.Title;
            CheckListDialog dialog = new CheckListDialog(title, "Choose the columns this table shows. Your choice is remembered.", items, new List<string> { "Apply", "Defaults", "Cancel" }) { MinimumChecked = 1 };
            dialog.ApplyTheme(Theme);
            object? result = await App.ShowAsync(dialog).ConfigureAwait(false);
            if (!(result is CheckListResult chosen) || chosen.Button == 2) return;
            Post(() =>
            {
                List<string> shown;
                if (chosen.Button == 1)
                {
                    XenoTableColumns.Reset(Preferences, table.Id);
                    shown = table.Defaults.ToList();
                }
                else
                {
                    shown = table.Columns.Where((column, index) => index < chosen.Checked.Count && chosen.Checked[index]).ToList();
                    if (shown.Count == 0) return;
                    XenoTableColumns.Save(Preferences, table.Id, shown);
                }

                SavePreferences();
                table.Apply(shown);
                Notify("Showing " + table.Visible.Count + " of " + table.Columns.Count + " columns" + (String.IsNullOrWhiteSpace(table.Title) ? "." : " in " + table.Title + "."), NotificationSeverity.Info);
            });
        }

        /// <summary>
        /// Import Models: choose the provider endpoints to read, pick models from their live inventories, and advertise
        /// the picked ones. Reports the outcome on the message line.
        /// </summary>
        /// <param name="after">Optional follow-up on the loop thread after models were imported.</param>
        /// <returns>Task that completes when the flow ends.</returns>
        public async Task ImportModelsAsync(Action? after = null)
        {
            List<string> providers = XenoModelImport.Providers(Workspace);
            if (providers.Count == 0)
            {
                Post(() => Notify("Add a provider first. Import reads the models a provider endpoint on this machine has.", NotificationSeverity.Warning));
                return;
            }

            CheckListDialog pick = new CheckListDialog(XenoModelImport.Title, XenoModelImport.ProvidersIntro, providers.Select(name => new CheckListItem { Label = name, Checked = true }), new List<string> { "Load Models", "Cancel" }) { MinimumChecked = 1 };
            pick.ApplyTheme(Theme);
            object? picked = await ShowPostedAsync(pick).ConfigureAwait(false);
            if (!(picked is CheckListResult choice) || choice.Button != 0)
            {
                Post(() => Report(XenoModelImport.Title + ": cancelled, nothing changed."));
                return;
            }

            List<string> chosen = providers.Where((name, index) => index < choice.Checked.Count && choice.Checked[index]).ToList();
            Post(() => Progress("Reading models from " + String.Join(", ", chosen) + "..."));
            TaskCompletionSource<List<ModelImportSource>?> loaded = new TaskCompletionSource<List<ModelImportSource>?>(TaskCreationOptions.RunContinuationsAsynchronously);
            await LoadAsync(token => XenoModelImport.LoadAsync(Workspace, chosen, token), sources => loaded.TrySetResult(sources), message => loaded.TrySetResult(null)).ConfigureAwait(false);
            List<ModelImportSource>? sections = await loaded.Task.ConfigureAwait(false);
            if (sections == null) return;
            Post(() => Report("Read " + sections.Count + " provider" + (sections.Count == 1 ? String.Empty : "s") + "."));

            ModelImportModal modal = new ModelImportModal(sections);
            modal.ApplyTheme(Theme);
            object? result = await ShowPostedAsync(modal).ConfigureAwait(false);
            if (!(result is List<ModelImportItem> items))
            {
                Post(() => Report(XenoModelImport.Title + ": cancelled, nothing changed."));
                return;
            }

            if (items.Count == 0)
            {
                Post(() => Report(XenoModelImport.Title + ": no models selected, nothing changed."));
                return;
            }

            Post(() => Progress("Importing " + items.Count + " model" + (items.Count == 1 ? String.Empty : "s") + "..."));
            await LoadAsync(token => Workspace.ImportModelsAsync(items, token), imported =>
            {
                Notify(XenoModelImport.Describe(imported), imported.Failed.Count > 0 ? NotificationSeverity.Warning : NotificationSeverity.Success);
                after?.Invoke();
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Open Manage Models for a provider. Pulls started there keep running after it closes.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="after">Optional follow-up on the loop thread after changes and when the dialog closes.</param>
        /// <returns>Task that completes when the dialog closes.</returns>
        public async Task ManageModelsAsync(string provider, Action? after = null)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ManageModelsModal modal = new ManageModelsModal(this, provider, () => after?.Invoke());
            modal.ApplyTheme(Theme);
            try
            {
                await ShowPostedAsync(modal).ConfigureAwait(false);
            }
            finally
            {
                modal.Detach();
            }

            Post(() => after?.Invoke());
        }

        /// <summary>
        /// Save preferences.
        /// </summary>
        public void SavePreferences()
        {
            Preferences.Save(PreferencesPath);
        }

        #endregion

        #region Private-Methods

        private Task<object?> ShowPostedAsync(Modal modal)
        {
            // Modals are pushed on the loop thread, whichever thread the flow continues on.
            TaskCompletionSource<Task<object?>> shown = new TaskCompletionSource<Task<object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(() => shown.TrySetResult(App.ShowAsync(modal)));
            return shown.Task.Unwrap();
        }

        private void SetMessage(string message, NotificationSeverity severity, bool working)
        {
            Message = message ?? String.Empty;
            MessageSeverity = severity;
            MessageUtc = DateTime.UtcNow;
            Working = working;
            App.RequestRender();
        }

        private static bool IsReportable(Exception ex)
        {
            return ex is ArgumentException
                || ex is KeyNotFoundException
                || ex is InvalidOperationException
                || ex is System.IO.IOException
                || ex is UnauthorizedAccessException
                || ex is System.Net.Http.HttpRequestException
                || ex is Microsoft.Data.Sqlite.SqliteException
                || ex is OperationCanceledException;
        }

        #endregion
    }
}
