namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Import Models: choose which provider endpoints to read, list their live models with a checkbox and a
    /// completions-or-embeddings choice each, and advertise the checked ones. Closes with the
    /// <see cref="ModelImportResult"/>, or null when cancelled. The flow itself is <see cref="XenoModelImport"/>.
    /// </summary>
    public sealed class ModelImportWindow : XenoDialogWindow
    {
        #region Private-Members

        private static readonly List<string> CapabilityChoices = new List<string> { "Completions", "Embeddings" };
        private readonly XenoWorkspace _Workspace;
        private readonly List<CheckBox> _ProviderBoxes = new List<CheckBox>();
        private readonly StackPanel _Models = new StackPanel { Spacing = 10 };
        private readonly Button _Load;
        private readonly Button _Import;
        private readonly TextBlock _Count;
        private readonly Border _Error;
        private List<ModelImportSource> _Sources = new List<ModelImportSource>();
        private bool _Busy = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the dialog.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public ModelImportWindow(XenoWorkspace workspace)
        {
            _Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Title = XenoModelImport.Title;
            Width = 760;
            Height = 640;
            MinWidth = 560;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;

            WrapPanel providers = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (string provider in XenoModelImport.Providers(workspace))
            {
                CheckBox box = new CheckBox { Content = provider, IsChecked = true, Margin = new Thickness(0, 0, 16, 4) };
                _ProviderBoxes.Add(box);
                providers.Children.Add(box);
            }

            _Load = Ui.Button("Load Models", () => _ = LoadAsync());
            _Import = Ui.Primary("Import", () => _ = ImportAsync());
            _Import.IsEnabled = false;
            _Count = Ui.Muted(String.Empty);
            _Count.VerticalAlignment = VerticalAlignment.Center;
            _Error = Ui.Notice(String.Empty, XenoToneEnum.Danger);
            _Error.IsVisible = false;

            StackPanel top = Ui.Stack(10, Ui.Title(XenoModelImport.Title), Ui.Muted(XenoModelImport.ProvidersIntro));
            if (_ProviderBoxes.Count == 0) top.Children.Add(Ui.Notice("No provider endpoints yet. Add one on the Providers page first.", XenoToneEnum.Info));
            else top.Children.Add(Ui.Row(providers, _Load));
            top.Children.Add(Ui.Muted(XenoModelImport.ModelsIntro));
            top.Margin = new Thickness(0, 0, 0, 12);

            Button cancel = Ui.Button("Cancel", () => CloseWith(null));
            cancel.IsCancel = true;
            DockPanel footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            StackPanel buttons = Ui.Row(cancel, _Import);
            DockPanel.SetDock(buttons, Dock.Right);
            footer.Children.Add(buttons);
            footer.Children.Add(_Count);

            DockPanel root = new DockPanel { Margin = new Thickness(20) };
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            DockPanel.SetDock(_Error, Dock.Bottom);
            root.Children.Add(_Error);
            root.Children.Add(new ScrollViewer { Content = _Models, Padding = new Thickness(0, 0, 12, 0) });
            Content = root;
            Opened += (sender, args) =>
            {
                if (_ProviderBoxes.Count > 0) _ = LoadAsync();
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show the dialog.
        /// </summary>
        /// <param name="owner">Owner window.</param>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Result, or null when cancelled.</returns>
        public static Task<ModelImportResult?> ShowAsync(Window? owner, XenoWorkspace workspace)
        {
            return XenoDialogs.ShowModalAsync<ModelImportResult?>(new ModelImportWindow(workspace), owner);
        }

        #endregion

        #region Private-Methods

        private async Task LoadAsync()
        {
            if (_Busy) return;
            List<string> chosen = _ProviderBoxes.Where(item => item.IsChecked == true).Select(item => (string)item.Content!).ToList();
            if (chosen.Count == 0)
            {
                ShowError("Choose at least one provider endpoint.");
                return;
            }

            _Busy = true;
            _Error.IsVisible = false;
            _Load.IsEnabled = false;
            _Import.IsEnabled = false;
            _Models.Children.Clear();
            _Models.Children.Add(Ui.Muted("Reading models from " + String.Join(", ", chosen) + "..."));
            try
            {
                _Sources = await XenoModelImport.LoadAsync(_Workspace, chosen, CancellationToken.None).ConfigureAwait(true);
                RenderSources();
            }
            finally
            {
                _Busy = false;
                _Load.IsEnabled = true;
                UpdateCount();
            }
        }

        private void RenderSources()
        {
            _Models.Children.Clear();
            foreach (ModelImportSource source in _Sources)
            {
                StackPanel section = new StackPanel { Spacing = 6 };
                section.Children.Add(Ui.Heading(source.Heading));
                if (!source.Reachable)
                {
                    section.Children.Add(Ui.Notice(source.Error ?? "The provider did not answer.", XenoToneEnum.Danger));
                }

                foreach (ModelImportCandidate candidate in source.Candidates)
                {
                    section.Children.Add(BuildRow(candidate));
                }

                _Models.Children.Add(section);
            }

            if (_Sources.Count == 0) _Models.Children.Add(Ui.Muted("No providers chosen."));
        }

        private Control BuildRow(ModelImportCandidate candidate)
        {
            Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,150,110") };
            CheckBox box = new CheckBox { Content = candidate.Name, IsChecked = candidate.Selected, IsEnabled = !candidate.Advertised, VerticalAlignment = VerticalAlignment.Center };
            box.IsCheckedChanged += (sender, args) =>
            {
                candidate.Selected = box.IsChecked == true;
                UpdateCount();
            };
            Grid.SetColumn(box, 0);
            grid.Children.Add(box);

            TextBlock size = Ui.Muted(candidate.Size);
            size.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(size, 1);
            grid.Children.Add(size);

            ComboBox capability = new ComboBox { ItemsSource = CapabilityChoices, SelectedIndex = candidate.Embeddings ? 1 : 0, IsEnabled = !candidate.Advertised, MinWidth = 140 };
            capability.SelectionChanged += (sender, args) => candidate.Embeddings = capability.SelectedIndex == 1;
            Grid.SetColumn(capability, 2);
            grid.Children.Add(capability);

            if (candidate.Advertised)
            {
                Border badge = Ui.Badge(candidate.Status, XenoToneEnum.Success);
                badge.HorizontalAlignment = HorizontalAlignment.Left;
                badge.VerticalAlignment = VerticalAlignment.Center;
                badge.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(badge, 3);
                grid.Children.Add(badge);
            }

            return grid;
        }

        private void UpdateCount()
        {
            int count = XenoModelImport.SelectedCount(_Sources);
            _Count.Text = count == 0 ? "No models selected." : count + " model" + (count == 1 ? String.Empty : "s") + " selected.";
            _Import.IsEnabled = !_Busy && count > 0;
        }

        private async Task ImportAsync()
        {
            List<ModelImportItem> items = XenoModelImport.Items(_Sources);
            if (items.Count == 0 || _Busy) return;
            _Busy = true;
            _Import.IsEnabled = false;
            _Error.IsVisible = false;
            try
            {
                ModelImportResult result = await _Workspace.ImportModelsAsync(items, CancellationToken.None).ConfigureAwait(true);
                CloseWith(result);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                ShowError(ex.Message);
            }
            finally
            {
                _Busy = false;
                UpdateCount();
            }
        }

        private void ShowError(string message)
        {
            if (_Error.Child is SelectableTextBlock text) text.Text = message;
            _Error.IsVisible = true;
        }

        #endregion
    }
}
