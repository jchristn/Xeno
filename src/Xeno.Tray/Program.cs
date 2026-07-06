namespace Xeno.Tray
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;

    /// <summary>
    /// Xeno tray application entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Run the tray application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }

        /// <summary>
        /// Build the Avalonia app builder.
        /// </summary>
        /// <returns>Avalonia app builder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
