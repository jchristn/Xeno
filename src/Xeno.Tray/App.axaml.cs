namespace Xeno.Tray
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;

    /// <summary>
    /// Xeno tray application.
    /// </summary>
    public sealed class App : Application
    {
        private XenoTrayViewModel? _ViewModel;

        /// <inheritdoc />
        public override void Initialize()
        {
            _ViewModel = new XenoTrayViewModel(new XenoTrayClientController(new XenoTrayOptions(), new XenoTrayProcessRunner()));
            DataContext = _ViewModel;
            AvaloniaXamlLoader.Load(this);
        }

        /// <inheritdoc />
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            _ViewModel?.Start();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
