namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Tools.Workspace;
    using Xeno.Client.Tools.Workspace.Files;
    using Xeno.Core.Serialization;

    /// <summary>
    /// The file and search workspace tools ported from Mux (read_file, write_file, edit_file, multi_edit, delete_file,
    /// file_metadata, list_directory, manage_directory, glob, grep) and their confinement to the working directory.
    /// </summary>
    public static class WorkspaceFileToolTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// File tool suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor FileToolSuite()
        {
            const string suite = "WorkspaceFileTools";
            return new TestSuiteDescriptor(suite, "Workspace file tools", new List<TestCaseDescriptor>
            {
                Case(suite, "CatalogListsTenTools", "FileTools.Create returns the ten tools in order with Mux names, schemas, and approval flags", () =>
                {
                    IReadOnlyList<IWorkspaceTool> tools = FileTools.Create();
                    string[] names = { "read_file", "write_file", "edit_file", "multi_edit", "delete_file", "file_metadata", "list_directory", "manage_directory", "glob", "grep" };
                    Check(tools.Select(tool => tool.Name).SequenceEqual(names), "Unexpected tools: " + String.Join(",", tools.Select(tool => tool.Name)));
                    string[] mutating = { "write_file", "edit_file", "multi_edit", "delete_file", "manage_directory" };
                    Check(tools.All(tool => tool.Mutating == mutating.Contains(tool.Name)), "A Mutating flag is wrong.");
                    Check(tools.All(tool => tool.Description.Length > 20 && tool.Schema.Required.All(required => tool.Schema.Properties.ContainsKey(required))), "A tool lacks a description or requires an undeclared argument.");
                    Check(Find("read_file").Schema.Properties.Keys.SequenceEqual(new[] { "file_path", "offset", "limit" }), "read_file arguments differ from Mux.");
                    Check(Find("edit_file").Schema.Properties.ContainsKey("old_string") && Find("edit_file").Schema.Properties.ContainsKey("replace_all"), "edit_file arguments are wrong.");
                    Check(Find("multi_edit").Schema.Properties["edits"].Items?.Required?.Contains("old_string") == true, "multi_edit edit items are wrong.");
                    Check(Find("manage_directory").Schema.Properties["action"].Enum?.Count == 3, "manage_directory actions are wrong.");
                    Check(Find("grep").Schema.Properties.ContainsKey("include") && Find("glob").Schema.Required.Single() == "pattern", "Search tool arguments are wrong.");
                    return Task.CompletedTask;
                }),
                Case(suite, "ReadFileNumbersLines", "read_file numbers lines, reads offset/limit ranges, and refuses a large file without a range", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        File.WriteAllText(temp.Work("notes.txt"), "alpha\r\nbeta\ngamma\ndelta\n");
                        string all = await InvokeTextAsync("read_file", "{\"file_path\":\"notes.txt\"}", temp).ConfigureAwait(false);
                        Check(all == "     1\talpha\n     2\tbeta\n     3\tgamma\n     4\tdelta\n", "Whole read is wrong: " + all);
                        string range = await InvokeTextAsync("read_file", "{\"file_path\":\"notes.txt\",\"offset\":2,\"limit\":2}", temp).ConfigureAwait(false);
                        Check(range == "     2\tbeta\n     3\tgamma\n", "Range read is wrong: " + range);
                        string absolute = await InvokeTextAsync("read_file", Json(new ReadFileArguments { FilePath = temp.Work("notes.txt"), Offset = 4 }), temp).ConfigureAwait(false);
                        Check(absolute == "     4\tdelta\n", "Absolute path inside was not read: " + absolute);

                        await ExpectThrowsAsync<FileNotFoundException>(() => InvokeAsync("read_file", "{\"file_path\":\"missing.txt\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("read_file", "{}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<IOException>(() => InvokeAsync("read_file", "{\"file_path\":\".\"}", temp)).ConfigureAwait(false);

                        StringBuilder big = new StringBuilder();
                        while (big.Length <= ReadFileTool.MaxInlineBytes) big.Append("line of padding text for the large file\n");
                        File.WriteAllText(temp.Work("big.txt"), big.ToString());
                        IOException large = await ExpectThrowsAsync<IOException>(() => InvokeAsync("read_file", "{\"file_path\":\"big.txt\"}", temp)).ConfigureAwait(false);
                        Check(large.Message.Contains("offset", StringComparison.Ordinal) && large.Message.Contains("limit", StringComparison.Ordinal), "The large-file message does not point at offset and limit: " + large.Message);
                        string paged = await InvokeTextAsync("read_file", "{\"file_path\":\"big.txt\",\"offset\":10,\"limit\":1}", temp).ConfigureAwait(false);
                        Check(paged == "    10\tline of padding text for the large file\n", "Paged read of a large file is wrong: " + paged);
                    }
                }),
                Case(suite, "WriteFileKeepsStyle", "write_file creates parents, counts lines, and keeps an existing file's line endings and BOM", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        WriteFileResult created = await InvokeAsync<WriteFileResult>("write_file", "{\"file_path\":\"deep/er/new.txt\",\"content\":\"one\\ntwo\"}", temp).ConfigureAwait(false);
                        Check(created.Success && created.FilePath == "deep/er/new.txt" && created.LineCount == 2, "Create result is wrong: " + Json(created));
                        Check(File.ReadAllText(temp.Work("deep/er/new.txt")).Replace("\r\n", "\n") == "one\ntwo", "Created content is wrong.");

                        File.WriteAllText(temp.Work("crlf.txt"), "a\r\nb\r\n", new UTF8Encoding(true));
                        await InvokeAsync("write_file", "{\"file_path\":\"crlf.txt\",\"content\":\"x\\ny\\nz\"}", temp).ConfigureAwait(false);
                        byte[] bytes = File.ReadAllBytes(temp.Work("crlf.txt"));
                        Check(bytes.Length > 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf, "The BOM was lost.");
                        Check(File.ReadAllText(temp.Work("crlf.txt")) == "x\r\ny\r\nz", "CRLF line endings were not kept.");
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("write_file", "{\"file_path\":\"no-content.txt\"}", temp)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "EditFileReplacesExactlyOnce", "edit_file needs one exact match unless replace_all is set, and keeps CRLF", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        File.WriteAllText(temp.Work("code.cs"), "int a = 1;\r\nint b = 2;\r\nint c = 2;\r\n");
                        EditFileResult edited = await InvokeAsync<EditFileResult>("edit_file", "{\"file_path\":\"code.cs\",\"old_string\":\"int a = 1;\\nint b\",\"new_string\":\"int a = 10;\\nint b\"}", temp).ConfigureAwait(false);
                        Check(edited.EditsApplied == 1 && edited.FilePath == "code.cs", "Edit result is wrong: " + Json(edited));
                        Check(File.ReadAllText(temp.Work("code.cs")) == "int a = 10;\r\nint b = 2;\r\nint c = 2;\r\n", "The edit or its line endings are wrong.");

                        string before = File.ReadAllText(temp.Work("code.cs"));
                        InvalidOperationException missing = await ExpectThrowsAsync<InvalidOperationException>(() => InvokeAsync("edit_file", "{\"file_path\":\"code.cs\",\"old_string\":\"int z\",\"new_string\":\"x\"}", temp)).ConfigureAwait(false);
                        Check(missing.Message.Contains("not found", StringComparison.Ordinal), "Missing message is wrong: " + missing.Message);
                        InvalidOperationException ambiguous = await ExpectThrowsAsync<InvalidOperationException>(() => InvokeAsync("edit_file", "{\"file_path\":\"code.cs\",\"old_string\":\" = 2;\",\"new_string\":\" = 3;\"}", temp)).ConfigureAwait(false);
                        Check(ambiguous.Message.Contains("2 locations", StringComparison.Ordinal) && ambiguous.Message.Contains("lines 2, 3", StringComparison.Ordinal), "Ambiguous message is wrong: " + ambiguous.Message);
                        Check(File.ReadAllText(temp.Work("code.cs")) == before, "A failed edit changed the file.");
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("edit_file", "{\"file_path\":\"code.cs\",\"old_string\":\"\",\"new_string\":\"x\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<FileNotFoundException>(() => InvokeAsync("edit_file", "{\"file_path\":\"nope.cs\",\"old_string\":\"a\",\"new_string\":\"b\"}", temp)).ConfigureAwait(false);

                        EditFileResult all = await InvokeAsync<EditFileResult>("edit_file", "{\"file_path\":\"code.cs\",\"old_string\":\" = 2;\",\"new_string\":\" = 3;\",\"replace_all\":true}", temp).ConfigureAwait(false);
                        Check(all.EditsApplied == 2 && File.ReadAllText(temp.Work("code.cs")) == "int a = 10;\r\nint b = 3;\r\nint c = 3;\r\n", "replace_all is wrong: " + Json(all));
                    }
                }),
                Case(suite, "MultiEditIsAtomic", "multi_edit applies edits in order and writes nothing when any edit is missing or ambiguous", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        File.WriteAllText(temp.Work("m.txt"), "red green blue\nred2\n");
                        EditFileResult result = await InvokeAsync<EditFileResult>("multi_edit", "{\"file_path\":\"m.txt\",\"edits\":[{\"old_string\":\"green\",\"new_string\":\"GREEN\"},{\"old_string\":\" blue\",\"new_string\":\"-teal\"}]}", temp).ConfigureAwait(false);
                        Check(result.EditsApplied == 2 && File.ReadAllText(temp.Work("m.txt")) == "red GREEN-teal\nred2\n", "Sequential edits are wrong: " + File.ReadAllText(temp.Work("m.txt")));
                        Check(result.NewLineCount == 3, "new_line_count is wrong.");

                        string before = File.ReadAllText(temp.Work("m.txt"));
                        InvalidOperationException missing = await ExpectThrowsAsync<InvalidOperationException>(() => InvokeAsync("multi_edit", "{\"file_path\":\"m.txt\",\"edits\":[{\"old_string\":\"teal\",\"new_string\":\"x\"},{\"old_string\":\"purple\",\"new_string\":\"y\"}]}", temp)).ConfigureAwait(false);
                        Check(missing.Message.Contains("index 1", StringComparison.Ordinal), "Missing edit index is wrong: " + missing.Message);
                        Check(File.ReadAllText(temp.Work("m.txt")) == before, "A failed multi_edit applied an earlier edit.");
                        await ExpectThrowsAsync<InvalidOperationException>(() => InvokeAsync("multi_edit", "{\"file_path\":\"m.txt\",\"edits\":[{\"old_string\":\"teal\",\"new_string\":\"x\"},{\"old_string\":\"red\",\"new_string\":\"y\"}]}", temp)).ConfigureAwait(false);
                        Check(File.ReadAllText(temp.Work("m.txt")) == before, "An ambiguous multi_edit changed the file.");
                        await ExpectThrowsAsync<InvalidOperationException>(() => InvokeAsync("multi_edit", "{\"file_path\":\"m.txt\",\"edits\":[{\"old_string\":\"GREEN\",\"new_string\":\"x\"},{\"old_string\":\"GREEN-\",\"new_string\":\"y\"}]}", temp)).ConfigureAwait(false);
                        Check(File.ReadAllText(temp.Work("m.txt")) == before, "A conflicting multi_edit changed the file.");
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("multi_edit", "{\"file_path\":\"m.txt\",\"edits\":[]}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("multi_edit", "{\"file_path\":\"m.txt\",\"edits\":[{\"old_string\":\"teal\"}]}", temp)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "DeleteMetadataAndDirectories", "delete_file, file_metadata, and manage_directory create, rename, and delete inside the working directory", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        File.WriteAllText(temp.Work("gone.txt"), "bye");
                        FileMetadataResult file = await InvokeAsync<FileMetadataResult>("file_metadata", "{\"path\":\"gone.txt\"}", temp).ConfigureAwait(false);
                        Check(file.Type == "file" && file.SizeBytes == 3 && file.Extension == ".txt" && file.Path == "gone.txt" && file.FileCount == null, "File metadata is wrong: " + Json(file));
                        DeleteFileResult deleted = await InvokeAsync<DeleteFileResult>("delete_file", "{\"file_path\":\"gone.txt\"}", temp).ConfigureAwait(false);
                        Check(deleted.Success && !File.Exists(temp.Work("gone.txt")), "delete_file did not delete.");
                        await ExpectThrowsAsync<FileNotFoundException>(() => InvokeAsync("delete_file", "{\"file_path\":\"gone.txt\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<FileNotFoundException>(() => InvokeAsync("file_metadata", "{\"path\":\"gone.txt\"}", temp)).ConfigureAwait(false);

                        ManageDirectoryResult made = await InvokeAsync<ManageDirectoryResult>("manage_directory", "{\"action\":\"create\",\"path\":\"a/b\"}", temp).ConfigureAwait(false);
                        Check(made.Path == "a/b" && Directory.Exists(temp.Work("a/b")), "create failed.");
                        ManageDirectoryResult again = await InvokeAsync<ManageDirectoryResult>("manage_directory", "{\"action\":\"create\",\"path\":\"a/b\"}", temp).ConfigureAwait(false);
                        Check(again.Success && again.Message == "Directory already exists.", "create of an existing directory is wrong.");
                        File.WriteAllText(temp.Work("a/b/f.txt"), "x");
                        await ExpectThrowsAsync<IOException>(() => InvokeAsync("delete_file", "{\"file_path\":\"a\"}", temp)).ConfigureAwait(false);
                        FileMetadataResult directory = await InvokeAsync<FileMetadataResult>("file_metadata", "{\"path\":\"a\"}", temp).ConfigureAwait(false);
                        Check(directory.Type == "directory" && directory.DirectoryCount == 1 && directory.FileCount == 0 && directory.SizeBytes == null, "Directory metadata is wrong: " + Json(directory));

                        ManageDirectoryResult renamed = await InvokeAsync<ManageDirectoryResult>("manage_directory", "{\"action\":\"rename\",\"path\":\"a\",\"new_path\":\"c\"}", temp).ConfigureAwait(false);
                        Check(renamed.FromPath == "a" && renamed.ToPath == "c" && File.Exists(temp.Work("c/b/f.txt")) && !Directory.Exists(temp.Work("a")), "rename failed.");
                        Directory.CreateDirectory(temp.Work("d"));
                        await ExpectThrowsAsync<IOException>(() => InvokeAsync("manage_directory", "{\"action\":\"rename\",\"path\":\"c\",\"new_path\":\"d\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("manage_directory", "{\"action\":\"rename\",\"path\":\"c\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("manage_directory", "{\"action\":\"explode\",\"path\":\"c\"}", temp)).ConfigureAwait(false);
                        ManageDirectoryResult removed = await InvokeAsync<ManageDirectoryResult>("manage_directory", "{\"action\":\"delete\",\"path\":\"c\"}", temp).ConfigureAwait(false);
                        Check(removed.Success && !Directory.Exists(temp.Work("c")), "delete of a non-empty directory failed.");
                        await ExpectThrowsAsync<DirectoryNotFoundException>(() => InvokeAsync("manage_directory", "{\"action\":\"delete\",\"path\":\"c\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("manage_directory", "{\"action\":\"delete\",\"path\":\".\"}", temp)).ConfigureAwait(false);
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("manage_directory", Json(new ManageDirectoryArguments { Action = "rename", Path = temp.WorkingDirectory + Path.DirectorySeparatorChar, NewPath = "moved" }), temp)).ConfigureAwait(false);
                        Check(Directory.Exists(temp.WorkingDirectory), "The working directory was removed.");
                    }
                }),
                Case(suite, "ListGlobAndGrep", "list_directory sorts directories first, glob matches ** and braces, and grep reports file:line matches", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        Directory.CreateDirectory(temp.Work("src/sub"));
                        Directory.CreateDirectory(temp.Work("Docs"));
                        File.WriteAllText(temp.Work("b.txt"), "nothing here\n");
                        File.WriteAllText(temp.Work("A.md"), "# Title\nTODO: write docs\n");
                        File.WriteAllText(temp.Work("src/main.cs"), "class Main\n{\n    // TODO: tidy\n}\n");
                        File.WriteAllText(temp.Work("src/sub/util.cs"), "class Util { } // TODO later\n");
                        File.WriteAllText(temp.Work("src/sub/data.json"), "{ \"todo\": 1 }\n");
                        File.WriteAllBytes(temp.Work("src/blob.bin"), new byte[] { 84, 79, 68, 79, 0, 1, 2 });

                        string listing = await InvokeTextAsync("list_directory", "{\"path\":\".\"}", temp).ConfigureAwait(false);
                        Check(listing == "[DIR]  Docs\n[DIR]  src\n[FILE] A.md\n[FILE] b.txt\n", "Listing is wrong: " + listing);
                        await ExpectThrowsAsync<DirectoryNotFoundException>(() => InvokeAsync("list_directory", "{\"path\":\"nope\"}", temp)).ConfigureAwait(false);

                        string cs = await InvokeTextAsync("glob", "{\"pattern\":\"**/*.cs\"}", temp).ConfigureAwait(false);
                        Check(cs == "Found 2 matching file(s):\nsrc/main.cs\nsrc/sub/util.cs\n", "glob **/*.cs is wrong: " + cs);
                        string braces = await InvokeTextAsync("glob", "{\"pattern\":\"**/*.{json,md}\"}", temp).ConfigureAwait(false);
                        Check(braces == "Found 2 matching file(s):\nA.md\nsrc/sub/data.json\n", "glob braces are wrong: " + braces);
                        string scoped = await InvokeTextAsync("glob", "{\"pattern\":\"*.cs\",\"path\":\"src\"}", temp).ConfigureAwait(false);
                        Check(scoped == "Found 1 matching file(s):\nsrc/main.cs\n", "glob with a path is wrong: " + scoped);

                        string todo = await InvokeTextAsync("grep", "{\"pattern\":\"TODO\"}", temp).ConfigureAwait(false);
                        Check(todo == "A.md:2: TODO: write docs\nsrc/main.cs:3:     // TODO: tidy\nsrc/sub/util.cs:1: class Util { } // TODO later\n", "grep is wrong: " + todo);
                        string included = await InvokeTextAsync("grep", "{\"pattern\":\"(?i)todo\",\"path\":\"src\",\"include\":\"*.json\"}", temp).ConfigureAwait(false);
                        Check(included == "src/sub/data.json:1: { \"todo\": 1 }\n", "grep include is wrong: " + included);
                        string none = await InvokeTextAsync("grep", "{\"pattern\":\"zebra\"}", temp).ConfigureAwait(false);
                        Check(none == "No matches found.\n", "grep without matches is wrong: " + none);
                        await ExpectThrowsAsync<ArgumentException>(() => InvokeAsync("grep", "{\"pattern\":\"(unclosed\"}", temp)).ConfigureAwait(false);

                        StringBuilder many = new StringBuilder();
                        for (int i = 0; i < GrepTool.MaxMatches + 20; i++) many.Append("hit ").Append(i).Append('\n');
                        File.WriteAllText(temp.Work("many.txt"), many.ToString());
                        string capped = await InvokeTextAsync("grep", "{\"pattern\":\"^hit\",\"include\":\"many.txt\"}", temp).ConfigureAwait(false);
                        Check(capped.Split('\n').Count(line => line.StartsWith("many.txt:", StringComparison.Ordinal)) == GrepTool.MaxMatches && capped.Contains("(output truncated at 100 matches)", StringComparison.Ordinal), "grep cap is wrong.");
                    }
                }),
                Case(suite, "PathsStayInsideWorkingDirectory", "../ escapes, absolute paths outside, and symbolic links pointing outside are rejected by every tool", async () =>
                {
                    using (TempWorkspace temp = new TempWorkspace())
                    {
                        File.WriteAllText(temp.Work("inside.txt"), "inside\n");
                        string secret = temp.Outside("secret.txt");
                        List<string> escapes = new List<string> { "../outside/secret.txt", secret };
                        foreach (string path in escapes)
                        {
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("read_file", Json(new ReadFileArguments { FilePath = path }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("write_file", Json(new WriteFileArguments { FilePath = path, Content = "pwned" }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("edit_file", Json(new EditFileArguments { FilePath = path, OldString = "secret", NewString = "pwned" }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("multi_edit", Json(new MultiEditArguments { FilePath = path, Edits = new List<MultiEditOperation> { new MultiEditOperation { OldString = "secret", NewString = "pwned" } } }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("delete_file", Json(new DeleteFileArguments { FilePath = path }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("file_metadata", Json(new FileMetadataArguments { Path = path }), temp)).ConfigureAwait(false);
                        }

                        List<string> outsideDirectories = new List<string> { "..", "../outside", temp.OutsideDirectory, "~" };
                        foreach (string path in outsideDirectories)
                        {
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("list_directory", Json(new ListDirectoryArguments { Path = path }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("glob", Json(new GlobArguments { Pattern = "**/*", Path = path }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("grep", Json(new GrepArguments { Pattern = "secret", Path = path }), temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("manage_directory", Json(new ManageDirectoryArguments { Action = "delete", Path = path }), temp)).ConfigureAwait(false);
                        }

                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("manage_directory", "{\"action\":\"create\",\"path\":\"../outside/made\"}", temp)).ConfigureAwait(false);
                        Directory.CreateDirectory(temp.Work("mine"));
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("manage_directory", "{\"action\":\"rename\",\"path\":\"mine\",\"new_path\":\"../outside/mine\"}", temp)).ConfigureAwait(false);
                        Check(Directory.Exists(temp.Work("mine")) && !Directory.Exists(temp.Outside("made")) && !Directory.Exists(temp.Outside("mine")), "A directory escaped the working directory.");

                        bool linked = TryLink(temp.Work("link.txt"), secret, false) && TryLink(temp.Work("linkdir"), temp.OutsideDirectory, true);
                        if (linked)
                        {
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("read_file", "{\"file_path\":\"link.txt\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("write_file", "{\"file_path\":\"link.txt\",\"content\":\"pwned\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("edit_file", "{\"file_path\":\"link.txt\",\"old_string\":\"secret\",\"new_string\":\"pwned\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("read_file", "{\"file_path\":\"linkdir/secret.txt\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("write_file", "{\"file_path\":\"linkdir/new.txt\",\"content\":\"pwned\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("list_directory", "{\"path\":\"linkdir\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("grep", "{\"pattern\":\"secret\",\"path\":\"linkdir\"}", temp)).ConfigureAwait(false);
                            await ExpectThrowsAsync<UnauthorizedAccessException>(() => InvokeAsync("manage_directory", "{\"action\":\"delete\",\"path\":\"linkdir\"}", temp)).ConfigureAwait(false);

                            string listing = await InvokeTextAsync("list_directory", "{\"path\":\".\"}", temp).ConfigureAwait(false);
                            Check(!listing.Contains("link", StringComparison.Ordinal) && listing.Contains("inside.txt", StringComparison.Ordinal), "list_directory shows a link that leads outside: " + listing);
                            string globbed = await InvokeTextAsync("glob", "{\"pattern\":\"**/*\"}", temp).ConfigureAwait(false);
                            Check(!globbed.Contains("secret", StringComparison.Ordinal) && !globbed.Contains("link", StringComparison.Ordinal) && globbed.Contains("inside.txt", StringComparison.Ordinal), "glob leaked an outside path: " + globbed);
                            string grepped = await InvokeTextAsync("grep", "{\"pattern\":\"secret|inside\"}", temp).ConfigureAwait(false);
                            Check(!grepped.Contains("secret", StringComparison.Ordinal) && grepped.Contains("inside.txt:1: inside", StringComparison.Ordinal), "grep leaked outside content: " + grepped);

                            Check(TryLink(temp.Work("inner-link.txt"), "inside.txt", false), "Could not link inside.");
                            string inner = await InvokeTextAsync("read_file", "{\"file_path\":\"inner-link.txt\"}", temp).ConfigureAwait(false);
                            Check(inner == "     1\tinside\n", "A link inside the working directory was not followed.");
                        }

                        Check(File.ReadAllText(secret) == "secret\n" && Directory.GetFileSystemEntries(temp.OutsideDirectory).Length == 1, "A file outside the working directory was changed.");
                    }
                })
            });
        }

        #endregion

        #region Private-Methods

        private static IWorkspaceTool Find(string name)
        {
            return FileTools.Create().Single(tool => tool.Name == name);
        }

        private static Task<object> InvokeAsync(string name, string arguments, TempWorkspace temp)
        {
            WorkspaceToolContext context = new WorkspaceToolContext { ConversationId = "cnv_test", TurnId = "trn_test", WorkingDirectory = temp.WorkingDirectory, Settings = new ClientChatSettings() };
            return Find(name).InvokeAsync(arguments, context, CancellationToken.None);
        }

        private static async Task<string> InvokeTextAsync(string name, string arguments, TempWorkspace temp)
        {
            object result = await InvokeAsync(name, arguments, temp).ConfigureAwait(false);
            if (result is string text) return text;
            throw new InvalidOperationException(name + " did not return text.");
        }

        private static async Task<T> InvokeAsync<T>(string name, string arguments, TempWorkspace temp) where T : class
        {
            object result = await InvokeAsync(name, arguments, temp).ConfigureAwait(false);
            T? typed = JsonUtil.Deserialize<T>(JsonUtil.Serialize(result));
            if (typed == null) throw new InvalidOperationException(name + " returned no " + typeof(T).Name + ".");
            return typed;
        }

        private static string Json(object value)
        {
            return JsonUtil.Serialize(value);
        }

        private static bool TryLink(string path, string target, bool directory)
        {
            try
            {
                if (directory) Directory.CreateSymbolicLink(path, target);
                else File.CreateSymbolicLink(path, target);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is PlatformNotSupportedException)
            {
                return false;
            }
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

        // A temporary root holding the working directory ("work") and a sibling the tools must not reach ("outside",
        // with secret.txt).
        private sealed class TempWorkspace : IDisposable
        {
            private readonly string _Root;

            public TempWorkspace()
            {
                _Root = Path.Combine(Path.GetTempPath(), "xeno-file-tools-" + Guid.NewGuid().ToString("N"));
                WorkingDirectory = Path.Combine(_Root, "work");
                OutsideDirectory = Path.Combine(_Root, "outside");
                Directory.CreateDirectory(WorkingDirectory);
                Directory.CreateDirectory(OutsideDirectory);
                File.WriteAllText(Path.Combine(OutsideDirectory, "secret.txt"), "secret\n");
            }

            public string WorkingDirectory { get; }

            public string OutsideDirectory { get; }

            public string Work(string relative)
            {
                return Path.Combine(WorkingDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
            }

            public string Outside(string relative)
            {
                return Path.Combine(OutsideDirectory, relative);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_Root)) Directory.Delete(_Root, true);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Best effort.
                }
            }
        }

        #endregion
    }
}
