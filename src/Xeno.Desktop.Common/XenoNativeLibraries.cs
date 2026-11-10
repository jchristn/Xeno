namespace Xeno.Desktop.Common
{
    using System;
    using System.IO;

    /// <summary>
    /// Starts an Avalonia app and, on Linux, turns a missing native library into a short message with the command that
    /// installs it. Minimal distro images and WSL (where WSLg supplies only the display server) often lack the X11
    /// client libraries Avalonia loads; without this the app ends with a DllNotFoundException stack trace such as
    /// "Unable to load shared library 'libICE.so.6'".
    /// </summary>
    public static class XenoNativeLibraries
    {
        /// <summary>
        /// Debian/Ubuntu packages with the libraries Avalonia.X11 and SkiaSharp load (libX11, libICE, libSM, libXext,
        /// libXrandr, libXi, libXcursor, libXfixes, fontconfig).
        /// </summary>
        public const string DebianPackages = "libx11-6 libice6 libsm6 libxext6 libxrandr2 libxi6 libxcursor1 libxfixes3 libfontconfig1";

        /// <summary>
        /// Fedora packages with the same libraries.
        /// </summary>
        public const string FedoraPackages = "libX11 libICE libSM libXext libXrandr libXi libXcursor libXfixes fontconfig";

        /// <summary>
        /// Run the app. On Linux, a DllNotFoundException (also when wrapped in other exceptions) prints the install
        /// command to standard error and returns exit code 1. Other exceptions, and every exception on other
        /// platforms, are rethrown.
        /// </summary>
        /// <param name="appName">App name for the message, for example "Xeno".</param>
        /// <param name="start">Starts the app and returns its exit code.</param>
        /// <returns>Exit code.</returns>
        public static int Run(string appName, Func<int> start)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            if (!OperatingSystem.IsLinux()) return start();

            try
            {
                return start();
            }
            catch (Exception e)
            {
                DllNotFoundException? missing = FindMissingLibrary(e);
                if (missing == null) throw;
                Console.Error.WriteLine(Describe(appName, missing.Message));
                return 1;
            }
        }

        /// <summary>
        /// Find a DllNotFoundException in an exception or its inner exceptions.
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <returns>The DllNotFoundException, or null.</returns>
        public static DllNotFoundException? FindMissingLibrary(Exception? exception)
        {
            for (int depth = 0; exception != null && depth < 32; depth++)
            {
                if (exception is DllNotFoundException missing) return missing;
                if (exception is AggregateException aggregate)
                {
                    foreach (Exception inner in aggregate.InnerExceptions)
                    {
                        DllNotFoundException? found = FindMissingLibrary(inner);
                        if (found != null) return found;
                    }
                }

                exception = exception.InnerException;
            }

            return null;
        }

        /// <summary>
        /// Build the message for a missing library.
        /// </summary>
        /// <param name="appName">App name.</param>
        /// <param name="detail">The DllNotFoundException message.</param>
        /// <returns>Message.</returns>
        public static string Describe(string appName, string detail)
        {
            string hint;
            if (File.Exists("/usr/bin/apt-get")) hint = "  sudo apt install " + DebianPackages;
            else if (File.Exists("/usr/bin/dnf")) hint = "  sudo dnf install " + FedoraPackages;
            else hint = "  Debian/Ubuntu: sudo apt install " + DebianPackages + Environment.NewLine + "  Fedora:        sudo dnf install " + FedoraPackages;

            return "Error: " + appName + " cannot start because a shared library is missing: " + detail + Environment.NewLine
                + "Install the libraries it needs with:" + Environment.NewLine
                + hint;
        }
    }
}
