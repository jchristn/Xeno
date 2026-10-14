namespace Xeno.Desktop
{
    using System;
    using System.Threading;
    using Avalonia;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Entry point for xeno-desktop.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Page requested on the command line with --page, or null.
        /// </summary>
        public static XenoPageEnum? StartPage { get; private set; } = null;

        /// <summary>
        /// Message to show in the status bar at startup, for example the server chosen with --server, or null.
        /// </summary>
        public static string? StartupMessage { get; private set; } = null;

        /// <summary>
        /// Run the desktop app.
        /// </summary>
        /// <param name="args">Arguments: see <see cref="XenoLaunchOptions.Usage"/> (--page, --server, --home).</param>
        /// <returns>Exit code.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            XenoLaunchOptions options = XenoLaunchOptions.Parse(args);
            if (options.ShowHelp || options.Error != null)
            {
                if (options.Error != null) Console.Error.WriteLine(options.Error);
                Console.WriteLine("Usage: xeno-desktop [--page <page>] [--server <url>] [--home <dir>]");
                Console.WriteLine();
                foreach (string line in XenoLaunchOptions.Usage) Console.WriteLine(line);
                return options.Error != null ? 1 : 0;
            }

            StartPage = options.StartPage;
            options.ApplyHome();
            if (options.Server != null)
            {
                using (XenoWorkspace workspace = new XenoWorkspace())
                {
                    StartupMessage = options.ApplyServerAsync(workspace, CancellationToken.None).GetAwaiter().GetResult();
                }

                Console.WriteLine(StartupMessage);
            }

            using (SingleInstanceLock? instance = SingleInstanceLock.TryAcquire(XenoAppLauncher.DesktopLockName))
            {
                if (instance == null)
                {
                    Console.Error.WriteLine("Xeno is already running.");
                    return 0;
                }

                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        /// <summary>
        /// Build the Avalonia application.
        /// </summary>
        /// <returns>Builder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new MacOSPlatformOptions
                {
                    ShowInDock = true,
                    DisableNativeMenus = false,
                    DisableDefaultApplicationMenuItems = false
                })
                .LogToTrace();
        }
    }
}
