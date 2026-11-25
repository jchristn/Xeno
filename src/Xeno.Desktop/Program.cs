namespace Xeno.Desktop
{
    using System;
    using System.Threading;
    using Avalonia;
    using Avalonia.Media;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

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
        /// True when the app was started with --settings, to open the Settings window at startup.
        /// </summary>
        public static bool OpenSettingsAtStart { get; private set; } = false;

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
                Console.WriteLine("Usage: xeno-desktop [--page <page>] [--settings] [--server <url>] [--home <dir>]");
                Console.WriteLine();
                foreach (string line in XenoLaunchOptions.Usage) Console.WriteLine(line);
                return options.Error != null ? 1 : 0;
            }

            StartPage = options.StartPage;
            OpenSettingsAtStart = options.OpenSettings;
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
                    // Hand the request to the running app so it comes to the front (and opens the page or Settings).
                    try
                    {
                        DesktopActivation.Post(new DesktopActivationRequest { Page = options.StartPage, OpenSettings = options.OpenSettings });
                        Console.WriteLine("Xeno is already running; asked it to come to the front.");
                    }
                    catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine("Xeno is already running, and could not be asked to come to the front: " + ex.Message);
                    }

                    return 0;
                }

                return XenoNativeLibraries.Run("Xeno", () => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args));
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
                .With(new FontManagerOptions
                {
                    // Default every control to the OS's own UI font (SF Pro, Segoe UI, Noto Sans/Ubuntu/Cantarell), with
                    // the bundled Inter only when none is installed. Avalonia's own default is Helvetica on macOS.
                    DefaultFamilyName = XenoFonts.SansFamilyName
                })
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
