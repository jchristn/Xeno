namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Provider model management: the pull coordinator, imports, the CLI's provider models / pull / delete-model and
    /// model import commands, and --columns on CLI tables.
    /// </summary>
    public static class ProviderModelCliTestSuites
    {
        private const string ProviderName = "local-ollama";

        private const string TagsBody = "{\"models\":["
            + "{\"name\":\"llama3:latest\",\"model\":\"llama3:latest\",\"modified_at\":\"2026-05-01T12:00:00Z\",\"size\":4661224676,\"digest\":\"365c0bd3c000a25d28ddbf732fe1c6add414de7275464c4e4d1c3b5fcb5d8ad1\",\"details\":{\"family\":\"llama\",\"parameter_size\":\"8.0B\",\"quantization_level\":\"Q4_0\"}},"
            + "{\"name\":\"qwen2.5-coder-with-a-really-long-descriptive-model-name:14b-instruct-q8_0\",\"model\":\"qwen2.5-coder\",\"modified_at\":\"2026-05-02T12:00:00Z\",\"size\":15700000000,\"digest\":\"abc\",\"details\":{\"family\":\"qwen2\",\"parameter_size\":\"14.8B\",\"quantization_level\":\"Q8_0\"}},"
            + "{\"name\":\"nomic-embed-text:latest\",\"model\":\"nomic-embed-text:latest\",\"modified_at\":\"2026-05-03T12:00:00Z\",\"size\":274302450,\"digest\":\"def\",\"details\":{\"family\":\"nomic-bert\",\"parameter_size\":\"137M\",\"quantization_level\":\"F16\"}}"
            + "]}";

        /// <summary>
        /// Provider model management suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor ProviderModelSuite()
        {
            const string suite = "ProviderModels";
            return new TestSuiteDescriptor(suite, "Provider model management and CLI tables", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "PullClientUsesWorker", "Front ends start, list, and cancel pulls through the running client worker", executeAsync: async delegate
                {
                    string job = "{\"Id\":\"job1\",\"Provider\":\"local-ollama\",\"Model\":\"qwen2.5:7b\",\"State\":\"Running\",\"Status\":\"pulling aaa\",\"CompletedBytes\":10,\"TotalBytes\":40,\"Percent\":25,\"StartedUtc\":\"2026-11-25T10:00:00Z\",\"UpdatedUtc\":\"2026-11-25T10:00:01Z\"}";
                    StubHttpHandler worker = new StubHttpHandler()
                        .On("/api/v1/providers/local-ollama/pulls", System.Net.HttpStatusCode.Accepted, job)
                        .On("/api/v1/model-pulls", System.Net.HttpStatusCode.OK, "{\"Jobs\":[" + job + "],\"Active\":1}")
                        .On("/api/v1/model-pulls/job1", System.Net.HttpStatusCode.OK, job.Replace("Running", "Cancelled"));
                    LocalApiEndpoint endpoint = new LocalApiEndpoint { Url = "http://127.0.0.1:9", Token = "secret" };
                    using (ModelPullCoordinator local = new ModelPullCoordinator())
                    using (XenoModelPullClient client = new XenoModelPullClient(() => endpoint, worker, local))
                    {
                        Check(client.RunsInWorker, "A published local API endpoint means pulls run in the worker.");
                        ModelPullJob started = await client.StartAsync("local-ollama", "qwen2.5:7b", true, CancellationToken.None).ConfigureAwait(false);
                        Check(started.Id == "job1" && started.Percent == 25, "Start should return the worker's job.");
                        Check(local.List().Count == 0, "Nothing should run in this process while the worker is up.");
                        HttpRequestMessage post = worker.Requests.First(item => item.Method == HttpMethod.Post);
                        Check(post.Headers.GetValues("X-Xeno-Local-Token").Single() == "secret", "Requests carry the local API token.");
                        Check(client.Snapshot().Any(item => item.Id == "job1"), "The snapshot should include the worker's job without a call.");
                        List<ModelPullJob> listed = await client.ListAsync(CancellationToken.None).ConfigureAwait(false);
                        Check(listed.Count == 1 && listed[0].Model == "qwen2.5:7b", "List should return the worker's jobs.");
                        Check(await client.CancelAsync("job1", CancellationToken.None).ConfigureAwait(false), "Cancel should reach the worker.");
                        Check(worker.Requests.Any(item => item.Method == HttpMethod.Delete && item.RequestUri!.AbsolutePath == "/api/v1/model-pulls/job1"), "Cancel should DELETE the job on the worker.");
                    }
                }),
                new TestCaseDescriptor(suite, "PullClientFallsBackWithoutWorker", "Without a running worker, pulls run in this process and say so", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (ModelPullCoordinator local = new ModelPullCoordinator())
                    using (XenoModelPullClient client = new XenoModelPullClient(() => null, new StubHttpHandler(), local))
                    {
                        Check(!client.RunsInWorker, "No endpoint means no worker.");
                        bool threw = false;
                        try
                        {
                            await client.StartAsync("no-such-provider", "x", false, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (KeyNotFoundException)
                        {
                            threw = true;
                        }

                        Check(threw, "Falling back to this process should still validate the provider.");
                        Check((await client.ListAsync(CancellationToken.None).ConfigureAwait(false)).Count == 0, "No pulls yet.");
                    }
                }),
                new TestCaseDescriptor(suite, "PullAggregatesLayerProgress", "A pull's progress sums every layer digest the provider reports", executeAsync: async delegate
                {
                    List<string> lines = new List<string>
                    {
                        "{\"status\":\"pulling manifest\"}\n",
                        "{\"status\":\"pulling aaa\",\"digest\":\"sha256:aaa\",\"total\":100,\"completed\":50}\n",
                        "{\"status\":\"pulling bbb\",\"digest\":\"sha256:bbb\",\"total\":300,\"completed\":0}\n",
                        "{\"status\":\"pulling aaa\",\"digest\":\"sha256:aaa\",\"total\":100,\"completed\":100}\n",
                        "{\"status\":\"pulling bbb\",\"digest\":\"sha256:bbb\",\"total\":300,\"completed\":300}\n",
                        "{\"status\":\"verifying sha256 digest\"}\n",
                        "{\"status\":\"success\"}\n"
                    };
                    using (FakeProviderServer server = new FakeProviderServer(request => PullResponse(request, lines, 0)))
                    using (ModelPullCoordinator pulls = new ModelPullCoordinator())
                    {
                        List<ModelPullJob> seen = new List<ModelPullJob>();
                        object gate = new object();
                        pulls.JobChanged += (sender, job) => { lock (gate) seen.Add(job); };
                        ModelPullJob started = pulls.Start(Ollama(server.BaseUrl), "llama3", false, null);
                        ModelPullJob? done = await pulls.WaitAsync(started.Id, TimeoutToken()).ConfigureAwait(false);
                        Check(done != null && done.State == ModelPullStateEnum.Succeeded, "The pull did not succeed: " + done?.State + " " + done?.Error);
                        Check(done!.TotalBytes == 400 && done.CompletedBytes == 400 && done.Percent == 100, "Final totals are wrong: " + done.CompletedBytes + "/" + done.TotalBytes);
                        lock (gate)
                        {
                            Check(seen.Any(item => item.TotalBytes == 400 && item.CompletedBytes == 50 && item.Percent == 12.5), "The two layers were not summed while pulling.");
                            Check(seen.Any(item => item.TotalBytes == 100 && item.Percent == 50), "The first layer's progress was not reported.");
                        }

                        Check(!done.Advertised, "A pull without advertise should not report advertised.");
                    }
                }),
                new TestCaseDescriptor(suite, "PullDedupesAndCancels", "Two pulls of the same model share one job, and cancelling ends it as cancelled", executeAsync: async delegate
                {
                    List<string> lines = Enumerable.Range(1, 40).Select(index => "{\"status\":\"pulling aaa\",\"digest\":\"sha256:aaa\",\"total\":4000,\"completed\":" + (index * 100) + "}\n").ToList();
                    lines.Add("{\"status\":\"success\"}\n");
                    using (FakeProviderServer server = new FakeProviderServer(request => PullResponse(request, lines, 200)))
                    using (ModelPullCoordinator pulls = new ModelPullCoordinator())
                    {
                        ModelPullJob first = pulls.Start(Ollama(server.BaseUrl), "llama3", false, null);
                        ModelPullJob second = pulls.Start(Ollama(server.BaseUrl), "llama3:latest", true, null);
                        Check(first.Id == second.Id, "A concurrent identical pull started a second job.");
                        Check(pulls.List().Count == 1, "Expected one job.");
                        await Task.Delay(300).ConfigureAwait(false);
                        Check(pulls.Cancel(first.Id), "Cancel did not find the active job.");
                        ModelPullJob? done = await pulls.WaitAsync(first.Id, TimeoutToken()).ConfigureAwait(false);
                        Check(done != null && done.State == ModelPullStateEnum.Cancelled, "The pull did not end cancelled: " + done?.State + " " + done?.Error);
                        Check(server.Requests.Count(item => item.Path == "/api/pull") == 1, "The provider saw more than one pull request.");
                        Check(!pulls.Cancel(first.Id), "A finished job cannot be cancelled again.");
                    }
                }),
                new TestCaseDescriptor(suite, "PullErrorLineFails", "An Ollama {\"error\"} line fails the pull with the provider's message", executeAsync: async delegate
                {
                    List<string> lines = new List<string> { "{\"status\":\"pulling manifest\"}\n", "{\"error\":\"pull model manifest: file does not exist\"}\n" };
                    bool called = false;
                    using (FakeProviderServer server = new FakeProviderServer(request => PullResponse(request, lines, 0)))
                    using (ModelPullCoordinator pulls = new ModelPullCoordinator())
                    {
                        ModelPullJob started = pulls.Start(Ollama(server.BaseUrl), "nope", true, (job, token) => { called = true; return Task.CompletedTask; });
                        ModelPullJob? done = await pulls.WaitAsync(started.Id, TimeoutToken()).ConfigureAwait(false);
                        Check(done != null && done.State == ModelPullStateEnum.Failed && (done.Error ?? String.Empty).Contains("file does not exist", StringComparison.Ordinal), "The error line did not fail the pull: " + done?.State + " " + done?.Error);
                        Check(!called && !done!.Advertised, "A failed pull must not advertise.");
                    }
                }),
                new TestCaseDescriptor(suite, "PullSuccessAdvertises", "A successful pull calls onSucceeded and reports the model advertised", executeAsync: async delegate
                {
                    List<string> lines = new List<string> { "{\"status\":\"pulling aaa\",\"digest\":\"sha256:aaa\",\"total\":10,\"completed\":10}\n", "{\"status\":\"success\"}\n" };
                    string? advertisedModel = null;
                    using (FakeProviderServer server = new FakeProviderServer(request => PullResponse(request, lines, 0)))
                    using (ModelPullCoordinator pulls = new ModelPullCoordinator())
                    {
                        ModelPullJob started = pulls.Start(Ollama(server.BaseUrl), "llama3", true, (job, token) => { advertisedModel = job.Model; return Task.CompletedTask; });
                        ModelPullJob? done = await pulls.WaitAsync(started.Id, TimeoutToken()).ConfigureAwait(false);
                        Check(done != null && done.State == ModelPullStateEnum.Succeeded && done.Advertised && done.Error == null, "The pull did not succeed and advertise: " + done?.State + " " + done?.Error);
                        Check(advertisedModel == "llama3", "onSucceeded was not called with the job.");
                    }
                }),
                new TestCaseDescriptor(suite, "ImportSkipsAdvertisedIgnoringLatest", "Imports skip advertised models (Ollama ignores ':latest') and report failures", executeAsync: delegate
                {
                    ClientSettings settings = new ClientSettings();
                    ClientRunnerSettings ollama = Ollama("http://127.0.0.1:1");
                    ollama.Models.Add(new ClientModelSettings { Name = "llama3", SupportsCompletions = true });
                    settings.Runners.Add(ollama);
                    settings.Runners.Add(new ClientRunnerSettings { Name = "vllm", ApiType = "OpenAICompatible", Endpoint = "http://127.0.0.1:2", Models = new List<ClientModelSettings> { new ClientModelSettings { Name = "gpt" } } });
                    Check(ProviderModelManager.IsAdvertised(ollama, "llama3:latest") && ProviderModelManager.IsAdvertised(ollama, "LLAMA3"), "Ollama should ignore ':latest' and case.");
                    Check(!ProviderModelManager.IsAdvertised(settings.Runners[1], "gpt:latest"), "Non-Ollama providers compare names exactly.");
                    ModelImportResult result = ProviderModelManager.ImportModels(settings, new List<ModelImportItem>
                    {
                        new ModelImportItem { Provider = ProviderName, Name = "llama3:latest" },
                        new ModelImportItem { Provider = ProviderName, Name = "nomic-embed-text", SupportsCompletions = false, SupportsEmbeddings = true },
                        new ModelImportItem { Provider = "missing", Name = "x" },
                        new ModelImportItem { Provider = ProviderName, Name = " " }
                    });
                    Check(result.Added.SequenceEqual(new[] { ProviderName + "/nomic-embed-text" }), "Added is wrong: " + String.Join(", ", result.Added));
                    Check(result.Skipped.SequenceEqual(new[] { ProviderName + "/llama3:latest" }), "Skipped is wrong: " + String.Join(", ", result.Skipped));
                    Check(result.Failed.Count == 2, "Failed is wrong: " + String.Join(", ", result.Failed));
                    ClientModelSettings? embed = ollama.Models.FirstOrDefault(item => item.Name == "nomic-embed-text");
                    Check(embed != null && embed.SupportsEmbeddings && !embed.SupportsCompletions, "The embedding model was not imported as embeddings.");
                    Check(ProviderModelManager.LooksLikeEmbeddingModel("nomic-embed-text", null) && !ProviderModelManager.LooksLikeEmbeddingModel("llama3", "llama"), "Embedding detection is wrong.");
                    Check(ProviderModelManager.RemoveAdvertisement(settings, ProviderName, "llama3:latest") == 1 && !ProviderModelManager.IsAdvertised(ollama, "llama3"), "RemoveAdvertisement should ignore ':latest'.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "TableColumnsSelectAndReject", "CLI tables pick columns by name and reject unknown names with the valid list", executeAsync: delegate
                {
                    CliTable table = new CliTable()
                        .AddColumn("name", "NAME", 8)
                        .AddColumn("size", "SIZE", 0, true)
                        .AddColumn("digest", "DIGEST", 0, false, false);
                    table.AddRow("a-very-long-model", "1 GB", "abc");
                    List<string> defaults = table.Render(null, String.Empty);
                    Check(defaults[0] == "NAME      SIZE" && !defaults[0].Contains("DIGEST", StringComparison.Ordinal), "Default header is wrong: '" + defaults[0] + "'");
                    Check(defaults[1] == "a-ver...  1 GB", "Clipping or alignment is wrong: '" + defaults[1] + "'");
                    List<string> picked = table.Render("DIGEST, name", String.Empty);
                    Check(picked[0].StartsWith("DIGEST", StringComparison.Ordinal) && picked[0].EndsWith("NAME", StringComparison.Ordinal) && !picked[0].Contains("SIZE", StringComparison.Ordinal), "Picked header is wrong: '" + picked[0] + "'");
                    Check(table.Render("all", String.Empty)[0].Contains("DIGEST", StringComparison.Ordinal), "'all' should show every column.");
                    ArgumentException? error = null;
                    try
                    {
                        table.Render("name,bogus");
                    }
                    catch (ArgumentException ex)
                    {
                        error = ex;
                    }

                    Check(error != null && error.Message.Contains("'bogus'", StringComparison.Ordinal) && error.Message.Contains("name, size, digest", StringComparison.Ordinal), "The unknown column error is wrong: " + error?.Message);
                    ExpectThrows<ArgumentException>(() => table.Render(" , "));
                    ModelPullJob job = new ModelPullJob { Model = "llama3", Status = "pulling sha256:6a0746a1ec1aa3e1bcf0ea1b1f8cb6c64f4fc5b5c4a8a1a54d1b2c3d4e5f6a7b8", CompletedBytes = 512, TotalBytes = 1024, Percent = 50 };
                    string bar = ModelPullProgressText.BarLine(job, 80);
                    Check(bar.Length <= 79 && bar.Contains("50.0%", StringComparison.Ordinal) && bar.Contains("pulling 6a0746a1ec1a", StringComparison.Ordinal) && bar.StartsWith("[", StringComparison.Ordinal), "The progress bar is wrong: " + bar);
                    Check(ModelPullProgressText.PlainLine(job).StartsWith("llama3: 50.0%", StringComparison.Ordinal), "The plain progress line is wrong: " + ModelPullProgressText.PlainLine(job));
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "CliProviderModelsAndColumns", "xeno provider models lists live models in a narrow table and honors --columns", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer server = new FakeProviderServer(OllamaHandler))
                    {
                        SaveSettings(server.BaseUrl, "llama3");
                        LocalUsageTestSuites.CliResult list = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "models", ProviderName).ConfigureAwait(false);
                        Check(list.ExitCode == 0, "provider models failed: " + list.Error);
                        Check(list.Output.Contains("ADVERTISED", StringComparison.Ordinal) && list.Output.Contains("llama3:latest", StringComparison.Ordinal) && list.Output.Contains("8.0B", StringComparison.Ordinal) && list.Output.Contains("Q4_0", StringComparison.Ordinal), "The listing is missing data: " + list.Output);
                        string llamaLine = list.Output.Split('\n').First(line => line.Contains("llama3:latest", StringComparison.Ordinal));
                        Check(llamaLine.TrimEnd().EndsWith("yes", StringComparison.Ordinal), "llama3 should read as advertised: " + llamaLine);
                        Check(list.Output.Split('\n').All(line => line.TrimEnd('\r').Length <= 120), "A line is wider than 120 columns: " + list.Output);
                        Check(list.Output.Contains("2 not advertised", StringComparison.Ordinal), "The import hint is missing: " + list.Output);

                        LocalUsageTestSuites.CliResult json = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "models", ProviderName, "--json").ConfigureAwait(false);
                        ProviderModelInventory? inventory = JsonUtil.Deserialize<ProviderModelInventory>(json.Output);
                        Check(json.ExitCode == 0 && inventory != null && inventory.Reachable && inventory.Models.Count == 3 && inventory.Models.Count(item => item.Advertised) == 1, "provider models --json is wrong: " + json.Output);

                        LocalUsageTestSuites.CliResult picked = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "models", ProviderName, "--columns", "Size,NAME").ConfigureAwait(false);
                        string header = picked.Output.Split('\n').First(line => line.Contains("NAME", StringComparison.Ordinal)).Trim();
                        Check(picked.ExitCode == 0 && header.StartsWith("SIZE", StringComparison.Ordinal) && header.EndsWith("NAME", StringComparison.Ordinal) && !picked.Output.Contains("QUANT", StringComparison.Ordinal), "--columns did not pick columns: " + picked.Output);

                        LocalUsageTestSuites.CliResult bad = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "models", ProviderName, "--columns", "name,bogus").ConfigureAwait(false);
                        Check(bad.ExitCode == 1 && bad.Error.Contains("Unknown column 'bogus'", StringComparison.Ordinal) && bad.Error.Contains("Valid columns: name, size, params, quant, modified, advertised", StringComparison.Ordinal), "An unknown column was not explained: " + bad.Output + bad.Error);
                        Check(!bad.Output.Contains("NAME", StringComparison.Ordinal), "No table should print when --columns is invalid: " + bad.Output);

                        LocalUsageTestSuites.CliResult missing = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "models", "nope").ConfigureAwait(false);
                        Check(missing.ExitCode == 1 && missing.Error.Contains("Provider endpoint not found", StringComparison.Ordinal), "An unknown provider was not explained: " + missing.Error);
                    }
                }),
                new TestCaseDescriptor(suite, "CliPullSucceedsAndFails", "xeno provider pull shows progress, advertises on success, and exits non-zero with the provider's error", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer server = new FakeProviderServer(OllamaHandler))
                    {
                        SaveSettings(server.BaseUrl, null);
                        LocalUsageTestSuites.CliResult ok = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "pull", ProviderName, "phi3", "--advertise").ConfigureAwait(false);
                        Check(ok.ExitCode == 0, "The pull failed: " + ok.Output + ok.Error);
                        Check(ok.Output.Contains("Pulled phi3 onto " + ProviderName, StringComparison.Ordinal) && ok.Output.Contains("Advertised phi3", StringComparison.Ordinal), "The pull did not report success: " + ok.Output);
                        Check(ok.Output.Contains("phi3: ", StringComparison.Ordinal) && !ok.Output.Contains("\r", StringComparison.Ordinal), "Redirected output should use plain progress lines: " + ok.Output);
                        Check(ProviderModelManager.IsAdvertised(ClientSettings.Load().Runners.Single(), "phi3"), "The pulled model was not advertised.");

                        LocalUsageTestSuites.CliResult failed = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "pull", ProviderName, "missing-model").ConfigureAwait(false);
                        Check(failed.ExitCode == 1 && failed.Error.Contains("file does not exist", StringComparison.Ordinal), "A failed pull did not exit non-zero with the provider's error: " + failed.Output + failed.Error);
                        Check(!ProviderModelManager.IsAdvertised(ClientSettings.Load().Runners.Single(), "missing-model"), "A failed pull must not advertise.");
                    }
                }),
                new TestCaseDescriptor(suite, "CliDeleteModelRemovesAdvertisement", "xeno provider delete-model asks first, then deletes from disk and removes the advertisement", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer server = new FakeProviderServer(OllamaHandler))
                    {
                        SaveSettings(server.BaseUrl, "llama3");
                        LocalUsageTestSuites.CliResult unconfirmed = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "delete-model", ProviderName, "llama3:latest").ConfigureAwait(false);
                        Check(unconfirmed.ExitCode == 1 && unconfirmed.Error.Contains("--yes", StringComparison.Ordinal), "Without --yes and no terminal the delete should refuse: " + unconfirmed.Output + unconfirmed.Error);
                        Check(!server.Requests.Any(item => item.Path == "/api/delete"), "The provider was called without confirmation.");

                        LocalUsageTestSuites.CliResult deleted = await LocalUsageTestSuites.RunCliAsync(home.Directory, "provider", "rm-model", ProviderName, "llama3:latest", "--yes").ConfigureAwait(false);
                        Check(deleted.ExitCode == 0 && deleted.Output.Contains("Deleted llama3:latest from " + ProviderName, StringComparison.Ordinal) && deleted.Output.Contains("Removed 1 advertisement", StringComparison.Ordinal), "The delete did not report what it removed: " + deleted.Output + deleted.Error);
                        FakeProviderRequest? call = server.Requests.FirstOrDefault(item => item.Path == "/api/delete");
                        Check(call != null && call.Method == "DELETE" && call.Body.Contains("llama3:latest", StringComparison.Ordinal), "The provider did not get DELETE /api/delete for the model.");
                        Check(ClientSettings.Load().Runners.Single().Models.Count == 0, "The advertisement was not removed.");
                    }
                }),
                new TestCaseDescriptor(suite, "CliImportAddsAndSkips", "xeno model import lists candidates without a selection, then adds new models and skips advertised ones", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer server = new FakeProviderServer(OllamaHandler))
                    {
                        SaveSettings(server.BaseUrl, "llama3");
                        LocalUsageTestSuites.CliResult listed = await LocalUsageTestSuites.RunCliAsync(home.Directory, "model", "import", ProviderName).ConfigureAwait(false);
                        Check(listed.ExitCode == 0 && listed.Output.Contains("nomic-embed-text:latest", StringComparison.Ordinal) && listed.Output.Contains("--all", StringComparison.Ordinal) && listed.Output.Contains("Nothing was imported", StringComparison.Ordinal), "Without a selection import should list candidates: " + listed.Output + listed.Error);
                        Check(ClientSettings.Load().Runners.Single().Models.Count == 1, "Listing candidates must not import.");

                        LocalUsageTestSuites.CliResult imported = await LocalUsageTestSuites.RunCliAsync(home.Directory, "model", "import", ProviderName, "--all").ConfigureAwait(false);
                        Check(imported.ExitCode == 0 && imported.Output.Contains("Added (2)", StringComparison.Ordinal) && imported.Output.Contains("Skipped (already advertised) (1)", StringComparison.Ordinal) && imported.Output.Contains(ProviderName + "/llama3:latest", StringComparison.Ordinal), "Import did not report added and skipped: " + imported.Output + imported.Error);
                        ClientRunnerSettings provider = ClientSettings.Load().Runners.Single();
                        ClientModelSettings? embed = provider.Models.FirstOrDefault(item => item.Name == "nomic-embed-text:latest");
                        Check(provider.Models.Count == 3 && embed != null && embed.SupportsEmbeddings && !embed.SupportsCompletions, "Import saved the wrong models.");

                        LocalUsageTestSuites.CliResult unknown = await LocalUsageTestSuites.RunCliAsync(home.Directory, "model", "import", ProviderName, "--model", "not-there", "--json").ConfigureAwait(false);
                        ModelImportResult? result = JsonUtil.Deserialize<ModelImportResult>(unknown.Output);
                        Check(unknown.ExitCode == 1 && result != null && result.Failed.Count == 1 && result.Failed[0].Contains("not-there", StringComparison.Ordinal), "A model not on the provider should fail: " + unknown.Output + unknown.Error);
                    }
                })
            });
        }

        private static ClientRunnerSettings Ollama(string endpoint)
        {
            return new ClientRunnerSettings { Name = ProviderName, ApiType = ProviderConfiguration.OllamaApiType, Endpoint = endpoint, Models = new List<ClientModelSettings>() };
        }

        private static void SaveSettings(string endpoint, string? advertised)
        {
            ClientSettings settings = new ClientSettings();
            ClientRunnerSettings provider = Ollama(endpoint);
            if (advertised != null) provider.Models.Add(new ClientModelSettings { Name = advertised, SupportsCompletions = true });
            settings.Runners.Add(provider);
            settings.Save();
        }

        private static FakeProviderResponse OllamaHandler(FakeProviderRequest request)
        {
            if (request.Path == "/api/tags") return FakeProviderResponse.Json(TagsBody);
            if (request.Path == "/api/delete") return FakeProviderResponse.Json("{}");
            if (request.Path == "/api/pull")
            {
                if (request.Body.Contains("missing-model", StringComparison.Ordinal))
                {
                    return PullResponse(request, new List<string> { "{\"status\":\"pulling manifest\"}\n", "{\"error\":\"pull model manifest: file does not exist\"}\n" }, 0);
                }

                return PullResponse(request, new List<string>
                {
                    "{\"status\":\"pulling manifest\"}\n",
                    "{\"status\":\"pulling aaa\",\"digest\":\"sha256:aaa\",\"total\":2048,\"completed\":1024}\n",
                    "{\"status\":\"pulling aaa\",\"digest\":\"sha256:aaa\",\"total\":2048,\"completed\":2048}\n",
                    "{\"status\":\"success\"}\n"
                }, 0);
            }

            return FakeProviderResponse.Json("{\"error\":\"not found\"}", 404);
        }

        private static FakeProviderResponse PullResponse(FakeProviderRequest request, List<string> lines, int delayMs)
        {
            if (request.Path != "/api/pull") return FakeProviderResponse.Json("{\"error\":\"not found\"}", 404);
            return new FakeProviderResponse { ContentType = "application/x-ndjson", Chunks = lines, ChunkDelayMs = delayMs };
        }

        private static CancellationToken TimeoutToken()
        {
            return new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
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
    }
}
