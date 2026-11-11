namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Dialogs;

    /// <summary>
    /// Server and account: where this client connects, how it signs in, connectivity checks, and what the network
    /// offers this tenant.
    /// </summary>
    public sealed class ServerPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Server; }
        }

        #endregion

        #region Private-Members

        private StackPanel _Connection = new StackPanel();
        private StackPanel _Account = new StackPanel();
        private StackPanel _Network = new StackPanel();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public ServerPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control> { Ui.Button("Refresh", () => _ = RefreshAsync()) };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Connection = new StackPanel { Spacing = 10 };
            _Account = new StackPanel { Spacing = 10 };
            _Network = new StackPanel { Spacing = 8 };
            Render();
            return Ui.Stack(16,
                Ui.Card("Connection", _Connection, Ui.Button("Edit...", () => _ = ClientDialogs.EditServerAsync(Window, Session, Render))),
                Ui.Card("Account", _Account),
                Ui.Card("Connectivity", Ui.Muted("Ping the Xeno server to check that this machine can reach it."), Ui.Button("Test Connection", () => _ = Dialogs.ClientDialogs.TestConnectionAsync(Window, Session))),
                Ui.Card("Xeno network", _Network, Ui.Button("Check", () => _ = RefreshAsync())));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            Render();
            await GuardAsync("Xeno network", async () =>
            {
                ModelCatalog catalog = await Workspace.GetModelCatalogAsync(true, None).ConfigureAwait(true);
                _Network.Children.Clear();
                if (!catalog.NetworkAvailable)
                {
                    _Network.Children.Add(Ui.Notice(catalog.NetworkMessage, XenoToneEnum.Info));
                    return;
                }

                int shared = catalog.Entries.Count(item => item.OnNetwork && item.NetworkOwner == "shared");
                _Network.Children.Add(Ui.Body("The network offers " + catalog.NetworkCount + " model" + (catalog.NetworkCount == 1 ? String.Empty : "s") + " to this tenant (" + shared + " shared by other tenants)."));
                _Network.Children.Add(Ui.Muted("Models served here and on the network: " + catalog.Entries.Count(item => item.Availability == ModelAvailabilityEnum.Both) + ". See the Models page for the full list."));
            }).ConfigureAwait(true);
        }

        /// <inheritdoc />
        public override void OnStatusChanged()
        {
            Render();
        }

        #endregion

        #region Private-Methods

        private void Render()
        {
            ClientSettings settings = Workspace.LoadSettings();
            ClientStatusSnapshot? status = Session.Status;
            _Connection.Children.Clear();
            _Connection.Children.Add(Ui.Details(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Client name", settings.Name),
                new KeyValuePair<string, string>("Client ID", DisplayFormat.OrDash(settings.ClientId)),
                new KeyValuePair<string, string>("Tenant", settings.TenantId),
                new KeyValuePair<string, string>("WebSocket URL", settings.Server.Url),
                new KeyValuePair<string, string>("REST URL", DisplayFormat.OrDash(ServerEndpoints.TryBuildRestBaseUrl(settings)) + (String.IsNullOrWhiteSpace(settings.Server.RestUrl) ? "  (derived)" : String.Empty)),
                new KeyValuePair<string, string>("Heartbeat", (settings.Server.HeartbeatIntervalMs / 1000) + " s, liveness timeout " + (settings.Server.LivenessTimeoutMs / 1000) + " s"),
                new KeyValuePair<string, string>("Status", status == null ? DisplayFormat.Dash : status.StatusText + " - " + status.StatusDetail)
            }, 150));

            _Account.Children.Clear();
            bool signedIn = ServerEndpoints.HasWebSocketCredential(settings);
            _Account.Children.Add(Ui.Details(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Signed in", signedIn ? "yes" : "no"),
                new KeyValuePair<string, string>("Access key", DisplayFormat.Mask(settings.Server.AccessKey))
            }, 150));
            _Account.Children.Add(Ui.Row(
                Ui.Primary(signedIn ? "Sign In Again..." : "Sign In...", () => Window.Execute(XenoCommandEnum.SignIn)),
                Ui.Button("Set Access Key...", () => _ = ClientDialogs.SetAccessKeyAsync(Window, Session, Render)),
                Ui.Button("Sign Out...", () => _ = ClientDialogs.SignOutAsync(Window, Session, Render))));
            if (signedIn && status != null && status.Running) _Account.Children.Add(Ui.Muted("Credential changes take effect after the client restarts."));
        }

        #endregion
    }
}
