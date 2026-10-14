namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Dialog flows for editing providers, models, the server connection, and sign-in. Every flow validates through the
    /// shared <see cref="XenoWorkspace"/>, keeps the dialog open on an error, and calls a completion callback after a
    /// change is saved.
    /// </summary>
    public static class ClientDialogs
    {
        #region Public-Methods

        /// <summary>
        /// Add a provider endpoint.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="completed">Called after a provider is added.</param>
        /// <returns>Task.</returns>
        public static Task AddProviderAsync(Window owner, DesktopSession session, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            return RunSpecAsync(owner, session, XenoForms.AddProvider(session.Workspace), completed);
        }

        /// <summary>
        /// Edit a provider endpoint.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="provider">Provider to edit.</param>
        /// <param name="completed">Called after the provider is saved.</param>
        /// <returns>Task.</returns>
        public static Task EditProviderAsync(Window owner, DesktopSession session, ClientRunnerSettings provider, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            return RunSpecAsync(owner, session, XenoForms.EditProvider(session.Workspace, provider), completed);
        }

        /// <summary>
        /// Add a model to a provider.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="providerName">Provider to preselect, or null.</param>
        /// <param name="completed">Called after the model is added.</param>
        /// <returns>Task.</returns>
        public static async Task AddModelAsync(Window owner, DesktopSession session, string? providerName, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            XenoFormSpec? spec = XenoForms.AddModel(session.Workspace, providerName);
            if (spec == null)
            {
                await XenoDialogs.MessageAsync(owner, "Add Model", "Add a provider first. Models are served by a provider endpoint on this machine.", XenoToneEnum.Info).ConfigureAwait(true);
                return;
            }

            await RunSpecAsync(owner, session, spec, completed).ConfigureAwait(true);
        }

        /// <summary>
        /// Edit, rename, or move a model.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="providerName">Provider that owns the model.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="completed">Called after the model is saved.</param>
        /// <returns>Task.</returns>
        public static async Task EditModelAsync(Window owner, DesktopSession session, string providerName, string modelName, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            XenoFormSpec? spec = XenoForms.EditModel(session.Workspace, providerName, modelName);
            if (spec == null)
            {
                await XenoDialogs.MessageAsync(owner, "Edit Model", "The model no longer exists.", XenoToneEnum.Warning).ConfigureAwait(true);
                completed();
                return;
            }

            await RunSpecAsync(owner, session, spec, completed).ConfigureAwait(true);
        }

        /// <summary>
        /// Delete a model after confirmation.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="providerName">Provider that owns the model.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="completed">Called after the model is deleted.</param>
        /// <returns>Task.</returns>
        public static async Task DeleteModelAsync(Window owner, DesktopSession session, string providerName, string modelName, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            bool confirmed = await XenoDialogs.ConfirmAsync(owner, "Delete Model",
                "Remove '" + modelName + "' from provider '" + providerName + "'? The model stays installed on the provider; this client just stops advertising it.", "Delete").ConfigureAwait(true);
            if (!confirmed) return;
            try
            {
                int removed = await session.Workspace.DeleteModelAsync(providerName, modelName, CancellationToken.None).ConfigureAwait(true);
                session.Report(removed > 0 ? "Deleted model '" + modelName + "'." : "Model '" + modelName + "' was not found.");
            }
            catch (KeyNotFoundException ex)
            {
                session.Report(ex.Message);
            }

            completed();
        }

        /// <summary>
        /// Delete a provider after confirmation.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="providerName">Provider name.</param>
        /// <param name="completed">Called after the provider is deleted.</param>
        /// <returns>Task.</returns>
        public static async Task DeleteProviderAsync(Window owner, DesktopSession session, string providerName, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            bool confirmed = await XenoDialogs.ConfirmAsync(owner, "Delete Provider",
                "Delete provider '" + providerName + "' and stop advertising all of its models?", "Delete").ConfigureAwait(true);
            if (!confirmed) return;
            int removed = await session.Workspace.DeleteProviderAsync(providerName, CancellationToken.None).ConfigureAwait(true);
            session.Report(removed > 0 ? "Deleted provider '" + providerName + "'." : "Provider '" + providerName + "' was not found.");
            completed();
        }

        /// <summary>
        /// Test a provider and show the result.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="providerName">Provider name.</param>
        /// <returns>Task.</returns>
        public static async Task TestProviderAsync(Window owner, DesktopSession session, string providerName)
        {
            ArgumentNullException.ThrowIfNull(session);
            session.Report("Testing provider '" + providerName + "'...");
            try
            {
                Client.LocalDashboard.Models.LocalProviderTestResponse result = await session.Workspace.TestProviderAsync(providerName, CancellationToken.None).ConfigureAwait(true);
                string message = result.Success
                    ? "Provider '" + result.Name + "' at " + result.Endpoint + " responded with HTTP " + result.StatusCode + "."
                    : "Provider '" + result.Name + "' at " + result.Endpoint + " did not respond successfully: " + (result.Error ?? ("HTTP " + result.StatusCode)) + ".";
                session.Report(message);
                await XenoDialogs.MessageAsync(owner, "Test Provider", message, result.Success ? XenoToneEnum.Success : XenoToneEnum.Danger).ConfigureAwait(true);
            }
            catch (KeyNotFoundException ex)
            {
                session.Report(ex.Message);
            }
        }

        /// <summary>
        /// Sign in to a Xeno server.
        /// </summary>
        /// <param name="owner">Owner window, or null.</param>
        /// <param name="session">Session.</param>
        /// <param name="completed">Called after a successful sign-in.</param>
        /// <param name="execute">Runs the next step the confirmation offers (Start Client or Restart Client), or null to only confirm.</param>
        /// <returns>Task.</returns>
        public static async Task SignInAsync(Window? owner, DesktopSession session, Action completed, Action<XenoCommandEnum>? execute = null)
        {
            ArgumentNullException.ThrowIfNull(session);
            bool signedIn = await RunSpecAsync(owner, session, XenoForms.SignIn(session.Workspace), completed).ConfigureAwait(true);
            if (!signedIn) return;
            XenoFollowUp followUp = XenoClientActions.AfterSignIn(session.Status ?? session.Workspace.GetStatus());
            if (execute == null || followUp.Command == null)
            {
                await XenoDialogs.MessageAsync(owner, followUp.Title, followUp.Message, XenoToneEnum.Success).ConfigureAwait(true);
                return;
            }

            bool proceed = await XenoDialogs.ConfirmAsync(owner, followUp.Title, followUp.Message, followUp.ActionText).ConfigureAwait(true);
            if (proceed) execute(followUp.Command.Value);
        }

        /// <summary>
        /// Edit the server connection.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="completed">Called after the change is saved.</param>
        /// <returns>Task.</returns>
        public static Task EditServerAsync(Window owner, DesktopSession session, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            return RunSpecAsync(owner, session, XenoForms.EditServer(session.Workspace), completed);
        }

        /// <summary>
        /// Store an access key directly.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="completed">Called after the key is saved.</param>
        /// <returns>Task.</returns>
        public static Task SetAccessKeyAsync(Window owner, DesktopSession session, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            return RunSpecAsync(owner, session, XenoForms.SetAccessKey(session.Workspace), completed);
        }

        /// <summary>
        /// Remove the stored credential after confirmation.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="session">Session.</param>
        /// <param name="completed">Called after the credential is removed.</param>
        /// <returns>Task.</returns>
        public static async Task SignOutAsync(Window owner, DesktopSession session, Action completed)
        {
            ArgumentNullException.ThrowIfNull(session);
            bool confirmed = await XenoDialogs.ConfirmAsync(owner, "Sign Out", "Remove the stored client credential? The client cannot connect until you sign in again.", "Sign Out").ConfigureAwait(true);
            if (!confirmed) return;
            await session.Workspace.SetAccessKeyAsync(null, CancellationToken.None).ConfigureAwait(true);
            session.Report("Removed the stored credential.");
            await session.RefreshStatusAsync().ConfigureAwait(true);
            completed();
        }

        #endregion

        #region Private-Methods

        private static async Task<bool> RunSpecAsync(Window? owner, DesktopSession session, XenoFormSpec spec, Action completed)
        {
            string? message = await XenoFormDialog.ShowSpecAsync(owner, spec).ConfigureAwait(true);
            if (message == null)
            {
                session.Report(spec.Title + ": cancelled, nothing changed.");
                return false;
            }

            session.Report(message);
            await session.RefreshStatusAsync().ConfigureAwait(true);
            completed();
            return true;
        }

        #endregion
    }
}
