namespace Xeno.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xeno.Publisher.Manifest;

    /// <summary>
    /// Publishes an artifact for one runtime: the pre-build steps once, then a self-contained dotnet publish. Every
    /// channel starts from this payload.
    /// </summary>
    public sealed class Payload
    {
        private readonly string _RepoRoot;
        private readonly ArtifactSettings _Artifact;
        private readonly string _Framework;
        private readonly string _Version;
        private readonly string _Root;
        private readonly bool _Clean;
        private readonly Dictionary<string, string> _Published = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _PreBuildDone = false;

        /// <summary>
        /// Creates a payload builder.
        /// </summary>
        /// <param name="repoRoot">Repository root.</param>
        /// <param name="artifact">Artifact to publish.</param>
        /// <param name="framework">Target framework.</param>
        /// <param name="version">Version to stamp into the binaries.</param>
        /// <param name="root">Directory under which per-runtime publish folders are created.</param>
        /// <param name="clean">True to run pre-build steps even when their output exists.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public Payload(string repoRoot, ArtifactSettings artifact, string framework, string version, string root, bool clean)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            _RepoRoot = repoRoot ?? throw new ArgumentNullException(nameof(repoRoot));
            _Artifact = artifact;
            _Framework = framework ?? throw new ArgumentNullException(nameof(framework));
            _Version = version ?? throw new ArgumentNullException(nameof(version));
            _Root = root ?? throw new ArgumentNullException(nameof(root));
            _Clean = clean;
        }

        /// <summary>
        /// Publishes the artifact for a runtime, or returns the folder already published in this run.
        /// </summary>
        /// <param name="target">Runtime target.</param>
        /// <returns>The publish folder.</returns>
        /// <exception cref="PublisherException">The project is missing or the output lacks an expected executable.</exception>
        /// <exception cref="ProcessFailedException">A pre-build step or dotnet publish failed.</exception>
        public string Publish(RuntimeTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (_Published.TryGetValue(target.Rid, out string? existing)) return existing;
            RunPreBuild();

            string csproj = Path.GetFullPath(Path.Combine(_RepoRoot, _Artifact.Csproj));
            if (!File.Exists(csproj)) throw new PublisherException("Artifact '" + _Artifact.Id + "' project not found at " + csproj + ".");

            string output = FileSystemUtil.Reset(Path.Combine(_Root, _Artifact.Id + "-" + target.Rid));
            List<string> arguments = new List<string>
            {
                "publish", csproj,
                "-c", "Release",
                "-r", target.Rid,
                "-f", _Framework,
                "--self-contained", "true",
                "-p:Version=" + _Version,
                "-p:DebugType=none",
                "-p:GenerateDocumentationFile=false",
                "--nologo",
                "-o", output
            };
            if (_Artifact.SingleFile)
            {
                arguments.Add("-p:PublishSingleFile=true");
                arguments.Add("-p:IncludeNativeLibrariesForSelfExtract=true");
            }

            Console.WriteLine("[payload] publishing " + _Artifact.Id + " for " + target.Rid);
            ProcessRunner.Run("dotnet", arguments, _RepoRoot);

            foreach (string executable in _Artifact.Executables)
            {
                string path = Path.Combine(output, executable + target.ExecutableExtension);
                if (!File.Exists(path)) throw new PublisherException("The " + target.Rid + " publish of " + _Artifact.Csproj + " has no " + executable + target.ExecutableExtension + ". Check build.artifacts[].executables in publisher.json.");
            }

            _Published[target.Rid] = output;
            return output;
        }

        /// <summary>
        /// Runs the pre-build steps if this run has not run them yet.
        /// </summary>
        /// <exception cref="ProcessFailedException">A step failed.</exception>
        public void RunPreBuild()
        {
            if (_PreBuildDone) return;
            _PreBuildDone = true;
            foreach (PreBuildStep step in _Artifact.PreBuild)
            {
                if (!_Clean && !String.IsNullOrEmpty(step.SkipIfExists) && Path.Exists(Path.Combine(_RepoRoot, step.SkipIfExists)))
                {
                    Console.WriteLine("[prebuild] skipping '" + step.Description + "': " + step.SkipIfExists + " exists (use --clean to rebuild).");
                    continue;
                }

                Console.WriteLine("[prebuild] " + step.Description);
                string directory = Path.Combine(_RepoRoot, step.WorkingDirectory);
                if (RuntimeTarget.HostOs() == OsFamilyEnum.Windows)
                {
                    List<string> wrapped = new List<string> { "/d", "/s", "/c", step.Command };
                    wrapped.AddRange(step.Arguments);
                    ProcessRunner.Run("cmd.exe", wrapped, directory);
                }
                else
                {
                    ProcessRunner.Run(step.Command, step.Arguments, directory);
                }
            }
        }
    }
}
