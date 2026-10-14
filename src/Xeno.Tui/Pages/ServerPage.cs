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
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Server and account: connection settings, sign-in, connectivity checks, and what the network offers.
    /// </summary>
    internal sealed class ServerPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Server; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("i", "Sign in"), new KeyHint("e", "Edit"), new KeyHint("p", "Ping") }; }
        }

        #endregion

        #region Private-Members

        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly DetailsView _Details = new DetailsView();
        private readonly ButtonRow _Actions = new ButtonRow();
        private readonly LogView _Output = new LogView();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public ServerPage(TuiContext context) : base(context)
        {
            _Actions.Add("Sign In", () => _ = Context.SignInAsync(Render));
            _Actions.Add("Edit Connection", () => _ = Context.FormAsync(XenoForms.EditServer(Workspace), Render));
            _Actions.Add("Set Access Key", () => _ = Context.FormAsync(XenoForms.SetAccessKey(Workspace), Render));
            _Actions.Add("Sign Out", () => _ = SignOutAsync());
            _Actions.Add("Test Connection", () => _ = PingAsync());
            Frame(_Root, _Details, StackSize.Fixed(11), "Connection and account");
            Frame(_Root, _Actions, StackSize.Fixed(1), "Actions");
            Frame(_Root, _Output, StackSize.Weighted(1, 3), "Results");
            Render();
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            Render();
            return Context.LoadAsync(token => Workspace.GetModelCatalogAsync(true, token), catalog =>
            {
                List<string> lines = new List<string>();
                if (!catalog.NetworkAvailable) lines.Add("Xeno network: " + catalog.NetworkMessage);
                else
                {
                    int shared = catalog.Entries.Count(item => item.OnNetwork && item.NetworkOwner == "shared");
                    lines.Add("Xeno network: " + catalog.NetworkCount + " models available to this tenant (" + shared + " shared by other tenants).");
                    lines.Add("Served here and on the network: " + catalog.Entries.Count(item => item.Availability == ModelAvailabilityEnum.Both) + ".");
                }

                _Output.SetLines(lines);
            });
        }

        /// <inheritdoc />
        public override void OnStatusChanged(ClientStatusSnapshot status)
        {
            Render();
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'i'))
            {
                _ = Context.SignInAsync(Render);
                return true;
            }

            if (PageKeys.Is(key, 'e'))
            {
                _ = Context.FormAsync(XenoForms.EditServer(Workspace), Render);
                return true;
            }

            if (PageKeys.Is(key, 'p'))
            {
                _ = PingAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private void Render()
        {
            ClientSettings settings = Workspace.LoadSettings();
            ClientStatusSnapshot? status = Context.Status;
            _Details.Clear();
            _Details.Set("Client name", settings.Name);
            _Details.Set("Client ID", DisplayFormat.OrDash(settings.ClientId));
            _Details.Set("Tenant", settings.TenantId);
            _Details.Set("WebSocket URL", settings.Server.Url);
            _Details.Set("REST URL", DisplayFormat.OrDash(ServerEndpoints.TryBuildRestBaseUrl(settings)) + (String.IsNullOrWhiteSpace(settings.Server.RestUrl) ? " (derived)" : String.Empty));
            _Details.Set("Signed in", ServerEndpoints.HasWebSocketCredential(settings) ? "yes" : "no");
            _Details.Set("Access key", DisplayFormat.Mask(settings.Server.AccessKey));
            _Details.Set("Heartbeat", (settings.Server.HeartbeatIntervalMs / 1000) + " s, liveness timeout " + (settings.Server.LivenessTimeoutMs / 1000) + " s");
            _Details.Set("Status", status == null ? DisplayFormat.Dash : status.StatusText + " - " + status.StatusDetail);
        }

        private async Task SignOutAsync()
        {
            bool confirmed = await Context.ConfirmAsync("Sign Out", "Remove the stored client credential? The client cannot connect until you sign in again.", "Sign Out").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                await Workspace.SetAccessKeyAsync(null, token).ConfigureAwait(false);
                return "Removed the stored credential.";
            }, Render).ConfigureAwait(false);
        }

        private Task PingAsync()
        {
            Context.Progress("Pinging the Xeno server...");
            return Context.LoadAsync(token => Workspace.PingServerAsync(4, token), attempts =>
            {
                List<string> lines = attempts.Select(attempt => attempt.Success
                    ? "Reply " + attempt.Sequence + ": " + attempt.ElapsedMs + " ms, status " + DisplayFormat.OrDash(attempt.Status)
                    : "Attempt " + attempt.Sequence + ": " + (attempt.Error ?? ("HTTP " + attempt.StatusCode + " " + attempt.ReasonPhrase))).ToList();
                int received = attempts.Count(item => item.Success);
                lines.Add(received + " of " + attempts.Count + " pings answered.");
                _Output.SetLines(lines);
                Context.Notify(lines[lines.Count - 1], received == attempts.Count ? NotificationSeverity.Success : received == 0 ? NotificationSeverity.Error : NotificationSeverity.Warning);
            });
        }

        #endregion
    }
}
