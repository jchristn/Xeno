namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Dialogs;

    /// <summary>
    /// Models: one catalog of the models this machine serves and the models the Xeno network offers it, with filters
    /// by availability and name.
    /// </summary>
    public sealed class ModelsPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Models; }
        }

        /// <inheritdoc />
        public override bool Scrolls
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Filters = new List<string> { "All models", "Served by this machine", "Offered by the network" };
        private XenoTable<ModelRow>? _Table;
        private ComboBox _Filter = new ComboBox();
        private TextBox _Search = new TextBox();
        private StackPanel _Notices = new StackPanel();
        private ModelCatalog? _Catalog;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public ModelsPage(DesktopSession session, MainWindow window) : base(session, window)
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
                Ui.Primary("Add Model...", () => _ = ClientDialogs.AddModelAsync(Window, Session, null, () => _ = RefreshAsync()))
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Filter = new ComboBox { ItemsSource = Filters, SelectedIndex = 0, MinWidth = 210 };
            _Filter.SelectionChanged += (sender, args) => Apply();
            _Search = new TextBox { PlaceholderText = "Filter by name", MinWidth = 260 };
            _Search.TextChanged += (sender, args) => Apply();
            _Notices = new StackPanel { Spacing = 8 };
            _Table = new XenoTable<ModelRow>(new List<XenoTableColumn<ModelRow>>
            {
                new XenoTableColumn<ModelRow>("Model", row => row.Name) { Monospace = true }.Star(3),
                new XenoTableColumn<ModelRow>("Available from", row => row.Availability) { Cell = AvailabilityCell }.Fixed(190),
                new XenoTableColumn<ModelRow>("Local providers", row => row.Providers).Star(2),
                new XenoTableColumn<ModelRow>("Capabilities", row => row.Capabilities).Star(2),
                new XenoTableColumn<ModelRow>("Network owner", row => row.NetworkOwner).Fixed(120),
                new XenoTableColumn<ModelRow>("Managed", row => row.ModelManagement).Fixed(90)
            }, RowActions, "No models yet. Add a provider and its models to serve them, or sign in to see the models the Xeno network offers.");
            _Table.RowActivated += (sender, row) => Edit(row);

            DockPanel root = new DockPanel();
            StackPanel top = Ui.Stack(10, Ui.Row(_Filter, _Search), _Notices);
            top.Margin = new Avalonia.Thickness(0, 0, 0, 12);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            root.Children.Add(_Table);
            Apply();
            return root;
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Models", async () =>
            {
                Report("Loading models...");
                _Catalog = await Workspace.GetModelCatalogAsync(true, None).ConfigureAwait(true);
                Apply();
                Report(_Catalog.LocalCount + " model" + (_Catalog.LocalCount == 1 ? String.Empty : "s") + " served here" + (_Catalog.NetworkAvailable ? ", " + _Catalog.NetworkCount + " on the network." : "."));
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void Apply()
        {
            if (_Table == null) return;
            _Notices.Children.Clear();
            if (_Catalog == null)
            {
                _Table.SetRows(new List<ModelRow>());
                return;
            }

            if (!_Catalog.NetworkAvailable && !String.IsNullOrWhiteSpace(_Catalog.NetworkMessage))
            {
                _Notices.Children.Add(Ui.Notice("Network models are unavailable: " + _Catalog.NetworkMessage, XenoToneEnum.Info));
            }

            IEnumerable<ModelCatalogEntry> entries = _Catalog.Entries;
            if (_Filter.SelectedIndex == 1) entries = entries.Where(item => item.ServedLocally);
            if (_Filter.SelectedIndex == 2) entries = entries.Where(item => item.OnNetwork);
            string search = _Search.Text ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(search)) entries = entries.Where(item => item.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            _Table.SetRows(entries.Select(ModelRow.From), (left, right) => String.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        }

        private Control AvailabilityCell(ModelRow row)
        {
            XenoToneEnum tone = row.Source.Availability == ModelAvailabilityEnum.Both ? XenoToneEnum.Success
                : row.Source.Availability == ModelAvailabilityEnum.XenoNetwork ? XenoToneEnum.Info : XenoToneEnum.Neutral;
            Border badge = Ui.Badge(row.Availability, tone);
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            return badge;
        }

        private IReadOnlyList<XenoRowAction<ModelRow>> RowActions(ModelRow row)
        {
            List<XenoRowAction<ModelRow>> actions = new List<XenoRowAction<ModelRow>>();
            if (row.Source.ServedLocally)
            {
                actions.Add(new XenoRowAction<ModelRow>("Edit...", Edit));
                foreach (string provider in row.Source.LocalProviders)
                {
                    string target = provider;
                    actions.Add(new XenoRowAction<ModelRow>("Delete from " + provider, item => _ = ClientDialogs.DeleteModelAsync(Window, Session, target, item.Name, () => _ = RefreshAsync())) { Destructive = true });
                }
            }

            actions.Add(new XenoRowAction<ModelRow>("Copy Name", item => _ = XenoClipboard.CopyAsync(Window, item.Name)));
            return actions;
        }

        private void Edit(ModelRow row)
        {
            if (!row.Source.ServedLocally || row.Source.LocalProviders.Count == 0)
            {
                Report("'" + row.Name + "' is offered by the Xeno network; it is not configured on this machine.");
                return;
            }

            _ = ClientDialogs.EditModelAsync(Window, Session, row.Source.LocalProviders[0], row.Name, () => _ = RefreshAsync());
        }

        #endregion
    }
}
