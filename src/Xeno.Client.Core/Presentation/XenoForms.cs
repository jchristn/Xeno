namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Xeno.Client.Services;

    /// <summary>
    /// The editing forms both front ends offer: providers, models, sign-in, the server connection, and the access key.
    /// Each form validates and saves through <see cref="XenoWorkspace"/>, so the desktop app and the terminal app apply
    /// identical rules and messages.
    /// </summary>
    public static class XenoForms
    {
        #region Public-Methods

        /// <summary>
        /// Form to add a provider endpoint.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Form.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static XenoFormSpec AddProvider(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            return new XenoFormSpec
            {
                Title = "Add Provider",
                Intro = "A provider is an inference endpoint on this machine (Ollama, vLLM, or any OpenAI-compatible server) whose models this client serves to the Xeno network.",
                SubmitText = "Add Provider",
                Fields = ProviderFields(null),
                SubmitAsync = async (form, token) =>
                {
                    try
                    {
                        ClientRunnerSettings provider = new ClientRunnerSettings
                        {
                            Name = form["name"],
                            ApiType = form["apiType"],
                            Endpoint = form["endpoint"],
                            Credential = String.IsNullOrWhiteSpace(form["credential"]) ? null : form["credential"],
                            TimeoutMs = ParseTimeoutSeconds(form["timeout"]),
                            AllowModelManagement = form["modelManagement"] == "true",
                            Models = new List<ClientModelSettings>()
                        };
                        ProviderAddResult result = await workspace.AddProviderAsync(provider, token).ConfigureAwait(false);
                        if (result.Added) return null;
                        return result.NameConflict
                            ? "A provider named '" + result.Provider.Name + "' already exists."
                            : "Provider '" + result.Provider.Name + "' already uses " + result.Provider.Endpoint + ".";
                    }
                    catch (ArgumentException ex)
                    {
                        return ex.Message;
                    }
                },
                SuccessMessage = form => "Added provider '" + form["name"] + "'. Add the models it serves so the network can use them."
            };
        }

        /// <summary>
        /// Form to edit a provider endpoint.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="provider">Provider.</param>
        /// <returns>Form.</returns>
        /// <exception cref="ArgumentNullException">Workspace or provider is null.</exception>
        public static XenoFormSpec EditProvider(XenoWorkspace workspace, ClientRunnerSettings provider)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(provider);
            string originalName = provider.Name;
            List<XenoFormFieldSpec> fields = ProviderFields(provider);
            fields.Add(new XenoFormFieldSpec { Key = "clearCredential", Label = "Remove the stored credential", Kind = XenoSettingKindEnum.Toggle, Value = "false" });
            return new XenoFormSpec
            {
                Title = "Edit Provider",
                Fields = fields,
                SubmitAsync = async (form, token) =>
                {
                    try
                    {
                        await workspace.UpdateProviderAsync(originalName, new ProviderUpdateRequest
                        {
                            NewName = form["name"],
                            ApiType = form["apiType"],
                            Endpoint = form["endpoint"],
                            Credential = String.IsNullOrWhiteSpace(form["credential"]) ? null : form["credential"],
                            ClearCredential = form["clearCredential"] == "true",
                            TimeoutMs = ParseTimeoutSeconds(form["timeout"]),
                            AllowModelManagement = form["modelManagement"] == "true"
                        }, token).ConfigureAwait(false);
                        return null;
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
                    {
                        return ex.Message;
                    }
                },
                SuccessMessage = form => "Saved provider '" + form["name"] + "'."
            };
        }

        /// <summary>
        /// Form to add a model to a provider, or null when there are no providers.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="providerName">Provider to preselect, or null.</param>
        /// <returns>Form, or null when no provider exists.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static XenoFormSpec? AddModel(XenoWorkspace workspace, string? providerName)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            List<string> providers = workspace.LoadSettings().Runners.Select(item => item.Name).ToList();
            if (providers.Count == 0) return null;
            return new XenoFormSpec
            {
                Title = "Add Model",
                Intro = "Models you add here are advertised to the Xeno network while the client is connected.",
                SubmitText = "Add Model",
                Fields = new List<XenoFormFieldSpec>
                {
                    new XenoFormFieldSpec { Key = "provider", Label = "Provider", Kind = XenoSettingKindEnum.Choice, Choices = providers, Value = providerName ?? providers[0] },
                    new XenoFormFieldSpec { Key = "name", Label = "Model name", Required = true, Placeholder = "llama3.2:latest", Help = "The exact model name the provider serves, for example from 'ollama list'." },
                    new XenoFormFieldSpec { Key = "completions", Label = "Serves completions (chat and generate)", Kind = XenoSettingKindEnum.Toggle, Value = "true" },
                    new XenoFormFieldSpec { Key = "embeddings", Label = "Serves embeddings", Kind = XenoSettingKindEnum.Toggle, Value = "false" }
                },
                SubmitAsync = async (form, token) =>
                {
                    try
                    {
                        await workspace.AddModelAsync(form["provider"], form["name"], form["completions"] == "true", form["embeddings"] == "true", token).ConfigureAwait(false);
                        return null;
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
                    {
                        return ex.Message;
                    }
                },
                SuccessMessage = form => "Added model '" + form["name"] + "' to '" + form["provider"] + "'."
            };
        }

        /// <summary>
        /// Form to edit, rename, or move a model, or null when it no longer exists.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="providerName">Provider that owns the model.</param>
        /// <param name="modelName">Model name.</param>
        /// <returns>Form, or null.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static XenoFormSpec? EditModel(XenoWorkspace workspace, string providerName, string modelName)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ClientSettings settings = workspace.LoadSettings();
            ClientRunnerSettings? provider = ProviderConfiguration.FindProvider(settings, providerName);
            ClientModelSettings? model = provider?.Models.FirstOrDefault(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
            if (provider == null || model == null) return null;
            return new XenoFormSpec
            {
                Title = "Edit Model",
                Fields = new List<XenoFormFieldSpec>
                {
                    new XenoFormFieldSpec { Key = "provider", Label = "Provider", Kind = XenoSettingKindEnum.Choice, Choices = settings.Runners.Select(item => item.Name).ToList(), Value = provider.Name, Help = "Choose another provider to move the model." },
                    new XenoFormFieldSpec { Key = "name", Label = "Model name", Required = true, Value = model.Name },
                    new XenoFormFieldSpec { Key = "completions", Label = "Serves completions (chat and generate)", Kind = XenoSettingKindEnum.Toggle, Value = model.SupportsCompletions ? "true" : "false" },
                    new XenoFormFieldSpec { Key = "embeddings", Label = "Serves embeddings", Kind = XenoSettingKindEnum.Toggle, Value = model.SupportsEmbeddings ? "true" : "false" }
                },
                SubmitAsync = async (form, token) =>
                {
                    try
                    {
                        await workspace.UpdateModelAsync(new ModelUpdateRequest
                        {
                            Provider = providerName,
                            Name = modelName,
                            NewProvider = form["provider"],
                            NewName = form["name"],
                            SupportsCompletions = form["completions"] == "true",
                            SupportsEmbeddings = form["embeddings"] == "true"
                        }, token).ConfigureAwait(false);
                        return null;
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is KeyNotFoundException)
                    {
                        return ex.Message;
                    }
                },
                SuccessMessage = form => "Saved model '" + form["name"] + "'."
            };
        }

        /// <summary>
        /// Form to sign in to a Xeno server.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Form.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static XenoFormSpec SignIn(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ClientSettings settings = workspace.LoadSettings();
            return new XenoFormSpec
            {
                Title = "Sign In to Xeno",
                Intro = "Signing in stores a tenant-bound client credential on this machine. Your password is not saved.",
                SubmitText = "Sign In",
                ProgressText = "Signing in...",
                Fields = new List<XenoFormFieldSpec>
                {
                    new XenoFormFieldSpec { Key = "server", Label = "Xeno server", Value = ServerEndpoints.TryBuildRestBaseUrl(settings) ?? String.Empty, Required = true, Help = "cloud for the Xeno cloud, or your server's URL, for example https://app.xenocloud.ai:9000 or http://10.0.0.20:9000. A different server replaces this client's server connection." },
                    new XenoFormFieldSpec { Key = "tenant", Label = "Tenant", Value = settings.TenantId },
                    new XenoFormFieldSpec { Key = "username", Label = "Username", Required = true },
                    new XenoFormFieldSpec { Key = "password", Label = "Password", Required = true, Secret = true }
                },
                SubmitAsync = async (form, token) =>
                {
                    LoginResult result = await workspace.SignInAsync(form["server"], form["username"], form["password"], form["tenant"], token).ConfigureAwait(false);
                    return result.Success ? null : "Sign-in failed: " + result.Error;
                },
                SuccessMessage = form => "Signed in to " + form["server"] + (String.IsNullOrWhiteSpace(form["tenant"]) ? String.Empty : " (tenant " + form["tenant"] + ")") + " as " + form["username"] + "."
            };
        }

        /// <summary>
        /// Form to edit the server connection.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Form.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static XenoFormSpec EditServer(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ClientSettings settings = workspace.LoadSettings();
            return new XenoFormSpec
            {
                Title = "Server Connection",
                Intro = "Changes apply the next time the client starts.",
                Fields = new List<XenoFormFieldSpec>
                {
                    new XenoFormFieldSpec { Key = "name", Label = "Client name", Value = settings.Name, Required = true, Help = "How this machine appears in the Xeno dashboard." },
                    new XenoFormFieldSpec { Key = "ws", Label = "WebSocket URL", Value = settings.Server.Url, Required = true, Help = "For example wss://app.xenocloud.ai:9000/ws/v1/clients (the Xeno cloud, the default)." },
                    new XenoFormFieldSpec { Key = "rest", Label = "REST URL", Value = settings.Server.RestUrl ?? String.Empty, Placeholder = "(derived from the WebSocket URL)" },
                    new XenoFormFieldSpec { Key = "tenant", Label = "Tenant", Value = settings.TenantId, Required = true }
                },
                SubmitAsync = async (form, token) =>
                {
                    try
                    {
                        await workspace.UpdateServerAsync(form["ws"], form["rest"], form["name"], form["tenant"], token).ConfigureAwait(false);
                        return null;
                    }
                    catch (ArgumentException ex)
                    {
                        return ex.Message;
                    }
                },
                SuccessMessage = form => "Saved the server connection. Restart the client to apply it."
            };
        }

        /// <summary>
        /// Form to store an access key directly.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Form.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static XenoFormSpec SetAccessKey(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            return new XenoFormSpec
            {
                Title = "Set Access Key",
                Intro = "Use an access key issued by the Xeno dashboard instead of signing in with a password.",
                Fields = new List<XenoFormFieldSpec>
                {
                    new XenoFormFieldSpec { Key = "accessKey", Label = "Access key", Required = true, Secret = true }
                },
                SubmitAsync = async (form, token) =>
                {
                    await workspace.SetAccessKeyAsync(form["accessKey"], token).ConfigureAwait(false);
                    return null;
                },
                SuccessMessage = form => "Stored the access key. Restart the client to use it."
            };
        }

        #endregion

        #region Private-Methods

        private static List<XenoFormFieldSpec> ProviderFields(ClientRunnerSettings? provider)
        {
            List<string> apiTypes = ProviderConfiguration.KnownApiTypes.ToList();
            if (provider != null && !apiTypes.Contains(provider.ApiType, StringComparer.OrdinalIgnoreCase)) apiTypes.Add(provider.ApiType);
            return new List<XenoFormFieldSpec>
            {
                new XenoFormFieldSpec { Key = "name", Label = "Name", Required = true, Value = provider?.Name ?? String.Empty, Placeholder = "local-ollama" },
                new XenoFormFieldSpec { Key = "apiType", Label = "API type", Kind = XenoSettingKindEnum.Choice, Choices = apiTypes, Value = provider?.ApiType ?? ProviderConfiguration.OllamaApiType },
                new XenoFormFieldSpec { Key = "endpoint", Label = "Endpoint", Required = true, Value = provider?.Endpoint ?? "http://127.0.0.1:11434", Help = "Base URL of the provider. It must not point at this client's local inference server." },
                new XenoFormFieldSpec { Key = "credential", Label = "Credential", Secret = true, Placeholder = provider == null ? "(optional)" : (String.IsNullOrWhiteSpace(provider.Credential) ? "(none)" : "(unchanged)"), Help = "Bearer token sent to the provider, if it requires one." },
                new XenoFormFieldSpec { Key = "timeout", Label = "Timeout (seconds)", Kind = XenoSettingKindEnum.Number, Value = ((provider?.TimeoutMs ?? 300000) / 1000).ToString(CultureInfo.InvariantCulture) },
                new XenoFormFieldSpec { Key = "modelManagement", Label = "Let the Xeno server pull and delete models (Ollama only)", Kind = XenoSettingKindEnum.Toggle, Value = provider != null && provider.AllowModelManagement ? "true" : "false" }
            };
        }

        private static int ParseTimeoutSeconds(string value)
        {
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) || seconds < 1)
            {
                throw new ArgumentException("Timeout must be at least 1 second.");
            }

            return seconds * 1000;
        }

        #endregion
    }
}
