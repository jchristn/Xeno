namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Core.Models;

    /// <summary>
    /// Suites for hybrid model routing: listener port migration, routing settings, provider sharing flags, and concurrency limits.
    /// </summary>
    public static class HybridRoutingTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Routing settings, migration, validation, and concurrency limit resolution.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor RoutingSettingsSuite()
        {
            const string suite = "HybridRoutingSettings";
            return new TestSuiteDescriptor(suite, "Hybrid Routing Settings", new List<TestCaseDescriptor>
            {
                Case(suite, "ClientSettingsMigratesLegacyLocalInferencePort", "A stored listener port of 11434 moves to 11435 once and leaves a notice", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        File.WriteAllText(ClientSettings.SettingsFile, "{\"Name\":\"migrate\",\"LocalInferenceServer\":{\"Enabled\":true,\"Host\":\"127.0.0.1\",\"Port\":11434},\"Providers\":[]}");
                        ClientSettings settings = ClientSettings.Load();
                        Check(settings.LocalInferenceServer.Port == 11435, "Legacy port was not migrated.");
                        Check(settings.LocalInferencePortMigratedFrom == 11434, "Migration was not reported on the loaded settings.");
                        Check(File.ReadAllText(ClientSettings.SettingsFile).Contains("\"Port\": 11435", StringComparison.Ordinal), "Migrated port was not written back to client.json.");
                        ClientConnectionState state = ClientConnectionState.Load();
                        Check(state.LocalInferencePortMigratedFrom == 11434 && state.LocalInferencePortMigratedTo == 11435, "Migration notice was not recorded in client state.");
                        ClientStatusSnapshot status = ClientStatusSnapshot.From(ClientSettings.Load(), ClientConnectionState.Load());
                        Check(status.LocalInferencePortNotice.Contains("11434", StringComparison.Ordinal) && status.LocalInferencePortNotice.Contains("11435", StringComparison.Ordinal), "Status snapshot did not carry the notice.");
                        ClientConnectionState.AcknowledgeLocalInferencePortMigration();
                        Check(!ClientConnectionState.Load().LocalInferencePortMigratedFrom.HasValue, "Acknowledging did not clear the notice.");
                        ClientSettings reloaded = ClientSettings.Load();
                        Check(!reloaded.LocalInferencePortMigratedFrom.HasValue, "A second load migrated again.");
                        Check(!ClientConnectionState.Load().LocalInferencePortMigratedFrom.HasValue, "A second load recorded a new notice.");
                    }
                }),
                Case(suite, "ClientSettingsKeepsCustomLocalInferencePort", "A custom listener port is never migrated", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        File.WriteAllText(ClientSettings.SettingsFile, "{\"Name\":\"custom\",\"LocalInferenceServer\":{\"Enabled\":true,\"Host\":\"127.0.0.1\",\"Port\":11600},\"Providers\":[]}");
                        ClientSettings settings = ClientSettings.Load();
                        Check(settings.LocalInferenceServer.Port == 11600, "Custom port changed.");
                        Check(!settings.LocalInferencePortMigratedFrom.HasValue, "Custom port reported a migration.");
                        Check(!ClientConnectionState.Load().LocalInferencePortMigratedFrom.HasValue, "Custom port recorded a notice.");
                    }
                }),
                Case(suite, "ClientSettingsBackfillsRoutingDefaults", "Missing routing settings are backfilled and written back", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        File.WriteAllText(ClientSettings.SettingsFile, "{\"Name\":\"backfill\",\"LocalInferenceServer\":{\"Enabled\":false,\"Host\":\"127.0.0.1\",\"Port\":11435,\"RequestTimeoutMs\":300000},\"Providers\":[]}");
                        ClientSettings settings = ClientSettings.Load();
                        Check(settings.LocalInferenceServer.RoutingMode == LocalInferenceRoutingModeEnum.PreferLocal, "Routing mode default is wrong.");
                        Check(settings.LocalInferenceServer.Routes != null && settings.LocalInferenceServer.Routes.Count == 0, "Routes default is wrong.");
                        Check(settings.LocalInferenceServer.DiscoverLocalModels, "Discovery default is wrong.");
                        Check(settings.LocalInferenceServer.HealthCheckIntervalMs == 15000, "Health interval default is wrong.");
                        Check(settings.LocalInferenceServer.LocalQueueTimeoutMs == 30000, "Queue timeout default is wrong.");
                        Check(settings.DefaultMaxConcurrentRequests == 4, "Global concurrency default is wrong.");
                        string written = File.ReadAllText(ClientSettings.SettingsFile);
                        Check(written.Contains("RoutingMode", StringComparison.Ordinal) && written.Contains("PreferLocal", StringComparison.Ordinal), "Routing mode was not written back.");
                        Check(written.Contains("DefaultMaxConcurrentRequests", StringComparison.Ordinal), "Global concurrency limit was not written back.");
                        Check(written.Contains("LocalQueueTimeoutMs", StringComparison.Ordinal), "Queue timeout was not written back.");
                    }
                }),
                Case(suite, "ClientSettingsBackfillsProviderRoutingFlags", "Existing providers are shared, routable, and inherit the global limit", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        File.WriteAllText(ClientSettings.SettingsFile, "{\"Name\":\"flags\",\"Providers\":[{\"Name\":\"ollama\",\"ApiType\":\"Ollama\",\"Endpoint\":\"http://127.0.0.1:11434\",\"AllowModelManagement\":false,\"Models\":[{\"Name\":\"llama3.2:latest\"}]}]}");
                        ClientSettings settings = ClientSettings.Load();
                        ClientRunnerSettings provider = settings.Providers.Single();
                        Check(provider.ShareWithNetwork, "Existing provider must stay shared with the network.");
                        Check(provider.RouteLocalRequests, "Existing provider must be routable locally.");
                        Check(!provider.MaxConcurrentRequests.HasValue, "Existing provider must inherit the global limit.");
                        Check(!provider.Models.Single().MaxConcurrentRequests.HasValue, "Existing model must inherit the provider limit.");
                        string written = File.ReadAllText(ClientSettings.SettingsFile);
                        Check(written.Contains("ShareWithNetwork", StringComparison.Ordinal) && written.Contains("RouteLocalRequests", StringComparison.Ordinal), "Provider flags were not written back.");
                    }
                }),
                Case(suite, "ConcurrencySettingsClampToRange", "Global, provider, and model limits clamp to 1 through 256", () =>
                {
                    ClientSettings settings = EmptySettings();
                    settings.DefaultMaxConcurrentRequests = 0;
                    Check(settings.DefaultMaxConcurrentRequests == 1, "Global limit did not clamp up.");
                    settings.DefaultMaxConcurrentRequests = 1000;
                    Check(settings.DefaultMaxConcurrentRequests == 256, "Global limit did not clamp down.");
                    ClientRunnerSettings provider = Provider("p", "Ollama", "http://127.0.0.1:11434");
                    provider.MaxConcurrentRequests = -3;
                    Check(provider.MaxConcurrentRequests == 1, "Provider limit did not clamp.");
                    provider.MaxConcurrentRequests = null;
                    Check(!provider.MaxConcurrentRequests.HasValue, "Provider limit could not be cleared.");
                    ClientLocalInferenceServerSettings listener = new ClientLocalInferenceServerSettings { HealthCheckIntervalMs = 10, LocalQueueTimeoutMs = -1 };
                    Check(listener.HealthCheckIntervalMs == 1000 && listener.LocalQueueTimeoutMs == 0, "Listener intervals did not clamp.");
                    listener.Port = 0;
                    Check(listener.Port == 11435, "Out-of-range port did not reset to the new default.");
                }),
                Case(suite, "LimitsResolveModelThenProviderThenGlobal", "Concurrency limits resolve from the model, then the provider, then the global default", () =>
                {
                    ClientSettings settings = EmptySettings();
                    settings.DefaultMaxConcurrentRequests = 6;
                    ClientRunnerSettings provider = Provider("p", "Ollama", "http://127.0.0.1:11434");
                    provider.Models.Add(new ClientModelSettings { Name = "big:latest", MaxConcurrentRequests = 2 });
                    provider.Models.Add(new ClientModelSettings { Name = "small:latest" });
                    settings.Providers.Add(provider);

                    ConcurrencyLimits inherited = ConcurrencyLimitResolver.Resolve(settings, provider, "small");
                    Check(inherited.ProviderLimit == 6 && inherited.ProviderLimitSource == "global" && !inherited.ModelLimit.HasValue && inherited.EffectiveModelLimit == 6, "Global default was not inherited.");

                    ConcurrencyLimits modelLimited = ConcurrencyLimitResolver.Resolve(settings, provider, "big");
                    Check(modelLimited.ModelLimit == 2 && modelLimited.EffectiveModelLimit == 2, "Model limit was not applied (name normalization included).");

                    provider.MaxConcurrentRequests = 1;
                    ConcurrencyLimits capped = ConcurrencyLimitResolver.Resolve(settings, provider, "big:latest");
                    Check(capped.ProviderLimitSource == "provider" && capped.EffectiveModelLimit == 1 && capped.ModelLimitExceedsProvider, "Provider limit did not cap the model limit.");

                    ClientRunnerSettings localOnly = Provider("cloud", "OpenAICompatible", "https://ollama.com");
                    localOnly.ShareWithNetwork = false;
                    localOnly.MaxConcurrentRequests = 10;
                    settings.Providers.Add(localOnly);
                    Check(ConcurrencyLimitResolver.NetworkCapacity(settings) == 1, "Network capacity must count only shared providers.");
                }),
                Case(suite, "ModelNamesNormalizeLikeOllama", "Model names compare equal with and without the latest tag", () =>
                {
                    Check(LocalModelNameNormalizer.Normalize("llama3.1") == "llama3.1:latest", "Untagged name was not normalized.");
                    Check(LocalModelNameNormalizer.Normalize(" llama3.1:8b ") == "llama3.1:8b", "Tagged name changed.");
                    Check(LocalModelNameNormalizer.Normalize("hf.co/org/model") == "hf.co/org/model:latest", "Path name was not normalized.");
                    Check(LocalModelNameNormalizer.Normalize("hf.co/org/model:Q4_K_M") == "hf.co/org/model:Q4_K_M", "Path name with tag changed.");
                    Check(LocalModelNameNormalizer.Normalize("model:") == "model:latest", "Trailing colon was not normalized.");
                    Check(LocalModelNameNormalizer.Normalize(null) == String.Empty, "Null did not normalize to empty.");
                    Check(LocalModelNameNormalizer.AreEqual("Llama3.1", "llama3.1:latest"), "Equal names compared unequal.");
                    Check(!LocalModelNameNormalizer.AreEqual("llama3.1:8b", "llama3.1:70b"), "Different tags compared equal.");
                    Check(!LocalModelNameNormalizer.AreEqual(String.Empty, String.Empty), "Empty names must not compare equal.");
                }),
                Case(suite, "RouteRuleValidationRejectsUnknownProvider", "Route rules must name a configured provider for local targets", () =>
                {
                    List<ClientRunnerSettings> providers = new List<ClientRunnerSettings> { Provider("ollama", "Ollama", "http://127.0.0.1:11434") };
                    LocalInferenceRouteValidator.Validate(new List<LocalInferenceRouteRule>
                    {
                        new LocalInferenceRouteRule { Model = "llama3.1", Target = LocalInferenceRouteTargetEnum.Local, Provider = "OLLAMA" },
                        new LocalInferenceRouteRule { Model = "gpt-oss:120b", Target = LocalInferenceRouteTargetEnum.Network, FallbackToNetwork = true }
                    }, providers);
                    ArgumentException unknown = ExpectThrows<ArgumentException>(() => LocalInferenceRouteValidator.Validate(new List<LocalInferenceRouteRule>
                    {
                        new LocalInferenceRouteRule { Model = "llama3.1", Target = LocalInferenceRouteTargetEnum.Local, Provider = "missing" }
                    }, providers));
                    Check(unknown.ParamName == "Routes[0].Provider", "Unknown provider error did not name the field.");
                    ArgumentException missing = ExpectThrows<ArgumentException>(() => LocalInferenceRouteValidator.Validate(new List<LocalInferenceRouteRule>
                    {
                        new LocalInferenceRouteRule { Model = "llama3.1", Target = LocalInferenceRouteTargetEnum.Local }
                    }, providers));
                    Check(missing.ParamName == "Routes[0].Provider", "Missing provider error did not name the field.");
                    ArgumentException noModel = ExpectThrows<ArgumentException>(() => LocalInferenceRouteValidator.Validate(new List<LocalInferenceRouteRule>
                    {
                        new LocalInferenceRouteRule { Model = "  ", Target = LocalInferenceRouteTargetEnum.Network }
                    }, providers));
                    Check(noModel.ParamName == "Routes[0].Model", "Missing model error did not name the field.");
                }),
                Case(suite, "RouteRuleValidationRejectsDuplicateModels", "Two rules for the same model are rejected, even with different spellings", () =>
                {
                    ArgumentException duplicate = ExpectThrows<ArgumentException>(() => LocalInferenceRouteValidator.Validate(new List<LocalInferenceRouteRule>
                    {
                        new LocalInferenceRouteRule { Model = "llama3.1", Target = LocalInferenceRouteTargetEnum.Network },
                        new LocalInferenceRouteRule { Model = "llama3.1:latest", Target = LocalInferenceRouteTargetEnum.Network }
                    }, new List<ClientRunnerSettings>()));
                    Check(duplicate.ParamName == "Routes[1].Model", "Duplicate error did not name the second rule.");
                }),
                Case(suite, "SettingsEditorValidatesRoutesAndGlobalLimit", "Saving settings validates route rules and the global limit", () =>
                {
                    ClientSettings settings = EmptySettings();
                    settings.Providers.Add(Provider("ollama", "Ollama", "http://127.0.0.1:11434"));
                    LocalClientSettingsUpdateRequest request = LocalSettingsEditor.FromSettings(settings);
                    request.LocalInferenceServer.Routes = new List<LocalInferenceRouteRule> { new LocalInferenceRouteRule { Model = "x", Target = LocalInferenceRouteTargetEnum.Local, Provider = "nope" } };
                    ExpectThrows<ArgumentException>(() => LocalSettingsEditor.Apply(settings, request));
                    request.LocalInferenceServer.Routes = new List<LocalInferenceRouteRule>();

                    request = LocalSettingsEditor.FromSettings(settings);
                    request.DefaultMaxConcurrentRequests = 999;
                    ExpectThrows<ArgumentException>(() => LocalSettingsEditor.Apply(settings, request));

                    request = LocalSettingsEditor.FromSettings(settings);
                    request.DefaultMaxConcurrentRequests = 9;
                    request.LocalInferenceServer.RoutingMode = LocalInferenceRoutingModeEnum.LocalOnly;
                    LocalSettingsEditor.Apply(settings, request);
                    Check(settings.DefaultMaxConcurrentRequests == 9, "Global limit was not applied.");
                    Check(settings.LocalInferenceServer.RoutingMode == LocalInferenceRoutingModeEnum.LocalOnly, "Routing mode was not applied.");
                }),
                Case(suite, "WorkerDoesNotAnnounceLocalOnlyProviders", "Local-only providers stay out of the network inventory", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ClientRunnerSettings shared = Provider("shared", "Ollama", "http://127.0.0.1:11434");
                    shared.Models.Add(new ClientModelSettings { Name = "llama3.2:latest" });
                    ClientRunnerSettings localOnly = Provider("cloud", "OpenAICompatible", "https://ollama.com");
                    localOnly.ShareWithNetwork = false;
                    localOnly.Models.Add(new ClientModelSettings { Name = "gpt-oss:120b" });
                    settings.Providers.Add(shared);
                    settings.Providers.Add(localOnly);
                    ClientInventoryMessage inventory = settings.BuildInventoryMessage();
                    Check(inventory.Providers.Count == 1 && inventory.Providers[0].Name == "shared", "Local-only provider was announced to the network.");
                    string legacy = Xeno.Core.Serialization.JsonUtil.Serialize(settings.BuildInventory());
                    Check(!legacy.Contains("gpt-oss", StringComparison.Ordinal), "Local-only model leaked into the legacy inventory.");
                }),
                Case(suite, "ProviderUpdatesCarryRoutingFlagsAndLimits", "Provider and model updates set and clear routing flags and limits", () =>
                {
                    ClientSettings settings = EmptySettings();
                    ClientRunnerSettings provider = Provider("ollama", "Ollama", "http://127.0.0.1:11434");
                    provider.Models.Add(new ClientModelSettings { Name = "llama3.2:latest" });
                    settings.Providers.Add(provider);
                    ProviderConfiguration.UpdateProvider(settings, "ollama", new ProviderUpdateRequest { ShareWithNetwork = false, RouteLocalRequests = false, MaxConcurrentRequests = 3 });
                    Check(!provider.ShareWithNetwork && !provider.RouteLocalRequests && provider.MaxConcurrentRequests == 3, "Provider flags were not applied.");
                    ProviderConfiguration.UpdateProvider(settings, "ollama", new ProviderUpdateRequest { InheritMaxConcurrentRequests = true });
                    Check(!provider.MaxConcurrentRequests.HasValue && !provider.ShareWithNetwork, "Inherit cleared more than the limit.");
                    ExpectThrows<ArgumentException>(() => ProviderConfiguration.UpdateProvider(settings, "ollama", new ProviderUpdateRequest { MaxConcurrentRequests = 0 }));

                    ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Provider = "ollama", Name = "llama3.2:latest", MaxConcurrentRequests = 2 });
                    Check(provider.Models[0].MaxConcurrentRequests == 2, "Model limit was not applied.");
                    LocalModelEntry entry = ProviderConfiguration.BuildModelList(settings).Models.Single();
                    Check(entry.MaxConcurrentRequests == 2 && entry.EffectiveMaxConcurrentRequests == 2 && !entry.ShareWithNetwork, "Model entry did not report limits and sharing.");
                    ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Provider = "ollama", Name = "llama3.2:latest", InheritMaxConcurrentRequests = true });
                    Check(!provider.Models[0].MaxConcurrentRequests.HasValue, "Model limit was not cleared.");

                    ProviderConfiguration.UpsertProvider(settings, new LocalProviderUpsertRequest { Name = "ollama", Endpoint = "http://127.0.0.1:11434", ShareWithNetwork = true, MaxConcurrentRequests = 7 }, null);
                    Check(provider.ShareWithNetwork && provider.MaxConcurrentRequests == 7, "Dashboard upsert did not apply flags.");
                    ClientRunnerSettings redacted = ProviderConfiguration.Redact(settings).Providers.Single();
                    Check(redacted.MaxConcurrentRequests == 7 && redacted.ShareWithNetwork, "Redacted settings dropped routing fields.");
                }),
                Case(suite, "LocalInferencePortConflictNamesOllama", "The Ollama probe recognizes Ollama and ignores other listeners", () =>
                {
                    using (FakeOllamaPullServer ollama = new FakeOllamaPullServer())
                    {
                        string? version = OllamaPortProbe.DetectOllamaAsync(ollama.Port, 2000).GetAwaiter().GetResult();
                        Check(version == "0.0.0-test", "Ollama was not detected: " + (version ?? "(null)"));
                    }

                    string? nothing = OllamaPortProbe.DetectOllamaAsync(FreePort(), 500).GetAwaiter().GetResult();
                    Check(nothing == null, "A closed port was reported as Ollama.");
                    ExpectThrows<ArgumentOutOfRangeException>(() => OllamaPortProbe.DetectOllamaAsync(0).GetAwaiter().GetResult());
                })
            });
        }

        /// <summary>
        /// Router decisions: prefer-local, rules, normalization, health, modes, and API shape support.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor RouterSuite()
        {
            const string suite = "HybridRouter";
            return new TestSuiteDescriptor(suite, "Hybrid Router", new List<TestCaseDescriptor>
            {
                Case(suite, "RouterPrefersLocalProvider", "A model served on this machine routes to its provider", () =>
                {
                    ClientSettings settings = RouterSettings();
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.Target == LocalInferenceRouteTargetKindEnum.Local && decision.ProviderName == "ollama" && decision.Reason == "preferLocal", "Local model did not route locally: " + decision.RouteHeader + " " + decision.Reason);
                    Check(decision.RouteHeader == "local:ollama" && !decision.FallbackToNetwork, "Route header or fallback is wrong.");
                    LocalInferenceRouteDecision remote = LocalInferenceRouter.Resolve(settings, null, "gpt-oss:120b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(remote.Target == LocalInferenceRouteTargetKindEnum.Network && remote.Reason == "notLocal" && remote.RouteHeader == "network", "Unknown model did not route to the network.");
                }),
                Case(suite, "RouterHonorsNetworkRule", "A network rule overrides a local provider", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "llama3.1:8b", Target = LocalInferenceRouteTargetEnum.Network });
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.Target == LocalInferenceRouteTargetKindEnum.Network && decision.Reason == "rule", "Network rule was ignored.");
                }),
                Case(suite, "RouterHonorsProviderRule", "A provider rule picks that provider even when another serves the model first", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.Providers[1].Models.Add(new ClientModelSettings { Name = "llama3.1:8b" });
                    settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "llama3.1:8b", Target = LocalInferenceRouteTargetEnum.Local, Provider = "vllm" });
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.ProviderName == "vllm" && decision.Reason == "rule", "Provider rule was ignored.");
                }),
                Case(suite, "RouterNormalizesLatestTag", "Untagged and :latest names route the same way", () =>
                {
                    ClientSettings settings = RouterSettings();
                    Check(LocalInferenceRouter.Resolve(settings, null, "qwen3", LocalInferenceApiShapeEnum.Ollama, false).ProviderName == "ollama", "Untagged name did not match the :latest model.");
                    Check(LocalInferenceRouter.Resolve(settings, null, "QWEN3:latest", LocalInferenceApiShapeEnum.Ollama, false).ProviderName == "ollama", "Case and tag did not normalize.");
                }),
                Case(suite, "RouterUsesProviderOrderAndHealth", "Duplicates go to the first healthy provider in configured order", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.Providers[1].Models.Add(new ClientModelSettings { Name = "llama3.1:8b" });
                    Check(LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false).ProviderName == "ollama", "Configured order was not respected.");
                    List<LocalBackendStatus> statuses = new List<LocalBackendStatus> { new LocalBackendStatus { Provider = "ollama", Healthy = false } };
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, statuses, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.ProviderName == "vllm" && decision.Reason == "preferLocal", "An unhealthy first provider was not skipped.");
                    statuses.Add(new LocalBackendStatus { Provider = "vllm", Healthy = false });
                    LocalInferenceRouteDecision allDown = LocalInferenceRouter.Resolve(settings, statuses, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(allDown.Target == LocalInferenceRouteTargetKindEnum.Local && allDown.Reason == "localUnhealthy" && !allDown.FallbackToNetwork, "All-unhealthy without a rule must stay local and not fall back.");
                }),
                Case(suite, "RouterPrefersConfiguredOverDiscovered", "A provider with the model configured beats an earlier provider that only reported it", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.Providers[1].Models.Add(new ClientModelSettings { Name = "gpt-oss:20b" });
                    List<LocalBackendStatus> statuses = new List<LocalBackendStatus> { new LocalBackendStatus { Provider = "ollama", Healthy = true, DiscoveredModels = new List<string> { "gpt-oss:20b" } } };
                    Check(LocalInferenceRouter.Resolve(settings, statuses, "gpt-oss:20b", LocalInferenceApiShapeEnum.OpenAi, false).ProviderName == "vllm", "A discovered match beat a configured one.");
                    LocalOpenAiModel listed = LocalModelListingBuilder.LocalOpenAiModels(settings, statuses).Single(item => item.Id == "gpt-oss:20b");
                    Check(listed.OwnedBy == "vllm", "The listing named the discovering provider instead of the configured one.");
                }),
                Case(suite, "RouterSkipsProvidersNotRoutable", "Providers with local requests turned off are never chosen", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.Providers[0].RouteLocalRequests = false;
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.Target == LocalInferenceRouteTargetKindEnum.Network, "A non-routable provider was chosen.");
                }),
                Case(suite, "RouterUsesDiscoveredModels", "Models a provider reports are routable only when discovery is on", () =>
                {
                    ClientSettings settings = RouterSettings();
                    List<LocalBackendStatus> statuses = new List<LocalBackendStatus> { new LocalBackendStatus { Provider = "ollama", Healthy = true, DiscoveredModels = new List<string> { "gpt-oss:120b-cloud" } } };
                    Check(LocalInferenceRouter.Resolve(settings, statuses, "gpt-oss:120b-cloud", LocalInferenceApiShapeEnum.Ollama, false).ProviderName == "ollama", "Discovered cloud model was not routed locally.");
                    settings.LocalInferenceServer.DiscoverLocalModels = false;
                    Check(LocalInferenceRouter.Resolve(settings, statuses, "gpt-oss:120b-cloud", LocalInferenceApiShapeEnum.Ollama, false).Target == LocalInferenceRouteTargetKindEnum.Network, "Discovery off still routed a discovered model locally.");
                }),
                Case(suite, "RouterRespectsEmbeddingSupport", "Embedding requests only go to providers whose model serves embeddings", () =>
                {
                    ClientSettings settings = RouterSettings();
                    Check(LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, true).Target == LocalInferenceRouteTargetKindEnum.Network, "A completions-only model took an embeddings request.");
                    Check(LocalInferenceRouter.Resolve(settings, null, "nomic-embed-text", LocalInferenceApiShapeEnum.OpenAi, true).ProviderName == "ollama", "An embeddings model was not routed locally.");
                }),
                Case(suite, "RouterNetworkOnlyMatchesLegacyBehavior", "NetworkOnly sends everything to the network", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.LocalInferenceServer.RoutingMode = LocalInferenceRoutingModeEnum.NetworkOnly;
                    settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "llama3.1:8b", Target = LocalInferenceRouteTargetEnum.Local, Provider = "ollama" });
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.Target == LocalInferenceRouteTargetKindEnum.Network && decision.Reason == "networkOnlyMode", "NetworkOnly did not override rules.");
                }),
                Case(suite, "RouterLocalOnlyNeverUsesTunnel", "LocalOnly never routes to the network, even for network rules or fallback", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.LocalInferenceServer.RoutingMode = LocalInferenceRoutingModeEnum.LocalOnly;
                    Check(LocalInferenceRouter.Resolve(settings, null, "gpt-oss:120b", LocalInferenceApiShapeEnum.OpenAi, false).Target == LocalInferenceRouteTargetKindEnum.Unavailable, "LocalOnly sent an unknown model to the network.");
                    settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "gpt-oss:120b", Target = LocalInferenceRouteTargetEnum.Network });
                    Check(LocalInferenceRouter.Resolve(settings, null, "gpt-oss:120b", LocalInferenceApiShapeEnum.OpenAi, false).Reason == "localOnlyMode", "LocalOnly honored a network rule.");
                    settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "llama3.1:8b", Target = LocalInferenceRouteTargetEnum.Local, Provider = "ollama", FallbackToNetwork = true });
                    Check(!LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false).FallbackToNetwork, "LocalOnly allowed network fallback.");
                }),
                Case(suite, "RouterFallsBackForUnhealthyRuleProvider", "A rule with fallback goes to the network when its provider is known to be down", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.LocalInferenceServer.Routes.Add(new LocalInferenceRouteRule { Model = "llama3.1:8b", Target = LocalInferenceRouteTargetEnum.Local, Provider = "ollama", FallbackToNetwork = true });
                    LocalInferenceRouteDecision healthy = LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(healthy.Target == LocalInferenceRouteTargetKindEnum.Local && healthy.FallbackToNetwork, "Rule with fallback did not route locally first.");
                    List<LocalBackendStatus> down = new List<LocalBackendStatus> { new LocalBackendStatus { Provider = "ollama", Healthy = false } };
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, down, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false);
                    Check(decision.Target == LocalInferenceRouteTargetKindEnum.Network && decision.Reason == "localUnhealthy", "Known-down provider with fallback did not go to the network.");
                    settings.LocalInferenceServer.Routes[0].Provider = "deleted";
                    Check(LocalInferenceRouter.Resolve(settings, null, "llama3.1:8b", LocalInferenceApiShapeEnum.OpenAi, false).Reason == "ruleProviderUnavailable", "A rule naming a missing provider was not reported.");
                }),
                Case(suite, "RouterReportsUnsupportedShape", "Ollama-shaped requests to OpenAI-only providers are flagged", () =>
                {
                    ClientSettings settings = RouterSettings();
                    LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, null, "mistral-small", LocalInferenceApiShapeEnum.Ollama, false);
                    Check(decision.ProviderName == "vllm" && !decision.ShapeSupported, "Shape support was not flagged.");
                    Check(LocalInferenceRouter.Resolve(settings, null, "mistral-small", LocalInferenceApiShapeEnum.OpenAi, false).ShapeSupported, "OpenAI shape must always be supported.");
                }),
                Case(suite, "CatalogMarksRouteTargets", "The model catalog records where each model routes, discovered models, and local-only providers", () =>
                {
                    ClientSettings settings = RouterSettings();
                    settings.Providers[1].ShareWithNetwork = false;
                    List<LocalBackendStatus> statuses = new List<LocalBackendStatus> { new LocalBackendStatus { Provider = "ollama", Healthy = true, DiscoveredModels = new List<string> { "gpt-oss:120b-cloud", "llama3.1:8b" } } };
                    ModelCatalog catalog = ModelCatalogBuilder.Merge(settings, new List<NetworkModelEntry> { new NetworkModelEntry { Id = "deepseek-r1:671b", OwnedBy = "network" }, new NetworkModelEntry { Id = "llama3.1:8b", OwnedBy = "network" } }, String.Empty, statuses);
                    ModelCatalogEntry local = catalog.Entries.Single(item => item.Name == "llama3.1:8b");
                    Check(local.RouteTarget == "local:ollama" && local.Availability == ModelAvailabilityEnum.Both && !local.Discovered, "Local model route is wrong.");
                    ModelCatalogEntry cloud = catalog.Entries.Single(item => item.Name == "gpt-oss:120b-cloud");
                    Check(cloud.Discovered && cloud.RouteTarget == "local:ollama" && cloud.LocalOnly, "Discovered model was not marked.");
                    Check(catalog.Entries.Single(item => item.Name == "deepseek-r1:671b").RouteTarget == "network", "Network model route is wrong.");
                    Check(catalog.Entries.Single(item => item.Name == "mistral-small").LocalOnly, "Model on a local-only provider was not marked.");
                    Check(catalog.Entries.Count(item => LocalModelNameNormalizer.AreEqual(item.Name, "llama3.1:8b")) == 1, "Discovered duplicate created a second entry.");
                })
            });
        }

        private static ClientSettings RouterSettings()
        {
            ClientSettings settings = EmptySettings();
            ClientRunnerSettings ollama = Provider("ollama", "Ollama", "http://127.0.0.1:11434");
            ollama.Models.Add(new ClientModelSettings { Name = "llama3.1:8b" });
            ollama.Models.Add(new ClientModelSettings { Name = "qwen3:latest" });
            ollama.Models.Add(new ClientModelSettings { Name = "nomic-embed-text:latest", SupportsCompletions = false, SupportsEmbeddings = true });
            ClientRunnerSettings vllm = Provider("vllm", "vLLM", "http://127.0.0.1:8000");
            vllm.Models.Add(new ClientModelSettings { Name = "mistral-small" });
            settings.Providers.Add(ollama);
            settings.Providers.Add(vllm);
            return settings;
        }

        /// <summary>
        /// Front-end surfaces for routing settings: settings catalog, forms, status, and tray.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor RoutingFrontEndSuite()
        {
            const string suite = "HybridRoutingFrontEnd";
            return new TestSuiteDescriptor(suite, "Hybrid Routing Front Ends", new List<TestCaseDescriptor>
            {
                Case(suite, "SettingsCatalogIncludesRoutingFields", "The shared settings catalog edits routing mode, discovery, health, queue, and the global limit", () =>
                {
                    string[] keys = { "inference.routingMode", "inference.discoverLocalModels", "inference.healthInterval", "inference.localQueueTimeout", "concurrency.defaultMax" };
                    foreach (string key in keys)
                    {
                        Check(XenoSettingsCatalog.Fields.Any(item => item.Key == key), "Settings catalog is missing " + key + ".");
                    }

                    XenoSettingField port = XenoSettingsCatalog.Fields.Single(item => item.Key == "inference.port");
                    Check(port.Help.Contains("11435", StringComparison.Ordinal), "Port help does not name the new default.");
                    Check(!XenoSettingsCatalog.Fields.Single(item => item.Key == "inference.routingMode").RequiresRestart, "Routing mode must apply without a restart.");

                    ClientSettings settings = EmptySettings();
                    LocalClientSettingsUpdateRequest request = LocalSettingsEditor.FromSettings(settings);
                    XenoSettingField routing = XenoSettingsCatalog.Fields.Single(item => item.Key == "inference.routingMode");
                    routing.Write(request, "networkonly");
                    Check(request.LocalInferenceServer.RoutingMode == LocalInferenceRoutingModeEnum.NetworkOnly, "Routing mode was not written by the catalog.");
                    XenoSettingField limit = XenoSettingsCatalog.Fields.Single(item => item.Key == "concurrency.defaultMax");
                    limit.Write(request, "12");
                    Check(request.DefaultMaxConcurrentRequests == 12, "Global limit was not written by the catalog.");
                }),
                Case(suite, "ProviderFormsIncludeRoutingFlags", "Provider and model forms carry sharing, local routing, and limit fields", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        ClientSettings settings = new ClientSettings { Providers = new List<ClientRunnerSettings> { Provider("ollama", "Ollama", "http://127.0.0.1:11434") } };
                        settings.Providers[0].Models.Add(new ClientModelSettings { Name = "llama3.2:latest" });
                        settings.Save();
                        XenoWorkspace workspace = new XenoWorkspace();
                        XenoFormSpec add = XenoForms.AddProvider(workspace);
                        foreach (string key in new[] { "shareWithNetwork", "routeLocalRequests", "maxConcurrent" })
                        {
                            Check(add.Fields.Any(item => item.Key == key), "Add Provider form is missing " + key + ".");
                        }

                        XenoFormSpec edit = XenoForms.EditProvider(workspace, settings.Providers[0]);
                        Dictionary<string, string> values = edit.Fields.ToDictionary(item => item.Key, item => item.Value ?? String.Empty);
                        values["shareWithNetwork"] = "false";
                        values["maxConcurrent"] = "3";
                        string? error = edit.SubmitAsync(values, default).GetAwaiter().GetResult();
                        Check(error == null, "Edit Provider failed: " + error);
                        ClientRunnerSettings saved = ClientSettings.Load().Providers.Single();
                        Check(!saved.ShareWithNetwork && saved.MaxConcurrentRequests == 3, "Edit Provider did not save routing fields.");

                        values["maxConcurrent"] = "abc";
                        Check(edit.SubmitAsync(values, default).GetAwaiter().GetResult() != null, "An invalid limit was accepted.");

                        XenoFormSpec? model = XenoForms.EditModel(workspace, "ollama", "llama3.2:latest");
                        Check(model != null && model.Fields.Any(item => item.Key == "maxConcurrent"), "Edit Model form has no limit field.");
                        Dictionary<string, string> modelValues = model!.Fields.ToDictionary(item => item.Key, item => item.Value ?? String.Empty);
                        modelValues["maxConcurrent"] = "2";
                        Check(model.SubmitAsync(modelValues, default).GetAwaiter().GetResult() == null, "Edit Model failed.");
                        Check(ClientSettings.Load().Providers.Single().Models.Single().MaxConcurrentRequests == 2, "Model limit was not saved.");
                    }
                }),
                Case(suite, "TrayShowsLocalEndpointUrl", "The tray status line names the local endpoint when the listener is enabled", () =>
                {
                    ClientStatusSnapshot disabled = new ClientStatusSnapshot { StatusText = "Connected", LocalInferenceEnabled = false, LocalInferenceUrl = "http://127.0.0.1:11435" };
                    Check(XenoCommandAvailability.TrayStatusLine(disabled) == "Xeno: Connected", "Disabled listener changed the status line.");
                    ClientStatusSnapshot listening = new ClientStatusSnapshot { StatusText = "Connected", LocalInferenceEnabled = true, LocalInferenceListening = true, LocalInferenceUrl = "http://127.0.0.1:11435/" };
                    Check(XenoCommandAvailability.TrayStatusLine(listening) == "Xeno: Connected - local endpoint 127.0.0.1:11435", "Listening status line is wrong: " + XenoCommandAvailability.TrayStatusLine(listening));
                    ClientStatusSnapshot stopped = new ClientStatusSnapshot { StatusText = "Stopped", LocalInferenceEnabled = true, LocalInferenceListening = false, LocalInferenceUrl = "http://127.0.0.1:11435" };
                    Check(XenoCommandAvailability.TrayStatusLine(stopped).EndsWith("(not running)", StringComparison.Ordinal), "Stopped listener was not marked.");
                    Check(XenoCommandAvailability.TrayStatusLine(null) == "Xeno: checking...", "Unknown status changed.");
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

        private static int FreePort()
        {
            System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion
    }
}
