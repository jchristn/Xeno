namespace Xeno.Desktop.Common
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// About window shared by the desktop app and the tray agent: product, version, client identity, file locations,
    /// links, and a Copy Diagnostics button.
    /// </summary>
    public sealed class AboutWindow : XenoDialogWindow
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Create the About window.
        /// </summary>
        /// <param name="productName">Product name shown in the title, for example "Xeno" or "Xeno Agent".</param>
        public AboutWindow(string productName)
        {
            Title = "About " + productName;
            Width = 560;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;

            ClientStatusSnapshot snapshot = ClientStatusSnapshot.Capture();
            string diagnostics = XenoDiagnostics.Build(productName, snapshot);

            StackPanel stack = new StackPanel { Spacing = 14, Margin = new Thickness(24) };
            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            Bitmap? logo = XenoAssets.LoadLogo();
            if (logo != null) header.Children.Add(new Image { Source = logo, Width = 64, Height = 64 });
            StackPanel titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = productName, FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = XenoTheme.Current.Text });
            titles.Children.Add(Ui.Muted("Version " + XenoProduct.Version));
            header.Children.Add(titles);
            stack.Children.Add(header);
            stack.Children.Add(Ui.Body(XenoProduct.Tagline));

            stack.Children.Add(Ui.Details(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Client", DisplayFormat.OrDash(snapshot.Name)),
                new KeyValuePair<string, string>("Client ID", DisplayFormat.OrDash(snapshot.ClientId)),
                new KeyValuePair<string, string>("Status", snapshot.StatusText),
                new KeyValuePair<string, string>("Xeno server", DisplayFormat.OrDash(snapshot.RestBaseUrl)),
                new KeyValuePair<string, string>("Settings", snapshot.SettingsFile),
                new KeyValuePair<string, string>("Data folder", ClientSettings.SettingsDirectory),
                new KeyValuePair<string, string>(".NET", RuntimeInformation.FrameworkDescription),
                new KeyValuePair<string, string>("OS", RuntimeInformation.OSDescription)
            }, 110));

            StackPanel links = Ui.Row(
                Ui.Button("Documentation", () => XenoAppLauncher.OpenUrl(XenoProduct.DocumentationUrl)),
                Ui.Button("Report an Issue", () => XenoAppLauncher.OpenUrl(XenoProduct.IssuesUrl)),
                Ui.Button("Community", () => XenoAppLauncher.OpenUrl(XenoProduct.CommunityUrl)));
            stack.Children.Add(links);

            Button copy = Ui.Button("Copy Diagnostics", () => _ = XenoClipboard.CopyAsync(this, diagnostics));
            Button close = Ui.Primary("Close", () => CloseWith(null));
            close.IsDefault = true;
            close.IsCancel = true;
            StackPanel buttons = Ui.Row(copy, close);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            stack.Children.Add(buttons);
            Content = stack;
        }

        #endregion

    }
}
