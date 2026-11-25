namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Providers: inference endpoints on this machine and the models each serves.
    /// </summary>
    internal sealed class ProvidersPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Providers; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("a", "Add"), new KeyHint("e", "Edit"), new KeyHint("t", "Test"), new KeyHint("m", "Add model"), new KeyHint("p", "Manage models"), new KeyHint("d", "Delete") }; }
        }

        #endregion

        #region Private-Members

        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly DataTable<ProviderRow> _Providers;
        private readonly DataTable<ProviderModelRow> _Models;
        private List<ProviderRow> _Rows = new List<ProviderRow>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public ProvidersPage(TuiContext context) : base(context)
        {
            _Providers = Table<ProviderRow>("No providers yet. Press a to add the Ollama, vLLM, or OpenAI-compatible endpoint that runs your models.", row => row.Name);
            Column(_Providers, "Name", row => row.Name, 18);
            Column(_Providers, "API type", row => row.ApiType, 16);
            Column(_Providers, "Endpoint", row => row.Endpoint, 0);
            Column(_Providers, "Models", row => row.ModelCount.ToString(System.Globalization.CultureInfo.InvariantCulture), 7);
            Column(_Providers, "Managed", row => row.ModelManagement, 8);
            Column(_Providers, "Credential", row => row.Credential, 16);
            _Providers.SelectionChanged += (sender, args) => ShowModels();
            _Providers.RowActivated += index => EditProvider();

            _Models = Table<ProviderModelRow>("This provider has no models yet. Press m to add one.", row => row.Provider + "/" + row.Name);
            Column(_Models, "Model", row => row.Name, 0);
            Column(_Models, "Capabilities", row => row.Capabilities, 26);
            Column(_Models, "Source", row => row.Source, 30);
            _Models.RowActivated += index => EditModel();

            Frame(_Root, _Providers, StackSize.Weighted(3, 4), "Providers");
            Frame(_Root, _Models, StackSize.Weighted(2, 3), "Models on the selected provider");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            return LoadAsync(token => Task.FromResult(Workspace.GetProviders()), rows =>
            {
                _Rows = rows;
                _Providers.Bind(rows);
                ShowModels();
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'a'))
            {
                _ = Context.FormAsync(XenoForms.AddProvider(Workspace), () => _ = RefreshAsync());
                return true;
            }

            if (PageKeys.Is(key, 'e'))
            {
                if (ModelsFocused()) EditModel();
                else EditProvider();
                return true;
            }

            if (PageKeys.Is(key, 'm'))
            {
                _ = Context.FormAsync(XenoForms.AddModel(Workspace, _Providers.SelectedRow?.Name), () => _ = RefreshAsync());
                return true;
            }

            if (PageKeys.Is(key, 't'))
            {
                _ = TestAsync();
                return true;
            }

            if (PageKeys.Is(key, 'p'))
            {
                ProviderRow? selected = _Providers.SelectedRow;
                if (selected == null) Context.Notify("Select a provider to manage its models.", NotificationSeverity.Info);
                else _ = Context.ManageModelsAsync(selected.Name, () => _ = RefreshAsync());
                return true;
            }

            if (PageKeys.Is(key, 'd'))
            {
                _ = ModelsFocused() ? DeleteModelAsync() : DeleteProviderAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private bool ModelsFocused()
        {
            return Object.ReferenceEquals(_Root.FocusedWidget, _Models);
        }

        private void ShowModels()
        {
            ProviderRow? row = _Providers.SelectedRow;
            _Models.Bind(row == null ? new List<ProviderModelRow>() : ProviderModelRow.ForProvider(row.Source, Workspace.DiscoveredModels(row.Name)));
        }

        private void EditProvider()
        {
            ProviderRow? row = _Providers.SelectedRow;
            if (row != null) _ = Context.FormAsync(XenoForms.EditProvider(Workspace, row.Source), () => _ = RefreshAsync());
        }

        private void EditModel()
        {
            ProviderModelRow? row = _Models.SelectedRow;
            if (row != null) _ = Context.FormAsync(XenoForms.EditModel(Workspace, row.Provider, row.Name), () => _ = RefreshAsync());
        }

        private async Task TestAsync()
        {
            ProviderRow? row = _Providers.SelectedRow;
            if (row == null) return;
            Context.Progress("Testing " + row.Name + "...");
            await Context.LoadAsync(token => Workspace.TestProviderAsync(row.Name, token), result =>
            {
                string message = result.Success
                    ? "Provider '" + result.Name + "' responded with HTTP " + result.StatusCode + "."
                    : "Provider '" + result.Name + "' did not respond successfully: " + (result.Error ?? ("HTTP " + result.StatusCode)) + ".";
                Context.Notify(message, result.Success ? NotificationSeverity.Success : NotificationSeverity.Error);
            }).ConfigureAwait(false);
        }

        private async Task DeleteProviderAsync()
        {
            ProviderRow? row = _Providers.SelectedRow;
            if (row == null) return;
            bool confirmed = await Context.ConfirmAsync("Delete Provider", "Delete provider '" + row.Name + "' and stop advertising its models?", "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.DeleteProviderAsync(row.Name, token).ConfigureAwait(false);
                return removed > 0 ? "Deleted provider '" + row.Name + "'." : "Provider '" + row.Name + "' was not found.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private async Task DeleteModelAsync()
        {
            ProviderModelRow? row = _Models.SelectedRow;
            if (row == null) return;
            if (!row.Configured)
            {
                Context.Notify("'" + row.Name + "' was found on " + row.Provider + " and is not configured, so there is nothing to delete.", NotificationSeverity.Info);
                return;
            }

            bool confirmed = await Context.ConfirmAsync("Delete Model", "Stop advertising '" + row.Name + "' from '" + row.Provider + "'?", "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.DeleteModelAsync(row.Provider, row.Name, token).ConfigureAwait(false);
                return removed > 0 ? "Deleted model '" + row.Name + "'." : "Model '" + row.Name + "' was not found.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        #endregion
    }
}
