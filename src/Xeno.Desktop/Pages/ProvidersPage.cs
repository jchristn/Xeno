namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Dialogs;

    /// <summary>
    /// Providers: the inference endpoints on this machine and the models each serves, with add, edit, test, and delete.
    /// </summary>
    public sealed class ProvidersPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Providers; }
        }

        /// <inheritdoc />
        public override bool Scrolls
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private XenoTable<ProviderRow>? _Providers;
        private XenoTable<ProviderModelRow>? _Models;
        private TextBlock _ModelsTitle = new TextBlock();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public ProvidersPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control>
            {
                Ui.Button("Refresh", () => _ = RefreshAsync()),
                Ui.Primary("Add Provider...", () => _ = ClientDialogs.AddProviderAsync(Window, Session, () => _ = RefreshAsync()))
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Providers = new XenoTable<ProviderRow>(new List<XenoTableColumn<ProviderRow>>
            {
                new XenoTableColumn<ProviderRow>("Name", row => row.Name).Star(2),
                new XenoTableColumn<ProviderRow>("API type", row => row.ApiType).Fixed(140),
                new XenoTableColumn<ProviderRow>("Endpoint", row => row.Endpoint) { Monospace = true }.Star(3),
                new XenoTableColumn<ProviderRow>("Models", row => row.ModelCount.ToString(System.Globalization.CultureInfo.InvariantCulture)) { SortKey = row => row.ModelCount }.Fixed(80),
                new XenoTableColumn<ProviderRow>("Timeout", row => row.Timeout).Fixed(90),
                new XenoTableColumn<ProviderRow>("Managed", row => row.ModelManagement).Fixed(90),
                new XenoTableColumn<ProviderRow>("Credential", row => row.Credential).Fixed(170)
            }, ProviderActions, "No providers yet. Add the Ollama, vLLM, or OpenAI-compatible endpoint that runs your models.");
            _Providers.RowActivated += (sender, row) => _ = ClientDialogs.EditProviderAsync(Window, Session, row.Source, () => _ = RefreshAsync());
            _Providers.SelectionChanged += (sender, row) => ShowModels(row);

            _Models = new XenoTable<ProviderModelRow>(new List<XenoTableColumn<ProviderModelRow>>
            {
                new XenoTableColumn<ProviderModelRow>("Model", row => row.Name) { Monospace = true }.Star(3),
                new XenoTableColumn<ProviderModelRow>("Capabilities", row => row.Capabilities).Star(2)
            }, ModelActions, "Select a provider to see its models.");
            _Models.RowActivated += (sender, row) => _ = ClientDialogs.EditModelAsync(Window, Session, row.Provider, row.Name, () => _ = RefreshAsync());

            _ModelsTitle = Ui.Heading("Models");
            Button addModel = Ui.Button("Add Model...", () => _ = ClientDialogs.AddModelAsync(Window, Session, _Providers?.SelectedRow?.Name, () => _ = RefreshAsync()));
            Button test = Ui.Button("Test Provider", () =>
            {
                ProviderRow? selected = _Providers?.SelectedRow;
                if (selected == null) Report("Select a provider to test.");
                else _ = ClientDialogs.TestProviderAsync(Window, Session, selected.Name);
            });

            Grid grid = new Grid { RowDefinitions = new RowDefinitions("3*,Auto,2*") };
            Grid.SetRow(_Providers, 0);
            grid.Children.Add(Ui.Adopt(_Providers));
            DockPanel modelsHeader = new DockPanel { Margin = new Thickness(0, 16, 0, 8) };
            StackPanel buttons = Ui.Row(test, addModel);
            DockPanel.SetDock(buttons, Dock.Right);
            modelsHeader.Children.Add(buttons);
            modelsHeader.Children.Add(Ui.Adopt(_ModelsTitle));
            Grid.SetRow(modelsHeader, 1);
            grid.Children.Add(modelsHeader);
            Grid.SetRow(_Models, 2);
            grid.Children.Add(Ui.Adopt(_Models));
            return grid;
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Providers", () =>
            {
                List<ProviderRow> rows = Workspace.GetProviders();
                _Providers?.SetRows(rows, (left, right) => String.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
                ShowModels(_Providers?.SelectedRow);
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void ShowModels(ProviderRow? provider)
        {
            if (_Models == null) return;
            if (provider == null)
            {
                _ModelsTitle.Text = "Models";
                _Models.SetRows(new List<ProviderModelRow>());
                return;
            }

            _ModelsTitle.Text = "Models served by " + provider.Name;
            _Models.SetEmptyText("This provider has no models yet. Add the models it serves so the Xeno network can use them.");
            _Models.SetRows(provider.Source.Models.Select(item => ProviderModelRow.From(provider.Source, item)));
        }

        private IReadOnlyList<XenoRowAction<ProviderRow>> ProviderActions(ProviderRow row)
        {
            return new List<XenoRowAction<ProviderRow>>
            {
                new XenoRowAction<ProviderRow>("Edit...", item => _ = ClientDialogs.EditProviderAsync(Window, Session, item.Source, () => _ = RefreshAsync())),
                new XenoRowAction<ProviderRow>("Test", item => _ = ClientDialogs.TestProviderAsync(Window, Session, item.Name)),
                new XenoRowAction<ProviderRow>("Add Model...", item => _ = ClientDialogs.AddModelAsync(Window, Session, item.Name, () => _ = RefreshAsync())),
                new XenoRowAction<ProviderRow>("Copy Endpoint", item => _ = XenoClipboard.CopyAsync(Window, item.Endpoint)),
                new XenoRowAction<ProviderRow>("Delete...", item => _ = ClientDialogs.DeleteProviderAsync(Window, Session, item.Name, () => _ = RefreshAsync())) { Destructive = true }
            };
        }

        private IReadOnlyList<XenoRowAction<ProviderModelRow>> ModelActions(ProviderModelRow row)
        {
            return new List<XenoRowAction<ProviderModelRow>>
            {
                new XenoRowAction<ProviderModelRow>("Edit...", item => _ = ClientDialogs.EditModelAsync(Window, Session, item.Provider, item.Name, () => _ = RefreshAsync())),
                new XenoRowAction<ProviderModelRow>("Copy Name", item => _ = XenoClipboard.CopyAsync(Window, item.Name)),
                new XenoRowAction<ProviderModelRow>("Delete...", item => _ = ClientDialogs.DeleteModelAsync(Window, Session, item.Provider, item.Name, () => _ = RefreshAsync())) { Destructive = true }
            };
        }

        #endregion
    }
}
