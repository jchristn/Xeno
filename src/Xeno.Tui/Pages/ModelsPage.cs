namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Models: the unified catalog of models this machine serves and models the Xeno network offers it.
    /// </summary>
    internal sealed class ModelsPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Models; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("a", "Add"), new KeyHint("i", "Import"), new KeyHint("e", "Edit"), new KeyHint("d", "Delete") }; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Filters = new List<string> { "All models", "Served by this machine", "Offered by the network" };
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _Filter = new Dropdown<string>(Filters);
        private readonly TextField _Search = new TextField { Placeholder = "type to filter by name" };
        private readonly Label _Notice = new Label(Text.From(String.Empty));
        private readonly DataTable<ModelRow> _Table;
        private ModelCatalog? _Catalog;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public ModelsPage(TuiContext context) : base(context)
        {
            _Filter.SelectedIndex = 0;
            _Filter.SelectionChanged += (sender, args) => Apply();
            _Search.ValueChanged += (sender, args) => Apply();
            _Table = Table<ModelRow>("No models yet. Add a provider and its models, or sign in to see the network's models.", row => row.Name);
            Column(_Table, "Model", row => row.Name, 0);
            Column(_Table, "Available from", row => row.Availability, 24);
            Column(_Table, "Local providers", row => row.Providers, 20);
            Column(_Table, "Capabilities", row => row.Capabilities, 24);
            Column(_Table, "Owner", row => row.NetworkOwner, 8);
            _Table.RowActivated += index => Edit();
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _Filter, StackSize.Weighted(1, 24), "Show");
            Frame(filters, _Search, StackSize.Weighted(2, 20), "Filter");
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Table, StackSize.Weighted(1, 4), "Models");
            Frame(_Root, _Notice, StackSize.Fixed(1));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            return LoadAsync(token => Workspace.GetModelCatalogAsync(true, token), catalog =>
            {
                _Catalog = catalog;
                Apply();
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'a'))
            {
                _ = Context.FormAsync(XenoForms.AddModel(Workspace, null) ?? ProviderFirst(), () => _ = RefreshAsync());
                return true;
            }

            if (PageKeys.Is(key, 'i'))
            {
                _ = Context.ImportModelsAsync(() => _ = RefreshAsync());
                return true;
            }

            if (PageKeys.Is(key, 'e'))
            {
                Edit();
                return true;
            }

            if (PageKeys.Is(key, 'd'))
            {
                _ = DeleteAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private void Apply()
        {
            if (_Catalog == null) return;
            IEnumerable<ModelCatalogEntry> entries = _Catalog.Entries;
            if (_Filter.SelectedIndex == 1) entries = entries.Where(item => item.ServedLocally);
            if (_Filter.SelectedIndex == 2) entries = entries.Where(item => item.OnNetwork);
            string search = _Search.Value.Trim();
            if (search.Length > 0) entries = entries.Where(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            _Table.Bind(entries.Select(ModelRow.From).ToList());
            _Notice.Content = _Catalog.NetworkAvailable
                ? Text.From(_Catalog.LocalCount + " served here, " + _Catalog.NetworkCount + " on the network.").Dim()
                : Text.From("Network models unavailable: " + _Catalog.NetworkMessage).Dim();
        }

        private void Edit()
        {
            ModelRow? row = _Table.SelectedRow;
            if (row == null) return;
            if (!row.Source.ServedLocally || row.Source.LocalProviders.Count == 0)
            {
                Context.Notify("'" + row.Name + "' is offered by the Xeno network; it is not configured on this machine.", NotificationSeverity.Info);
                return;
            }

            _ = Context.FormAsync(XenoForms.EditModel(Workspace, row.Source.LocalProviders[0], row.Name), () => _ = RefreshAsync());
        }

        private async Task DeleteAsync()
        {
            ModelRow? row = _Table.SelectedRow;
            if (row == null || !row.Source.ServedLocally) return;
            string provider = row.Source.LocalProviders[0];
            bool confirmed = await Context.ConfirmAsync("Delete Model", "Stop advertising '" + row.Name + "' from provider '" + provider + "'?", "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.DeleteModelAsync(provider, row.Name, token).ConfigureAwait(false);
                return removed > 0 ? "Deleted model '" + row.Name + "'." : "Model '" + row.Name + "' was not found.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private XenoFormSpec? ProviderFirst()
        {
            Context.Notify("Add a provider first. Models are served by a provider endpoint on this machine.", NotificationSeverity.Warning);
            return null;
        }

        #endregion
    }
}
