namespace Xeno.Publisher.Channels
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Homebrew cask for the .pkg installers. Casks are quarantined like any other download, so cask users still see
    /// the Gatekeeper prompt.
    /// </summary>
    public sealed class BrewCaskChannel : IChannel
    {
        /// <inheritdoc/>
        public string Id => "brewcask";

        /// <inheritdoc/>
        public string DisplayName => "Homebrew cask";

        /// <inheritdoc/>
        public OsFamilyEnum RequiredHost => OsFamilyEnum.Any;

        /// <inheritdoc/>
        public OsFamilyEnum TargetOs => OsFamilyEnum.MacOs;

        /// <inheritdoc/>
        public IReadOnlyList<ToolRequirement> Tools => Array.Empty<ToolRequirement>();

        /// <inheritdoc/>
        public IReadOnlyList<string> Build(ChannelContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string name = context.Project.Name;
            string cask = context.Option("caskName", name);
            string arm = Checksums.Sha256(context.RequireAsset(AssetNames.MacPackage(name, context.Version, RuntimeTarget.Parse("osx-arm64")), "macos"));
            string intel = Checksums.Sha256(context.RequireAsset(AssetNames.MacPackage(name, context.Version, RuntimeTarget.Parse("osx-x64")), "macos"));
            StringBuilder s = new StringBuilder();
            s.Append("cask \"").Append(cask).Append("\" do\n");
            s.Append("  arch arm: \"arm64\", intel: \"x64\"\n\n");
            s.Append("  version \"").Append(context.Version).Append("\"\n");
            s.Append("  sha256 arm:   \"").Append(arm).Append("\",\n         intel: \"").Append(intel).Append("\"\n\n");
            s.Append("  url \"https://github.com/").Append(context.Manifest.Destinations.GithubRepo).Append("/releases/download/v#{version}/").Append(name).Append("-#{version}-osx-#{arch}.pkg\"\n");
            s.Append("  name \"").Append(context.Project.DisplayName).Append("\"\n");
            s.Append("  desc \"").Append(context.Project.Tagline.TrimEnd('.')).Append("\"\n");
            s.Append("  homepage \"").Append(context.Project.Homepage).Append("\"\n\n");
            s.Append("  pkg \"").Append(name).Append("-#{version}-osx-#{arch}.pkg\"\n\n");
            s.Append("  uninstall pkgutil: \"").Append(MacBundleFiles.PackageId(context.Project)).Append('"');
            string label = MacBundleFiles.AgentLabel(context.Project, context.Artifact);
            if (!String.IsNullOrEmpty(label)) s.Append(",\n            launchctl: \"").Append(label).Append('"');
            s.Append(",\n            delete: [\"").Append(MacBundleFiles.InstalledApp(context.Project)).Append('"');
            foreach (string command in context.Artifact.PathCommands) s.Append(", \"/usr/local/bin/").Append(command).Append('"');
            s.Append("]\nend\n");

            string output = Path.Combine(ManifestOutput.Directory(context, Id), cask + ".rb");
            File.WriteAllText(output, s.ToString());
            Console.WriteLine("[brewcask] wrote " + output);
            return new[] { output };
        }
    }
}
