namespace Xeno.Publisher
{
    /// <summary>
    /// File names for every release asset. Each keeps the version so a downloads folder holding several releases
    /// stays readable; Debian and RPM follow their own conventions because package tools depend on them.
    /// </summary>
    public static class AssetNames
    {
        /// <summary>
        /// Windows installer: xeno-0.2.0-win-x64-setup.exe.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string WindowsSetup(string name, string version, RuntimeTarget target)
        {
            return name + "-" + version + "-" + target.Rid + "-setup.exe";
        }

        /// <summary>
        /// macOS installer package: xeno-0.2.0-osx-arm64.pkg.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string MacPackage(string name, string version, RuntimeTarget target)
        {
            return name + "-" + version + "-" + target.Rid + ".pkg";
        }

        /// <summary>
        /// macOS disk image: xeno-0.2.0-osx-arm64.dmg.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string MacDiskImage(string name, string version, RuntimeTarget target)
        {
            return name + "-" + version + "-" + target.Rid + ".dmg";
        }

        /// <summary>
        /// Debian package: xeno_0.2.0_amd64.deb.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string Deb(string name, string version, RuntimeTarget target)
        {
            return name + "_" + version + "_" + target.DebArch + ".deb";
        }

        /// <summary>
        /// RPM package: xeno-0.2.0-1.x86_64.rpm.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string Rpm(string name, string version, RuntimeTarget target)
        {
            return name + "-" + version + "-1." + target.RpmArch + ".rpm";
        }

        /// <summary>
        /// AppImage: xeno-0.2.0-x86_64.AppImage.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string AppImage(string name, string version, RuntimeTarget target)
        {
            return name + "-" + version + "-" + target.RpmArch + ".AppImage";
        }

        /// <summary>
        /// Portable archive: xeno-0.2.0-win-x64.zip, or .tar.gz on macOS and Linux.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>File name.</returns>
        public static string Archive(string name, string version, RuntimeTarget target)
        {
            return ArchiveFolder(name, version, target) + (target.Os == OsFamilyEnum.Windows ? ".zip" : ".tar.gz");
        }

        /// <summary>
        /// Top-level folder inside a portable archive: xeno-0.2.0-linux-x64.
        /// </summary>
        /// <param name="name">Project name.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <returns>Folder name.</returns>
        public static string ArchiveFolder(string name, string version, RuntimeTarget target)
        {
            return name + "-" + version + "-" + target.Rid;
        }
    }
}
