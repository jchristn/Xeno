namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Shared concurrency limits, the local endpoint request log and runtime status, the routing API, and the Local endpoint page.
    /// </summary>
    public static class HybridConcurrencyTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Concurrency gate shared by network and local requests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ConcurrencySuite()
        {
            const string suite = "HybridConcurrency";
            return new TestSuiteDescriptor(suite, "Hybrid Concurrency", new List<TestCaseDescriptor>
            {
                Case(suite, "GateIsSharedByWorkerAndListener", "Network and local requests draw from the same provider limit", async () =>
                {
                    ClientSettings settings = Limited(2, null);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? network = gate.TryAcquireNetwork(settings, provider, "a");
                    ConcurrencyLease? local = await gate.AcquireLocalAsync(settings, provider, "b", 0, CancellationToken.None).ConfigureAwait(false);
                    Check(network != null && local != null, "Two slots were not available.");
                    Check(gate.TryAcquireNetwork(settings, provider, "c") == null, "A third request got a slot past the limit.");
                    ProviderConcurrencySnapshot snapshot = gate.Snapshot(settings).Single();
                    Check(snapshot.InFlight == 2 && snapshot.InFlightLocal == 1 && snapshot.InFlightNetwork == 1 && snapshot.NetworkRejected == 1, "Snapshot counters are wrong.");
                    Check(gate.LocalInFlightOnSharedProviders(settings) == 1, "Local in-flight on shared providers is wrong.");
                    network!.Dispose();
                    network.Dispose();
                    Check(gate.Snapshot(settings).Single().InFlight == 1, "Releasing twice changed the count twice.");
                    local!.Dispose();
                }),
                Case(suite, "ModelLimitCapsOneModelOnly", "A model limit caps that model while other models keep using the provider", async () =>
                {
                    ClientSettings settings = Limited(4, 1);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? first = gate.TryAcquireNetwork(settings, provider, "big");
                    Check(first != null, "The first request for the limited model was refused.");
                    Check(gate.TryAcquireNetwork(settings, provider, "big:latest") == null, "The model limit was not applied (with name normalization).");
                    ConcurrencyLease? other = await gate.AcquireLocalAsync(settings, provider, "small", 0, CancellationToken.None).ConfigureAwait(false);
                    Check(other != null, "Another model was blocked by the model limit.");
                    first!.Dispose();
                    other!.Dispose();
                }),
                Case(suite, "ProviderLimitCapsAllModels", "The provider limit caps every model on the provider together", () =>
                {
                    ClientSettings settings = Limited(2, null);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? a = gate.TryAcquireNetwork(settings, provider, "x");
                    ConcurrencyLease? b = gate.TryAcquireNetwork(settings, provider, "y");
                    Check(a != null && b != null && gate.TryAcquireNetwork(settings, provider, "z") == null, "The provider limit did not cap all models.");
                    a!.Dispose();
                    b!.Dispose();
                    return Task.CompletedTask;
                }),
                Case(suite, "LocalRequestQueuesThenSucceeds", "A local request waits for a slot and gets it when one frees up", async () =>
                {
                    ClientSettings settings = Limited(1, null);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? held = gate.TryAcquireNetwork(settings, provider, "m");
                    Task<ConcurrencyLease?> waiting = gate.AcquireLocalAsync(settings, provider, "m", 5000, CancellationToken.None);
                    await Task.Delay(100).ConfigureAwait(false);
                    Check(!waiting.IsCompleted && gate.Snapshot(settings).Single().Queued == 1, "The local request was not queued.");
                    held!.Dispose();
                    ConcurrencyLease? lease = await waiting.ConfigureAwait(false);
                    Check(lease != null && lease.WaitedMs >= 50, "The queued request did not get the freed slot.");
                    Check(gate.Snapshot(settings).Single().Queued == 0, "The queue count was not cleared.");
                    lease!.Dispose();
                }),
                Case(suite, "LocalRequestQueueTimeoutReturnsNull", "A local request that waits too long is refused and counted", async () =>
                {
                    ClientSettings settings = Limited(1, null);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? held = gate.TryAcquireNetwork(settings, provider, "m");
                    ConcurrencyLease? refused = await gate.AcquireLocalAsync(settings, provider, "m", 150, CancellationToken.None).ConfigureAwait(false);
                    Check(refused == null, "A full provider handed out a slot.");
                    ProviderConcurrencySnapshot snapshot = gate.Snapshot(settings).Single();
                    Check(snapshot.LocalRejected == 1 && snapshot.Queued == 0, "The refusal was not counted or the queue leaked.");
                    held!.Dispose();
                }),
                Case(suite, "QueuedRequestCancelsPromptly", "Cancelling a queued local request gives up its place and throws", async () =>
                {
                    ClientSettings settings = Limited(1, null);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? held = gate.TryAcquireNetwork(settings, provider, "m");
                    using (CancellationTokenSource cancel = new CancellationTokenSource(100))
                    {
                        bool cancelled = false;
                        DateTime started = DateTime.UtcNow;
                        try
                        {
                            await gate.AcquireLocalAsync(settings, provider, "m", 10000, cancel.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            cancelled = true;
                        }

                        Check(cancelled && (DateTime.UtcNow - started).TotalMilliseconds < 2000, "Cancellation did not end the wait promptly.");
                    }

                    Check(gate.Snapshot(settings).Single().Queued == 0, "A cancelled waiter stayed in the queue.");
                    held!.Dispose();
                }),
                Case(suite, "GateResizesLive", "Raising a limit in settings frees capacity without a restart", () =>
                {
                    ClientSettings settings = Limited(1, null);
                    ClientRunnerSettings provider = settings.Providers[0];
                    ProviderConcurrencyGate gate = new ProviderConcurrencyGate();
                    ConcurrencyLease? first = gate.TryAcquireNetwork(settings, provider, "m");
                    Check(gate.TryAcquireNetwork(settings, provider, "m") == null, "Limit 1 allowed two.");
                    provider.MaxConcurrentRequests = 2;
                    ConcurrencyLease? second = gate.TryAcquireNetwork(settings, provider, "m");
                    Check(second != null, "Raising the limit did not take effect.");
                    first!.Dispose();
                    second!.Dispose();
                    return Task.CompletedTask;
                }),
                Case(suite, "ListenerReturns429WhenProviderFull", "The listener answers 429 with Retry-After when a local provider stays full", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/generate" ? FakeProviderResponse.Json("{\"done\":true}") : FakeProviderResponse.Json("{\"models\":[]}")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings =>
                    {
                        ClientRunnerSettings provider = ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b");
                        provider.MaxConcurrentRequests = 1;
                        settings.Providers.Add(provider);
                        settings.LocalInferenceServer.LocalQueueTimeoutMs = 0;
                    }))
                    {
                        ProviderConcurrencyGate.Shared.Reset();
                        ConcurrencyLease? held = ProviderConcurrencyGate.Shared.TryAcquireNetwork(listener.Settings, listener.Settings.Providers[0], "llama3.1:8b");
                        try
                        {
                            using (HttpResponseMessage response = await listener.Http.PostAsync(listener.Url("/api/generate"), new StringContent("{\"model\":\"llama3.1:8b\",\"prompt\":\"x\",\"stream\":false}", Encoding.UTF8, "application/json")).ConfigureAwait(false))
                            {
                                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                Check((int)response.StatusCode == 429 && response.Headers.Contains("Retry-After") && body.Contains("\"error\"", StringComparison.Ordinal), "A full provider did not return a provider-shaped 429: " + body);
                            }

                            Check(!ollama.Requests.Any(item => item.Path == "/api/generate"), "The request reached the full provider.");
                        }
                        finally
                        {
                            held?.Dispose();
                            ProviderConcurrencyGate.Shared.Reset();
                        }
                    }
                })
            });
        }

        /// <summary>
        /// Request log, runtime status, routing API, and front ends for the Local endpoint page.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor VisibilitySuite()
        {
            const string suite = "HybridVisibility";
            return new TestSuiteDescriptor(suite, "Hybrid Visibility", new List<TestCaseDescriptor>
            {
                Case(suite, "RequestLogRecordsMetadataOnly", "The listener logs route, status, timing, and tokens, never content", async () =>
                {
                    const string sentinel = "LOG-SENTINEL-99ab";
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/api/generate"
                        ? FakeProviderResponse.Json("{\"response\":\"" + sentinel + "\",\"done\":true,\"prompt_eval_count\":11,\"eval_count\":22}")
                        : FakeProviderResponse.Json("{\"models\":[]}")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    {
                        LocalInferenceRequestLog.Clear();
                        using (HttpResponseMessage response = await listener.Http.PostAsync(listener.Url("/api/generate"), new StringContent("{\"model\":\"llama3.1:8b\",\"prompt\":\"" + sentinel + "\",\"stream\":false}", Encoding.UTF8, "application/json")).ConfigureAwait(false))
                        {
                            Check((int)response.StatusCode == 200, "Local request failed.");
                        }

                        LocalInferenceRequestRecord record = LocalInferenceRequestLog.Read(0, 10).Single();
                        Check(record.Model == "llama3.1:8b" && record.Route == "local:ollama" && record.StatusCode == 200 && record.Reason == "preferLocal", "Record metadata is wrong.");
                        Check(record.PromptTokens == 11 && record.CompletionTokens == 22 && record.LatencyMs > 0 && record.FirstByteMs.HasValue, "Record usage or timing is missing.");
                        Check(!File.ReadAllText(LocalInferenceRequestLog.FilePath).Contains(sentinel, StringComparison.Ordinal), "Content was written to the request log.");
                    }
                }),
                Case(suite, "RequestLogHonorsRetention", "The request log keeps at most the configured number of records", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        for (int i = 0; i < 200; i++) LocalInferenceRequestLog.Append(new LocalInferenceRequestRecord { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Model = "m" }, 60);
                        Check(LocalInferenceRequestLog.Count() == 60, "Retention was not applied: " + LocalInferenceRequestLog.Count());
                        Check(LocalInferenceRequestLog.Read(0, 1).Single().Id == "199", "Newest record is not first.");
                    }

                    return Task.CompletedTask;
                }),
                Case(suite, "RuntimeStatusPublishesCounters", "The listener publishes provider health and request counters for other processes", async () =>
                {
                    using (FakeProviderServer ollama = new FakeProviderServer(request => request.Path == "/v1/chat/completions" ? FakeProviderResponse.Json("{}") : FakeProviderResponse.Json("{\"models\":[{\"name\":\"llama3.1:8b\"}]}")))
                    using (ListenerRoutingHarness listener = new ListenerRoutingHarness(settings => settings.Providers.Add(ListenerRoutingHarness.Provider("ollama", "Ollama", ollama.BaseUrl, "llama3.1:8b"))))
                    {
                        await listener.Service.Backends.CheckNowAsync(CancellationToken.None).ConfigureAwait(false);
                        using (HttpResponseMessage local = await listener.Http.PostAsync(listener.Url("/v1/chat/completions"), new StringContent("{\"model\":\"llama3.1:8b\",\"messages\":[]}", Encoding.UTF8, "application/json")).ConfigureAwait(false)) { }
                        using (HttpResponseMessage network = await listener.Http.PostAsync(listener.Url("/v1/chat/completions"), new StringContent("{\"model\":\"remote\",\"messages\":[]}", Encoding.UTF8, "application/json")).ConfigureAwait(false)) { }
                        LocalInferenceRuntimeStatus? status = null;
                        for (int i = 0; i < 60 && (status == null || status.LocalRequests < 1 || status.NetworkRequests < 1); i++)
                        {
                            await Task.Delay(100).ConfigureAwait(false);
                            status = LocalInferenceRuntimeStatus.Load();
                        }

                        Check(status != null && status.LocalRequests == 1 && status.NetworkRequests == 1, "Counters were not published.");
                        Check(status!.Backends.Single().Healthy == true && status.Concurrency.Single().Limit == 4, "Health or concurrency was not published.");
                        LocalInferenceRoutingView view = LocalInferenceRoutingService.BuildView(listener.Settings, status);
                        Check(view.RuntimeAvailable && view.HealthyBackends == 1 && view.Backends.Single().Health == "healthy", "Routing view did not use the published status.");
                    }

                    Check(LocalInferenceRuntimeStatus.Load() == null, "The status file was not removed when the listener stopped.");
                }),
                Case(suite, "RoutingServiceSavesAndDeletesRules", "Saving a rule replaces any rule for the same model, renames, and validates", () =>
                {
                    ClientSettings settings = new ClientSettings { Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:11434", "llama3.1:8b") } };
                    LocalInferenceRoutingService.SaveRule(settings, new LocalInferenceRouteRule { Model = "llama3.1", Target = LocalInferenceRouteTargetEnum.Local, Provider = "ollama" });
                    LocalInferenceRoutingService.SaveRule(settings, new LocalInferenceRouteRule { Model = "llama3.1:latest", Target = LocalInferenceRouteTargetEnum.Network, Provider = "ollama" });
                    Check(settings.LocalInferenceServer.Routes.Count == 1 && settings.LocalInferenceServer.Routes[0].Target == LocalInferenceRouteTargetEnum.Network && settings.LocalInferenceServer.Routes[0].Provider == null, "Saving did not replace the rule or clear the provider for a network target.");
                    LocalInferenceRoutingService.SaveRule(settings, new LocalInferenceRouteRule { Model = "qwen3", Target = LocalInferenceRouteTargetEnum.Network }, "llama3.1:latest");
                    Check(settings.LocalInferenceServer.Routes.Single().Model == "qwen3", "Renaming did not remove the old rule.");
                    ExpectThrows<ArgumentException>(() => LocalInferenceRoutingService.SaveRule(settings, new LocalInferenceRouteRule { Model = "x", Target = LocalInferenceRouteTargetEnum.Local, Provider = "missing" }));
                    Check(settings.LocalInferenceServer.Routes.Count == 1, "An invalid rule changed the settings.");
                    Check(LocalInferenceRoutingService.DeleteRule(settings, "qwen3:latest") == 1 && settings.LocalInferenceServer.Routes.Count == 0, "Deleting by normalized name failed.");
                    string markdown = LocalInferenceRoutingService.ExplainMarkdown(LocalInferenceRoutingService.Resolve(settings, null, "llama3.1:8b"));
                    Check(markdown.Contains("local:ollama", StringComparison.Ordinal), "Explanation did not name the route.");
                    return Task.CompletedTask;
                }),
                Case(suite, "DashboardRoutingApiManagesRules", "The local dashboard API lists routing, resolves models, saves and deletes rules, and pages requests", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        int port = FakeProviderServer.GetFreePort();
                        ClientSettings settings = new ClientSettings { ClientId = "xcl_routing_api", Name = "routing-api", Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:" + FakeProviderServer.GetFreePort(), "llama3.1:8b") } };
                        settings.LocalDashboard.Enabled = true;
                        settings.LocalDashboard.Host = "127.0.0.1";
                        settings.LocalDashboard.Port = port;
                        settings.LocalDashboard.RequireLocalToken = false;
                        settings.LocalDashboard.Database.Filename = Path.Combine(home.Directory, "local-dashboard.db");
                        settings.Save();
                        LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
                        await driver.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        LocalInferenceRequestLog.Append(new LocalInferenceRequestRecord { Id = "r1", Model = "llama3.1:8b", Route = "local:ollama", StatusCode = 200 });
                        using (LocalDashboardService service = new LocalDashboardService(settings, driver.RequestHistory, driver.TelemetryHistory, null, driver.ProviderTelemetryHistory, null))
                        using (HttpClient http = new HttpClient())
                        {
                            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                            string root = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            LocalInferenceRoutingView? view = JsonUtil.Deserialize<LocalInferenceRoutingView>(await http.GetStringAsync(root + "/api/v1/local-inference/routing").ConfigureAwait(false));
                            Check(view != null && view.Backends.Single().Provider == "ollama" && view.RoutingMode == "PreferLocal", "Routing view is wrong.");

                            string resolved = await http.GetStringAsync(root + "/api/v1/local-inference/resolve?model=llama3.1:8b").ConfigureAwait(false);
                            LocalRouteResolveResponse? decision = JsonUtil.Deserialize<LocalRouteResolveResponse>(resolved);
                            Check(decision != null && decision.Route == "local:ollama" && decision.Reason == "preferLocal", "Resolve is wrong: " + resolved);
                            Check(!resolved.Contains("Credential", StringComparison.OrdinalIgnoreCase) && !resolved.Contains("Endpoint", StringComparison.Ordinal), "Resolve leaked provider settings.");

                            using (HttpResponseMessage missing = await http.GetAsync(root + "/api/v1/local-inference/resolve").ConfigureAwait(false))
                            {
                                Check((int)missing.StatusCode == 400, "Resolve without a model was not a 400.");
                            }

                            using (HttpResponseMessage saved = await http.PutAsync(root + "/api/v1/local-inference/routes", new StringContent("{\"model\":\"qwen3\",\"target\":\"Network\"}", Encoding.UTF8, "application/json")).ConfigureAwait(false))
                            {
                                Check((int)saved.StatusCode == 200 && ClientSettings.Load().LocalInferenceServer.Routes.Single().Model == "qwen3", "Saving a rule did not persist it.");
                            }

                            using (HttpResponseMessage invalid = await http.PutAsync(root + "/api/v1/local-inference/routes", new StringContent("{\"model\":\"x\",\"target\":\"Local\",\"provider\":\"nope\"}", Encoding.UTF8, "application/json")).ConfigureAwait(false))
                            {
                                Check((int)invalid.StatusCode == 400, "An invalid rule was not a 400.");
                            }

                            using (HttpResponseMessage deleted = await http.DeleteAsync(root + "/api/v1/local-inference/routes/qwen3").ConfigureAwait(false))
                            {
                                Check((int)deleted.StatusCode == 200 && ClientSettings.Load().LocalInferenceServer.Routes.Count == 0, "Deleting a rule did not persist.");
                            }

                            using (HttpResponseMessage gone = await http.DeleteAsync(root + "/api/v1/local-inference/routes/qwen3").ConfigureAwait(false))
                            {
                                Check((int)gone.StatusCode == 404, "Deleting a missing rule was not a 404.");
                            }

                            LocalInferenceRequestPage? page = JsonUtil.Deserialize<LocalInferenceRequestPage>(await http.GetStringAsync(root + "/api/v1/local-inference/requests?page=1&pageSize=10").ConfigureAwait(false));
                            Check(page != null && page.Total == 1 && page.Items.Single().Id == "r1", "Request page is wrong.");

                            string check = await (await http.PostAsync(root + "/api/v1/local-inference/backends/check", new StringContent("{}")).ConfigureAwait(false)).Content.ReadAsStringAsync().ConfigureAwait(false);
                            LocalBackendCheckResponse? checkedBackends = JsonUtil.Deserialize<LocalBackendCheckResponse>(check);
                            Check(checkedBackends != null && checkedBackends.Backends.Single().Healthy == false, "Checking a dead provider did not report it unhealthy.");
                        }
                    }
                }),
                Case(suite, "RouteRuleFormValidates", "The shared route rule form saves valid rules and reports invalid ones", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        ClientSettings settings = new ClientSettings { Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("ollama", "Ollama", "http://127.0.0.1:11434", "llama3.1:8b") } };
                        settings.Save();
                        XenoWorkspace workspace = new XenoWorkspace();
                        XenoFormSpec form = XenoForms.RouteRule(workspace, null, "llama3.1:8b");
                        Check(form.Fields.Single(item => item.Key == "target").Choices.SequenceEqual(new[] { "ollama", XenoForms.NetworkTargetChoice }), "Targets are wrong.");
                        Dictionary<string, string> values = form.Fields.ToDictionary(item => item.Key, item => item.Value ?? String.Empty);
                        values["fallback"] = "true";
                        Check(await form.SubmitAsync(values, CancellationToken.None).ConfigureAwait(false) == null, "A valid rule was refused.");
                        LocalInferenceRouteRule saved = ClientSettings.Load().LocalInferenceServer.Routes.Single();
                        Check(saved.Provider == "ollama" && saved.FallbackToNetwork, "The rule was not saved as entered.");
                        values["model"] = String.Empty;
                        Check(await form.SubmitAsync(values, CancellationToken.None).ConfigureAwait(false) != null, "A rule with no model was accepted.");
                        XenoFormSpec edit = XenoForms.RouteRule(workspace, saved);
                        Dictionary<string, string> editValues = edit.Fields.ToDictionary(item => item.Key, item => item.Value ?? String.Empty);
                        editValues["target"] = XenoForms.NetworkTargetChoice;
                        Check(await edit.SubmitAsync(editValues, CancellationToken.None).ConfigureAwait(false) == null, "Editing failed.");
                        LocalInferenceRouteRule edited = ClientSettings.Load().LocalInferenceServer.Routes.Single();
                        Check(edited.Target == LocalInferenceRouteTargetEnum.Network && !edited.FallbackToNetwork, "A network rule kept its fallback.");
                    }
                }),
                Case(suite, "NavigationIncludesLocalEndpoint", "The Local endpoint page is in the shared navigation, the tray menu, and desktop launch options", () =>
                {
                    XenoPageDefinition page = XenoNavigationCatalog.Get(XenoPageEnum.LocalEndpoint);
                    Check(page.Command == XenoCommandEnum.GoLocalEndpoint && page.Section == "Models", "Navigation entry is wrong.");
                    Check(XenoNavigationCatalog.PageFor(XenoCommandEnum.GoLocalEndpoint) == XenoPageEnum.LocalEndpoint, "Go command does not map to the page.");
                    Check(XenoMenuLayout.Commands(XenoMenuLayout.TrayMenu()).Contains(XenoCommandEnum.GoLocalEndpoint), "The tray menu has no Open Local Endpoint item.");
                    Xeno.Client.Hosting.XenoLaunchOptions options = Xeno.Client.Hosting.XenoLaunchOptions.Parse(new[] { "--page", "localendpoint" });
                    Check(options.StartPage == XenoPageEnum.LocalEndpoint, "--page localendpoint was not accepted.");
                    return Task.CompletedTask;
                }),
                Case(suite, "LocalEndpointChartSplitsRoutes", "The traffic chart separates local, network, and fallback requests by bucket", () =>
                {
                    DateTime now = new DateTime(2026, 11, 9, 12, 2, 0, DateTimeKind.Utc);
                    List<LocalInferenceRequestRecord> records = new List<LocalInferenceRequestRecord>
                    {
                        new LocalInferenceRequestRecord { ReceivedUtc = now.AddMinutes(-1), Route = "local:ollama" },
                        new LocalInferenceRequestRecord { ReceivedUtc = now.AddMinutes(-1), Route = "network" },
                        new LocalInferenceRequestRecord { ReceivedUtc = now.AddMinutes(-1), Route = "network", Fallback = true },
                        new LocalInferenceRequestRecord { ReceivedUtc = now.AddHours(-3), Route = "network" }
                    };
                    XenoChart chart = XenoCharts.LocalEndpointTraffic(records, now);
                    Check(chart.HasData && chart.Labels.Count == 12 && chart.Series.Count == 3, "Chart shape is wrong.");
                    Check(chart.Series[0].Values.Sum() == 1 && chart.Series[1].Values.Sum() == 1 && chart.Series[2].Values.Sum() == 1, "Requests were not split by route, or an old request was counted.");
                    Check(!XenoCharts.LocalEndpointTraffic(new List<LocalInferenceRequestRecord>(), now).HasData, "An empty chart claims data.");
                    return Task.CompletedTask;
                }),
                Case(suite, "TrayShowsBackendHealth", "The tray status line adds provider health when the listener runs", () =>
                {
                    ClientStatusSnapshot status = new ClientStatusSnapshot { StatusText = "Connected", LocalInferenceEnabled = true, LocalInferenceListening = true, LocalInferenceUrl = "http://127.0.0.1:11435", LocalInferenceRoutableBackends = 2, LocalInferenceHealthyBackends = 1 };
                    Check(XenoCommandAvailability.TrayStatusLine(status) == "Xeno: Connected - local endpoint 127.0.0.1:11435, 1 of 2 providers healthy", "Tray line is wrong: " + XenoCommandAvailability.TrayStatusLine(status));
                    return Task.CompletedTask;
                })
            });
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string suite, string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: async delegate
            {
                await body().ConfigureAwait(false);
            });
        }

        private static ClientSettings Limited(int providerLimit, int? bigModelLimit)
        {
            ClientRunnerSettings provider = ListenerRoutingHarness.Provider("p", "Ollama", "http://127.0.0.1:11434", "big:latest", "small:latest");
            provider.MaxConcurrentRequests = providerLimit;
            provider.Models[0].MaxConcurrentRequests = bigModelLimit;
            return new ClientSettings { Providers = new List<ClientRunnerSettings> { provider } };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T ExpectThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }

        #endregion
    }
}
