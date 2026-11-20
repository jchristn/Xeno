namespace Xeno.Publisher.Channels
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// The .NET global tool package for the CLI, for dotnet tool install -g. The workflow pushes it to NuGet when a
    /// token exists.
    /// </summary>
    public sealed class DotnetToolChannel : IChannel
    {
        /// <inheritdoc/>
        public string Id => "dotnettool";

        /// <inheritdoc/>
        public string DisplayName => ".NET global tool";

        /// <inheritdoc/>
        public OsFamilyEnum RequiredHost => OsFamilyEnum.Any;

        /// <inheritdoc/>
        public OsFamilyEnum TargetOs => OsFamilyEnum.Any;

        /// <inheritdoc/>
        public IReadOnlyList<ToolRequirement> Tools => Array.Empty<ToolRequirement>();

        /// <inheritdoc/>
        public IReadOnlyList<string> Build(ChannelContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string csproj = context.RepoPath(context.Option("csproj", context.Artifact.Csproj));
            if (!File.Exists(csproj)) throw new PublisherException("dotnettool project not found at " + csproj + ".");
            string packageId = context.Option("packageId", context.Project.Name);
            context.Payload.RunPreBuild();
            ProcessRunner.Run("dotnet", new[] { "pack", csproj, "-c", "Release", "--nologo", "-o", context.OutputDir, "-p:Version=" + context.Version }, context.RepoRoot);
            string package = Path.Combine(context.OutputDir, packageId + "." + context.Version + ".nupkg");
            if (!File.Exists(package)) throw new PublisherException("dotnet pack finished but " + package + " is missing; check options.packageId.");
            return new[] { package };
        }
    }
}
