namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client.Tools.Workspace;
    using Xeno.Client.Tools.Workspace.Memory;
    using Xeno.Client.Tools.Workspace.Planning;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Task planning (plan_tasks, update_task) and persistent memory (remember, forget, recall) workspace tools ported
    /// from Mux. Memory tests use temporary directories only.
    /// </summary>
    public static class WorkspacePlanningMemoryToolTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Planning and memory tool suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor PlanningMemoryToolSuite()
        {
            const string suite = "WorkspacePlanningMemory";
            return new TestSuiteDescriptor(suite, "Workspace planning and memory tools", new List<TestCaseDescriptor>
            {
                Case(suite, "ToolsDescribeThemselves", "Planning and memory tools keep Mux's names, schemas, and approval flags", () =>
                {
                    using (TempFolder temp = new TempFolder())
                    {
                        IReadOnlyList<IWorkspaceTool> planning = PlanningTools.Create(new TaskPlanStore());
                        IReadOnlyList<IWorkspaceTool> memory = MemoryTools.Create(Path.Combine(temp.Path, "memory"));
                        Check(String.Join(",", planning.Select(tool => tool.Name)) == "plan_tasks,update_task", "Planning tool names are wrong.");
                        Check(String.Join(",", memory.Select(tool => tool.Name)) == "remember,forget,recall", "Memory tool names are wrong.");
                        Check(planning.All(tool => !tool.Mutating), "Planning tools should not need approval.");
                        Check(memory.Single(tool => tool.Name == "remember").Mutating && memory.Single(tool => tool.Name == "forget").Mutating && !memory.Single(tool => tool.Name == "recall").Mutating, "Memory approval flags are wrong.");
                        Check(planning.Concat(memory).All(tool => tool.Description.Length > 20 && tool.Schema.Required.All(required => tool.Schema.Properties.ContainsKey(required))), "A tool lacks a description or requires an undeclared argument.");

                        IWorkspaceTool plan = planning[0];
                        Check(plan.Schema.Required.SequenceEqual(new[] { "tasks" }) && plan.Schema.Properties["tasks"].Type == "array", "plan_tasks should require a tasks array.");
                        Xeno.Core.Chat.Wire.ChatToolPropertyDto? item = plan.Schema.Properties["tasks"].Items;
                        Check(item != null && item.Type == "object" && item.Properties != null && item.Properties.ContainsKey("dependsOn") && item.Properties["dependsOn"].Items?.Type == "string", "plan_tasks items are wrong.");
                        Check(item != null && item.Required != null && item.Required.SequenceEqual(new[] { "id", "title" }), "plan_tasks items should require id and title.");
                        List<string>? statuses = planning[1].Schema.Properties["status"].Enum;
                        Check(statuses != null && String.Join(",", statuses) == "pending,in_progress,completed,failed,skipped,blocked", "update_task statuses are wrong.");
                        Check(memory[0].Schema.Required.SequenceEqual(new[] { "name", "description" }) && memory[0].Schema.Properties["scope"].Enum!.SequenceEqual(new[] { "project", "global" }), "remember schema is wrong.");
                        Check(memory[2].Schema.Required.Count == 0, "recall takes no required arguments.");
                    }

                    return Task.CompletedTask;
                }),
                Case(suite, "PlanValidation", "plan_tasks rejects empty, duplicate, unknown, self, and cyclic tasks with the problems listed", async () =>
                {
                    TaskPlanStore plans = new TaskPlanStore();
                    PlanTasksTool tool = new PlanTasksTool(plans);
                    WorkspaceToolContext context = Context("cnv_a");

                    TaskPlanErrorResult invalid = (TaskPlanErrorResult)await tool.InvokeAsync("{\"tasks\":[{\"id\":\"t1\",\"title\":\"One\"},{\"id\":\"t1\",\"title\":\"\"},{\"id\":\"\",\"title\":\"No id\"},{\"id\":\"t3\",\"title\":\"Three\",\"dependsOn\":[\"t3\",\"t9\"]}]}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(invalid.Error == "invalid_plan" && invalid.Problems != null, "Invalid plan was accepted.");
                    List<string> problems = invalid.Problems!;
                    Check(problems.Contains("Duplicate task id 't1'.") && problems.Contains("Task 't1' has an empty title.") && problems.Contains("A task has an empty id."), "Id and title problems are missing: " + String.Join(" | ", problems));
                    Check(problems.Contains("Task 't3' depends on itself.") && problems.Contains("Task 't3' depends on unknown task 't9'."), "Dependency problems are missing: " + String.Join(" | ", problems));
                    Check(plans.Get("cnv_a") == null || plans.Get("cnv_a")!.IsEmpty, "An invalid plan was stored.");

                    TaskPlanErrorResult cycle = (TaskPlanErrorResult)await tool.InvokeAsync("{\"tasks\":[{\"id\":\"a\",\"title\":\"A\",\"dependsOn\":[\"c\"]},{\"id\":\"b\",\"title\":\"B\",\"dependsOn\":[\"a\"]},{\"id\":\"c\",\"title\":\"C\",\"dependsOn\":[\"b\"]}]}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(cycle.Problems != null && cycle.Problems.SequenceEqual(new[] { "The plan contains a dependency cycle." }), "Cycle was not reported once.");
                    Check(!TaskPlanValidator.Validate(new List<AgentTask?> { null }).IsValid && TaskPlanValidator.Validate(null).IsValid, "Validator null handling is wrong.");

                    object ok = await tool.InvokeAsync("{\"tasks\":[{\"id\":\"t1\",\"title\":\"Read\"},{\"id\":\"t2\",\"title\":\"Write\",\"dependsOn\":[\"t1\"]}]}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(ok is PlanTasksResult result && result.Ok && result.TaskCount == 2, "Valid plan was not accepted.");
                    Check(JsonUtil.Serialize(ok) == "{\"ok\":true,\"taskCount\":2}", "plan_tasks result shape differs from Mux: " + JsonUtil.Serialize(ok));
                    Check(JsonUtil.Serialize(invalid).StartsWith("{\"error\":\"invalid_plan\",\"problems\":[", StringComparison.Ordinal), "invalid_plan shape differs from Mux: " + JsonUtil.Serialize(invalid));
                    TaskPlan plan = plans.Get("cnv_a")!;
                    Check(plan.TotalCount == 2 && plan.LastChangeKind == TaskPlanChangeKindEnum.PlanCreated && plan.Snapshot()[1].DependsOn.Single() == "t1", "Plan was not stored.");

                    TaskPlanErrorResult disabled = (TaskPlanErrorResult)await tool.InvokeAsync("{\"tasks\":[]}", Context(String.Empty), CancellationToken.None).ConfigureAwait(false);
                    Check(disabled.Error == "task_planning_disabled", "A conversation id should be required.");
                    await ExpectThrowsAsync<ArgumentException>(() => tool.InvokeAsync("not json", context, CancellationToken.None)).ConfigureAwait(false);
                }),
                Case(suite, "TaskUpdates", "update_task advances statuses, stamps times, requires a failure note, and lists valid ids", async () =>
                {
                    TaskPlanStore plans = new TaskPlanStore();
                    IReadOnlyList<IWorkspaceTool> tools = PlanningTools.Create(plans);
                    WorkspaceToolContext context = Context("cnv_u");
                    await tools[0].InvokeAsync("{\"tasks\":[{\"id\":\"t1\",\"title\":\"Read\"},{\"id\":\"t2\",\"title\":\"Write\",\"dependsOn\":[\"t1\"]},{\"id\":\"t3\",\"title\":\"Ship\"}]}", context, CancellationToken.None).ConfigureAwait(false);
                    TaskPlan plan = plans.Get("cnv_u")!;
                    Check(String.Join(",", plan.ReadyTasks().Select(task => task.Id)) == "t1,t3", "Ready tasks are wrong before updates.");

                    object started = await tools[1].InvokeAsync("{\"id\":\"t1\",\"status\":\"in_progress\"}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(JsonUtil.Serialize(started) == "{\"ok\":true,\"id\":\"t1\",\"status\":\"in_progress\"}", "update_task result shape differs from Mux: " + JsonUtil.Serialize(started));
                    AgentTask t1 = plan.Snapshot()[0];
                    Check(t1.Status == AgentTaskStatusEnum.InProgress && t1.StartedUtc != null && t1.CompletedUtc == null, "in_progress did not stamp the start.");

                    await tools[1].InvokeAsync("{\"id\":\"t1\",\"status\":\"completed\",\"note\":\"done\"}", context, CancellationToken.None).ConfigureAwait(false);
                    t1 = plan.Snapshot()[0];
                    Check(t1.Status == AgentTaskStatusEnum.Completed && t1.CompletedUtc != null && t1.DurationMs != null && t1.Note == "done", "completed did not stamp the finish.");
                    Check(String.Join(",", plan.ReadyTasks().Select(task => task.Id)) == "t2,t3" && plan.CompletedCount == 1, "Dependencies were not released.");

                    TaskPlanErrorResult noteRequired = (TaskPlanErrorResult)await tools[1].InvokeAsync("{\"id\":\"t2\",\"status\":\"failed\"}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(noteRequired.Error == "note_required", "A failure without a note was accepted.");
                    await tools[1].InvokeAsync("{\"id\":\"t2\",\"status\":\"failed\",\"note\":\"compiler error\"}", context, CancellationToken.None).ConfigureAwait(false);
                    AgentTask t2 = plan.Snapshot()[1];
                    Check(t2.Status == AgentTaskStatusEnum.Failed && t2.FailureMessage == "compiler error" && t2.DurationMs == null, "Failure was not recorded.");

                    await tools[1].InvokeAsync("{\"id\":\"t3\",\"status\":\"BLOCKED\",\"note\":\"waiting\"}", context, CancellationToken.None).ConfigureAwait(false);
                    await tools[1].InvokeAsync("{\"id\":\"t3\",\"status\":\"blocked\",\"note\":\"still waiting\"}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(plan.LastChangeKind == TaskPlanChangeKindEnum.TaskNoteUpdated && plan.LastChangedTaskId == "t3" && plan.Snapshot()[2].Note == "still waiting", "A note-only change was not recorded.");

                    TaskPlanErrorResult badStatus = (TaskPlanErrorResult)await tools[1].InvokeAsync("{\"id\":\"t3\",\"status\":\"done\"}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(badStatus.Error == "invalid_status" && badStatus.Message!.Contains("'done'", StringComparison.Ordinal), "An unknown status was accepted.");
                    TaskPlanErrorResult noId = (TaskPlanErrorResult)await tools[1].InvokeAsync("{\"status\":\"completed\"}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(noId.Error == "invalid_argument", "A missing id was accepted.");
                    TaskPlanErrorResult unknown = (TaskPlanErrorResult)await tools[1].InvokeAsync("{\"id\":\"t9\",\"status\":\"completed\"}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(unknown.Error == "unknown_task" && unknown.ValidIds != null && unknown.ValidIds.SequenceEqual(new[] { "t1", "t2", "t3" }), "Unknown task did not list the valid ids.");

                    TaskPlanSummary summary = plan.Summary();
                    Check(summary.Total == 3 && summary.Completed == 1 && summary.Failed == 1 && summary.Blocked == 1, "Summary is wrong.");
                    Check(JsonUtil.Serialize(plan.Snapshot()[0]).Contains("\"status\":\"completed\"", StringComparison.Ordinal), "Task status should serialize as Mux's lowercase names.");

                    int version = plan.Version;
                    await tools[0].InvokeAsync("{\"tasks\":[{\"id\":\"n1\",\"title\":\"New\"}]}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(plan.LastChangeKind == TaskPlanChangeKindEnum.PlanReplaced && plan.Version > version && plan.Snapshot().Single().Status == AgentTaskStatusEnum.Pending, "Re-planning did not replace the plan.");
                    await tools[0].InvokeAsync("{\"tasks\":[]}", context, CancellationToken.None).ConfigureAwait(false);
                    Check(plan.IsEmpty && plan.LastChangeKind == TaskPlanChangeKindEnum.PlanCleared, "An empty plan did not clear.");
                    ExpectThrows<TaskPlanValidationException>(() => plan.SetPlan(new List<AgentTask> { new AgentTask { Id = "x" } }));
                }),
                Case(suite, "PlansPerConversation", "Each conversation keeps its own plan, and removing one leaves the others", async () =>
                {
                    TaskPlanStore plans = new TaskPlanStore();
                    IReadOnlyList<IWorkspaceTool> tools = PlanningTools.Create(plans);
                    await tools[0].InvokeAsync("{\"tasks\":[{\"id\":\"a1\",\"title\":\"A\"}]}", Context("cnv_1"), CancellationToken.None).ConfigureAwait(false);
                    await tools[0].InvokeAsync("{\"tasks\":[{\"id\":\"b1\",\"title\":\"B\"},{\"id\":\"b2\",\"title\":\"B2\"}]}", Context("cnv_2"), CancellationToken.None).ConfigureAwait(false);
                    Check(plans.Get("cnv_1")!.Snapshot().Single().Id == "a1" && plans.Get("cnv_2")!.TotalCount == 2, "Plans were mixed between conversations.");

                    TaskPlanErrorResult unknown = (TaskPlanErrorResult)await tools[1].InvokeAsync("{\"id\":\"b1\",\"status\":\"completed\"}", Context("cnv_1"), CancellationToken.None).ConfigureAwait(false);
                    Check(unknown.Error == "unknown_task" && unknown.ValidIds!.SequenceEqual(new[] { "a1" }), "A task from another conversation was updated.");
                    TaskPlanErrorResult none = (TaskPlanErrorResult)await tools[1].InvokeAsync("{\"id\":\"x\",\"status\":\"completed\"}", Context("cnv_3"), CancellationToken.None).ConfigureAwait(false);
                    Check(none.Error == "unknown_task" && none.ValidIds!.Count == 0, "A conversation without a plan should report no tasks.");

                    plans.Remove("cnv_1");
                    plans.Remove("missing");
                    Check(plans.Get("cnv_1") == null && plans.Get("cnv_2") != null && plans.Get(String.Empty) == null, "Remove affected the wrong plans.");
                }),
                Case(suite, "MemorySaveReadListDelete", "remember creates and updates by name, recall reads and searches, forget deletes", async () =>
                {
                    using (TempFolder temp = new TempFolder())
                    {
                        string root = Path.Combine(temp.Path, "memory");
                        string project = temp.Directory("project");
                        IReadOnlyList<IWorkspaceTool> tools = MemoryTools.Create(root);
                        WorkspaceToolContext context = Context("cnv_m", project);

                        MemorySavedResult saved = (MemorySavedResult)await tools[0].InvokeAsync("{\"name\":\"Test Command\",\"description\":\"Run dotnet test\",\"content\":\"Use dotnet test src/Xeno.sln.\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(saved.Saved && saved.Created && saved.Name == "test-command" && saved.Scope == "project" && saved.Description == "Run dotnet test", "First save is wrong.");
                        string file = Path.Combine(new MemoryStore(root).DirectoryFor(MemoryScopeEnum.Project, project), "test-command.md");
                        Check(File.Exists(file) && File.ReadAllText(file).StartsWith("---\nname: Test Command\ndescription: Run dotnet test\nscope: project\ncreated: ", StringComparison.Ordinal), "Memory file format differs from Mux.");
                        DateTime created = new MemoryStore(root).Get("test-command", null, project)!.CreatedUtc;

                        await Task.Delay(20).ConfigureAwait(false);
                        MemorySavedResult updated = (MemorySavedResult)await tools[0].InvokeAsync("{\"name\":\"test command\",\"description\":\"Run the automated tests\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(!updated.Created && updated.Name == "test-command", "Saving the same name did not update.");
                        MemoryReadResult read = (MemoryReadResult)await tools[2].InvokeAsync("{\"name\":\"Test-Command\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(read.Description == "Run the automated tests" && read.Content == "Run the automated tests" && read.Scope == "project", "Update did not replace the memory.");
                        MemoryEntry entry = new MemoryStore(root).Get("test-command", MemoryScopeEnum.Project, project)!;
                        Check(entry.CreatedUtc == created && entry.UpdatedUtc > created, "Update should keep the created time.");
                        Check(JsonUtil.Serialize(read).Contains("\"updated_utc\":", StringComparison.Ordinal), "recall result shape differs from Mux.");

                        await tools[0].InvokeAsync("{\"name\":\"prefers-tabs\",\"description\":\"User prefers tabs\",\"content\":\"Indent with tabs in every repository.\",\"scope\":\"global\"}", context, CancellationToken.None).ConfigureAwait(false);
                        MemorySearchResult all = (MemorySearchResult)await tools[2].InvokeAsync("{}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(all.Count == 2 && all.Memories[0].Scope == "project" && all.Memories[1].Name == "prefers-tabs" && all.Memories[1].Scope == "global", "Listing is wrong.");
                        MemorySearchResult search = (MemorySearchResult)await tools[2].InvokeAsync("{\"query\":\"TABS repository\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(search.Count == 1 && search.Memories[0].Name == "prefers-tabs", "Search should match every word case-insensitively.");
                        MemorySearchResult nothing = (MemorySearchResult)await tools[2].InvokeAsync("{\"query\":\"tabs spaces\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(nothing.Count == 0, "Search matched a memory missing a word.");

                        MemoryErrorResult missing = (MemoryErrorResult)await tools[2].InvokeAsync("{\"name\":\"nope\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(missing.Error == "not_found", "Reading a missing memory should be not_found.");
                        MemoryErrorResult noName = (MemoryErrorResult)await tools[0].InvokeAsync("{\"name\":\"  \",\"description\":\"x\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(noName.Error == "invalid_arguments", "A blank name was accepted.");
                        MemoryErrorResult noText = (MemoryErrorResult)await tools[0].InvokeAsync("{\"name\":\"empty\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(noText.Error == "invalid_arguments", "A memory without description or content was accepted.");
                        MemoryErrorResult badScope = (MemoryErrorResult)await tools[0].InvokeAsync("{\"name\":\"x\",\"description\":\"x\",\"scope\":\"team\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(badScope.Error == "invalid_arguments", "An unknown scope was accepted.");

                        MemoryDeletedResult deleted = (MemoryDeletedResult)await tools[1].InvokeAsync("{\"name\":\"Prefers Tabs\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(deleted.Deleted && deleted.Name == "prefers-tabs" && deleted.Scope == "global", "forget should find a global memory when no scope is given.");
                        MemoryErrorResult gone = (MemoryErrorResult)await tools[1].InvokeAsync("{\"name\":\"prefers-tabs\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(gone.Error == "not_found" && gone.Message.Contains("recall", StringComparison.Ordinal), "Deleting twice should be not_found.");
                    }
                }),
                Case(suite, "MemoryScopesAndIndex", "Project memories are keyed by repository root, global ones are shared, and MEMORY.md is rewritten", async () =>
                {
                    using (TempFolder temp = new TempFolder())
                    {
                        string root = Path.Combine(temp.Path, "memory");
                        string repo = temp.Directory("repo");
                        Directory.CreateDirectory(Path.Combine(repo, ".git"));
                        string nested = temp.Directory(Path.Combine("repo", "src", "app"));
                        string other = temp.Directory("other");
                        IReadOnlyList<IWorkspaceTool> tools = MemoryTools.Create(root);

                        Check(MemoryStore.ProjectKey(nested) == MemoryStore.ProjectKey(repo) && MemoryStore.ProjectKey(repo).StartsWith("repo-", StringComparison.Ordinal) && MemoryStore.ProjectKey(repo).Length == "repo-".Length + 12, "Project key should be the repository root's name plus a hash.");
                        Check(MemoryStore.ProjectKey(other) != MemoryStore.ProjectKey(repo), "Different projects share a key.");

                        await tools[0].InvokeAsync("{\"name\":\"build\",\"description\":\"Build with dotnet\",\"scope\":\"project\"}", Context("c", nested), CancellationToken.None).ConfigureAwait(false);
                        await tools[0].InvokeAsync("{\"name\":\"style\",\"description\":\"No var\",\"scope\":\"user\"}", Context("c", repo), CancellationToken.None).ConfigureAwait(false);
                        MemorySearchResult fromRepo = (MemorySearchResult)await tools[2].InvokeAsync("{}", Context("c", repo), CancellationToken.None).ConfigureAwait(false);
                        MemorySearchResult fromOther = (MemorySearchResult)await tools[2].InvokeAsync("{}", Context("c", other), CancellationToken.None).ConfigureAwait(false);
                        Check(fromRepo.Count == 2, "A subdirectory's project memory is missing at the repository root.");
                        Check(fromOther.Count == 1 && fromOther.Memories[0].Name == "style", "Another project should see only global memories.");
                        MemoryErrorResult wrongScope = (MemoryErrorResult)await tools[1].InvokeAsync("{\"name\":\"build\",\"scope\":\"global\"}", Context("c", repo), CancellationToken.None).ConfigureAwait(false);
                        Check(wrongScope.Error == "not_found", "forget with a scope should look only there.");

                        string projectFolder = Path.Combine(root, MemoryStore.ProjectKey(repo));
                        string globalFolder = Path.Combine(root, MemoryStore.GlobalFolderName);
                        Check(File.ReadAllText(Path.Combine(projectFolder, MemoryStore.IndexFileName)) == "# Memory index (project)\n\n- [build](build.md): Build with dotnet\n", "Project index is wrong.");
                        Check(File.ReadAllText(Path.Combine(globalFolder, MemoryStore.IndexFileName)) == "# Memory index (global)\n\n- [style](style.md): No var\n", "Global index is wrong.");
                        Check(!Directory.EnumerateFiles(projectFolder, "*.tmp").Any() && !Directory.EnumerateFiles(globalFolder, "*.tmp").Any(), "Atomic writes left temporary files.");

                        File.WriteAllText(Path.Combine(projectFolder, "notes.md"), "Just some notes without frontmatter.\n");
                        File.WriteAllText(Path.Combine(projectFolder, "noname.md"), "---\ndescription: missing name\n---\n\nbody\n");
                        File.WriteAllText(Path.Combine(projectFolder, "unterminated.md"), "---\nname: open\n");
                        File.WriteAllText(Path.Combine(projectFolder, "handmade.md"), "---\r\nname: Handmade\r\ndescription: Written by hand\r\nupdated: 2020-01-01T00:00:00Z\r\n---\r\n\r\nHand body\r\n");
                        await tools[0].InvokeAsync("{\"name\":\"deploy\",\"description\":\"Deploy with scripts\"}", Context("c", repo), CancellationToken.None).ConfigureAwait(false);
                        List<MemoryEntry> listed = new MemoryStore(root).List(MemoryScopeEnum.Project, repo);
                        Check(String.Join(",", listed.Select(item => item.Slug)) == "deploy,build,handmade", "Listing should skip files without valid frontmatter and sort newest first: " + String.Join(",", listed.Select(item => item.Slug)));
                        Check(listed[2].Content == "Hand body" && listed[2].UpdatedUtc == new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Hand-written memory was not parsed.");
                        string index = File.ReadAllText(Path.Combine(projectFolder, MemoryStore.IndexFileName));
                        Check(index.StartsWith("# Memory index (project)\n\n- [deploy](deploy.md): Deploy with scripts\n", StringComparison.Ordinal) && index.Contains("- [Handmade](handmade.md): Written by hand", StringComparison.Ordinal) && !index.Contains("notes", StringComparison.Ordinal), "Index was not rewritten on save.");

                        await tools[1].InvokeAsync("{\"name\":\"deploy\"}", Context("c", repo), CancellationToken.None).ConfigureAwait(false);
                        Check(!File.ReadAllText(Path.Combine(projectFolder, MemoryStore.IndexFileName)).Contains("deploy", StringComparison.Ordinal) && !File.Exists(Path.Combine(projectFolder, "deploy.md")), "Index was not rewritten on delete.");
                        Check(new MemoryStore(root).Clear(MemoryScopeEnum.Project, repo) == 2 && File.ReadAllText(Path.Combine(projectFolder, MemoryStore.IndexFileName)) == "# Memory index (project)\n\n", "Clear did not empty the scope.");
                        Check(File.Exists(Path.Combine(projectFolder, "notes.md")), "Clear removed a file that is not a memory.");
                    }
                }),
                Case(suite, "MemoryPromptIndex", "The prompt index lists project then global memories within a byte budget", () =>
                {
                    using (TempFolder temp = new TempFolder())
                    {
                        string root = Path.Combine(temp.Path, "memory");
                        string project = temp.Directory("project");
                        string empty = MemoryPromptBuilder.Build(root, project, MemoryPromptBuilder.DefaultMaxBytes);
                        Check(empty.StartsWith("\n\n## Memory\n\n", StringComparison.Ordinal) && empty.Contains("`recall`", StringComparison.Ordinal) && empty.Contains("`remember`", StringComparison.Ordinal) && empty.Contains("`forget`", StringComparison.Ordinal) && empty.EndsWith("\nNo memories are saved yet.\n", StringComparison.Ordinal), "Empty index is wrong: " + empty);

                        MemoryStore store = new MemoryStore(root);
                        store.Save("build", "Build with dotnet", null, MemoryScopeEnum.Project, project, out _);
                        Thread.Sleep(20);
                        store.Save("tests", "Run the automated tests", null, MemoryScopeEnum.Project, project, out _);
                        store.Save("style", "No var", null, MemoryScopeEnum.Global, project, out _);
                        string full = MemoryPromptBuilder.Build(store, project, MemoryPromptBuilder.DefaultMaxBytes);
                        Check(full.EndsWith("\nProject memories:\n- tests: Run the automated tests\n- build: Build with dotnet\n\nGlobal memories:\n- style: No var\n", StringComparison.Ordinal), "Full index is wrong: " + full);

                        int firstLine = "- tests: Run the automated tests\n".Length;
                        string clipped = MemoryPromptBuilder.Build(store, project, firstLine);
                        Check(clipped.Contains("- tests: Run the automated tests\n", StringComparison.Ordinal) && !clipped.Contains("- build:", StringComparison.Ordinal) && !clipped.Contains("Global memories", StringComparison.Ordinal), "Budget was not applied: " + clipped);
                        Check(clipped.Contains("(2 older memories are not listed", StringComparison.Ordinal), "Omitted entries were not noted: " + clipped);
                        string none = MemoryPromptBuilder.Build(store, project, 0);
                        Check(none.Contains("(3 older memories are not listed", StringComparison.Ordinal) && !none.Contains("Project memories", StringComparison.Ordinal), "A zero budget should list nothing.");
                    }

                    return Task.CompletedTask;
                })
            });
        }

        #endregion

        #region Private-Methods

        private static WorkspaceToolContext Context(string conversationId)
        {
            return new WorkspaceToolContext { ConversationId = conversationId, TurnId = "trn_test", WorkingDirectory = Path.GetTempPath() };
        }

        private static WorkspaceToolContext Context(string conversationId, string workingDirectory)
        {
            return new WorkspaceToolContext { ConversationId = conversationId, TurnId = "trn_test", WorkingDirectory = workingDirectory };
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

        private sealed class TempFolder : IDisposable
        {
            public TempFolder()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xeno-planmem-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public string Directory(string relative)
            {
                string full = System.IO.Path.Combine(Path, relative);
                System.IO.Directory.CreateDirectory(full);
                return full;
            }

            public void Dispose()
            {
                try
                {
                    if (System.IO.Directory.Exists(Path)) System.IO.Directory.Delete(Path, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        #endregion
    }
}
