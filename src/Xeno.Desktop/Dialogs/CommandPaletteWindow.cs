namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;

    /// <summary>
    /// A searchable launcher over every command in the shared menu layout.
    /// </summary>
    public sealed class CommandPaletteWindow : XenoDialogWindow
    {
        #region Private-Members

        private readonly List<XenoCommandEnum> _Commands;
        private readonly IXenoMenuHost _Host;
        private readonly TextBox _Search = new TextBox();
        private readonly ListBox _List = new ListBox();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the palette.
        /// </summary>
        /// <param name="host">Command host.</param>
        /// <exception cref="ArgumentNullException">Host is null.</exception>
        public CommandPaletteWindow(IXenoMenuHost host)
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
            _Commands = XenoMenuLayout.Commands(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, OperatingSystem.IsMacOS()))
                .Where(item => item != XenoCommandEnum.CommandPalette)
                .ToList();
            Title = "Command Palette";
            Width = 520;
            Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;
            _Search.PlaceholderText = "Type a command";
            _Search.TextChanged += (sender, args) => Filter();
            _Search.KeyDown += OnKeyDown;
            _List.DoubleTapped += (sender, args) => Run();
            _List.KeyDown += OnKeyDown;
            DockPanel root = new DockPanel { Margin = new Thickness(14) };
            DockPanel.SetDock(_Search, Dock.Top);
            _Search.Margin = new Thickness(0, 0, 0, 10);
            root.Children.Add(_Search);
            root.Children.Add(_List);
            Content = root;
            Opened += (sender, args) => _Search.Focus();
            Filter();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show the palette and run the chosen command.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="host">Command host.</param>
        /// <returns>Task.</returns>
        public static async Task ShowAsync(Window owner, IXenoMenuHost host)
        {
            CommandPaletteWindow palette = new CommandPaletteWindow(host);
            object? result = await XenoDialogs.ShowModalAsync<object>(palette, owner).ConfigureAwait(true);
            if (result is XenoCommandEnum command && host.CanExecute(command)) host.Execute(command);
        }

        #endregion

        #region Private-Methods

        private void Filter()
        {
            string query = (_Search.Text ?? String.Empty).Trim();
            List<PaletteItem> items = _Commands
                .Where(item => _Host.CanExecute(item))
                .Select(item => new PaletteItem(item, XenoCommandAvailability.Title(item)))
                .Where(item => query.Length == 0 || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _List.ItemsSource = items;
            if (items.Count > 0) _List.SelectedIndex = 0;
        }

        private void OnKeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter)
            {
                Run();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                CloseWith(null);
                args.Handled = true;
            }
            else if (args.Key == Key.Down && sender == _Search && _List.ItemCount > 0)
            {
                _List.Focus();
                args.Handled = true;
            }
        }

        private void Run()
        {
            if (_List.SelectedItem is PaletteItem item) CloseWith(item.Command);
        }

        #endregion
    }
}
