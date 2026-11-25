namespace Xeno.Tray
{
    using System;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Media;
    using Xeno.Client.Hosting;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Entry point for xeno-tray, the Xeno Agent.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Run the tray agent. Only one instance runs per client home directory.
        /// </summary>
        /// <param name="args">Arguments: --home &lt;dir&gt; uses a different client home directory.</param>
        /// <returns>Exit code.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            XenoLaunchOptions options = XenoLaunchOptions.Parse(args);
            if (options.Error != null)
            {
                Console.Error.WriteLine(options.Error);
                return 1;
            }

            options.ApplyHome();
            using (SingleInstanceLock? instance = SingleInstanceLock.TryAcquire(XenoAppLauncher.TrayLockName))
            {
                if (instance == null)
                {
                    Console.Error.WriteLine("Xeno Agent is already running.");
                    return 0;
                }

                XenoDialogs.HideDockWhenIdle = true;
                return XenoNativeLibraries.Run("Xeno Agent", () => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown));
            }
        }

        /// <summary>
        /// Build the Avalonia application. The agent lives in the menu bar or notification area and appears in the
        /// macOS Dock only while one of its windows is open.
        /// </summary>
        /// <returns>Builder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new FontManagerOptions
                {
                    // Match the desktop app: the OS's own UI font, with the bundled Inter only when none is installed.
                    DefaultFamilyName = XenoFonts.SansFamilyName
                })
                .With(new MacOSPlatformOptions { ShowInDock = false })
                .LogToTrace();
        }
    }
}
