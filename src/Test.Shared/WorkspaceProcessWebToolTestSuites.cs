namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Tools.Workspace;
    using Xeno.Client.Tools.Workspace.Processes;
    using Xeno.Client.Tools.Workspace.Web;

    /// <summary>
    /// The workspace process and web tools ported from Mux: run_process (argument list or explicit shell, timeout kills
    /// the process tree, working directory confined), the background process tools and their per-conversation
    /// registry, web_retrieve (HTML to text, size caps, private addresses refused), and web_search over Tavily and
    /// You.com. HTTP is faked; no test touches the network.
    /// </summary>
    public static class WorkspaceProcessWebToolTestSuites
    {
        #region Private-Members

        private static readonly bool _IsWindows = OperatingSystem.IsWindows();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Process and web tool suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ProcessWebToolSuite()
        {
            const string suite = "WorkspaceProcessWebTools";
            return new TestSuiteDescriptor(suite, "Workspace process and web tools", new List<TestCaseDescriptor>
            {
                Case(suite, "ToolsAreDescribed", "Process and web tools keep Mux's names, describe their arguments, and mark what changes things", () =>
                {
                    using (BackgroundProcessRegistry registry = new BackgroundProcessRegistry())
                    {
                        IReadOnlyList<IWorkspaceTool> processes = ProcessTools.Create(registry);
                        IReadOnlyList<IWorkspaceTool> web = WebTools.Create(new FakeWebHandler());
                        Check(String.Join(",", processes.Select(tool => tool.Name)) == "run_process,process_start,process_output,process_list,process_stop", "Process tool names are wrong.");
                        Check(String.Join(",", web.Select(tool => tool.Name)) == "web_retrieve,web_search", "Web tool names are wrong.");
                        Check(processes.Where(tool => tool.Mutating).Select(tool => tool.Name).SequenceEqual(new[] { "run_process", "process_start", "process_stop" }), "Process mutating flags are wrong.");
                        Check(web.All(tool => !tool.Mutating), "Web tools should be read-only.");
                        Check(processes.Concat(web).All(tool => tool.Description.Length > 20 && tool.Schema.Required.All(required => tool.Schema.Properties.ContainsKey(required))), "A tool lacks a description or requires an undeclared argument.");
                        Check(processes[0].Schema.Properties.ContainsKey("args") && processes[0].Schema.Properties.ContainsKey("shell") && processes[0].Schema.Properties.ContainsKey("timeout_ms"), "run_process lacks args, shell, or timeout_ms.");
                    }

                    return Task.CompletedTask;
                }),
                Case(suite, "RunProcessCapturesOutput", "run_process runs a program with an argument list, and with shell returns stdout, stderr, and the exit code", async () =>
                {
                    using (TempDirectory temp = new TempDirectory())
                    {
                        RunProcessTool tool = new RunProcessTool();
                        WorkspaceToolContext context = Context(temp.Path, "c1");
                        RunProcessResult version = (RunProcessResult)await tool.InvokeAsync(Json(new RunProcessArguments { Command = DotnetPath(), Args = new List<string> { "--version" } }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(version.ExitCode == 0 && !version.TimedOut && version.Stdout.Trim().Length > 0, "dotnet --version did not run: " + version.Stderr);

                        string line = _IsWindows ? "echo hello& echo oops 1>&2& exit /b 3" : "echo hello; echo oops 1>&2; exit 3";
                        RunProcessResult shell = (RunProcessResult)await tool.InvokeAsync(Json(new RunProcessArguments { Command = line, Shell = true }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(shell.ExitCode == 3 && shell.Stdout.Contains("hello", StringComparison.Ordinal) && shell.Stderr.Contains("oops", StringComparison.Ordinal), "Shell output or exit code is wrong: " + shell.ExitCode);

                        await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync(Json(new RunProcessArguments { Command = "echo hi" }), context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync("{}", context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<InvalidOperationException>(() => tool.InvokeAsync(Json(new RunProcessArguments { Command = "xeno-no-such-program-" + Guid.NewGuid().ToString("N") }), context, CancellationToken.None)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "RunProcessBoundsOutput", "run_process keeps output within the tool output limit and says it was truncated", async () =>
                {
                    using (TempDirectory temp = new TempDirectory())
                    {
                        WorkspaceToolContext context = Context(temp.Path, "c1");
                        context.Settings.MaxToolOutputCharacters = 1000;
                        string line = _IsWindows ? "for /l %i in (1,1,2000) do @echo xxxxxxxxxx" : "i=0; while [ $i -lt 2000 ]; do echo xxxxxxxxxx; i=$((i+1)); done";
                        RunProcessResult result = (RunProcessResult)await new RunProcessTool().InvokeAsync(Json(new RunProcessArguments { Command = line, Shell = true }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(result.ExitCode == 0 && result.Stdout.Contains("[truncated", StringComparison.Ordinal) && result.Stdout.Length < 1200, "Output was not bounded: " + result.Stdout.Length);
                    }
                }),
                Case(suite, "RunProcessTimeoutKillsTree", "run_process kills the process and its children on timeout and stops on cancellation", async () =>
                {
                    using (TempDirectory temp = new TempDirectory())
                    {
                        RunProcessTool tool = new RunProcessTool();
                        WorkspaceToolContext context = Context(temp.Path, "c1");
                        string line = _IsWindows ? "ping -n 30 127.0.0.1 > nul" : "sleep 30 & echo $! > child.pid; wait";
                        Stopwatch watch = Stopwatch.StartNew();
                        RunProcessResult result = (RunProcessResult)await tool.InvokeAsync(Json(new RunProcessArguments { Command = line, Shell = true, TimeoutMs = 1000 }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(result.TimedOut && result.ExitCode == -1 && watch.Elapsed < TimeSpan.FromSeconds(20), "The process did not time out: " + watch.Elapsed);
                        if (!_IsWindows)
                        {
                            int child = Int32.Parse(File.ReadAllText(Path.Combine(temp.Path, "child.pid")).Trim());
                            Check(await ProcessGoneAsync(child, 5000).ConfigureAwait(false), "The child process survived the timeout.");
                        }

                        using (CancellationTokenSource cancel = new CancellationTokenSource(500))
                        {
                            string sleep = _IsWindows ? "ping -n 30 127.0.0.1 > nul" : "sleep 30";
                            watch.Restart();
                            await ExpectThrowsAsync<OperationCanceledException>(() => tool.InvokeAsync(Json(new RunProcessArguments { Command = sleep, Shell = true }), context, cancel.Token)).ConfigureAwait(false);
                            Check(watch.Elapsed < TimeSpan.FromSeconds(20), "Cancellation did not stop the process promptly.");
                        }
                    }
                }),
                Case(suite, "RunProcessConfinesWorkingDirectory", "run_process runs in a subdirectory and refuses a working directory outside the conversation's", async () =>
                {
                    using (TempDirectory temp = new TempDirectory())
                    using (TempDirectory outside = new TempDirectory())
                    {
                        Directory.CreateDirectory(Path.Combine(temp.Path, "sub"));
                        RunProcessTool tool = new RunProcessTool();
                        WorkspaceToolContext context = Context(temp.Path, "c1");
                        RunProcessResult result = (RunProcessResult)await tool.InvokeAsync(Json(new RunProcessArguments { Command = _IsWindows ? "cd" : "pwd", Shell = true, WorkingDirectory = "sub" }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(result.ExitCode == 0 && result.Stdout.Trim().EndsWith("sub", StringComparison.OrdinalIgnoreCase), "The process did not run in the subdirectory: " + result.Stdout);
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => tool.InvokeAsync(Json(new RunProcessArguments { Command = DotnetPath(), Args = new List<string> { "--version" }, WorkingDirectory = ".." }), context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => tool.InvokeAsync(Json(new RunProcessArguments { Command = DotnetPath(), Args = new List<string> { "--version" }, WorkingDirectory = outside.Path }), context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync(Json(new RunProcessArguments { Command = DotnetPath(), Args = new List<string> { "--version" }, WorkingDirectory = "missing" }), context, CancellationToken.None)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "BackgroundProcessLifecycle", "process_start waits for readiness, process_output reads new output, process_list is per conversation, and process_stop stops", async () =>
                {
                    using (TempDirectory temp = new TempDirectory())
                    using (BackgroundProcessRegistry registry = new BackgroundProcessRegistry())
                    {
                        IReadOnlyList<IWorkspaceTool> tools = ProcessTools.Create(registry);
                        IWorkspaceTool start = Find(tools, "process_start");
                        IWorkspaceTool output = Find(tools, "process_output");
                        IWorkspaceTool list = Find(tools, "process_list");
                        IWorkspaceTool stop = Find(tools, "process_stop");
                        WorkspaceToolContext context = Context(temp.Path, "c1");
                        WorkspaceToolContext other = Context(temp.Path, "c2");

                        string server = _IsWindows ? "echo ready& ping -n 30 127.0.0.1 > nul" : "echo ready; sleep 30";
                        ProcessStartResult started = (ProcessStartResult)await start.InvokeAsync(Json(new ProcessStartArguments { Command = server, Shell = true, Name = "web", WaitFor = "ready", TimeoutMs = 15000 }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(started.Id == "p1" && started.Ready == true && started.Running && started.Output != null && started.Output.Contains("ready", StringComparison.Ordinal), "process_start did not see readiness.");

                        string staged = _IsWindows ? "echo a& ping -n 2 127.0.0.1 > nul& echo b& ping -n 30 127.0.0.1 > nul" : "echo a; sleep 1; echo b; sleep 30";
                        ProcessStartResult second = (ProcessStartResult)await start.InvokeAsync(Json(new ProcessStartArguments { Command = staged, Shell = true }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(second.Id == "p2" && second.Next != null, "The second process did not start.");
                        ProcessOutputResult read = (ProcessOutputResult)await output.InvokeAsync(Json(new ProcessOutputArguments { Id = "p2", WaitFor = "^b", TimeoutMs = 15000 }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(read.Matched == true && read.Running && read.Output.Contains("a", StringComparison.Ordinal) && read.Output.Contains("b", StringComparison.Ordinal), "process_output did not wait for b.");
                        ProcessOutputResult empty = (ProcessOutputResult)await output.InvokeAsync(Json(new ProcessOutputArguments { Id = "P2" }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(empty.Output.Length == 0 && empty.Matched == null, "A second read returned old output.");

                        ProcessListResult listed = (ProcessListResult)await list.InvokeAsync("{}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(listed.Processes.Count == 2 && listed.Processes[0].Name == "web" && listed.Processes.All(item => item.Running && item.Pid > 0), "process_list is wrong.");
                        ProcessListResult otherList = (ProcessListResult)await list.InvokeAsync("{}", other, CancellationToken.None).ConfigureAwait(false);
                        Check(otherList.Processes.Count == 0, "Another conversation sees this conversation's processes.");
                        await ExpectThrowsAsync<KeyNotFoundException>(() => output.InvokeAsync(Json(new ProcessOutputArguments { Id = "p1" }), other, CancellationToken.None)).ConfigureAwait(false);

                        int pid = started.Pid;
                        ProcessStopResult stopped = (ProcessStopResult)await stop.InvokeAsync(Json(new ProcessStopArguments { Id = "p1" }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(stopped.WasRunning == true && stopped.Running == false, "process_stop did not stop p1.");
                        Check(await ProcessGoneAsync(pid, 5000).ConfigureAwait(false), "p1 is still alive.");
                        ProcessStopResult all = (ProcessStopResult)await stop.InvokeAsync(Json(new ProcessStopArguments { Id = "all" }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(all.Stopped == 1, "process_stop all stopped " + all.Stopped + ".");
                        listed = (ProcessListResult)await list.InvokeAsync("{}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(listed.Processes.All(item => !item.Running && item.StoppedByUser), "Stopped processes are not marked.");

                        ProcessStartResult quick = (ProcessStartResult)await start.InvokeAsync(Json(new ProcessStartArguments { Command = DotnetPath(), Args = new List<string> { "--version" }, WaitFor = "[0-9]+\\.[0-9]+", TimeoutMs = 30000 }), context, CancellationToken.None).ConfigureAwait(false);
                        ProcessOutputResult finished = (ProcessOutputResult)await output.InvokeAsync(Json(new ProcessOutputArguments { Id = quick.Id, WaitFor = "never-printed", TimeoutMs = 30000 }), context, CancellationToken.None).ConfigureAwait(false);
                        Check(!finished.Running && finished.ExitCode == 0 && finished.Matched == false, "An exited process is not reported as exited with code 0.");

                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => start.InvokeAsync(Json(new ProcessStartArguments { Command = DotnetPath(), Args = new List<string> { "--version" }, WorkingDirectory = ".." }), context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => output.InvokeAsync(Json(new ProcessOutputArguments { Id = quick.Id, WaitFor = "(" }), context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<KeyNotFoundException>(() => stop.InvokeAsync(Json(new ProcessStopArguments { Id = "p99" }), context, CancellationToken.None)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "BackgroundRegistryLimitsAndDispose", "The registry caps running processes per conversation and Dispose kills every process", async () =>
                {
                    using (TempDirectory temp = new TempDirectory())
                    {
                        string sleep = _IsWindows ? "ping -n 60 127.0.0.1 > nul" : "sleep 60";
                        BackgroundProcessRegistry registry = new BackgroundProcessRegistry(maxConcurrent: 1);
                        BackgroundProcessInfo first = registry.Start("c1", sleep, null, true, temp.Path);
                        BackgroundProcessInfo second = registry.Start("c2", sleep, null, true, temp.Path);
                        ExpectThrows<InvalidOperationException>(() => registry.Start("c1", sleep, null, true, temp.Path));
                        Check(registry.CountRunning() == 2 && registry.List("c1").Single().Id == "p1" && registry.List("c2").Single().Id == "p1", "Each conversation should have its own p1.");

                        registry.Dispose();
                        Check(await ProcessGoneAsync(first.ProcessId, 5000).ConfigureAwait(false) && await ProcessGoneAsync(second.ProcessId, 5000).ConfigureAwait(false), "Dispose left a process running.");
                        ExpectThrows<InvalidOperationException>(() => registry.Start("c1", sleep, null, true, temp.Path));
                        registry.Dispose();
                    }
                }),
                Case(suite, "WebRetrieveConvertsHtml", "web_retrieve follows redirects, turns HTML into text, and caps the size", async () =>
                {
                    StringBuilder big = new StringBuilder("<html><body><p>");
                    big.Append('x', WebRetrieveTool.MaxDownloadBytes + 1000);
                    big.Append("</p></body></html>");
                    FakeWebHandler handler = new FakeWebHandler()
                        .On("http://example.test/start", request => Redirect("/page"))
                        .On("http://example.test/page", request => Html("<html><head><title>Test &amp; Page</title><style>.x{color:red}</style></head><body><script>let secret = 1;</script><h1>Heading</h1><p>Hello &amp; welcome</p><ul><li>one</li><li>two</li></ul><!-- hidden --></body></html>"))
                        .On("http://example.test/long", request => Html("<p>" + new string('y', 500) + "</p>"))
                        .On("http://example.test/big", request => Html(big.ToString()))
                        .On("http://example.test/data.json", request => Text("{\"a\":1}", "application/json"))
                        .On("http://example.test/image.png", request => Text("not really", "image/png"));
                    IWorkspaceTool tool = Find(WebTools.Create(handler), "web_retrieve");
                    WorkspaceToolContext context = Context(Path.GetTempPath(), "c1");

                    WebRetrieveResult page = (WebRetrieveResult)await tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/start", IncludeHtml = true }), context, CancellationToken.None).ConfigureAwait(false);
                    Check(page.Status == 200 && page.FinalUrl == "http://example.test/page" && page.Url == "http://example.test/start", "Redirect was not followed: " + page.FinalUrl);
                    Check(page.Title == "Test & Page", "Title is wrong: " + page.Title);
                    Check(page.Text.Contains("Heading", StringComparison.Ordinal) && page.Text.Contains("Hello & welcome", StringComparison.Ordinal) && page.Text.Contains("one\n", StringComparison.Ordinal), "Text is wrong: " + page.Text);
                    Check(!page.Text.Contains("secret", StringComparison.Ordinal) && !page.Text.Contains("color", StringComparison.Ordinal) && !page.Text.Contains("hidden", StringComparison.Ordinal) && !page.Text.Contains("<", StringComparison.Ordinal), "Text has scripts, styles, comments, or tags: " + page.Text);
                    Check(page.Html != null && page.Html.Contains("<h1>", StringComparison.Ordinal) && page.HtmlTruncated == false, "HTML was not included.");

                    WebRetrieveResult capped = (WebRetrieveResult)await tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/long", MaxContentChars = 50 }), context, CancellationToken.None).ConfigureAwait(false);
                    Check(capped.Text.Length == 50 && capped.TextTruncated && capped.Html == null, "max_content_chars was not applied.");
                    WebRetrieveResult huge = (WebRetrieveResult)await tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/big", MaxContentChars = 500000 }), context, CancellationToken.None).ConfigureAwait(false);
                    Check(huge.TextTruncated && huge.Text.Length == 500000, "The download cap was not applied.");
                    WebRetrieveResult json = (WebRetrieveResult)await tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/data.json" }), context, CancellationToken.None).ConfigureAwait(false);
                    Check(json.Text == "{\"a\":1}" && json.Title.Length == 0, "JSON was not returned as text.");
                    await ExpectThrowsAsync<InvalidOperationException>(() => tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/image.png" }), context, CancellationToken.None)).ConfigureAwait(false);
                    await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "file:///etc/passwd" }), context, CancellationToken.None)).ConfigureAwait(false);
                    await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync("{}", context, CancellationToken.None)).ConfigureAwait(false);
                }),
                Case(suite, "WebRetrieveRefusesPrivateAddresses", "web_retrieve refuses loopback, link-local, and private addresses, also on redirects, unless allowed", async () =>
                {
                    FakeWebHandler handler = new FakeWebHandler()
                        .On("http://example.test/sneaky", request => Redirect("http://127.0.0.1/admin"))
                        .On("http://127.0.0.1/admin", request => Html("<p>admin</p>"));
                    IWorkspaceTool tool = Find(WebTools.Create(handler), "web_retrieve");
                    WorkspaceToolContext context = Context(Path.GetTempPath(), "c1");
                    string[] refused = { "http://127.0.0.1/admin", "http://[::1]/", "http://10.0.0.5/", "http://172.20.1.1/", "http://192.168.1.1/", "http://169.254.169.254/latest/meta-data", "http://localhost:8080/", "http://app.localhost/", "http://0.0.0.0/", "http://[fd00::1]/", "http://[::ffff:127.0.0.1]/" };
                    foreach (string url in refused)
                    {
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => tool.InvokeAsync(Json(new WebRetrieveArguments { Url = url }), context, CancellationToken.None)).ConfigureAwait(false);
                    }

                    Check(handler.Requests.Count == 0, "A refused address reached the network.");
                    await ExpectThrowsAsync<UnauthorizedAccessException>(() => tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/sneaky" }), context, CancellationToken.None)).ConfigureAwait(false);
                    Check(handler.Requests.Count == 1, "The redirect to loopback was followed.");

                    context.Settings.WebRetrieveAllowPrivateNetworks = true;
                    WebRetrieveResult allowed = (WebRetrieveResult)await tool.InvokeAsync(Json(new WebRetrieveArguments { Url = "http://example.test/sneaky" }), context, CancellationToken.None).ConfigureAwait(false);
                    Check(allowed.FinalUrl == "http://127.0.0.1/admin" && allowed.Text == "admin", "Allowed private access did not work.");

                    Check(WebAddressPolicy.IsNonPublic(IPAddress.Parse("100.64.0.1")) && WebAddressPolicy.IsNonPublic(IPAddress.Parse("fe80::1")) && WebAddressPolicy.IsNonPublic(IPAddress.Parse("224.0.0.1")), "Non-public ranges are not refused.");
                    Check(!WebAddressPolicy.IsNonPublic(IPAddress.Parse("8.8.8.8")) && !WebAddressPolicy.IsNonPublic(IPAddress.Parse("2606:4700:4700::1111")) && !WebAddressPolicy.IsNonPublic(IPAddress.Parse("172.32.0.1")), "Public addresses are refused.");
                }),
                Case(suite, "WebSearchTavily", "web_search sends Tavily requests with a bearer key and maps answers, results, and images", async () =>
                {
                    string body = "{\"query\":\"xeno\",\"answer\":\"An answer.\",\"request_id\":\"req-1\",\"response_time\":0.42,"
                        + "\"images\":[\"https://img.test/a.png\",{\"url\":\"https://img.test/b.png\",\"description\":\"B\"}],"
                        + "\"results\":[{\"title\":\"One\",\"url\":\"https://one.test\",\"content\":\"First snippet\",\"score\":0.9,\"favicon\":\"https://one.test/f.ico\",\"published_date\":\"Mon, 10 Nov 2025 10:00:00 GMT\"},"
                        + "{\"title\":\"Two\",\"url\":\"https://two.test\",\"content\":\"Second snippet\",\"score\":0.5}],"
                        + "\"auto_parameters\":{\"topic\":\"general\"},\"usage\":{\"credits\":1}}";
                    FakeWebHandler handler = new FakeWebHandler().On("https://api.tavily.com/search", request => Text(body, "application/json"));
                    IWorkspaceTool tool = Find(WebTools.Create(handler), "web_search");
                    WorkspaceToolContext context = Context(Path.GetTempPath(), "c1");
                    context.Settings.WebSearch = new ChatWebSearchSettings { Provider = "Tavily", TavilyApiKey = "tvly-secret", MaxResults = 7 };

                    WebSearchResult result = (WebSearchResult)await tool.InvokeAsync("{\"query\":\"  xeno  \",\"max_results\":3,\"include_domains\":[\"a.test\",\"A.test\",\" \"],\"freshness\":\"week\"}", context, CancellationToken.None).ConfigureAwait(false);
                    FakeWebRequest sent = handler.Requests.Single();
                    Check(sent.Method == "POST" && sent.Authorization == "Bearer tvly-secret", "The Tavily request is wrong.");
                    TavilySearchRequest? payload = JsonSerializer.Deserialize<TavilySearchRequest>(sent.Body);
                    Check(payload != null && payload.Query == "xeno" && payload.MaxResults == 3 && payload.IncludeAnswer == "basic" && payload.TimeRange == "week" && payload.IncludeDomains != null && payload.IncludeDomains.Count == 1 && payload.ExcludeDomains == null, "The Tavily body is wrong: " + sent.Body);
                    Check(result.ProviderName == "Tavily" && result.ProviderType == "tavily" && result.Answer == "An answer." && result.RequestId == "req-1" && result.LatencySeconds == 0.42, "Tavily metadata is wrong.");
                    List<WebSearchResultItem> web = result.Sections["web"];
                    Check(web.Count == 2 && web[0].Title == "One" && web[0].Snippets.Single() == "First snippet" && web[0].Score == 0.9 && web[0].PublishedAt != null && web[1].Url == "https://two.test", "Tavily results are wrong.");
                    Check(result.Images.Count == 2 && result.Images[1].Description == "B", "Tavily images are wrong.");
                    Check(result.Attempts.Single().Success && result.ProvidersTried.Single() == "Tavily" && !result.UsedFallback, "Attempts are wrong.");

                    await tool.InvokeAsync("{\"query\":\"q\",\"include_answer\":false}", context, CancellationToken.None).ConfigureAwait(false);
                    TavilySearchRequest? defaults = JsonSerializer.Deserialize<TavilySearchRequest>(handler.Requests[1].Body);
                    Check(defaults != null && defaults.MaxResults == 7 && defaults.IncludeAnswer == null && !handler.Requests[1].Body.Contains("include_answer", StringComparison.Ordinal), "Configured max results or include_answer false were not used.");

                    FakeWebHandler failing = new FakeWebHandler().On("https://api.tavily.com/search", request => Text("{\"detail\":\"bad key\"}", "application/json", HttpStatusCode.Unauthorized));
                    InvalidOperationException error = await ExpectThrowsAsync<InvalidOperationException>(() => Find(WebTools.Create(failing), "web_search").InvokeAsync("{\"query\":\"q\"}", context, CancellationToken.None)).ConfigureAwait(false);
                    Check(error.Message.Contains("401", StringComparison.Ordinal) && !error.Message.Contains("tvly-secret", StringComparison.Ordinal), "The Tavily error is wrong: " + error.Message);
                }),
                Case(suite, "WebSearchYou", "web_search sends You.com requests with an API key header and maps web and news results", async () =>
                {
                    string body = "{\"results\":{\"web\":[{\"title\":\"Web one\",\"url\":\"https://w.test\",\"description\":\"Desc\",\"snippets\":[\"Snip\"],\"favicon_url\":\"https://w.test/f.ico\",\"page_age\":\"2025-11-01T00:00:00\"}],"
                        + "\"news\":[{\"title\":\"News one\",\"url\":\"https://n.test\",\"description\":\"Fresh\",\"thumbnail_url\":\"https://n.test/t.jpg\"}]},"
                        + "\"metadata\":{\"search_uuid\":\"uuid-1\",\"latency\":0.3}}";
                    FakeWebHandler handler = new FakeWebHandler().On("https://ydc-index.io/v1/search", request => Text(body, "application/json"));
                    IWorkspaceTool tool = Find(WebTools.Create(handler), "web_search");
                    WorkspaceToolContext context = Context(Path.GetTempPath(), "c1");
                    context.Settings.WebSearch = new ChatWebSearchSettings { Provider = "You", YouApiKey = "ydc-secret" };

                    WebSearchResult result = (WebSearchResult)await tool.InvokeAsync("{\"query\":\"xeno\",\"offset\":20,\"exclude_domains\":[\"x.test\"]}", context, CancellationToken.None).ConfigureAwait(false);
                    FakeWebRequest sent = handler.Requests.Single();
                    Check(sent.Method == "POST" && sent.ApiKey == "ydc-secret" && sent.Authorization == null, "The You.com request is wrong.");
                    YouSearchRequest? payload = JsonSerializer.Deserialize<YouSearchRequest>(sent.Body);
                    Check(payload != null && payload.Query == "xeno" && payload.Count == 5 && payload.Offset == 9 && payload.SafeSearch == "moderate" && payload.ExcludeDomains!.Single() == "x.test", "The You.com body is wrong: " + sent.Body);
                    Check(result.ProviderName == "You.com" && result.ProviderType == "you" && result.RequestId == "uuid-1" && result.LatencySeconds == 0.3, "You.com metadata is wrong.");
                    WebSearchResultItem web = result.Sections["web"].Single();
                    WebSearchResultItem news = result.Sections["news"].Single();
                    Check(web.Snippets.SequenceEqual(new[] { "Desc", "Snip" }) && web.FaviconUrl == "https://w.test/f.ico" && web.PublishedAt != null, "You.com web result is wrong.");
                    Check(news.Section == "news" && news.Title == "News one" && news.ThumbnailUrl == "https://n.test/t.jpg", "You.com news result is wrong.");

                    await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync("{\"query\":\"q\",\"include_domains\":[\"a.test\"],\"exclude_domains\":[\"b.test\"]}", context, CancellationToken.None)).ConfigureAwait(false);
                    await ExpectThrowsAsync<InvalidOperationException>(() => tool.InvokeAsync("{\"query\":\"q\",\"provider\":\"tavily\"}", context, CancellationToken.None)).ConfigureAwait(false);
                    await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync("{}", context, CancellationToken.None)).ConfigureAwait(false);
                    context.Settings.WebSearch = new ChatWebSearchSettings();
                    await ExpectThrowsAsync<InvalidOperationException>(() => tool.InvokeAsync("{\"query\":\"q\"}", context, CancellationToken.None)).ConfigureAwait(false);
                    Check(handler.Requests.Count == 1, "A refused search reached the provider.");
                })
            });
        }

        #endregion

        #region Private-Methods

        private static WorkspaceToolContext Context(string directory, string conversationId)
        {
            return new WorkspaceToolContext { ConversationId = conversationId, TurnId = "t1", WorkingDirectory = directory, Settings = new ClientChatSettings() };
        }

        private static string Json(object value)
        {
            return JsonSerializer.Serialize(value, value.GetType());
        }

        private static IWorkspaceTool Find(IReadOnlyList<IWorkspaceTool> tools, string name)
        {
            return tools.Single(tool => tool.Name == name);
        }

        private static string DotnetPath()
        {
            string? host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            return !String.IsNullOrWhiteSpace(host) && File.Exists(host) ? host : "dotnet";
        }

        private static async Task<bool> ProcessGoneAsync(int pid, int timeoutMs)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using (Process process = Process.GetProcessById(pid))
                    {
                        if (process.HasExited) return true;
                    }
                }
                catch (ArgumentException)
                {
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            return false;
        }

        private static HttpResponseMessage Html(string html)
        {
            return Text(html, "text/html");
        }

        private static HttpResponseMessage Text(string text, string mediaType, HttpStatusCode status = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, mediaType), ReasonPhrase = status.ToString() };
        }

        private static HttpResponseMessage Redirect(string location)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        }

        private static TestCaseDescriptor Case(string suite, string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: async delegate
            {
                await body().ConfigureAwait(false);
            });
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

        private static async Task<T> ExpectThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }

        #endregion

        #region Private-Classes

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xeno-procweb-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                try { Directory.Delete(Path, true); } catch (Exception) { }
            }
        }

        private sealed class FakeWebRequest
        {
            public string Method { get; set; } = String.Empty;

            public string Url { get; set; } = String.Empty;

            public string? Authorization { get; set; } = null;

            public string? ApiKey { get; set; } = null;

            public string Body { get; set; } = String.Empty;
        }

        private sealed class FakeWebHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _Routes = new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase);

            public List<FakeWebRequest> Requests { get; } = new List<FakeWebRequest>();

            public FakeWebHandler On(string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _Routes[url] = respond;
                return this;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.AbsoluteUri ?? String.Empty;
                FakeWebRequest record = new FakeWebRequest
                {
                    Method = request.Method.Method,
                    Url = url,
                    Authorization = request.Headers.Authorization?.ToString(),
                    ApiKey = request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? keys) ? keys.FirstOrDefault() : null,
                    Body = request.Content == null ? String.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                };

                lock (Requests) Requests.Add(record);
                if (!_Routes.TryGetValue(url, out Func<HttpRequestMessage, HttpResponseMessage>? respond)) return Text("not found", "text/plain", HttpStatusCode.NotFound);
                return respond(request);
            }
        }

        #endregion
    }
}
