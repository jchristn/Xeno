namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Modals;
    using Xeno.Tui.Pages;

    /// <summary>
    /// Model management in the desktop and terminal apps: create forms that save and add another, Import Models, Manage
    /// Models (live inventory, pulls with progress and cancel, delete), and the Columns control on every table. The
    /// shared suite covers the presentation layer both apps render; the terminal suite drives the terminal app
    /// headlessly with the keyboard and the mouse against a fake Ollama.
    /// </summary>
    public static class ModelManagementTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Presentation layer shared by the desktop and terminal apps.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SharedSuite()
        {
            const string suite = "ModelManagement";
            return new TestSuiteDescriptor(suite, "Model Management (shared)", new List<TestCaseDescriptor>
            {
                Case(suite, "CreateFormsSaveOrSaveAndAddAnother", "Create forms save and close with Save and offer Save and Add Another, which clears only the entry field", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler()), () => null))
                    {
                        XenoFormSpec model = XenoForms.AddModel(workspace, null) ?? throw new InvalidOperationException("Add Model needs a provider.");
                        Check(model.SubmitText == "Save" && model.CanAddAnother && model.AddAnotherText == XenoForms.AddAnotherButtonText, "Add Model should offer Save and Save and Add Another.");
                        Dictionary<string, string> next = model.NextValues(new Dictionary<string, string> { { "provider", "local-ollama" }, { "name", "llama3.2" }, { "completions", "true" }, { "embeddings", "true" }, { "maxConcurrent", "2" } });
                        Check(next["name"] == String.Empty && next["provider"] == "local-ollama" && next["embeddings"] == "true" && next["maxConcurrent"] == "2", "Save and Add Another should clear the model name and keep the provider and toggles.");
                        XenoFormSpec provider = XenoForms.AddProvider(workspace);
                        Check(provider.SubmitText == "Save" && provider.CanAddAnother && provider.ClearOnAddAnother.Contains("name"), "Add Provider should offer Save and Add Another.");
                        XenoFormSpec rule = XenoForms.RouteRule(workspace, null, null);
                        Check(rule.SubmitText == "Save" && rule.CanAddAnother && rule.ClearOnAddAnother.SequenceEqual(new[] { "model" }), "Add Route Rule should offer Save and Add Another.");
                        Check(!XenoForms.RouteRule(workspace, new LocalInferenceRouteRule { Model = "m", Target = LocalInferenceRouteTargetEnum.Network }, null).CanAddAnother, "Editing a rule should not offer Save and Add Another.");
                        Check(!XenoForms.EditProvider(workspace, workspace.LoadSettings().Runners[0]).CanAddAnother, "Editing a provider should not offer Save and Add Another.");
                        XenoFormSpec? configure = XenoForms.EditModel(workspace, "local-ollama", "found-only:latest");
                        Check(configure != null && !configure.CanAddAnother, "Configuring a found model should not offer Save and Add Another.");

                        string? first = model.SubmitAsync(new Dictionary<string, string> { { "provider", "local-ollama" }, { "name", "alpha:latest" }, { "completions", "true" }, { "embeddings", "false" }, { "maxConcurrent", "" } }, CancellationToken.None).GetAwaiter().GetResult();
                        string? duplicate = model.SubmitAsync(new Dictionary<string, string> { { "provider", "local-ollama" }, { "name", "" }, { "completions", "true" }, { "embeddings", "false" }, { "maxConcurrent", "" } }, CancellationToken.None).GetAwaiter().GetResult();
                        Check(first == null, "Saving a model failed: " + first);
                        Check(!String.IsNullOrWhiteSpace(duplicate), "An invalid entry should return an error so the form stays open.");
                    }
                }),
                AsyncCase(suite, "ImportReadsLiveInventories", "Import lists each provider's live models, marks advertised ones, prefills embeddings, and reports unreachable providers inline", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeOllama ollama = new FakeOllama())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler()), () => null))
                    {
                        await UseProvidersAsync(workspace, ollama, true).ConfigureAwait(false);
                        List<string> providers = XenoModelImport.Providers(workspace);
                        Check(providers.SequenceEqual(new[] { "lab-ollama", "down-vllm" }), "Providers to read are wrong: " + String.Join(", ", providers));
                        List<ModelImportSource> sources = await XenoModelImport.LoadAsync(workspace, providers, CancellationToken.None).ConfigureAwait(false);
                        ModelImportSource lab = sources.Single(item => item.Provider == "lab-ollama");
                        ModelImportSource down = sources.Single(item => item.Provider == "down-vllm");
                        Check(lab.Reachable && lab.Candidates.Count == 3, "The Ollama inventory was not read: " + lab.Heading);
                        Check(!down.Reachable && !String.IsNullOrWhiteSpace(down.Error) && down.Heading.Contains("could not read models", StringComparison.Ordinal), "An unreachable provider should report its error: " + down.Heading);
                        ModelImportCandidate advertised = lab.Candidates.Single(item => item.Name == "qwen3:8b");
                        Check(advertised.Advertised && !advertised.Selected && advertised.Status == "advertised", "An advertised model should be marked and not selected.");
                        ModelImportCandidate embed = lab.Candidates.Single(item => item.Name == "nomic-embed-text:latest");
                        Check(embed.Selected && embed.Embeddings && embed.Capability == "embeddings", "An embeddings model should be prefilled as embeddings.");
                        ModelImportCandidate llama = lab.Candidates.Single(item => item.Name == "llama3.2:latest");
                        Check(llama.Selected && !llama.Embeddings && llama.Size == "2.0 GB", "A chat model should be selected as completions: " + llama.Size);
                        List<ModelImportItem> items = XenoModelImport.Items(sources);
                        Check(items.Count == 2 && XenoModelImport.SelectedCount(sources) == 2, "Only the two unadvertised models should be imported.");
                        ModelImportResult result = await workspace.ImportModelsAsync(items, CancellationToken.None).ConfigureAwait(false);
                        Check(XenoModelImport.Describe(result) == "Imported 2 models.", "Import summary is wrong: " + XenoModelImport.Describe(result));
                        ClientModelSettings saved = workspace.LoadSettings().Runners.Single(item => item.Name == "lab-ollama").Models.Single(item => item.Name == "nomic-embed-text:latest");
                        Check(saved.SupportsEmbeddings && !saved.SupportsCompletions, "The embeddings choice was not saved.");
                        ModelImportResult again = await workspace.ImportModelsAsync(items, CancellationToken.None).ConfigureAwait(false);
                        Check(XenoModelImport.Describe(again).Contains("2 already advertised", StringComparison.Ordinal), "A repeat import should report skipped models: " + XenoModelImport.Describe(again));
                    }
                }),
                Case(suite, "ManageModelsTextAndRows", "Manage Models formats inventory rows, pull progress, the active-pull indicator, the Ollama-only note, and the delete confirmation", () =>
                {
                    ProviderModelInventory inventory = new ProviderModelInventory
                    {
                        Provider = "lab",
                        ApiType = "Ollama",
                        Reachable = true,
                        CanPull = true,
                        CanDelete = true,
                        Models = new List<ProviderInventoryModel>
                        {
                            new ProviderInventoryModel { Name = "llama3.2:latest", SizeBytes = 2147483648, ParameterSize = "3.2B", Quantization = "Q4_K_M", Modified = "2026-01-02T03:04:05Z", Advertised = true }
                        }
                    };
                    ProviderInventoryRow row = XenoModelManagement.Rows(inventory).Single();
                    Check(row.Size == "2.0 GB" && row.ParameterSize == "3.2B" && row.Quantization == "Q4_K_M" && row.Advertised == "yes" && row.Modified.StartsWith("2026-01-0", StringComparison.Ordinal), "Inventory row is wrong.");
                    Check(XenoModelManagement.Summary(inventory) == "1 model on lab, 1 advertised.", "Summary is wrong: " + XenoModelManagement.Summary(inventory));
                    inventory.CanPull = false;
                    Check(XenoModelManagement.Summary(inventory).Contains("Ollama providers only", StringComparison.Ordinal), "Other providers should say pull and delete are Ollama-only.");
                    ModelPullJob job = new ModelPullJob { Provider = "lab", Model = "llama3.2", State = ModelPullStateEnum.Running, Status = "pulling", Percent = 42.7, CompletedBytes = 1024, TotalBytes = 4096 };
                    Check(XenoModelManagement.PullLine(job) == "Pulling llama3.2 42%", "Pull line is wrong: " + XenoModelManagement.PullLine(job));
                    Check(XenoModelManagement.ActiveIndicator(new[] { job }) == "Pulling llama3.2 42%", "Indicator is wrong.");
                    Check(XenoModelManagement.ActiveIndicator(new[] { job, new ModelPullJob { Provider = "lab", Model = "b", State = ModelPullStateEnum.Queued } }) == "Pulling 2 models", "Indicator for several pulls is wrong.");
                    Check(XenoModelManagement.ActiveIndicator(new[] { new ModelPullJob { State = ModelPullStateEnum.Succeeded } }) == null, "Finished pulls should not show the indicator.");
                    Check(Math.Abs(XenoModelManagement.Fraction(job) - 0.427) < 0.001 && XenoModelManagement.PullDetail(job).Contains("1.0 KB of 4.0 KB", StringComparison.Ordinal), "Progress is wrong.");
                    Check(XenoModelManagement.PullsFor(new[] { job, new ModelPullJob { Provider = "other", State = ModelPullStateEnum.Running } }, "lab").Count == 1, "Pulls should be filtered by provider.");
                    Check(XenoModelManagement.DeleteConfirmation("lab", "llama3.2", true).Contains("removes its advertisement", StringComparison.Ordinal), "Delete confirmation should say the advertisement is removed.");
                    Check(XenoModelManagement.FinishedMessage(new ModelPullJob { Provider = "lab", Model = "x", State = ModelPullStateEnum.Failed, Error = "boom" }) == "Pull of x failed: boom.", "Failure message is wrong.");
                }),
                Case(suite, "TableColumnsPersistPerTable", "Column choices default to at most six columns, persist per table, ignore unknown columns, and reset", () =>
                {
                    string file = Path.Combine(Path.GetTempPath(), "xeno-columns-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        List<string> columns = new List<string> { "Name", "API type", "Endpoint", "Models", "Timeout", "Managed", "Credential" };
                        List<string> defaults = columns.Where(item => item != "Timeout").ToList();
                        XenoFrontEndPreferences preferences = new XenoFrontEndPreferences();
                        string id = XenoTableColumns.TableId("desktop", "ProviderRow", columns);
                        Check(XenoTableColumns.Visible(preferences, id, columns, defaults).SequenceEqual(defaults), "Defaults should show.");
                        Check(!XenoTableColumns.Save(preferences, id, new List<string>()), "An empty choice should be refused.");
                        Check(XenoTableColumns.Save(preferences, id, new[] { "Endpoint", "Name", "Gone" }), "Saving failed.");
                        Check(preferences.Save(file), "Preferences did not save.");
                        XenoFrontEndPreferences loaded = XenoFrontEndPreferences.Load(file);
                        Check(XenoTableColumns.Visible(loaded, id, columns, defaults).SequenceEqual(new[] { "Name", "Endpoint" }), "The choice did not round-trip in display order.");
                        Check(XenoTableColumns.Visible(loaded, "desktop:other", columns, defaults).SequenceEqual(defaults), "Another table should keep its defaults.");
                        Check(XenoTableColumns.Reset(loaded, id) && XenoTableColumns.Visible(loaded, id, columns, defaults).SequenceEqual(defaults), "Reset should restore the defaults.");
                        Check(XenoTableColumns.Defaults(columns, new List<string>()).SequenceEqual(new[] { "Name" }), "A table always shows a column.");
                    }
                    finally
                    {
                        File.Delete(file);
                    }
                }),
                Case(suite, "ImportCommandIsInTheSharedMenus", "Import Models is a shared command in the File menu of the desktop and terminal apps", () =>
                {
                    foreach (XenoSurfaceEnum surface in new[] { XenoSurfaceEnum.Desktop, XenoSurfaceEnum.Terminal })
                    {
                        Check(XenoMenuLayout.Commands(XenoMenuLayout.MenuBar(surface, false)).Contains(XenoCommandEnum.ImportModels), surface + " menus should offer Import Models.");
                    }

                    Check(XenoCommandAvailability.Title(XenoCommandEnum.ImportModels) == "Import Models", "Palette title is wrong.");
                })
            });
        }

        /// <summary>
        /// Terminal app model management, driven headlessly.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TuiSuite()
        {
            const string suite = "TuiModelManagement";
            return new TestSuiteDescriptor(suite, "Model Management (terminal app)", new List<TestCaseDescriptor>
            {
                Case(suite, "SaveAndAddAnotherKeepsTheFormOpen", "Ctrl+N and the Save and Add Another button save, clear the model name, and confirm; Ctrl+S saves and closes", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Models))
                    {
                        tui.Press("a");
                        Check(tui.App.Modals.IsActive, "a should open Add Model.");
                        Contains(tui.Screen(), "Save and Add Another");
                        Contains(tui.Screen(), "Ctrl+N save and add another");
                        Snapshot(tui, "AddModel");
                        tui.Press("tab");
                        tui.Press("ctrl+n");
                        Check(tui.App.Modals.IsActive, "A missing name should keep the form open.");
                        Contains(tui.Screen(), "Model name is required.");
                        tui.Type("alpha:latest");
                        tui.Press("ctrl+n");
                        WaitFor(() => HasModel("local-ollama", "alpha:latest"), tui, "Ctrl+N did not save the model.");
                        Check(tui.App.Modals.IsActive, "Save and Add Another should keep the form open.");
                        WaitFor(() => tui.Tui.Context.Message.Contains("Added model 'alpha:latest'", StringComparison.Ordinal), tui, "The message line should confirm the model.");
                        Contains(tui.Screen(), "✓ Added model 'alpha:latest'");
                        tui.Type("gamma:latest");
                        tui.ClickText("Save and Add Another");
                        WaitFor(() => HasModel("local-ollama", "gamma:latest"), tui, "Clicking Save and Add Another did not save the model.");
                        Check(tui.App.Modals.IsActive, "Clicking Save and Add Another should keep the form open.");
                        tui.Type("beta:latest");
                        tui.Press("ctrl+s");
                        WaitFor(() => !tui.App.Modals.IsActive, tui, "Save should close the form.");
                        Check(HasModel("local-ollama", "beta:latest"), "Save did not save the last model; the name field was not cleared: " + String.Join(", ", ClientSettings.Load().Runners[0].Models.Select(item => item.Name)));
                    }
                }),
                Case(suite, "SaveButtonClosesWithTheMouse", "Clicking Save saves and closes; clicking Cancel after adding another keeps what was added", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.LocalEndpoint))
                    {
                        tui.Press("a");
                        Contains(tui.Screen(), "Add Route Rule");
                        tui.Type("first:latest");
                        tui.Press("ctrl+n");
                        WaitFor(() => ClientSettings.Load().LocalInferenceServer.Routes.Any(item => item.Model == "first:latest"), tui, "The first rule was not saved.");
                        TUIKit.Point cancel = tui.Find("Cancel", 3) ?? throw new InvalidOperationException("No Cancel button:" + Environment.NewLine + tui.Screen());
                        tui.Click(cancel.X + 1, cancel.Y);
                        Check(!tui.App.Modals.IsActive, "Cancel should close the form.");
                        Check(!tui.Tui.Context.Message.Contains("nothing changed", StringComparison.Ordinal), "Closing after adding should not say nothing changed.");
                        tui.Press("a");
                        tui.Type("second:latest");
                        TUIKit.Point save = tui.Find("Save ", 3) ?? throw new InvalidOperationException("No Save button:" + Environment.NewLine + tui.Screen());
                        tui.Click(save.X + 1, save.Y);
                        WaitFor(() => !tui.App.Modals.IsActive, tui, "Clicking Save should close the form.");
                        Check(ClientSettings.Load().LocalInferenceServer.Routes.Any(item => item.Model == "second:latest"), "Clicking Save did not save the rule.");
                    }
                }),
                Case(suite, "ImportPicksModelsFromLiveInventories", "i opens Import Models: providers are chosen, unreachable ones show their error, advertised models are marked, and picks import with their capability", () =>
                {
                    using (FakeOllama ollama = new FakeOllama())
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        UseProvidersAsync(tui.Workspace, ollama, true).GetAwaiter().GetResult();
                        tui.Tui.Navigate(XenoPageEnum.Models);
                        tui.Settle();
                        tui.Press("i");
                        Check(tui.App.Modals.IsActive, "i should open Import Models.");
                        string screen = tui.Screen();
                        Contains(screen, "[x] lab-ollama");
                        Contains(screen, "[x] down-vllm");
                        tui.Press("enter");
                        WaitFor(() => tui.Screen().Contains("lab-ollama: 3 models", StringComparison.Ordinal), tui, "The models step did not appear.");
                        screen = tui.Screen();
                        Contains(screen, "down-vllm: could not read models");
                        Contains(screen, "[-] qwen3:8b");
                        Contains(screen, "advertised");
                        Contains(screen, "‹embeddings›");
                        Contains(screen, "2 models selected.");
                        Snapshot(tui, "ImportModels");
                        tui.ClickText("‹completions›");
                        Contains(tui.Screen(), "2 models selected.");
                        tui.Press("enter");
                        WaitFor(() => !tui.App.Modals.IsActive && HasModel("lab-ollama", "llama3.2:latest"), tui, "Import did not add the models.");
                        ClientModelSettings llama = ClientSettings.Load().Runners.Single(item => item.Name == "lab-ollama").Models.Single(item => item.Name == "llama3.2:latest");
                        Check(llama.SupportsEmbeddings && !llama.SupportsCompletions, "Clicking the capability should import llama3.2 as embeddings.");
                        Check(HasModel("lab-ollama", "nomic-embed-text:latest"), "The embeddings model was not imported.");
                        WaitFor(() => tui.Tui.Context.Message.StartsWith("Imported 2 models", StringComparison.Ordinal), tui, "The message line should report the import: " + tui.Tui.Context.Message);
                    }
                }),
                Case(suite, "ImportCanBeDrivenWithTheKeyboardAndCancelled", "Import Models: Space unpicks a model, Esc cancels without changes, and Import Models runs from the command palette", () =>
                {
                    using (FakeOllama ollama = new FakeOllama())
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        UseProvidersAsync(tui.Workspace, ollama, false).GetAwaiter().GetResult();
                        tui.Tui.Execute(XenoCommandEnum.ImportModels);
                        tui.Settle();
                        Contains(tui.Screen(), "[x] lab-ollama");
                        tui.ClickText("Load Models");
                        WaitFor(() => tui.Screen().Contains("lab-ollama: 3 models", StringComparison.Ordinal), tui, "The models step did not appear.");
                        tui.Press("space");
                        Contains(tui.Screen(), "1 model selected.");
                        tui.Press("escape");
                        Check(!tui.App.Modals.IsActive, "Esc should cancel.");
                        Check(!HasModel("lab-ollama", "nomic-embed-text:latest"), "Cancelling should change nothing.");
                    }
                }),
                Case(suite, "ManageModelsPullsCancelsAndDeletes", "p opens Manage Models: it lists live models, pulls with progress, keeps pulling after close, cancels, and deletes after confirming", () =>
                {
                    using (FakeOllama ollama = new FakeOllama())
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        try
                        {
                            UseProvidersAsync(tui.Workspace, ollama, false).GetAwaiter().GetResult();
                            tui.Tui.Navigate(XenoPageEnum.Providers);
                            tui.Settle();
                            tui.Press("p");
                            Check(tui.App.Modals.IsActive, "p should open Manage Models.");
                            WaitFor(() => tui.Screen().Contains("Q4_K_M", StringComparison.Ordinal), tui, "The live models did not load.");
                            string screen = tui.Screen();
                            Contains(screen, "Models on lab-ollama");
                            Contains(screen, "llama3.2:latest");
                            Contains(screen, "3.2B");
                            Contains(screen, "2.0 GB");
                            Contains(screen, "[ ] Advertise when finished");

                            tui.Press("tab");
                            tui.Type("tiny:latest");
                            tui.Press("enter");
                            WaitFor(() => tui.Workspace.ListModelPulls().Any(job => job.Model == "tiny:latest" && job.State == ModelPullStateEnum.Succeeded), tui, "The pull did not finish.");
                            WaitFor(() => tui.Screen().Contains("Pulled tiny:latest", StringComparison.Ordinal), tui, "The dialog should report the finished pull.");
                            WaitFor(() => tui.Screen().Contains("Q8_0", StringComparison.Ordinal), tui, "The inventory should refresh after the pull.");
                            Check(!HasModel("lab-ollama", "tiny:latest"), "Without advertise the pulled model should not be advertised.");

                            tui.Type("slow:latest");
                            tui.Press("enter");
                            WaitFor(() => tui.Screen().Contains("Pulling slow:latest", StringComparison.Ordinal), tui, "The header and dialog should show the active pull.");
                            Contains(tui.Screen(), "[ Cancel ]");
                            Snapshot(tui, "ManageModels");
                            tui.Press("escape");
                            Check(!tui.App.Modals.IsActive, "Esc should close the dialog.");
                            Check(tui.Workspace.ListModelPulls().Any(job => job.Model == "slow:latest" && job.Active), "The pull should keep running after the dialog closes.");
                            Contains(tui.Screen().Split('\n')[0], "Pulling slow:latest");

                            tui.Press("p");
                            WaitFor(() => tui.Screen().Contains("[ Cancel ]", StringComparison.Ordinal), tui, "Reopening should show the active pull.");
                            tui.ClickText("[ Cancel ]");
                            WaitFor(() => tui.Workspace.ListModelPulls().Any(job => job.Model == "slow:latest" && job.State == ModelPullStateEnum.Cancelled), tui, "Clicking Cancel did not cancel the pull.");
                            WaitFor(() => !tui.Screen().Split('\n')[0].Contains("Pulling", StringComparison.Ordinal), tui, "The header should stop showing the pull.");

                            tui.Press("down");
                            tui.Press("down");
                            tui.Press("d");
                            WaitFor(() => tui.Screen().Contains("Delete Model", StringComparison.Ordinal), tui, "d should ask before deleting.");
                            Contains(tui.Screen(), "Delete 'qwen3:8b' from lab-ollama?");
                            Contains(tui.Screen(), "removes its advertisement");
                            tui.Press("enter");
                            WaitFor(() => ollama.Deleted.Contains("qwen3:8b") && !HasModel("lab-ollama", "qwen3:8b"), tui, "Delete should remove the model from Ollama and its advertisement.");
                            WaitFor(() => tui.Tui.Context.Message.Contains("removed its advertisement", StringComparison.Ordinal), tui, "The delete should be reported: " + tui.Tui.Context.Message);
                            tui.ClickText("[ Close ]");
                            Check(!tui.App.Modals.IsActive, "Clicking Close should close the dialog.");
                        }
                        finally
                        {
                            foreach (ModelPullJob job in tui.Workspace.ListModelPulls().Where(item => item.Active)) tui.Workspace.CancelModelPullAsync(job.Id, CancellationToken.None).GetAwaiter().GetResult();
                        }
                    }
                }),
                Case(suite, "ManageModelsAdvertisesAndListsOtherProviders", "Advertise when finished adds the pulled model, and a provider other than Ollama is listed with the Ollama-only note from the Local endpoint page", () =>
                {
                    using (FakeOllama ollama = new FakeOllama())
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        UseProvidersAsync(tui.Workspace, ollama, false).GetAwaiter().GetResult();
                        tui.Workspace.AddProviderAsync(new ClientRunnerSettings { Name = "lab-vllm", ApiType = "vLLM", Endpoint = ollama.BaseUrl + "/vllm", Models = new List<ClientModelSettings>() }, CancellationToken.None).GetAwaiter().GetResult();
                        tui.Tui.Navigate(XenoPageEnum.Providers);
                        tui.Settle();
                        tui.Press("p");
                        WaitFor(() => tui.Screen().Contains("Q4_K_M", StringComparison.Ordinal), tui, "The live models did not load.");
                        tui.Press("tab");
                        tui.Type("tiny:latest");
                        tui.ClickText("Advertise when finished");
                        Contains(tui.Screen(), "[x] Advertise when finished");
                        tui.ClickText("[ Pull ]");
                        WaitFor(() => HasModel("lab-ollama", "tiny:latest"), tui, "Advertise when finished should advertise the pulled model.");
                        tui.Press("escape");

                        tui.Tui.Navigate(XenoPageEnum.LocalEndpoint);
                        tui.Settle();
                        TUIKit.Point vllm = tui.Find("lab-vllm", 3) ?? throw new InvalidOperationException("No lab-vllm row:" + Environment.NewLine + tui.Screen());
                        tui.Click(vllm.X, vllm.Y);
                        tui.ClickText("Manage Models ]");
                        WaitFor(() => tui.Screen().Contains("mistral-7b", StringComparison.Ordinal), tui, "The vLLM models did not load.");
                        string screen = tui.Screen();
                        Contains(screen, "Ollama providers only");
                        Check(!screen.Contains("[ Pull ]", StringComparison.Ordinal) && !screen.Contains("[ Delete ]", StringComparison.Ordinal), "Pull and delete are Ollama-only.");
                        tui.Press("escape");
                    }
                }),
                Case(suite, "ColumnsChooseWithKeyboardAndMouse", "v and the [Columns] button choose a table's columns, the choice persists per table, and Defaults restores it", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Providers))
                    {
                        string preferencesPath = tui.Tui.Context.PreferencesPath ?? throw new InvalidOperationException("No preferences path.");
                        Check(tui.Screen().Contains("Endpoint", StringComparison.Ordinal) && tui.Screen().Contains("[Columns]", StringComparison.Ordinal), "Providers should show Endpoint and a Columns button.");
                        tui.Press("v");
                        Check(tui.App.Modals.IsActive, "v should open the column chooser.");
                        Contains(tui.Screen(), "Columns: Providers");
                        tui.Press("down");
                        tui.Press("down");
                        tui.Press("space");
                        Contains(tui.Screen(), "[ ] Endpoint");
                        tui.Press("enter");
                        Check(!tui.App.Modals.IsActive, "Apply should close the chooser.");
                        Check(!tui.Screen().Contains("http://127.0.0.1:11434", StringComparison.Ordinal), "The Endpoint column should be hidden:" + Environment.NewLine + tui.Screen());
                        XenoFrontEndPreferences saved = XenoFrontEndPreferences.Load(preferencesPath);
                        List<string>? stored = saved.TableColumns.TryGetValue("terminal:ProvidersPage:0", out List<string>? value) ? value : null;
                        Check(stored != null && !stored.Contains("Endpoint") && stored.Contains("Name"), "The choice was not saved per table.");
                        Check(!saved.TableColumns.ContainsKey("terminal:ProvidersPage:1"), "The other table on the page should keep its defaults.");

                        ProvidersPage reopened = new ProvidersPage(tui.Tui.Context);
                        reopened.ApplyColumnPreferences();
                        Check(!reopened.ColumnTables[0].Visible.Contains("Endpoint"), "A new page should show the saved choice.");

                        tui.ClickText("[Columns]");
                        Check(tui.App.Modals.IsActive, "Clicking [Columns] should open the chooser.");
                        tui.ClickText("[ ] Endpoint");
                        Contains(tui.Screen(), "[x] Endpoint");
                        tui.ClickText("[ Apply ]");
                        Contains(tui.Screen(), "http://127.0.0.1:11434");

                        tui.Press("v");
                        tui.ClickText("[ Defaults ]");
                        Check(!XenoFrontEndPreferences.Load(preferencesPath).TableColumns.ContainsKey("terminal:ProvidersPage:0"), "Defaults should forget the choice.");
                    }
                }),
                Case(suite, "EveryTableHasColumnsAndFitsByDefault", "Every terminal table offers Columns and shows at most six columns by default", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        int tables = 0;
                        foreach (XenoPageDefinition page in XenoNavigationCatalog.Pages)
                        {
                            tui.Tui.Navigate(page.Page);
                            tui.Settle();
                            TuiPage view = tui.Tui.CurrentView;
                            foreach (ITuiTableColumns table in view.ColumnTables)
                            {
                                tables++;
                                Check(table.Defaults.Count <= XenoTableColumns.MaxDefaultVisible, page.Title + " table '" + table.Title + "' shows " + table.Defaults.Count + " columns by default.");
                                Check(table.Visible.SequenceEqual(table.Defaults), page.Title + " table '" + table.Title + "' should start with its defaults.");
                            }

                            if (view.ColumnTables.Count > 0) Check(view.AllHints.Any(hint => hint.Key == "v"), page.Title + " should offer the Columns key.");
                        }

                        Check(tables >= 12, "Expected every page table to be registered, found " + tables + ".");
                        tui.Tui.Navigate(XenoPageEnum.Requests);
                        tui.Settle();
                        Check(!tui.Tui.CurrentView.ColumnTables[0].Visible.Contains("Tokens"), "Requests should hide Tokens by default.");
                    }
                })
            });
        }

        #endregion

        #region Private-Methods

        private static async Task UseProvidersAsync(XenoWorkspace workspace, FakeOllama ollama, bool includeUnreachable)
        {
            foreach (ClientRunnerSettings existing in workspace.LoadSettings().Runners.ToList())
            {
                await workspace.DeleteProviderAsync(existing.Name, CancellationToken.None).ConfigureAwait(false);
            }

            await workspace.AddProviderAsync(new ClientRunnerSettings { Name = "lab-ollama", ApiType = "Ollama", Endpoint = ollama.BaseUrl, Models = new List<ClientModelSettings>() }, CancellationToken.None).ConfigureAwait(false);
            await workspace.AddModelAsync("lab-ollama", "qwen3:8b", true, false, CancellationToken.None).ConfigureAwait(false);
            if (includeUnreachable)
            {
                int closed = FakeProviderServer.GetFreePort();
                await workspace.AddProviderAsync(new ClientRunnerSettings { Name = "down-vllm", ApiType = "vLLM", Endpoint = "http://127.0.0.1:" + closed, TimeoutMs = 2000, Models = new List<ClientModelSettings>() }, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static void Snapshot(TuiHarness tui, string name)
        {
            string? directory = Environment.GetEnvironmentVariable("XENO_TUI_SNAPSHOT_DIR");
            if (String.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name + ".txt"), tui.Screen());
        }

        private static bool HasModel(string provider, string model)
        {
            ClientRunnerSettings? runner = ClientSettings.Load().Runners.FirstOrDefault(item => item.Name == provider);
            return runner != null && runner.Models.Any(item => item.Name == model);
        }

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

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Contains(string screen, string expected)
        {
            if (!screen.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected '" + expected + "' on screen:" + Environment.NewLine + screen);
            }
        }

        private static void WaitFor(Func<bool> condition, TuiHarness tui, string message)
        {
            for (int i = 0; i < 100; i++)
            {
                if (condition()) return;
                tui.Settle(200);
            }

            throw new InvalidOperationException(message + Environment.NewLine + tui.Screen());
        }

        #endregion

        #region Private-Classes

        /// <summary>
        /// A fake Ollama on <see cref="FakeProviderServer"/>: /api/tags lists its models, /api/pull streams progress
        /// (slowly for "slow:latest") and adds the model, /api/delete removes one, and /vllm/v1/models answers as vLLM.
        /// </summary>
        private sealed class FakeOllama : IDisposable
        {
            private readonly object _Lock = new object();
            private readonly List<string[]> _Models = new List<string[]>
            {
                new[] { "llama3.2:latest", "2147483648", "llama", "3.2B", "Q4_K_M" },
                new[] { "nomic-embed-text:latest", "274302450", "nomic-bert", "137M", "F16" },
                new[] { "qwen3:8b", "5225388164", "qwen3", "8.2B", "Q4_K_M" }
            };

            private readonly FakeProviderServer _Server;

            internal FakeOllama()
            {
                _Server = new FakeProviderServer(Handle);
            }

            internal string BaseUrl
            {
                get { return _Server.BaseUrl; }
            }

            internal List<string> Deleted { get; } = new List<string>();

            public void Dispose()
            {
                _Server.Dispose();
            }

            private FakeProviderResponse Handle(FakeProviderRequest request)
            {
                if (request.Path == "/vllm/v1/models") return FakeProviderResponse.Json("{\"object\":\"list\",\"data\":[{\"id\":\"mistral-7b\",\"object\":\"model\",\"owned_by\":\"vllm\"}]}");
                if (request.Path == "/api/tags")
                {
                    lock (_Lock)
                    {
                        string models = String.Join(",", _Models.Select(item => "{\"name\":\"" + item[0] + "\",\"model\":\"" + item[0] + "\",\"size\":" + item[1] + ",\"modified_at\":\"2026-01-02T03:04:05Z\",\"details\":{\"family\":\"" + item[2] + "\",\"parameter_size\":\"" + item[3] + "\",\"quantization_level\":\"" + item[4] + "\"}}"));
                        return FakeProviderResponse.Json("{\"models\":[" + models + "]}");
                    }
                }

                if (request.Path == "/api/delete")
                {
                    lock (_Lock)
                    {
                        string name = _Models.Select(item => item[0]).FirstOrDefault(item => request.Body.Contains("\"" + item + "\"", StringComparison.Ordinal)) ?? String.Empty;
                        if (name.Length == 0) return FakeProviderResponse.Json("{\"error\":\"model not found\"}", 404);
                        _Models.RemoveAll(item => item[0] == name);
                        Deleted.Add(name);
                        return FakeProviderResponse.Json("{}");
                    }
                }

                if (request.Path == "/api/pull")
                {
                    bool slow = request.Body.Contains("slow:latest", StringComparison.Ordinal);
                    string model = slow ? "slow:latest" : "tiny:latest";
                    List<string> chunks = new List<string> { "{\"status\":\"pulling manifest\"}\n" };
                    int steps = slow ? 150 : 3;
                    for (int i = 1; i <= steps; i++)
                    {
                        chunks.Add("{\"status\":\"pulling abc\",\"digest\":\"sha256:abc\",\"total\":1000,\"completed\":" + (i * 1000 / steps) + "}\n");
                    }

                    chunks.Add("{\"status\":\"success\"}\n");
                    lock (_Lock)
                    {
                        if (!slow && !_Models.Any(item => item[0] == model)) _Models.Add(new[] { model, "1000", "tiny", "1M", "Q8_0" });
                    }

                    return new FakeProviderResponse { ContentType = "application/x-ndjson", Chunks = chunks, ChunkDelayMs = slow ? 200 : 50 };
                }

                return FakeProviderResponse.Json("{\"error\":\"not found\"}", 404);
            }
        }

        #endregion
    }
}
