namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// run_process, ported from Mux: runs a program in the conversation's working directory (or a subdirectory) and
    /// returns its stdout, stderr, and exit code. Unlike Mux, the command is a program with an argument list and no
    /// shell unless the call sets <c>shell</c>. On timeout or cancellation the whole process tree is killed. Output is
    /// bounded while it is captured and never logged.
    /// </summary>
    public sealed class RunProcessTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "run_process";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get
            {
                return "Runs a program and captures its output. Current runtime: " + ProcessCommandLine.OperatingSystemLabel
                    + ". The command is a program and args are its arguments, run without a shell; set shell to true to run a command line through "
                    + ProcessCommandLine.ShellInvocation + " instead. Use commands that are valid for that shell and operating system. "
                    + "The process runs in the conversation's working directory or a subdirectory of it. "
                    + "Returns stdout, stderr, exit code, and whether the process timed out. For long-running commands use process_start and poll with process_output.";
            }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("command", "The program to execute (for example git, dotnet, or ./build.sh). With shell true, a command line written for " + ProcessCommandLine.ShellProgram + " on " + ProcessCommandLine.OperatingSystemLabel + "."),
                    WorkspaceSchema.Array("args", "Optional array of command arguments, passed to the program as they are.", new ChatToolPropertyDto { Type = "string" }),
                    WorkspaceSchema.Bool("shell", "Run the command (joined with args) through " + ProcessCommandLine.ShellInvocation + " so pipes, redirection, and shell built-ins work. Defaults to false."),
                    WorkspaceSchema.Text("working_directory", "The working directory for the process, inside the conversation's working directory. Defaults to the conversation's working directory."),
                    WorkspaceSchema.Integer("timeout_ms", "Timeout in milliseconds. The process and its child processes are killed if it exceeds this. Defaults to and is at most the configured process timeout."),
                    "command");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return true; }
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            RunProcessArguments args = WorkspaceJson.Read<RunProcessArguments>(arguments);
            string directory = WorkspacePath.Resolve(args.WorkingDirectory, context.WorkingDirectory);
            if (!Directory.Exists(directory)) throw new ArgumentException("The working directory '" + args.WorkingDirectory + "' does not exist.");
            int maxTimeoutMs = context.Settings.ProcessTimeoutSeconds * 1000;
            int timeoutMs = Math.Clamp(args.TimeoutMs ?? maxTimeoutMs, 1000, maxTimeoutMs);
            ProcessStartInfo startInfo = ProcessCommandLine.Create(args.Command, args.Args, args.Shell, directory);
            BoundedOutput stdout = new BoundedOutput(context.Settings.MaxToolOutputCharacters);
            BoundedOutput stderr = new BoundedOutput(context.Settings.MaxToolOutputCharacters);

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (object sender, DataReceivedEventArgs e) => stdout.AppendLine(e.Data);
                process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => stderr.AppendLine(e.Data);
                try
                {
                    process.Start();
                }
                catch (Win32Exception ex)
                {
                    throw new InvalidOperationException("Could not start '" + startInfo.FileName + "': " + ex.Message);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                try { process.StandardInput.Close(); } catch (Exception) { }

                bool timedOut = false;
                using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMs))
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
                {
                    try
                    {
                        await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        ProcessCommandLine.KillTree(process);
                        try { process.WaitForExit(5000); } catch (Exception) { }
                        token.ThrowIfCancellationRequested();
                        timedOut = true;
                    }
                }

                return new RunProcessResult
                {
                    Stdout = stdout.ToString(),
                    Stderr = stderr.ToString(),
                    ExitCode = timedOut ? -1 : SafeExitCode(process),
                    TimedOut = timedOut
                };
            }
        }

        #endregion

        #region Private-Methods

        private static int SafeExitCode(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        #endregion
    }
}
