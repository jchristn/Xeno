namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Suites for the shared client library behind the CLI, local dashboard, desktop app, terminal app, and tray agent.
    /// </summary>
    public static class FrontEndTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Shared client services: configuration, catalog, endpoints, status, logs, settings, and launching.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SharedClientSuite()
        {
            const string suite = "SharedClient";
            return new TestSuiteDescriptor(suite, "Shared Client Library", new List<TestCaseDescriptor>
            {
                Case(suite, "ProviderAddRejectsDuplicateNameOrEndpoint", "Provider add reports duplicate names and endpoints", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ProviderAddResult first = ProviderConfiguration.AddProvider(settings, Provider("ollama", "Ollama", "http://127.0.0.1:11434"));
                    Check(first.Added, "First provider was not added.");
                    ProviderAddResult sameName = ProviderConfiguration.AddProvider(settings, Provider("OLLAMA", "Ollama", "http://127.0.0.1:11500"));
                    Check(!sameName.Added && sameName.NameConflict, "Duplicate name was not reported.");
                    ProviderAddResult sameEndpoint = ProviderConfiguration.AddProvider(settings, Provider("other", "Ollama", "http://127.0.0.1:11434/"));
                    Check(!sameEndpoint.Added && !sameEndpoint.NameConflict, "Duplicate endpoint was not reported.");
                    Check(settings.Runners.Count == 1, "Duplicates were added.");
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.AddProvider(settings, new ClientRunnerSettings { Name = "vllm", ApiType = "OpenAICompatible", Endpoint = "http://127.0.0.1:8000", AllowModelManagement = true }));
                }),
                Case(suite, "ProviderUpdateRenamesAndGuardsModelManagement", "Provider update renames and keeps model management Ollama-only", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ProviderConfiguration.AddProvider(settings, Provider("a", "Ollama", "http://127.0.0.1:11434"));
                    ProviderConfiguration.AddProvider(settings, Provider("b", "Ollama", "http://127.0.0.1:11435"));
                    ClientRunnerSettings renamed = ProviderConfiguration.UpdateProvider(settings, "a", new ProviderUpdateRequest { NewName = "renamed", AllowModelManagement = true, TimeoutMs = 5000 });
                    Check(renamed.Name == "renamed" && renamed.AllowModelManagement && renamed.TimeoutMs == 5000, "Update did not apply.");
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.UpdateProvider(settings, "renamed", new ProviderUpdateRequest { NewName = "b" }));
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.UpdateProvider(settings, "renamed", new ProviderUpdateRequest { ApiType = "OpenAICompatible" }));
                    ClientRunnerSettings switched = ProviderConfiguration.UpdateProvider(settings, "renamed", new ProviderUpdateRequest { ApiType = "OpenAICompatible", AllowModelManagement = false });
                    Check(!switched.AllowModelManagement && switched.ApiType == "OpenAICompatible", "Switching type did not clear model management.");
                    ExpectThrows<KeyNotFoundException>(() => ProviderConfiguration.UpdateProvider(settings, "missing", new ProviderUpdateRequest()));
                }),
                Case(suite, "ModelLifecycleAddMoveRenameDelete", "Models can be added, moved, renamed, and deleted", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ProviderConfiguration.AddProvider(settings, Provider("a", "Ollama", "http://127.0.0.1:11434"));
                    ProviderConfiguration.AddProvider(settings, Provider("b", "Ollama", "http://127.0.0.1:11435"));
                    ProviderConfiguration.AddModel(settings, "a", "llama3", true, false);
                    ProviderConfiguration.AddModel(settings, "b", "llama3", true, false);
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.AddModel(settings, "a", "none", false, false));
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Name = "llama3", NewName = "x" }));
                    ModelUpdateResult moved = ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Provider = "a", Name = "llama3", NewName = "nomic", SupportsEmbeddings = true, SupportsCompletions = false });
                    Check(moved.Model.Name == "nomic" && moved.Model.SupportsEmbeddings && !moved.Model.SupportsCompletions, "Rename did not apply.");
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Provider = "b", Name = "llama3", NewProvider = "a", NewName = "nomic" }));
                    ModelUpdateResult moved2 = ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Provider = "b", Name = "llama3", NewProvider = "a" });
                    Check(moved2.Provider.Name == "a" && settings.Runners[1].Models.Count == 0, "Move did not apply.");
                    Check(ProviderConfiguration.DeleteModel(settings, null, "llama3") == 1, "Delete did not remove the model.");
                    ExpectThrows<KeyNotFoundException>(() => ProviderConfiguration.DeleteModel(settings, "missing", "nomic"));
                    Check(ProviderConfiguration.BuildModelList(settings).Models.Count == 1, "Model list is wrong.");
                }),
                Case(suite, "ModelCatalogMergesLocalAndNetworkModels", "Model catalog merges local and network models", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ProviderConfiguration.AddProvider(settings, Provider("ollama", "Ollama", "http://127.0.0.1:11434"));
                    ProviderConfiguration.AddModel(settings, "ollama", "llama3", true, false);
                    ProviderConfiguration.AddModel(settings, "ollama", "local-only", true, true);
                    List<NetworkModelEntry> network = new List<NetworkModelEntry>
                    {
                        new NetworkModelEntry { Id = "LLAMA3", OwnedBy = "xeno" },
                        new NetworkModelEntry { Id = "qwen-72b", OwnedBy = "shared" }
                    };
                    ModelCatalog catalog = ModelCatalogBuilder.Merge(settings, network, String.Empty);
                    Check(catalog.Entries.Count == 3, "Expected three merged entries.");
                    Check(catalog.Entries.Single(item => item.Name == "llama3").Availability == ModelAvailabilityEnum.Both, "Shared model should be available from both.");
                    Check(catalog.Entries.Single(item => item.Name == "qwen-72b").Availability == ModelAvailabilityEnum.XenoNetwork, "Network model availability is wrong.");
                    Check(catalog.Entries.Single(item => item.Name == "local-only").Availability == ModelAvailabilityEnum.ThisMachine, "Local model availability is wrong.");
                    Check(catalog.LocalCount == 2 && catalog.NetworkCount == 2 && catalog.NetworkAvailable, "Counts are wrong.");
                    ModelCatalog local = ModelCatalogBuilder.BuildLocal(settings);
                    Check(!local.NetworkAvailable && local.Entries.Count == 2, "Local-only catalog is wrong.");
                }),
                Case(suite, "ServerEndpointsDeriveRestUrls", "Server endpoints derive REST URLs from WebSocket URLs", () =>
                {
                    ClientSettings settings = EmptySettings();
                    settings.Server.Url = "ws://example.test:9000/ws/v1/clients";
                    Check(ServerEndpoints.BuildRestBaseUrl(settings) == "http://example.test:9000", "ws was not mapped to http.");
                    settings.Server.Url = "wss://secure.test/ws/v1/clients";
                    Check(ServerEndpoints.BuildRestUri(settings, "/v1/models").ToString() == "https://secure.test/v1/models", "wss was not mapped to https.");
                    settings.Server.RestUrl = "http://rest.test:8000";
                    Check(ServerEndpoints.BuildRestBaseUrl(settings) == "http://rest.test:8000", "Explicit REST URL was ignored.");
                    settings.Server.RestUrl = "not a url";
                    Check(ServerEndpoints.TryBuildRestBaseUrl(settings) == null, "Invalid URL should return null.");
                }),
                Case(suite, "ServerTargetsResolveCloudAndCustomServers", "Server targets default to the Xeno cloud and accept custom servers", () =>
                {
                    Check(new ClientSettings().Server.Url == ServerTargets.CloudWebSocketUrl, "New clients do not point at the Xeno cloud.");
                    ServerTarget cloud = ServerTargets.Resolve("cloud");
                    Check(cloud.IsCloud && cloud.WebSocketUrl == ServerTargets.CloudWebSocketUrl && cloud.RestUrl == null && cloud.Label == "Xeno cloud", "cloud did not resolve to the Xeno cloud.");
                    Check(ServerTargets.Resolve("http://localhost:9000").WebSocketUrl == "ws://localhost:9000/ws/v1/clients", "http URL was not mapped to ws.");
                    Check(ServerTargets.Resolve("https://xeno.example.com").WebSocketUrl == "wss://xeno.example.com/ws/v1/clients", "https URL was not mapped to wss.");
                    Check(ServerTargets.Resolve("wss://xeno.example.com:8443").WebSocketUrl == "wss://xeno.example.com:8443/ws/v1/clients", "The client path was not added to a bare ws URL.");
                    Check(ServerTargets.Resolve("ws://host:9000/custom/path").WebSocketUrl == "ws://host:9000/custom/path", "An explicit ws path was changed.");
                    ServerTarget bare = ServerTargets.Resolve("10.0.0.5");
                    Check(bare.WebSocketUrl == "ws://10.0.0.5:9000/ws/v1/clients" && !bare.IsCloud && bare.Label == "10.0.0.5:9000", "A bare host did not default to port 9000.");
                    Check(ServerTargets.Resolve("myserver:8080").WebSocketUrl == "ws://myserver:8080/ws/v1/clients", "A bare host port was ignored.");
                    ExpectThrows<ArgumentException>(() => ServerTargets.Resolve(""));
                    ExpectThrows<ArgumentException>(() => ServerTargets.Resolve("ftp://host"));
                    ClientSettings settings = EmptySettings();
                    settings.Server.Url = "ws://localhost:9000/ws/v1/clients";
                    Check(ServerTargets.FromSettings(settings).Label == "localhost:9000", "Settings label is wrong.");
                }),
                Case(suite, "CloudDefaultUsesTls", "The Xeno cloud default is https/wss and older plain-HTTP cloud addresses are upgraded", () =>
                {
                    ClientSettings fresh = new ClientSettings();
                    Check(fresh.Server.Url == "wss://app.xenocloud.ai:9000/ws/v1/clients", "The default WebSocket URL is not wss.");
                    Check(ServerEndpoints.BuildRestBaseUrl(fresh) == "https://app.xenocloud.ai:9000", "The default REST URL is not https://app.xenocloud.ai:9000.");
                    Check(ServerTargets.Resolve("cloud").WebSocketUrl == ServerTargets.CloudWebSocketUrl, "cloud does not resolve to the TLS default.");
                    ClientSettings legacy = EmptySettings();
                    legacy.Server.Url = "ws://app.xenocloud.ai:9000/ws/v1/clients";
                    legacy.Server.RestUrl = "http://app.xenocloud.ai:9000";
                    Check(ServerTargets.UpgradeInsecureCloudUrls(legacy), "The old cloud address was not upgraded.");
                    Check(legacy.Server.Url == "wss://app.xenocloud.ai:9000/ws/v1/clients" && legacy.Server.RestUrl == "https://app.xenocloud.ai:9000", "The upgraded addresses are wrong.");
                    Check(!ServerTargets.UpgradeInsecureCloudUrls(legacy), "Upgrading twice changed the settings.");
                    ClientSettings local = EmptySettings();
                    local.Server.Url = "ws://localhost:9000/ws/v1/clients";
                    Check(!ServerTargets.UpgradeInsecureCloudUrls(local) && local.Server.Url == "ws://localhost:9000/ws/v1/clients", "A non-cloud server was changed.");

                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        File.WriteAllText(Path.Combine(home.Directory, "client.json"), "{\"Server\":{\"Url\":\"ws://app.xenocloud.ai:9000/ws/v1/clients\"}}");
                        Check(ClientSettings.Load().Server.Url == ServerTargets.CloudWebSocketUrl, "Loading settings did not upgrade the old cloud address.");
                        Check(File.ReadAllText(Path.Combine(home.Directory, "client.json")).Contains("wss://app.xenocloud.ai", StringComparison.Ordinal), "The upgraded address was not saved.");
                    }

                    using (System.Net.Http.HttpResponseMessage response = new System.Net.Http.HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "Bad Request", RequestMessage = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://app.xenocloud.ai:9000/v1.0/api/auth/login") })
                    {
                        string message = XenoServerClient.FormatHttpError(response, "<html><center>The plain HTTP request was sent to HTTPS port</center></html>");
                        Check(message.Contains("only accepts HTTPS", StringComparison.Ordinal) && message.Contains("https://app.xenocloud.ai:9000", StringComparison.Ordinal), "The HTTPS hint is missing: " + message);
                    }
                }),
                Case(suite, "LaunchOptionsParsePageServerAndHome", "Desktop and terminal launch options parse page, server, and home", () =>
                {
                    XenoLaunchOptions options = XenoLaunchOptions.Parse(new[] { "tui", "--page", "models", "--server", "http://localhost:9000", "--home", "~/.xeno-local", "--extra" }, 1);
                    Check(options.Error == null && options.StartPage == XenoPageEnum.Models, "--page was not parsed.");
                    Check(options.Server == "http://localhost:9000" && options.Home == "~/.xeno-local", "--server or --home was not parsed.");
                    Check(options.Remaining.Count == 1 && options.Remaining[0] == "--extra", "Unknown arguments were not kept.");
                    Check(XenoLaunchOptions.Parse(new[] { "--page", "nowhere" }).Error != null, "An unknown page was accepted.");
                    Check(XenoLaunchOptions.Parse(new[] { "--server" }).Error != null, "A missing server value was accepted.");
                    Check(XenoLaunchOptions.Parse(new[] { "--server", "gopher://x" }).Error != null, "An invalid server was accepted.");
                    Check(XenoLaunchOptions.Parse(new[] { "--help" }).ShowHelp, "--help was not recognized.");
                    XenoLaunchOptions settings = XenoLaunchOptions.Parse(new[] { "--settings", "--page", "providers" });
                    Check(settings.Error == null && settings.OpenSettings && settings.StartPage == XenoPageEnum.Providers, "--settings was not parsed.");
                    Check(!XenoLaunchOptions.Parse(new[] { "--page", "models" }).OpenSettings, "Settings opened without --settings.");
                }),
                AsyncCase(suite, "WorkspaceUseServerPersistsAndForwardsHome", "Pointing at a server saves client.json and the home is forwarded to other apps", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    {
                        Check(workspace.GetStatus().ServerLabel == "Xeno cloud", "A new client home does not show the Xeno cloud.");
                        XenoLaunchOptions options = XenoLaunchOptions.Parse(new[] { "--server", "localhost:9000" });
                        string? message = await options.ApplyServerAsync(workspace, CancellationToken.None).ConfigureAwait(false);
                        Check(message != null && message.Contains("localhost:9000", StringComparison.Ordinal), "No change message was returned.");
                        ClientSettings saved = ClientSettings.Load();
                        Check(saved.Server.Url == "ws://localhost:9000/ws/v1/clients" && saved.Server.RestUrl == null, "The server was not saved.");
                        Check(ServerEndpoints.BuildRestBaseUrl(saved) == "http://localhost:9000", "The REST URL was not derived.");
                        Check(workspace.GetStatus().ServerLabel == "localhost:9000", "Status does not show the new server.");
                        await workspace.UseServerAsync("cloud", CancellationToken.None).ConfigureAwait(false);
                        Check(ClientSettings.Load().Server.Url == ServerTargets.CloudWebSocketUrl, "cloud did not restore the default server.");
                        List<string> forwarded = XenoLaunchOptions.ForwardHomeArguments();
                        Check(forwarded.Count == 2 && forwarded[0] == "--home" && forwarded[1] == home.Directory, "The client home was not forwarded.");
                        string alternate = Path.Combine(home.Directory, "alternate");
                        new XenoLaunchOptions { Home = alternate }.ApplyHome();
                        Check(ClientSettings.SettingsDirectory == Path.GetFullPath(alternate) && Directory.Exists(alternate), "--home did not change the client home.");
                    }
                }),
                AsyncCase(suite, "SignInToAnotherServerMovesTheConnection", "Signing in to another server points the WebSocket and REST URLs at it", async () =>
                {
                    ClientSettings settings = EmptySettings();
                    Check(!ServerTargets.ApplySignInServer(settings, null), "A missing server changed the settings.");
                    Check(!ServerTargets.ApplySignInServer(settings, "https://app.xenocloud.ai:9000"), "Signing in to the configured server changed the settings.");
                    Check(settings.Server.Url == ServerTargets.CloudWebSocketUrl, "The cloud URL changed.");
                    Check(ServerTargets.ApplySignInServer(settings, "http://localhost:9000"), "A different server was not applied.");
                    Check(settings.Server.Url == "ws://localhost:9000/ws/v1/clients" && settings.Server.RestUrl == null, "The WebSocket URL does not follow the sign-in server.");
                    ExpectThrows<ArgumentException>(() => ServerTargets.ApplySignInServer(settings, "gopher://x"));

                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        StubHttpHandler handler = new StubHttpHandler().On("/v1.0/api/auth/login", HttpStatusCode.OK, "{\"id\":\"cred_1\",\"tenantId\":\"ten_lab\",\"accessKey\":\"xeno_key\"}");
                        using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(handler)))
                        {
                            LoginResult result = await workspace.SignInAsync("lab-server:9100", "admin@xeno", "password", null, CancellationToken.None).ConfigureAwait(false);
                            Check(result.Success, "Sign-in failed: " + result.Error);
                            Check(handler.Requests[0].RequestUri!.ToString() == "http://lab-server:9100/v1.0/api/auth/login", "Sign-in did not call the chosen server.");
                            ClientSettings saved = ClientSettings.Load();
                            Check(saved.Server.Url == "ws://lab-server:9100/ws/v1/clients" && saved.Server.AccessKey == "xeno_key" && saved.TenantId == "ten_lab", "Sign-in did not save the server and credential.");
                            LoginResult invalid = await workspace.SignInAsync("ftp://nowhere", "admin@xeno", "password", null, CancellationToken.None).ConfigureAwait(false);
                            Check(!invalid.Success && invalid.Error != null, "An invalid server was accepted.");
                        }
                    }
                }),
                AsyncCase(suite, "ServerClientListsNetworkModels", "Server client lists network models and reports failures", async () =>
                {
                    ClientSettings settings = EmptySettings();
                    settings.Server.Url = "ws://xeno.test:9000/ws/v1/clients";
                    using (XenoServerClient unauthenticated = new XenoServerClient(new StubHttpHandler()))
                    {
                        NetworkModelListResult none = await unauthenticated.ListNetworkModelsAsync(settings, CancellationToken.None).ConfigureAwait(false);
                        Check(!none.Success && none.Error != null, "Missing credential was not reported.");
                    }

                    settings.Server.AccessKey = "key_123";
                    StubHttpHandler handler = new StubHttpHandler().On("/v1/models", HttpStatusCode.OK, "{\"object\":\"list\",\"data\":[{\"id\":\"llama3\",\"object\":\"model\",\"created\":1,\"owned_by\":\"shared\"}]}");
                    using (XenoServerClient client = new XenoServerClient(handler))
                    {
                        NetworkModelListResult result = await client.ListNetworkModelsAsync(settings, CancellationToken.None).ConfigureAwait(false);
                        Check(result.Success && result.Models.Count == 1 && result.Models[0].OwnedBy == "shared", "Network models were not parsed.");
                        Check(handler.Requests[0].Headers.GetValues("x-access-key").Single() == "key_123", "Access key header was not sent.");
                    }

                    StubHttpHandler denied = new StubHttpHandler().On("/v1/models", HttpStatusCode.Forbidden, "{\"message\":\"Inference.Read required\"}");
                    using (XenoServerClient client = new XenoServerClient(denied))
                    {
                        NetworkModelListResult result = await client.ListNetworkModelsAsync(settings, CancellationToken.None).ConfigureAwait(false);
                        Check(!result.Success && result.StatusCode == 403 && result.Error!.Contains("Inference.Read required", StringComparison.Ordinal), "Server error was not reported.");
                    }
                }),
                Case(suite, "StatusSnapshotReflectsCredentialAndWorker", "Status snapshot reflects the credential and worker state", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ClientConnectionState state = new ClientConnectionState();
                    ClientStatusSnapshot signedOut = ClientStatusSnapshot.From(settings, state);
                    Check(signedOut.Status == ClientStatusEnum.NotSignedIn && !signedOut.CanStart, "Missing credential should report not signed in.");
                    settings.Server.AccessKey = "key";
                    ClientStatusSnapshot stopped = ClientStatusSnapshot.From(settings, state);
                    Check(stopped.Status == ClientStatusEnum.Stopped && stopped.CanStart && !stopped.CanStop, "Stopped state is wrong.");
                    state.ProcessId = Int32.MaxValue - 7;
                    state.IsConnected = true;
                    state.IsRegistered = true;
                    ClientStatusSnapshot dead = ClientStatusSnapshot.From(settings, state);
                    Check(dead.Status == ClientStatusEnum.Stopped && !dead.Connected, "A dead worker process must not report connected.");
                }),
                AsyncCase(suite, "LogReaderTailsAndFilters", "Log reader returns the tail and filters by severity", async () =>
                {
                    string file = Path.Combine(Path.GetTempPath(), "xeno-log-" + Guid.NewGuid().ToString("N") + ".log");
                    try
                    {
                        File.WriteAllLines(file, new[] { "t [INFO] one", "t [ERROR] two", "t [INFO] three", "t [WARN] four" });
                        List<string> tail = ClientLogReader.ReadTail(file, 2, "all");
                        Check(tail.Count == 2 && tail[1].EndsWith("four", StringComparison.Ordinal), "Tail is wrong.");
                        List<string> errors = await ClientLogReader.ReadTailAsync(file, 10, "error", CancellationToken.None).ConfigureAwait(false);
                        Check(errors.Count == 1 && errors[0].Contains("two", StringComparison.Ordinal), "Severity filter is wrong.");
                        Check(ClientLogReader.ReadTail(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N")), 10, null).Count == 0, "Missing file should return no lines.");
                    }
                    finally
                    {
                        File.Delete(file);
                    }
                }),
                Case(suite, "HistoryRangesUseExpectedBuckets", "History ranges use the expected spans and buckets", () =>
                {
                    DateTime now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
                    Check(HistoryRanges.StartUtc(HistoryRangeEnum.Hour, now) == now.AddHours(-1), "Hour span is wrong.");
                    Check(HistoryRanges.StartUtc(HistoryRangeEnum.Month, now) == now.AddDays(-30), "Month span is wrong.");
                    Check(HistoryRanges.DefaultBucketMinutes(HistoryRanges.Parse("week")) == 120, "Week bucket is wrong.");
                    Check(HistoryRanges.Parse("bogus") == HistoryRangeEnum.Hour, "Unknown range should map to hour.");
                }),
                Case(suite, "MarkdownKeepsValuesVerbatim", "Markdown built for the terminal app escapes values and fences bodies so they never become markup", () =>
                {
                    Check(XenoMarkdown.Escape("a|b *c* `d`") == "a\\|b \\*c\\* \\`d\\`", "Escape is wrong: " + XenoMarkdown.Escape("a|b *c* `d`"));
                    Check(XenoMarkdown.Escape("one\r\ntwo") == "one two", "Escape keeps line breaks.");
                    string fenced = XenoMarkdown.CodeBlock("before\n```\n# not a heading\n```", "json");
                    Check(fenced.StartsWith("````json\n", StringComparison.Ordinal) && fenced.EndsWith("\n````", StringComparison.Ordinal), "A body with a fence must get a longer fence: " + fenced);
                    string table = XenoMarkdown.Table(new List<string> { "Name", "Value" }, new List<IReadOnlyList<string>> { new List<string> { "x|y" }, new List<string> { "a", "b" } });
                    Check(table == "| Name | Value |\n| --- | --- |\n| x\\|y |  |\n| a | b |", "Table is wrong: " + table);
                    LocalRequestHistoryEntry entry = new LocalRequestHistoryEntry
                    {
                        Id = "req_md",
                        Method = "POST",
                        Path = "/v1/chat/completions",
                        ModelName = "llama3",
                        ResponseBody = "{\"content\":\"**bold** | table\"}"
                    };

                    string markdown = RequestDetailFormatter.ToMarkdown(entry, true);
                    Check(markdown.Contains("| Model | llama3 |", StringComparison.Ordinal), "Summary table is missing.");
                    Check(markdown.Contains("## Response body\n\n```json\n{\"content\":\"**bold** | table\"}\n```", StringComparison.Ordinal), "Response body is not fenced verbatim.");
                    Check(markdown.Contains("## Request body\n\n_Not stored._", StringComparison.Ordinal), "Missing request body is not explained.");
                    Check(!RequestDetailFormatter.ToMarkdown(entry, false).Contains("Response body", StringComparison.Ordinal), "Bodies shown when not asked for.");
                }),
                AsyncCase(suite, "LogRollupCollapsesRoutineServerMessages", "Routine server telemetry messages roll up into counted lines in the desktop and terminal log views", async () =>
                {
                    List<string> lines = new List<string>
                    {
                        "2026-10-14 18:14:23.718836Z [INFO] Connected to Xeno server.",
                        "2026-10-14 18:16:37.497214Z [DEBUG] Server message: server.telemetry.request.",
                        "2026-10-14 18:16:39.429377Z [DEBUG] Server message: server.telemetry.ack.",
                        "2026-10-14 18:16:39.432235Z [DEBUG] Server message: server.telemetry.ack.",
                        "2026-10-14 18:17:37.497214Z [DEBUG] Server message: server.telemetry.request.",
                        "2026-10-14 18:17:39.533205Z [DEBUG] Server message: server.telemetry.ack.",
                        "2026-10-14 18:18:00.000000Z [WARN] Provider local-ollama is unavailable.",
                        "2026-10-14 18:18:37.490491Z [DEBUG] Server message: server.telemetry.request.",
                        "2026-10-14 18:18:40.000000Z [DEBUG] Server message: server.inventory.ack."
                    };

                    List<string> collapsed = ClientLogRollup.Collapse(lines);
                    string firstRequest = ClientLogRollup.LocalTimeOfDay(lines[1])!;
                    string firstAck = ClientLogRollup.LocalTimeOfDay(lines[2])!;
                    Check(collapsed.Count == 6, "Expected 6 lines after the rollup, got " + collapsed.Count + ": " + String.Join(" | ", collapsed));
                    Check(collapsed[0] == lines[0], "Other lines must pass through unchanged.");
                    Check(collapsed[1] == "2026-10-14 18:17:37.497214Z [DEBUG] Server message: server.telemetry.request (2 since " + firstRequest + ")", "Request rollup is wrong: " + collapsed[1]);
                    Check(collapsed[2] == "2026-10-14 18:17:39.533205Z [DEBUG] Server message: server.telemetry.ack (3 since " + firstAck + ")", "Ack rollup is wrong: " + collapsed[2]);
                    Check(collapsed[3] == lines[6], "A different line must end the run.");
                    Check(collapsed[4] == lines[7], "A single routine message must stay as it is.");
                    Check(collapsed[5] == lines[8], "Other server messages must not roll up.");
                    Check(ClientLogRollup.RolledUpMessage(lines[2]) == "server.telemetry.ack" && ClientLogRollup.RolledUpMessage(lines[0]) == null, "Message detection is wrong.");
                    Check(firstAck.Length == 8 && firstAck[2] == ':', "Local time should be HH:mm:ss.");

                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler())))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(ClientConnectionState.LogFile)!);
                        List<string> noisy = new List<string> { "2026-10-14 18:00:00.000000Z [INFO] Started." };
                        for (int i = 0; i < 300; i++) noisy.Add("2026-10-14 18:" + (i / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":" + (i % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".000000Z [DEBUG] Server message: server.telemetry.ack.");
                        File.WriteAllLines(ClientConnectionState.LogFile, noisy);
                        List<string> shown = await workspace.ReadLogAsync(50, "all", false, CancellationToken.None).ConfigureAwait(false);
                        Check(shown.Count == 2 && shown[0].EndsWith("Started.", StringComparison.Ordinal) && shown[1].Contains("server.telemetry.ack (300 since ", StringComparison.Ordinal), "The workspace log view did not roll up: " + String.Join(" | ", shown));
                    }
                }),
                Case(suite, "ChartsMatchTheWebDashboard", "Charts are built once for every front end with the web dashboard's series, units, and empty states", () =>
                {
                    DateTime start = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);
                    List<LocalRequestHistorySummaryBucket> buckets = new List<LocalRequestHistorySummaryBucket>();
                    for (int i = 0; i < 60; i++) buckets.Add(new LocalRequestHistorySummaryBucket { StartUtc = start.AddMinutes(i), Success = i % 2, Failure = i == 59 ? 3 : 0, Total = i % 2 + (i == 59 ? 3 : 0) });
                    XenoChart requests = XenoCharts.Requests(buckets, HistoryRangeEnum.Hour);
                    Check(requests.HasData && requests.BucketCount == 60 && requests.Series.Count == 2, "Request chart shape is wrong.");
                    Check(requests.Series[0].Tone == XenoChartToneEnum.Success && requests.Series[1].Tone == XenoChartToneEnum.Danger, "Failures must be drawn in the danger color.");
                    Check(requests.Heading == "Requests, last hour" && requests.Maximum == 4 && requests.FormatValue(4) == "4 req", "Heading or maximum is wrong: " + requests.Heading + " " + requests.Maximum);
                    Check(requests.Labels[0] == start.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), "Labels must be local time.");
                    XenoChart narrow = requests.Downsample(20);
                    Check(narrow.BucketCount == 20 && narrow.Series[0].Values.Sum() == 30 && narrow.Series[1].Values.Sum() == 3, "Downsampling must keep request counts.");
                    Check(!XenoCharts.Requests(new List<LocalRequestHistorySummaryBucket>(), HistoryRangeEnum.Day).HasData, "An empty chart reported data.");
                    Check(XenoCharts.Requests(new List<LocalRequestHistorySummaryBucket>(), HistoryRangeEnum.Day).EmptyText.StartsWith("No requests in the last day.", StringComparison.Ordinal), "Empty text should name the range.");

                    Check(XenoCharts.HostMetrics.Count == 7 && XenoCharts.HostMetrics.Select(XenoCharts.HostMetricName).Take(3).SequenceEqual(new[] { "CPU", "Memory", "Disk" }), "The host metric picker must offer all seven metrics, like the web dashboards.");
                    List<Xeno.Client.Telemetry.Models.ClientTelemetryRollupBucket> cpu = new List<Xeno.Client.Telemetry.Models.ClientTelemetryRollupBucket>
                    {
                        new Xeno.Client.Telemetry.Models.ClientTelemetryRollupBucket { StartUtc = start, Count = 0, Average = null },
                        new Xeno.Client.Telemetry.Models.ClientTelemetryRollupBucket { StartUtc = start.AddMinutes(1), Count = 4, Average = 40 },
                        new Xeno.Client.Telemetry.Models.ClientTelemetryRollupBucket { StartUtc = start.AddMinutes(2), Count = 4, Average = 20 }
                    };
                    XenoChart host = XenoCharts.HostMetric(Xeno.Client.Telemetry.Models.ClientTelemetryMetricEnum.CpuUtilizationPercent, cpu, HistoryRangeEnum.Hour);
                    Check(host.HasData && host.Title == "CPU" && host.Unit == "%" && host.FormatValue(42.26) == "42.3%", "Host chart is wrong.");
                    Check(host.Kind == XenoChartKindEnum.Lines && host.Series.Select(item => item.Name).SequenceEqual(new[] { "Average", "Minimum", "Maximum" }) && host.Downsample(1).Series[0].Values[0] == 30, "Host charts should draw average, minimum, and maximum, and downsampling must average the average.");
                    XenoChart gpu = XenoCharts.HostMetric(Xeno.Client.Telemetry.Models.ClientTelemetryMetricEnum.GpuUtilizationPercent, cpu.Take(1).ToList(), HistoryRangeEnum.Week);
                    Check(!gpu.HasData && gpu.EmptyText.Contains("NVIDIA", StringComparison.Ordinal) && gpu.RangeText == "last week", "GPU empty state is wrong.");
                    XenoChart provider = XenoCharts.ProviderMetric(Xeno.Client.Telemetry.Models.ClientProviderTelemetryMetricEnum.TotalTokensPerSecond, "lab-vllm", new List<Xeno.Client.Telemetry.Models.ClientProviderTelemetryRollupBucket>(), HistoryRangeEnum.Hour);
                    Check(provider.Title == "Total tokens per second (lab-vllm)" && provider.Unit == "/s" && !provider.HasData, "Provider chart is wrong: " + provider.Title);

                    Check(XenoCharts.LabelLeft(50, 20, 0, 100) == 40 && XenoCharts.LabelLeft(2, 20, 0, 100) == 0 && XenoCharts.LabelLeft(98, 20, 0, 100) == 80, "Time labels should center under their bucket and stay inside the plot.");
                    Check(XenoCharts.LabelLeft(17, 52, 0, 34.47) == 0, "A time label wider than the plot must start at its left edge, not throw (desktop crash 2026-11-11).");

                    Check(HistoryRanges.Labels.Count == 4 && HistoryRanges.Describe(HistoryRangeEnum.Month) == "last month", "Range labels are wrong.");
                    Check(HistoryRanges.FromIndex(2) == HistoryRangeEnum.Week && HistoryRanges.FromIndex(-1) == HistoryRangeEnum.Hour && HistoryRanges.FromIndex(9) == HistoryRangeEnum.Hour, "Range indexes are wrong.");
                }),
                Case(suite, "MacMemoryReadsVmStat", "macOS memory telemetry counts active, wired, and compressed pages from vm_stat", () =>
                {
                    string output = "Mach Virtual Memory Statistics: (page size of 16384 bytes)\nPages free:                                   424002.\nPages active:                                3695032.\n"
                        + "Pages inactive:                              3442841.\nPages wired down:                             333251.\nPages occupied by compressor:                 161737.\n";
                    long? used = Xeno.Client.Telemetry.Providers.ClientSystemTelemetryProvider.ParseMacUsedMemory(output);
                    Check(used == (3695032L + 333251L + 161737L) * 16384L, "Used memory is wrong: " + used);
                    Check(Xeno.Client.Telemetry.Providers.ClientSystemTelemetryProvider.ParseMacUsedMemory("nonsense") == null, "Unreadable output must not report memory.");
                }),
                Case(suite, "SignInFollowUpOffersTheNextStep", "After signing in, every front end offers to start the client, or to restart it when it is running", () =>
                {
                    ClientStatusSnapshot stopped = new ClientStatusSnapshot { Name = "studio-mac", ServerLabel = "Xeno cloud", RestBaseUrl = "https://app.xenocloud.ai:9000", TenantId = "ten_default", SettingsFile = "/home/me/.xeno/client.json", Running = false };
                    XenoFollowUp start = XenoClientActions.AfterSignIn(stopped);
                    Check(start.Command == XenoCommandEnum.StartClient && start.ActionText == "Start Client", "A stopped client should be offered Start Client.");
                    Check(start.Message.Contains("Xeno cloud (https://app.xenocloud.ai:9000)", StringComparison.Ordinal) && start.Message.Contains("ten_default", StringComparison.Ordinal) && start.Message.Contains("studio-mac", StringComparison.Ordinal), "The confirmation should name the server, tenant, and machine: " + start.Message);
                    stopped.Running = true;
                    XenoFollowUp restart = XenoClientActions.AfterSignIn(stopped);
                    Check(restart.Command == XenoCommandEnum.RestartClient && restart.ActionText == "Restart Client", "A running client should be offered Restart Client.");
                    XenoFormSpec form;
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler())))
                    {
                        form = XenoForms.SignIn(workspace);
                    }

                    Check(form.ProgressText == "Signing in...", "The sign-in form should say it is signing in.");
                    string success = form.SuccessMessage(new Dictionary<string, string> { ["server"] = "cloud", ["tenant"] = "ten_default", ["username"] = "admin@xeno", ["password"] = "x" });
                    Check(success == "Signed in to cloud (tenant ten_default) as admin@xeno.", "Success message is wrong: " + success);
                }),
                Case(suite, "SettingsCatalogValidatesAndBuildsRequests", "Settings catalog validates values and keeps inference loopback-only", () =>
                {
                    ClientSettings settings = EmptySettings();
                    Dictionary<string, string> values = XenoSettingsCatalog.Read(settings);
                    Check(values["dashboard.port"] == "9343" && values["inference.enabled"] == "false", "Defaults were not read.");
                    values["inference.enabled"] = "true";
                    values["inference.port"] = "11500";
                    LocalClientSettingsUpdateRequest request = XenoSettingsCatalog.BuildRequest(settings, values);
                    Check(request.LocalInferenceServer!.Enabled && request.LocalInferenceServer.Port == 11500, "Values were not applied.");
                    LocalSettingsEditor.Apply(settings, request);
                    Check(settings.LocalInferenceServer.Port == 11500, "Request was not applied to settings.");
                    Dictionary<string, string> badPort = XenoSettingsCatalog.Read(settings);
                    badPort["dashboard.port"] = "70000";
                    ExpectThrows<ArgumentException>(() => XenoSettingsCatalog.BuildRequest(settings, badPort));
                    Dictionary<string, string> publicHost = XenoSettingsCatalog.Read(settings);
                    publicHost["inference.host"] = "0.0.0.0";
                    ExpectThrows<ArgumentException>(() => XenoSettingsCatalog.BuildRequest(settings, publicHost));
                    LocalClientSettingsUpdateRequest direct = LocalSettingsEditor.FromSettings(settings);
                    direct.LocalInferenceServer!.Host = "192.168.1.10";
                    ExpectThrows<ArgumentException>(() => LocalSettingsEditor.Apply(settings, direct));
                    Check(XenoSettingsCatalog.RequiresRestart(XenoSettingsCatalog.Read(EmptySettings()), XenoSettingsCatalog.Read(settings)), "Listener change should require a restart.");
                }),
                Case(suite, "ExecutableLocatorFollowsSearchOrder", "Executable locator follows override, sibling, path, and checkout order", () =>
                {
                    string root = Path.Combine(Path.GetTempPath(), "xeno-locate-" + Guid.NewGuid().ToString("N"));
                    bool windows = OperatingSystem.IsWindows();
                    string cliFile = windows ? "xeno.exe" : "xeno";
                    try
                    {
                        string sibling = Directory.CreateDirectory(Path.Combine(root, "app")).FullName;
                        string pathDir = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
                        string checkoutBin = Directory.CreateDirectory(Path.Combine(root, "repo", "src", "Xeno.Desktop", "bin", "Debug", "net10.0")).FullName;
                        string nested = Directory.CreateDirectory(Path.Combine(root, "repo", "work", "deep")).FullName;
                        File.WriteAllText(Path.Combine(pathDir, cliFile), String.Empty);
                        File.WriteAllText(Path.Combine(checkoutBin, "xeno-desktop.dll"), String.Empty);

                        XenoLocatorContext context = new XenoLocatorContext { IsWindows = windows, PathVariable = pathDir, SiblingDirectories = new List<string> { sibling }, CheckoutSearchRoots = new List<string> { nested } };
                        XenoExecutableLocation? cli = XenoExecutableLocator.Locate(XenoApplicationEnum.Cli, context);
                        Check(cli != null && cli.Source == "path", "CLI should come from PATH.");
                        XenoExecutableLocation? desktop = XenoExecutableLocator.Locate(XenoApplicationEnum.Desktop, context);
                        Check(desktop != null && desktop.Source == "checkout" && desktop.LeadingArguments.Count == 1 && desktop.LeadingArguments[0].EndsWith("xeno-desktop.dll", StringComparison.Ordinal), "Desktop should come from the checkout through dotnet.");

                        File.WriteAllText(Path.Combine(sibling, cliFile), String.Empty);
                        Check(XenoExecutableLocator.Locate(XenoApplicationEnum.Cli, context)!.Source == "sibling", "Sibling should win over PATH.");
                        context.EnvironmentOverrides[XenoExecutableLocator.CliEnvironmentVariable] = Path.Combine(pathDir, cliFile);
                        Check(XenoExecutableLocator.Locate(XenoApplicationEnum.Cli, context)!.Source == "environment", "Environment override should win.");
                        Check(XenoExecutableLocator.Locate(XenoApplicationEnum.Tray, context) == null, "Missing tray should return null.");
                    }
                    finally
                    {
                        Directory.Delete(root, true);
                    }
                }),
                Case(suite, "TerminalLaunchPlannerPlansEachPlatform", "Terminal launch planner builds per-platform commands", () =>
                {
                    XenoExecutableLocation location = new XenoExecutableLocation { Executable = "/opt/xeno it's/xeno", Source = "test" };
                    List<LaunchCommand> mac = TerminalLaunchPlanner.PlanTerminal(TerminalPlatformEnum.MacOS, location, new[] { "tui" }, "/Users/me", "Xeno Terminal", "/tmp");
                    Check(mac.Count == 1 && mac[0].FileName == "/usr/bin/open" && mac[0].ScriptPath!.EndsWith(".command", StringComparison.Ordinal), "macOS plan is wrong.");
                    Check(mac[0].ScriptContent!.Contains("'/opt/xeno it'\\''s/xeno' 'tui'", StringComparison.Ordinal), "macOS script did not quote the command.");
                    List<LaunchCommand> windows = TerminalLaunchPlanner.PlanTerminal(TerminalPlatformEnum.Windows, new XenoExecutableLocation { Executable = "C:\\Xeno\\xeno.exe" }, new[] { "tui" }, "C:\\Users\\me", "Xeno Terminal", "C:\\Temp");
                    Check(windows.Count == 1 && windows[0].FileName == "cmd.exe" && windows[0].RawArguments!.Contains("start \"Xeno Terminal\"", StringComparison.Ordinal) && windows[0].RawArguments!.Contains("\"C:\\Xeno\\xeno.exe\" \"tui\"", StringComparison.Ordinal), "Windows plan is wrong.");
                    List<LaunchCommand> linux = TerminalLaunchPlanner.PlanTerminal(TerminalPlatformEnum.Linux, location, new[] { "tui" }, "/home/me", "Xeno Terminal", "/tmp");
                    Check(linux.Count >= 5 && linux[0].FileName == "x-terminal-emulator" && linux.Any(item => item.FileName == "xterm"), "Linux candidates are wrong.");
                    Check(TerminalLaunchPlanner.PlanOpen(TerminalPlatformEnum.Linux, "http://127.0.0.1:9343").FileName == "xdg-open", "Linux open is wrong.");
                    Check(TerminalLaunchPlanner.PlanOpen(TerminalPlatformEnum.Windows, "http://127.0.0.1:9343").UseShellExecute, "Windows open should use the shell.");
                }),
                AsyncCase(suite, "WorkspaceProviderAndModelRoundTrip", "Workspace saves providers and models and requests re-advertisement", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler()), () => null))
                    {
                        ClientSettings initial = workspace.LoadSettings();
                        foreach (ClientRunnerSettings existing in initial.Runners.ToList()) await workspace.DeleteProviderAsync(existing.Name, CancellationToken.None).ConfigureAwait(false);
                        ProviderAddResult added = await workspace.AddProviderAsync(Provider("vllm", "OpenAICompatible", "http://127.0.0.1:8000"), CancellationToken.None).ConfigureAwait(false);
                        Check(added.Added, "Provider was not added.");
                        await workspace.AddModelAsync("vllm", "qwen", true, false, CancellationToken.None).ConfigureAwait(false);
                        Check(ClientConnectionState.IsInventoryUpdatePending(), "Inventory update was not requested.");
                        await ExpectThrowsAsync<ArgumentException>(() => workspace.AddModelAsync("vllm", "QWEN", true, false, CancellationToken.None)).ConfigureAwait(false);
                        ClientSettings reloaded = ClientSettings.Load();
                        Check(reloaded.Runners.Single().Models.Single().Name == "qwen", "Model was not saved.");
                        List<ProviderRow> rows = workspace.GetProviders();
                        Check(rows.Single().ModelNames == "qwen", "Provider rows are wrong.");
                        ModelCatalog catalog = await workspace.GetModelCatalogAsync(true, CancellationToken.None).ConfigureAwait(false);
                        Check(catalog.LocalCount == 1 && !catalog.NetworkAvailable && catalog.NetworkMessage.Length > 0, "Catalog without a credential is wrong.");
                        await ExpectThrowsAsync<InvalidOperationException>(() => workspace.StartClientAsync(CancellationToken.None)).ConfigureAwait(false);
                        bool restart = await workspace.UpdateSettingsAsync(new Dictionary<string, string> { ["history.maxEntries"] = "50" }, CancellationToken.None).ConfigureAwait(false);
                        Check(!restart && ClientSettings.Load().LocalDashboard.HistoryRetention.MaxEntries == 50, "History setting was not saved without a restart.");
                        Check(home.Directory.Length > 0, "Scope directory missing.");
                    }
                }),
                AsyncCase(suite, "WorkspaceReadsLocalHistory", "Workspace reads request history and statistics from the local database", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler()), () => null))
                    {
                        ClientSettings settings = workspace.LoadSettings();
                        using (LocalDataSession data = await LocalDataSession.OpenAsync(settings, CancellationToken.None).ConfigureAwait(false))
                        {
                            await data.RequestHistory.UpsertAsync(new LocalRequestHistoryEntry
                            {
                                Id = "req_1",
                                ReceivedUtc = DateTime.UtcNow,
                                CompletedUtc = DateTime.UtcNow,
                                Path = "/v1/chat/completions",
                                ModelName = "llama3",
                                ProviderName = "ollama",
                                StatusCode = 200,
                                Outcome = LocalRequestOutcomeEnum.Success
                            }, settings.LocalDashboard.HistoryRetention, CancellationToken.None).ConfigureAwait(false);
                        }

                        LocalDashboardStatisticsResponse statistics = await workspace.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
                        Check(statistics.Requests == 1, "Statistics are wrong.");
                        List<RequestRow> rows = await workspace.ListRequestsAsync(10, CancellationToken.None).ConfigureAwait(false);
                        Check(rows.Count == 1 && rows[0].Model == "llama3" && !rows[0].Failed, "Request rows are wrong.");
                        LocalRequestHistoryEntry? entry = await workspace.GetRequestAsync("req_1", CancellationToken.None).ConfigureAwait(false);
                        Check(entry != null && RequestDetailFormatter.ToText(entry, false).Contains("llama3", StringComparison.Ordinal), "Request detail is wrong.");
                        Check(await workspace.DeleteRequestAsync("req_1", CancellationToken.None).ConfigureAwait(false), "Delete failed.");
                        Check(home.Directory.Length > 0, "Scope directory missing.");
                    }
                })
            });
        }

        /// <summary>
        /// Tray agent behavior that does not need a display: menu contents and command availability.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TraySuite()
        {
            const string suite = "Tray";
            return new TestSuiteDescriptor(suite, "Tray Agent", new List<TestCaseDescriptor>
            {
                Case(suite, "TrayMenuLaunchesEveryFrontEnd", "Tray menu launches the desktop app, terminal app, and web dashboard", () =>
                {
                    List<XenoCommandEnum> commands = XenoMenuLayout.Commands(XenoMenuLayout.TrayMenu());
                    foreach (XenoCommandEnum required in new[] { XenoCommandEnum.OpenDesktopApp, XenoCommandEnum.OpenTerminalApp, XenoCommandEnum.OpenWebDashboard, XenoCommandEnum.StartClient, XenoCommandEnum.StopClient, XenoCommandEnum.RestartClient, XenoCommandEnum.About, XenoCommandEnum.Quit })
                    {
                        Check(commands.Contains(required), "Tray menu is missing " + required + ".");
                    }

                    Check(XenoMenuLayout.TrayMenu()[0].Kind == XenoMenuEntryKindEnum.StatusLine, "Tray menu should start with the status line.");
                    List<string> headers = XenoMenuLayout.TrayMenu().Select(item => item.Header).ToList();
                    Check(headers.Contains("Open Xeno Desktop") && headers.Contains("Settings") && !headers.Contains("Settings...") && !headers.Contains("Open Xeno"), "Tray menu labels are wrong: " + String.Join(", ", headers));
                    Check(!commands.Contains(XenoCommandEnum.GoChat) && !commands.Contains(XenoCommandEnum.GoLocalEndpoint), "The tray menu should not offer Chat or the Local endpoint page.");
                    Check(XenoMenuLayout.ApplicationMenu(XenoSurfaceEnum.Tray)[0].Header == "About Xeno Agent", "macOS application menu should name the agent.");
                }),
                Case(suite, "TrayMenuOpensSettings", "Tray menus offer Settings, which is always available", () =>
                {
                    Check(XenoMenuLayout.Commands(XenoMenuLayout.TrayMenu()).Contains(XenoCommandEnum.Settings), "The tray menu has no Settings item.");
                    Check(XenoMenuLayout.ApplicationMenu(XenoSurfaceEnum.Tray).Any(item => item.Command == XenoCommandEnum.Settings), "The tray's macOS application menu has no Settings item.");
                    Check(XenoMenuLayout.ApplicationMenu(XenoSurfaceEnum.Desktop).Any(item => item.Command == XenoCommandEnum.Settings && item.Key == ","), "The desktop application menu has no Settings item.");
                    Check(XenoCommandAvailability.IsEnabled(XenoCommandEnum.Settings, null), "Settings should be available before status loads.");
                    Check(XenoCommandAvailability.IsEnabled(XenoCommandEnum.Settings, new ClientStatusSnapshot { Status = ClientStatusEnum.NotSignedIn }), "Settings should be available when signed out.");
                }),
                Case(suite, "DesktopActivationHandsRequestsToRunningApp", "A request for the running desktop app is taken once, and stale requests are dropped", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        Check(DesktopActivation.Take() == null, "A request appeared from nowhere.");
                        DesktopActivation.Post(new DesktopActivationRequest { Page = XenoPageEnum.LocalEndpoint, OpenSettings = true });
                        Check(File.Exists(DesktopActivation.FilePath) && DesktopActivation.FilePath.StartsWith(ClientSettings.SettingsDirectory, StringComparison.Ordinal), "The request was not written under the client home.");
                        DesktopActivationRequest? taken = DesktopActivation.Take();
                        Check(taken != null && taken.Page == XenoPageEnum.LocalEndpoint && taken.OpenSettings, "The request did not round-trip.");
                        Check(DesktopActivation.Take() == null, "A request was taken twice.");

                        File.WriteAllText(DesktopActivation.FilePath, Xeno.Core.Serialization.JsonUtil.Serialize(new DesktopActivationRequest { OpenSettings = true, RequestedUtc = DateTime.UtcNow.AddMinutes(-5) }));
                        Check(DesktopActivation.Take() == null && !File.Exists(DesktopActivation.FilePath), "A stale request was acted on or left behind.");
                        File.WriteAllText(DesktopActivation.FilePath, "not json");
                        Check(DesktopActivation.Take() == null, "An unreadable request was acted on.");

                        List<string> args = XenoAppLauncher.DesktopArguments(new DesktopActivationRequest { Page = XenoPageEnum.Settings, OpenSettings = true });
                        Check(String.Join(" ", args) == "--page settings --settings", "Fresh launch arguments are wrong: " + String.Join(" ", args));
                        Check(XenoLaunchOptions.Parse(args).OpenSettings && XenoLaunchOptions.Parse(args).StartPage == XenoPageEnum.Settings, "Fresh launch arguments do not parse back.");
                    }
                }),
                Case(suite, "CommandAvailabilityFollowsStatus", "Start, stop, and dashboard availability follow the client status", () =>
                {
                    ClientStatusSnapshot signedOut = new ClientStatusSnapshot { Status = ClientStatusEnum.NotSignedIn, StatusText = "Not signed in" };
                    Check(!XenoCommandAvailability.IsEnabled(XenoCommandEnum.StartClient, signedOut), "Start should need a credential.");
                    Check(XenoCommandAvailability.DisabledReason(XenoCommandEnum.StartClient, signedOut)!.Contains("Sign in", StringComparison.Ordinal), "Disabled reason should explain sign-in.");
                    ClientStatusSnapshot stopped = new ClientStatusSnapshot { Status = ClientStatusEnum.Stopped, HasCredential = true, LocalDashboardEnabled = true, StatusText = "Stopped" };
                    Check(XenoCommandAvailability.IsEnabled(XenoCommandEnum.StartClient, stopped), "Start should be enabled when stopped.");
                    Check(!XenoCommandAvailability.IsEnabled(XenoCommandEnum.StopClient, stopped), "Stop should be disabled when stopped.");
                    Check(!XenoCommandAvailability.IsEnabled(XenoCommandEnum.OpenWebDashboard, stopped), "Dashboard should be disabled when stopped.");
                    ClientStatusSnapshot connected = new ClientStatusSnapshot { Status = ClientStatusEnum.Connected, HasCredential = true, Running = true, Connected = true, Registered = true, LocalDashboardEnabled = true, LocalDashboardListening = true, LocalDashboardUrl = "http://127.0.0.1:9343", StatusText = "Connected" };
                    Check(!XenoCommandAvailability.IsEnabled(XenoCommandEnum.StartClient, connected), "Start should be disabled when running.");
                    Check(XenoCommandAvailability.IsEnabled(XenoCommandEnum.RestartClient, connected), "Restart should be enabled when running.");
                    Check(XenoCommandAvailability.IsEnabled(XenoCommandEnum.OpenWebDashboard, connected), "Dashboard should be enabled when listening.");
                    Check(XenoCommandAvailability.StatusLine(connected) == "Xeno: Connected" && XenoCommandAvailability.StatusLine(null) == "Xeno: checking...", "Status line is wrong.");
                    Check(XenoCommandAvailability.IsEnabled(XenoCommandEnum.OpenTerminalApp, null), "Launchers should always be enabled.");
                }),
                Case(suite, "ClientActionsDescribeStartOutcomes", "Client actions describe start outcomes", () =>
                {
                    Check(XenoClientActions.DescribeStart(new ClientStartResult { Outcome = ClientStartOutcomeEnum.AlreadyRunning }).Contains("already", StringComparison.Ordinal), "Already-running message is wrong.");
                    Check(XenoClientActions.DescribeStart(new ClientStartResult { Outcome = ClientStartOutcomeEnum.Started, ProcessId = 42 }).Contains("42", StringComparison.Ordinal), "Started message is wrong.");
                    ClientStartResult failed = new ClientStartResult { Outcome = ClientStartOutcomeEnum.NotRegistered, State = new ClientConnectionState { LastDisconnectReason = "401 Unauthorized" } };
                    Check(XenoClientActions.DescribeStart(failed).Contains("401 Unauthorized", StringComparison.Ordinal), "Failure reason was not reported.");
                })
            });
        }

        /// <summary>
        /// Desktop app logic that does not need a display: menus, navigation, and preferences.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor DesktopSuite()
        {
            const string suite = "Desktop";
            return new TestSuiteDescriptor(suite, "Desktop App", new List<TestCaseDescriptor>
            {
                Case(suite, "MacMenuBarHasStandardMenus", "The macOS menu bar has File, Edit, View, Client, Window, and Help", () =>
                {
                    List<string> mac = XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, true).Select(item => item.Header).ToList();
                    Check(String.Join(",", mac) == "File,Edit,View,Client,Window,Help", "macOS menus are wrong: " + String.Join(",", mac));
                    List<string> other = XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, false).Select(item => item.Header).ToList();
                    Check(String.Join(",", other) == "File,View,Client,Help", "Windows and Linux menus are wrong: " + String.Join(",", other));
                    List<XenoMenuEntry> app = XenoMenuLayout.ApplicationMenu(XenoSurfaceEnum.Desktop);
                    Check(app[0].Header == "About Xeno" && app.Any(item => item.Command == XenoCommandEnum.Settings && item.Key == ","), "macOS application menu should offer About Xeno and Settings.");
                    List<XenoCommandEnum> commands = XenoMenuLayout.Commands(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, true));
                    Check(commands.Contains(XenoCommandEnum.OpenTerminalApp) && commands.Contains(XenoCommandEnum.OpenWebDashboard), "Desktop menus should launch the terminal app and web dashboard.");
                    Check(!XenoMenuLayout.Commands(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, false)).Contains(XenoCommandEnum.Undo), "Edit commands belong to the macOS menu only.");
                }),
                Case(suite, "MenuShortcutsAreUnique", "Menu shortcuts are unique for every surface", () =>
                {
                    foreach (XenoSurfaceEnum surface in new[] { XenoSurfaceEnum.Desktop, XenoSurfaceEnum.Terminal })
                    {
                        foreach (bool mac in new[] { true, false })
                        {
                            List<string> keys = XenoMenuLayout.MenuBar(surface, mac).SelectMany(item => item.Flatten())
                                .Where(item => item.Kind == XenoMenuEntryKindEnum.Item && !String.IsNullOrEmpty(item.Key))
                                .Select(item => (item.Shift ? "shift+" : String.Empty) + item.Key.ToLowerInvariant())
                                .ToList();
                            List<string> duplicates = keys.GroupBy(item => item).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
                            Check(duplicates.Count == 0, surface + " shortcuts collide: " + String.Join(", ", duplicates));
                        }
                    }
                }),
                Case(suite, "NavigationCatalogCoversEveryPage", "Every page is in the navigation catalog and the View menu", () =>
                {
                    List<XenoCommandEnum> view = XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, false).Single(item => item.Header == "View").Flatten().Select(item => item.Command).ToList();
                    foreach (XenoPageEnum page in Enum.GetValues<XenoPageEnum>())
                    {
                        XenoPageDefinition definition = XenoNavigationCatalog.Get(page);
                        Check(!String.IsNullOrWhiteSpace(definition.Title) && !String.IsNullOrWhiteSpace(definition.Description), page + " needs a title and description.");
                        Check(XenoNavigationCatalog.PageFor(definition.Command) == page, page + " command does not map back.");
                        Check(view.Contains(definition.Command), page + " is not in the View menu.");
                    }

                    Check(XenoNavigationCatalog.Pages.Where(item => item.ShortcutDigit >= 0).GroupBy(item => item.ShortcutDigit).All(group => group.Count() == 1), "Page shortcut digits collide.");
                    Check(XenoNavigationCatalog.Get(XenoPageEnum.HostTelemetry).Title == "Host" && XenoNavigationCatalog.Get(XenoPageEnum.ProviderTelemetry).Title == "Provider", "Telemetry pages should be named Host and Provider under the Telemetry section.");
                    Check(XenoNavigationCatalog.Get(XenoPageEnum.HostTelemetry).LongTitle == "Host Telemetry" && XenoNavigationCatalog.Get(XenoPageEnum.Server).Title == "General" && XenoNavigationCatalog.Get(XenoPageEnum.Server).LongTitle == "General", "Long titles are wrong.");
                    Check(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, false).Single(item => item.Header == "View").Flatten().Any(item => item.Header == "Host Telemetry"), "Menus should use the long title.");
                    Check(XenoCommandAvailability.Title(XenoCommandEnum.GoServer) == "Go to General", "Palette title is wrong.");
                }),
                Case(suite, "FrontEndPreferencesRoundTrip", "Front-end preferences save, load, and clamp", () =>
                {
                    string file = Path.Combine(Path.GetTempPath(), "xeno-prefs-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        XenoFrontEndPreferences prefs = new XenoFrontEndPreferences { DesktopAppearance = XenoAppearanceEnum.Dark, TerminalLastPage = XenoPageEnum.Logs, RefreshSeconds = 1 };
                        Check(prefs.RefreshSeconds == 2, "Refresh interval was not clamped.");
                        Check(prefs.Save(file), "Save failed.");
                        XenoFrontEndPreferences loaded = XenoFrontEndPreferences.Load(file);
                        Check(loaded.DesktopAppearance == XenoAppearanceEnum.Dark && loaded.TerminalLastPage == XenoPageEnum.Logs, "Preferences did not round-trip.");
                        File.WriteAllText(file, "{ not json");
                        Check(XenoFrontEndPreferences.Load(file).DesktopAppearance == XenoAppearanceEnum.System, "Corrupt preferences should fall back to defaults.");
                    }
                    finally
                    {
                        File.Delete(file);
                    }
                }),
                Case(suite, "PresentationRowsFormatValues", "Table rows format providers, models, and requests", () =>
                {
                    ClientRunnerSettings provider = Provider("ollama", "Ollama", "http://127.0.0.1:11434");
                    provider.Credential = "secret-token-1234";
                    provider.AllowModelManagement = true;
                    provider.Models.Add(new ClientModelSettings { Name = "llama3", SupportsCompletions = true, SupportsEmbeddings = true });
                    ProviderRow row = ProviderRow.From(provider);
                    Check(row.Credential.Contains("1234", StringComparison.Ordinal) && !row.Credential.Contains("secret", StringComparison.Ordinal), "Credential was not masked.");
                    Check(row.ModelManagement == "yes" && row.Timeout == "300 s", "Provider row values are wrong.");
                    Check(ProviderModelRow.From(provider, provider.Models[0]).Capabilities == "completions, embeddings", "Capabilities are wrong.");
                    RequestRow failed = RequestRow.From(new LocalRequestHistoryEntry { Id = "r", Outcome = LocalRequestOutcomeEnum.Failure, StatusCode = 502 });
                    Check(failed.Failed && failed.Status == "502", "Failed request row is wrong.");
                    Check(DisplayFormat.Bytes(1536) == "1.5 KB" && DisplayFormat.Milliseconds(2400) == "2.4 s" && DisplayFormat.Truncate("abcdefghij", 6) == "abc...", "Formatting helpers are wrong.");
                })
            });
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string suite, string id, string name, Action body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: delegate
            {
                body();
                return Task.CompletedTask;
            });
        }

        private static TestCaseDescriptor AsyncCase(string suite, string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: delegate { return body(); });
        }

        private static ClientSettings EmptySettings()
        {
            return new ClientSettings { Providers = new List<ClientRunnerSettings>() };
        }

        private static ClientRunnerSettings Provider(string name, string apiType, string endpoint)
        {
            return new ClientRunnerSettings { Name = name, ApiType = apiType, Endpoint = endpoint, Models = new List<ClientModelSettings>() };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void ExpectThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }

        private static async Task ExpectThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }

        #endregion
    }
}
