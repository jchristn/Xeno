namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Provider model commands: list a provider's live models, pull, delete, and import (advertise) them; and the shared
    /// table printer every CLI table uses for --columns.
    /// </summary>
    public static partial class Program
    {
        private const int PullPlainLineIntervalMs = 5000;
        private const int PullRedrawIntervalMs = 500;

        private static string? _Columns = null;

        private static bool IsProviderModelCommand(string value)
        {
            string sub = value.ToLowerInvariant();
            return sub == "models" || sub == "pull" || sub == "delete-model" || sub == "remove-model" || sub == "rm-model";
        }

        private static async Task<int> HandleProviderModelCommandAsync(string[] args)
        {
            if (args.Skip(2).Any(IsHelp))
            {
                PrintProviderHelp();
                return 0;
            }

            string sub = args[1].ToLowerInvariant();
            if (sub == "models") return await HandleProviderModelsAsync(args).ConfigureAwait(false);
            if (sub == "pull") return await HandleProviderPullAsync(args).ConfigureAwait(false);
            if (sub == "pulls") return await HandleProviderPullsAsync(args).ConfigureAwait(false);
            return await HandleProviderDeleteModelAsync(args).ConfigureAwait(false);
        }

        private static async Task<int> HandleProviderModelsAsync(string[] args)
        {
            List<string> positional = new List<string>();
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    WriteError("Unknown option for 'xeno provider models': " + args[i]);
                    return 1;
                }

                positional.Add(args[i]);
            }

            if (positional.Count != 1)
            {
                WriteError("Usage: xeno provider models <provider> [--columns <names>] [--json]");
                return 1;
            }

            using (XenoWorkspace workspace = new XenoWorkspace())
            {
                ProviderModelInventory inventory = await workspace.GetProviderInventoryAsync(positional[0], CancellationToken.None).ConfigureAwait(false);
                if (_UseJson)
                {
                    WriteJson(inventory);
                    return inventory.Reachable ? 0 : 1;
                }

                if (!inventory.Reachable)
                {
                    WriteError("Could not list the models on " + inventory.Provider + ": " + (inventory.Error ?? "the provider did not answer."));
                    return 1;
                }

                CliTable table = BuildInventoryTable(inventory);
                List<string> lines = table.Render(_Columns);
                WriteInfoHeading("Models on " + inventory.Provider + " (" + inventory.ApiType + ", " + inventory.Endpoint + ")");
                if (inventory.Models.Count == 0)
                {
                    Console.WriteLine("  The provider reports no models." + (inventory.CanPull ? " Pull one with 'xeno provider pull " + inventory.Provider + " <model>'." : String.Empty));
                    return 0;
                }

                foreach (string line in lines) Console.WriteLine(line);
                int notAdvertised = inventory.Models.Count(item => !item.Advertised);
                if (notAdvertised > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  " + notAdvertised.ToString(CultureInfo.InvariantCulture) + " not advertised. Advertise with 'xeno model import " + inventory.Provider + " --all' or '--model <name>'.");
                }

                return 0;
            }
        }

        private static CliTable BuildInventoryTable(ProviderModelInventory inventory)
        {
            CliTable table = new CliTable()
                .AddColumn("name", "NAME", 40)
                .AddColumn("size", "SIZE", 0, true)
                .AddColumn("params", "PARAMS", 10)
                .AddColumn("quant", "QUANT", 10)
                .AddColumn("modified", "MODIFIED")
                .AddColumn("advertised", "ADVERTISED")
                .AddColumn("family", "FAMILY", 16, false, false)
                .AddColumn("kind", "KIND", 0, false, false)
                .AddColumn("remote", "REMOTE", 0, false, false)
                .AddColumn("digest", "DIGEST", 0, false, false);
            foreach (ProviderInventoryModel model in inventory.Models)
            {
                table.AddRow(
                    model.Name,
                    model.SizeBytes.HasValue ? DisplayFormat.Bytes(model.SizeBytes) : "-",
                    DisplayFormat.OrDash(model.ParameterSize),
                    DisplayFormat.OrDash(model.Quantization),
                    FormatModifiedDate(model.Modified),
                    FormatBool(model.Advertised),
                    DisplayFormat.OrDash(model.Family),
                    model.LooksLikeEmbeddings ? "embeddings" : "completions",
                    FormatBool(model.Remote),
                    String.IsNullOrWhiteSpace(model.Digest) ? "-" : (model.Digest.Length > 12 ? model.Digest.Substring(0, 12) : model.Digest));
            }

            return table;
        }

        private static string FormatModifiedDate(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "-";
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
            {
                return parsed.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return value.Length > 10 ? value.Substring(0, 10) : value;
        }

        private static async Task<int> HandleProviderPullAsync(string[] args)
        {
            bool advertise = false;
            bool detach = false;
            List<string> positional = new List<string>();
            for (int i = 2; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--advertise", StringComparison.OrdinalIgnoreCase))
                {
                    advertise = true;
                    continue;
                }

                if (String.Equals(args[i], "--detach", StringComparison.OrdinalIgnoreCase))
                {
                    detach = true;
                    continue;
                }

                if (args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    WriteError("Unknown option for 'xeno provider pull': " + args[i]);
                    return 1;
                }

                positional.Add(args[i]);
            }

            if (positional.Count != 2)
            {
                WriteError("Usage: xeno provider pull <provider> <model> [--advertise] [--detach]");
                return 1;
            }

            using (XenoModelPullClient pulls = new XenoModelPullClient())
            {
                bool inWorker = pulls.RunsInWorker;
                if (detach && !inWorker)
                {
                    WriteError("--detach needs the Xeno client running, so the pull has somewhere to keep going. Start it with 'xeno start', or pull without --detach.");
                    return 1;
                }

                ModelPullJob job = await pulls.StartAsync(positional[0], positional[1], advertise, CancellationToken.None).ConfigureAwait(false);
                if (detach)
                {
                    if (_UseJson) WriteJson(job);
                    else WriteSuccess("Pulling " + job.Model + " onto " + job.Provider + " in the Xeno client. Follow it with 'xeno provider pulls'.");
                    return 0;
                }

                string id = job.Id;
                bool cancelRequested = false;
                ConsoleCancelEventHandler onCancel = (sender, e) =>
                {
                    // The first Ctrl+C cancels the pull cleanly; a second one ends the process.
                    if (cancelRequested) return;
                    cancelRequested = true;
                    e.Cancel = true;
                    _ = pulls.CancelAsync(id, CancellationToken.None);
                };

                Console.CancelKeyPress += onCancel;
                try
                {
                    if (!_UseJson)
                    {
                        Console.WriteLine("Pulling " + job.Model + " onto " + job.Provider + (inWorker ? " in the Xeno client" : String.Empty) + ". Press Ctrl+C to cancel" + (inWorker ? ", or rerun with --detach to leave it running." : "."));
                    }

                    job = await FollowPullAsync(pulls, job).ConfigureAwait(false);
                }
                finally
                {
                    Console.CancelKeyPress -= onCancel;
                }

                if (_UseJson)
                {
                    WriteJson(job);
                }
                else if (job.State == ModelPullStateEnum.Succeeded)
                {
                    WriteSuccess("Pulled " + job.Model + " onto " + job.Provider + (job.TotalBytes > 0 ? " (" + DisplayFormat.Bytes(job.TotalBytes) + ")" : String.Empty) + ".");
                    if (job.Advertised) WriteInfo("Advertised " + job.Model + " on " + job.Provider + ".");
                    else if (job.AdvertiseOnSuccess) WriteError(job.Error ?? "Pulled, but the model was not advertised.");
                    else Console.WriteLine("Advertise it with 'xeno model import " + job.Provider + " --model " + job.Model + "'.");
                }
                else if (job.State == ModelPullStateEnum.Cancelled)
                {
                    WriteWarning("Cancelled pulling " + job.Model + " onto " + job.Provider + ".");
                }
                else
                {
                    WriteError("Could not pull " + job.Model + " onto " + job.Provider + ": " + (job.Error ?? "the pull did not finish."));
                }

                if (job.State == ModelPullStateEnum.Succeeded) return job.AdvertiseOnSuccess && !job.Advertised ? 1 : 0;
                return job.State == ModelPullStateEnum.Cancelled ? 130 : 1;
            }
        }

        private static async Task<int> HandleProviderPullsAsync(string[] args)
        {
            bool clear = false;
            for (int i = 2; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--clear", StringComparison.OrdinalIgnoreCase))
                {
                    clear = true;
                    continue;
                }

                WriteError("Usage: xeno provider pulls [--clear]");
                return 1;
            }

            using (XenoModelPullClient pulls = new XenoModelPullClient())
            {
                if (clear)
                {
                    int removed = await pulls.ClearFinishedAsync(CancellationToken.None).ConfigureAwait(false);
                    WriteSuccess("Cleared " + removed + " finished pull(s).");
                    return 0;
                }

                List<ModelPullJob> jobs = await pulls.ListAsync(CancellationToken.None).ConfigureAwait(false);
                if (_UseJson)
                {
                    WriteJson(jobs);
                    return 0;
                }

                if (jobs.Count == 0)
                {
                    Console.WriteLine("No model pulls" + (pulls.RunsInWorker ? "." : " (the Xeno client is not running)."));
                    return 0;
                }

                foreach (ModelPullJob job in jobs)
                {
                    Console.WriteLine(ModelPullProgressText.PlainLine(job));
                }

                return 0;
            }
        }

        private static async Task<ModelPullJob> FollowPullAsync(XenoModelPullClient pulls, ModelPullJob job)
        {
            bool redraw = !_UseJson && !Console.IsOutputRedirected;
            bool plain = !_UseJson && Console.IsOutputRedirected;
            Stopwatch sinceLine = Stopwatch.StartNew();
            string lastStatus = String.Empty;
            string lastDrawn = String.Empty;
            ModelPullJob latest = job;
            ModelPullJob? final = await pulls.WaitAsync(job.Id, current =>
            {
                latest = current;
                if (redraw)
                {
                    string line = ModelPullProgressText.BarLine(current, ConsoleWidth());
                    if (!String.Equals(line, lastDrawn, StringComparison.Ordinal))
                    {
                        Console.Write("\r" + line.PadRight(Math.Max(lastDrawn.Length, line.Length)));
                        lastDrawn = line;
                    }
                }
                else if (plain)
                {
                    bool statusChanged = !String.Equals(current.Status, lastStatus, StringComparison.Ordinal);
                    if (statusChanged || sinceLine.ElapsedMilliseconds >= PullPlainLineIntervalMs || !current.Active)
                    {
                        Console.WriteLine(ModelPullProgressText.PlainLine(current));
                        lastStatus = current.Status;
                        sinceLine.Restart();
                    }
                }
            }, CancellationToken.None).ConfigureAwait(false);
            if (redraw && lastDrawn.Length > 0) Console.WriteLine();
            return final ?? latest;
        }

        private static int ConsoleWidth()
        {
            try
            {
                int width = Console.WindowWidth;
                return width > 0 ? width : 100;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is InvalidOperationException || ex is PlatformNotSupportedException)
            {
                return 100;
            }
        }

        private static async Task<int> HandleProviderDeleteModelAsync(string[] args)
        {
            bool yes = false;
            List<string> positional = new List<string>();
            for (int i = 2; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--yes", StringComparison.OrdinalIgnoreCase) || String.Equals(args[i], "-y", StringComparison.OrdinalIgnoreCase))
                {
                    yes = true;
                    continue;
                }

                if (args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    WriteError("Unknown option for 'xeno provider delete-model': " + args[i]);
                    return 1;
                }

                positional.Add(args[i]);
            }

            if (positional.Count != 2)
            {
                WriteError("Usage: xeno provider delete-model <provider> <model> [--yes]");
                return 1;
            }

            string providerName = positional[0];
            string modelName = positional[1];
            using (XenoWorkspace workspace = new XenoWorkspace())
            {
                ClientRunnerSettings? provider = ProviderConfiguration.FindProvider(workspace.LoadSettings(), providerName);
                if (provider == null)
                {
                    WriteError("Provider endpoint not found: " + providerName);
                    return 1;
                }

                if (!ProviderModelManager.CanManageModels(provider))
                {
                    WriteError("Xeno can delete models only from Ollama providers. '" + provider.Name + "' is " + provider.ApiType + ".");
                    return 1;
                }

                if (!yes)
                {
                    if (_UseJson || Console.IsInputRedirected)
                    {
                        WriteError("Add --yes to delete without a prompt: xeno provider delete-model " + provider.Name + " " + modelName + " --yes");
                        return 1;
                    }

                    Console.Write("Delete " + modelName + " from " + provider.Name + "'s disk" + (ProviderModelManager.IsAdvertised(provider, modelName) ? " and stop advertising it" : String.Empty) + "? [y/N]: ");
                    string? answer = Console.ReadLine();
                    if (!String.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) && !String.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteInfo("Not deleted.");
                        return 1;
                    }
                }

                int removed = await workspace.DeleteProviderModelAsync(provider.Name, modelName, CancellationToken.None).ConfigureAwait(false);
                if (_UseJson)
                {
                    WriteJson(new ProviderModelDeleteResult { Provider = provider.Name, Model = modelName, AdvertisementsRemoved = removed });
                    return 0;
                }

                WriteSuccess("Deleted " + modelName + " from " + provider.Name + ".");
                if (removed > 0) Console.WriteLine("Removed " + removed.ToString(CultureInfo.InvariantCulture) + " advertisement" + (removed == 1 ? String.Empty : "s") + " for " + modelName + ".");
                else Console.WriteLine(modelName + " was not advertised.");
                return 0;
            }
        }

        private static async Task<int> HandleModelImportAsync(string[] args)
        {
            if (args.Skip(2).Any(IsHelp))
            {
                PrintModelHelp();
                return 0;
            }

            bool all = false;
            List<string> providers = new List<string>();
            List<string> models = new List<string>();
            List<string> embeddings = new List<string>();
            for (int i = 2; i < args.Length; i++)
            {
                string option = args[i];
                if (String.Equals(option, "--all", StringComparison.OrdinalIgnoreCase))
                {
                    all = true;
                    continue;
                }

                if (String.Equals(option, "--model", StringComparison.OrdinalIgnoreCase) || String.Equals(option, "--name", StringComparison.OrdinalIgnoreCase) || String.Equals(option, "--embeddings", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        WriteError(option + " requires a model name.");
                        return 1;
                    }

                    List<string> target = String.Equals(option, "--embeddings", StringComparison.OrdinalIgnoreCase) ? embeddings : models;
                    target.AddRange(SplitList(args[++i]));
                    continue;
                }

                if (String.Equals(option, "--provider", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    providers.AddRange(SplitList(args[++i]));
                    continue;
                }

                if (option.StartsWith("--", StringComparison.Ordinal))
                {
                    WriteError("Unknown option for 'xeno model import': " + option);
                    return 1;
                }

                providers.AddRange(SplitList(option));
            }

            providers = providers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (providers.Count == 0)
            {
                WriteError("Usage: xeno model import <provider>[,<provider>...] [--all | --model <name> ...] [--embeddings <name> ...]");
                return 1;
            }

            if (all && models.Count > 0)
            {
                WriteError("Use either --all or --model, not both.");
                return 1;
            }

            using (XenoWorkspace workspace = new XenoWorkspace())
            {
                List<ProviderModelInventory> inventories = new List<ProviderModelInventory>();
                List<string> unreachable = new List<string>();
                foreach (string providerName in providers)
                {
                    ProviderModelInventory inventory = await workspace.GetProviderInventoryAsync(providerName, CancellationToken.None).ConfigureAwait(false);
                    if (inventory.Reachable) inventories.Add(inventory);
                    else unreachable.Add(inventory.Provider + ": " + (inventory.Error ?? "the provider did not answer"));
                }

                bool select = all || models.Count > 0 || embeddings.Count > 0;
                if (!select) return PrintImportCandidates(inventories, unreachable, providers);

                ModelImportResult notFound = new ModelImportResult();
                List<ModelImportItem> items = new List<ModelImportItem>();
                foreach (ProviderModelInventory inventory in inventories)
                {
                    foreach (ProviderInventoryModel model in inventory.Models)
                    {
                        bool namedEmbeddings = embeddings.Any(name => SameModel(name, model.Name));
                        if (!all && !namedEmbeddings && !models.Any(name => SameModel(name, model.Name))) continue;
                        bool isEmbeddings = namedEmbeddings || model.LooksLikeEmbeddings;
                        items.Add(new ModelImportItem { Provider = inventory.Provider, Name = model.Name, SupportsCompletions = !isEmbeddings, SupportsEmbeddings = isEmbeddings });
                    }
                }

                foreach (string name in models.Concat(embeddings).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!inventories.Any(inventory => inventory.Models.Any(model => SameModel(name, model.Name))))
                    {
                        notFound.Failed.Add(name + ": not found on " + String.Join(", ", providers));
                    }
                }

                ModelImportResult result = items.Count > 0
                    ? await workspace.ImportModelsAsync(items, CancellationToken.None).ConfigureAwait(false)
                    : new ModelImportResult();
                result.Failed.AddRange(notFound.Failed);
                result.Failed.AddRange(unreachable);
                if (_UseJson)
                {
                    WriteJson(result);
                    return result.Failed.Count == 0 ? 0 : 1;
                }

                WriteInfoHeading("Model import");
                PrintImportGroup("Added", result.Added);
                PrintImportGroup("Skipped (already advertised)", result.Skipped);
                PrintImportGroup("Failed", result.Failed);
                if (result.Added.Count == 0 && result.Skipped.Count == 0 && result.Failed.Count == 0) Console.WriteLine("  Nothing to import.");
                return result.Failed.Count == 0 ? 0 : 1;
            }
        }

        private static int PrintImportCandidates(List<ProviderModelInventory> inventories, List<string> unreachable, List<string> providers)
        {
            if (_UseJson)
            {
                WriteJson(new
                {
                    candidates = inventories.SelectMany(inventory => inventory.Models.Select(model => new
                    {
                        provider = inventory.Provider,
                        name = model.Name,
                        sizeBytes = model.SizeBytes,
                        parameterSize = model.ParameterSize,
                        advertised = model.Advertised,
                        looksLikeEmbeddings = model.LooksLikeEmbeddings
                    })).ToList(),
                    failed = unreachable
                });
                return unreachable.Count == 0 ? 0 : 1;
            }

            CliTable table = new CliTable()
                .AddColumn("provider", "PROVIDER", 20)
                .AddColumn("name", "NAME", 40)
                .AddColumn("size", "SIZE", 0, true)
                .AddColumn("params", "PARAMS", 10)
                .AddColumn("kind", "KIND")
                .AddColumn("advertised", "ADVERTISED")
                .AddColumn("quant", "QUANT", 10, false, false)
                .AddColumn("family", "FAMILY", 16, false, false);
            foreach (ProviderModelInventory inventory in inventories)
            {
                foreach (ProviderInventoryModel model in inventory.Models)
                {
                    table.AddRow(
                        inventory.Provider,
                        model.Name,
                        model.SizeBytes.HasValue ? DisplayFormat.Bytes(model.SizeBytes) : "-",
                        DisplayFormat.OrDash(model.ParameterSize),
                        model.LooksLikeEmbeddings ? "embeddings" : "completions",
                        FormatBool(model.Advertised),
                        DisplayFormat.OrDash(model.Quantization),
                        DisplayFormat.OrDash(model.Family));
                }
            }

            List<string> lines = table.Render(_Columns);
            WriteInfoHeading("Models found on " + String.Join(", ", providers));
            foreach (string error in unreachable) WriteError(error);
            if (table.Rows.Count == 0) Console.WriteLine("  No models found.");
            else foreach (string line in lines) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine("  Nothing was imported. Pick models to advertise:");
            Console.WriteLine("    xeno model import " + String.Join(",", providers) + " --all");
            Console.WriteLine("    xeno model import " + String.Join(",", providers) + " --model <name> [--model <name> ...]");
            Console.WriteLine("  Add --embeddings <name> to import a model as an embedding model.");
            return unreachable.Count == 0 ? 0 : 1;
        }

        private static void PrintImportGroup(string label, List<string> items)
        {
            if (items.Count == 0) return;
            Console.WriteLine("  " + label + " (" + items.Count.ToString(CultureInfo.InvariantCulture) + "):");
            foreach (string item in items) Console.WriteLine("    " + item);
        }

        private static bool SameModel(string requested, string reported)
        {
            return LocalModelNameNormalizer.AreEqual(requested, reported) || String.Equals(requested, reported, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitList(string value)
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string? ReadColumnsOption(string[] args, ref int index)
        {
            string arg = args[index];
            if (arg.StartsWith("--columns=", StringComparison.OrdinalIgnoreCase)) return arg.Substring("--columns=".Length);
            if (index + 1 >= args.Length) throw new ArgumentException("--columns requires a comma-separated list of column names.");
            return args[++index];
        }

        private static void PrintTable(CliTable table)
        {
            foreach (string line in table.Render(_Columns)) Console.WriteLine(line);
        }
    }
}
