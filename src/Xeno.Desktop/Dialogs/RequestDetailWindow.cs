namespace Xeno.Desktop.Dialogs
{
    using System;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Detail of one request: summary, timeline, headers, and stored bodies.
    /// </summary>
    public sealed class RequestDetailWindow : XenoDialogWindow
    {
        /// <summary>
        /// Create the window.
        /// </summary>
        /// <param name="entry">Request history entry.</param>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public RequestDetailWindow(LocalRequestHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            Title = "Request " + entry.Id;
            Width = 860;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;

            TabControl tabs = new TabControl();
            Xeno.Desktop.Controls.ColumnChart timing = new Xeno.Desktop.Controls.ColumnChart { Height = 150 };
            timing.SetChart(XenoUsageCharts.RequestTiming(XenoUsageCharts.StagesFor(entry)));
            tabs.Items.Add(new TabItem { Header = "Summary", Content = Scroll(Ui.Stack(14, Ui.Details(RequestDetailFormatter.Summary(entry), 150), Ui.Heading("Where the time went"), timing, Ui.Heading("Timeline"), Mono(String.Join(Environment.NewLine, RequestDetailFormatter.Timeline(entry))))) });
            tabs.Items.Add(new TabItem { Header = "Headers", Content = Scroll(Ui.Stack(14, Ui.Heading("Request headers"), Mono(RequestDetailFormatter.Headers(entry.RequestHeaders)), Ui.Heading("Response headers"), Mono(RequestDetailFormatter.Headers(entry.ResponseHeaders)))) });
            tabs.Items.Add(new TabItem { Header = "Request body", Content = Body(entry.RequestBody) });
            tabs.Items.Add(new TabItem { Header = "Response body", Content = Body(entry.ResponseBody) });

            Button copy = Ui.Button("Copy as Text", () => _ = XenoClipboard.CopyAsync(this, RequestDetailFormatter.ToText(entry, true)));
            Button close = Ui.Primary("Close", () => CloseWith(null));
            close.IsCancel = true;
            StackPanel buttons = Ui.Row(copy, close);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 12, 0, 0);
            DockPanel root = new DockPanel { Margin = new Thickness(16) };
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(tabs);
            Content = root;
        }

        private static Control Scroll(Control content)
        {
            return new ScrollViewer { Content = content, Padding = new Thickness(4, 12) };
        }

        private static Control Mono(string text)
        {
            return new TextBox { Text = text, IsReadOnly = true, FontFamily = Ui.Mono, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap, AcceptsReturn = true };
        }

        private static Control Body(string? body)
        {
            if (String.IsNullOrEmpty(body))
            {
                return new Border { Padding = new Thickness(8, 16), Child = Ui.Muted("Not stored. Bodies are kept only when history body persistence is enabled in Settings.") };
            }

            return new TextBox { Text = body, IsReadOnly = true, FontFamily = Ui.Mono, FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap, AcceptsReturn = true, Margin = new Thickness(0, 12, 0, 0) };
        }
    }
}
