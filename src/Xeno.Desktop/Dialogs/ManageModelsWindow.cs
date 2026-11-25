namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Threading;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Manage Models for one provider: its live models (size, parameters, quantization, modified, advertised), pulling a
    /// model by name with progress, cancelling a pull, and deleting a model from disk. Pulls keep running after the
    /// dialog closes. Providers other than Ollama are listed only.
    /// </summary>
    public sealed class ManageModelsWindow : XenoDialogWindow
    {
        #region Private-Members

        private readonly XenoWorkspace _Workspace;
        private readonly string _Provider;
        private readonly Action<string> _Report;
        private readonly Action _Changed;
        private readonly XenoTable<ProviderInventoryRow> _Table;
        private readonly TextBlock _Summary;
        private readonly Border _Note;
        private readonly Border _Error;
        private readonly StackPanel _PullPanel;
        private readonly TextBox _PullName;
        private readonly CheckBox _Advertise;
        private readonly Button _Delete;
        private readonly StackPanel _Pulls = new StackPanel { Spacing = 8 };
        private ProviderModelInventory? _Inventory;
        private bool _Loading = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the dialog.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="provider">Provider name.</param>
        /// <param name="report">Reports outcomes on the main window's status bar.</param>
        /// <param name="changed">Called after the provider's models or advertisements change.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ManageModelsWindow(XenoWorkspace workspace, string provider, Action<string> report, Action changed)
        {
            _Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _Report = report ?? throw new ArgumentNullException(nameof(report));
            _Changed = changed ?? throw new ArgumentNullException(nameof(changed));
            Title = XenoModelManagement.Title(provider);
            Width = 900;
            Height = 680;
            MinWidth = 640;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;

            _Summary = Ui.Muted("Reading models...");
            _Note = Ui.Notice(XenoModelManagement.OllamaOnlyNote, XenoToneEnum.Info);
            _Note.IsVisible = false;
            _Error = Ui.Notice(String.Empty, XenoToneEnum.Danger);
            _Error.IsVisible = false;

            _Table = new XenoTable<ProviderInventoryRow>(new List<XenoTableColumn<ProviderInventoryRow>>
            {
                new XenoTableColumn<ProviderInventoryRow>("Model", row => row.Name) { Monospace = true }.Star(3),
                new XenoTableColumn<ProviderInventoryRow>("Size", row => row.Size) { SortKey = row => row.SizeBytes }.Fixed(90),
                new XenoTableColumn<ProviderInventoryRow>("Parameters", row => row.ParameterSize).Fixed(100),
                new XenoTableColumn<ProviderInventoryRow>("Quantization", row => row.Quantization).Fixed(110),
                new XenoTableColumn<ProviderInventoryRow>("Modified", row => row.Modified).Fixed(140),
                new XenoTableColumn<ProviderInventoryRow>("Advertised", row => row.Advertised).Fixed(95)
            }, RowActions, "No models on this provider yet.", "ManageModels");

            _Delete = Ui.Button("Delete Model...", () =>
            {
                ProviderInventoryRow? row = _Table.SelectedRow;
                if (row == null) ShowError("Select a model to delete.");
                else _ = DeleteAsync(row);
            });
            _Delete.IsVisible = false;
            Button refresh = Ui.Button("Refresh", () => _ = LoadAsync());

            _PullName = new TextBox { PlaceholderText = XenoModelManagement.PullPlaceholder, MinWidth = 300 };
            _PullName.KeyDown += (sender, args) =>
            {
                if (args.Key == Avalonia.Input.Key.Enter)
                {
                    _ = StartPullAsync();
                    args.Handled = true;
                }
            };
            _Advertise = new CheckBox { Content = XenoModelManagement.AdvertiseLabel, IsChecked = false, VerticalAlignment = VerticalAlignment.Center };
            Button pull = Ui.Primary("Pull", StartPullFromButton);
            _PullPanel = Ui.Stack(8, Ui.Heading("Pull a model"), Ui.Row(_PullName, _Advertise, pull), _Pulls);
            _PullPanel.IsVisible = false;

            Button close = Ui.Button("Close", () => CloseWith(null));
            close.IsCancel = true;
            DockPanel header = new DockPanel();
            StackPanel headerButtons = Ui.Row(refresh, _Delete);
            DockPanel.SetDock(headerButtons, Dock.Right);
            header.Children.Add(headerButtons);
            header.Children.Add(Ui.Stack(4, Ui.Title(XenoModelManagement.Title(provider)), _Summary));

            StackPanel top = Ui.Stack(10, header, _Note, _Error);
            top.Margin = new Thickness(0, 0, 0, 12);
            StackPanel closeRow = Ui.Row(close);
            closeRow.HorizontalAlignment = HorizontalAlignment.Right;
            StackPanel bottom = Ui.Stack(12, _PullPanel, closeRow);
            bottom.Margin = new Thickness(0, 12, 0, 0);

            DockPanel root = new DockPanel { Margin = new Thickness(20) };
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);
            root.Children.Add(_Table);
            Content = root;

            _Workspace.ModelPulls.JobChanged += OnJobChanged;
            Closed += (sender, args) => _Workspace.ModelPulls.JobChanged -= OnJobChanged;
            Opened += (sender, args) =>
            {
                RenderPulls();
                _ = LoadAsync();
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show the dialog for a provider.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="workspace">Workspace.</param>
        /// <param name="provider">Provider name.</param>
        /// <param name="report">Reports outcomes on the main window's status bar.</param>
        /// <param name="changed">Called after the provider's models or advertisements change.</param>
        /// <returns>Task that completes when the dialog closes.</returns>
        public static Task ShowAsync(Window? owner, XenoWorkspace workspace, string provider, Action<string> report, Action changed)
        {
            return XenoDialogs.ShowModalAsync<object>(new ManageModelsWindow(workspace, provider, report, changed), owner);
        }

        #endregion

        #region Private-Methods

        private async Task LoadAsync()
        {
            if (_Loading) return;
            _Loading = true;
            _Summary.Text = "Reading models from " + _Provider + "...";
            try
            {
                ProviderModelInventory inventory = await _Workspace.GetProviderInventoryAsync(_Provider, CancellationToken.None).ConfigureAwait(true);
                _Inventory = inventory;
                _Summary.Text = XenoModelManagement.Summary(inventory).Replace(" " + XenoModelManagement.OllamaOnlyNote, String.Empty, StringComparison.Ordinal);
                _Note.IsVisible = !inventory.CanPull;
                _PullPanel.IsVisible = inventory.CanPull;
                _Delete.IsVisible = inventory.CanDelete;
                _Table.SetEmptyText(inventory.Reachable ? "No models on this provider yet." : "Could not read models: " + (inventory.Error ?? "no answer") + ".");
                _Table.SetRows(XenoModelManagement.Rows(inventory), (left, right) => String.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException || ex is System.Net.Http.HttpRequestException || ex is System.IO.IOException)
            {
                _Summary.Text = ex.Message;
                _Table.SetRows(new List<ProviderInventoryRow>());
            }
            finally
            {
                _Loading = false;
            }
        }

        private IReadOnlyList<XenoRowAction<ProviderInventoryRow>> RowActions(ProviderInventoryRow row)
        {
            List<XenoRowAction<ProviderInventoryRow>> actions = new List<XenoRowAction<ProviderInventoryRow>>
            {
                new XenoRowAction<ProviderInventoryRow>("Copy Name", item => _ = XenoClipboard.CopyAsync(this, item.Name))
            };
            if (row.IsAdvertised)
            {
                actions.Add(new XenoRowAction<ProviderInventoryRow>("Unadvertise", item => _ = SetAdvertisedAsync(item, false)));
            }
            else
            {
                actions.Add(new XenoRowAction<ProviderInventoryRow>("Advertise", item => _ = SetAdvertisedAsync(item, true)));
            }

            if (_Inventory != null && _Inventory.CanDelete)
            {
                actions.Add(new XenoRowAction<ProviderInventoryRow>("Delete...", item => _ = DeleteAsync(item)) { Destructive = true });
            }

            return actions;
        }

        private async Task SetAdvertisedAsync(ProviderInventoryRow row, bool advertise)
        {
            _Error.IsVisible = false;
            try
            {
                if (advertise)
                {
                    await _Workspace.AddModelAsync(_Provider, row.Name, true, false, CancellationToken.None).ConfigureAwait(true);
                    _Report(row.Name + " is now advertised on " + _Provider + ".");
                }
                else
                {
                    await _Workspace.DeleteModelAsync(_Provider, row.Name, CancellationToken.None).ConfigureAwait(true);
                    _Report(row.Name + " is no longer advertised on " + _Provider + ".");
                }

                _Changed();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is NotSupportedException)
            {
                ShowError(ex.Message);
            }

            await LoadAsync().ConfigureAwait(true);
        }

        private void StartPullFromButton()
        {
            _ = StartPullAsync();
        }

        private async Task StartPullAsync()
        {
            string model = (_PullName.Text ?? String.Empty).Trim();
            if (model.Length == 0)
            {
                ShowError("Enter the model to pull, for example " + XenoModelManagement.PullPlaceholder + ".");
                return;
            }

            _Error.IsVisible = false;
            try
            {
                ModelPullJob job = await _Workspace.StartModelPullAsync(_Provider, model, _Advertise.IsChecked == true, CancellationToken.None).ConfigureAwait(true);
                _PullName.Text = String.Empty;
                _Report(XenoModelManagement.PullLine(job) + " on " + _Provider + ". It keeps going if you close this dialog" + (_Workspace.ModelPulls.RunsInWorker ? String.Empty : " (but stops if you quit the app, because the Xeno client is not running)") + ".");
                RenderPulls();
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is ObjectDisposedException || ex is UnauthorizedAccessException)
            {
                ShowError(ex.Message);
            }
        }

        private async Task DeleteAsync(ProviderInventoryRow row)
        {
            bool confirmed = await XenoDialogs.ConfirmAsync(this, "Delete Model", XenoModelManagement.DeleteConfirmation(_Provider, row.Name, row.IsAdvertised), "Delete").ConfigureAwait(true);
            if (!confirmed) return;
            _Error.IsVisible = false;
            try
            {
                int removed = await _Workspace.DeleteProviderModelAsync(_Provider, row.Name, CancellationToken.None).ConfigureAwait(true);
                _Report(XenoModelManagement.Deleted(_Provider, row.Name, removed));
                _Changed();
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is ArgumentException)
            {
                ShowError(ex.Message);
            }

            await LoadAsync().ConfigureAwait(true);
        }

        private void OnJobChanged(object? sender, ModelPullJob job)
        {
            // Raised on a background thread.
            Dispatcher.UIThread.Post(() =>
            {
                if (!String.Equals(job.Provider, _Provider, StringComparison.OrdinalIgnoreCase)) return;
                RenderPulls();
                if (job.State == ModelPullStateEnum.Succeeded) _ = LoadAsync();
            });
        }

        private async Task CancelPullAsync(string id, string model)
        {
            try
            {
                if (await _Workspace.CancelModelPullAsync(id, CancellationToken.None).ConfigureAwait(true)) _Report("Cancelling the pull of " + model + "...");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException || ex is NotSupportedException || ex is UnauthorizedAccessException)
            {
                ShowError(ex.Message);
            }
        }

        private void RenderPulls()
        {
            _Pulls.Children.Clear();
            foreach (ModelPullJob job in XenoModelManagement.PullsFor(_Workspace.ListModelPulls(), _Provider))
            {
                ProgressBar bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = XenoModelManagement.Fraction(job) * 100, Height = 6, MinWidth = 260, IsIndeterminate = job.Active && !job.Percent.HasValue, VerticalAlignment = VerticalAlignment.Center };
                TextBlock line = Ui.Body(XenoModelManagement.PullLine(job));
                TextBlock detail = Ui.Muted(job.Active ? XenoModelManagement.PullDetail(job) : String.Empty);
                DockPanel row = new DockPanel();
                if (job.Active)
                {
                    string id = job.Id;
                    Button cancel = Ui.Button("Cancel Pull", () => _ = CancelPullAsync(id, job.Model));
                    DockPanel.SetDock(cancel, Dock.Right);
                    row.Children.Add(cancel);
                }

                row.Children.Add(Ui.Stack(4, line, bar, detail));
                _Pulls.Children.Add(row);
            }
        }

        private void ShowError(string message)
        {
            if (_Error.Child is SelectableTextBlock text) text.Text = message;
            _Error.IsVisible = true;
        }

        #endregion
    }
}
