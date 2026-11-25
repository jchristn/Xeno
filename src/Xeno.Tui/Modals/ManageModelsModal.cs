namespace Xeno.Tui.Modals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Unicode;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Pages;

    /// <summary>
    /// Manage Models for one provider: its live models, a field to pull a model by name (with "advertise when
    /// finished"), a progress bar and status for each of its pulls with a Cancel button, and Delete. Pulls keep running
    /// after the dialog closes. Providers other than Ollama are listed only, with a note. Keys: Up and Down move in the
    /// table, Tab moves between the table, the pull field, and the advertise option, Enter in the field pulls, d deletes
    /// the selected model, c cancels the newest active pull, r refreshes, v chooses columns, and Esc closes. Every button
    /// takes a click. Call <see cref="Detach"/> after it closes.
    /// </summary>
    internal sealed class ManageModelsModal : DialogModal, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Latest inventory, or null while loading.
        /// </summary>
        public ProviderModelInventory? Inventory { get; private set; } = null;

        /// <summary>
        /// Pull field.
        /// </summary>
        public TextField PullField { get; } = new TextField { Placeholder = XenoModelManagement.PullPlaceholder, UseLineEditingKeys = true };

        /// <summary>
        /// "Advertise when finished" option.
        /// </summary>
        public Checkbox Advertise { get; } = new Checkbox(XenoModelManagement.AdvertiseLabel, false);

        /// <summary>
        /// Models table.
        /// </summary>
        public DataTable<ProviderInventoryRow> Table { get; }

        /// <summary>
        /// Column choice for the models table.
        /// </summary>
        public ITuiTableColumns Columns { get; }

        #endregion

        #region Private-Members

        private const int FocusTable = 0;
        private const int FocusField = 1;
        private const int FocusAdvertise = 2;
        private const int TableRows = 10;
        private readonly TuiContext _Context;
        private readonly Action _Changed;
        private readonly Dictionary<string, Rect> _Buttons = new Dictionary<string, Rect>(StringComparer.Ordinal);
        private readonly Dictionary<string, Rect> _CancelButtons = new Dictionary<string, Rect>(StringComparer.Ordinal);
        private List<ModelPullJob> _Jobs = new List<ModelPullJob>();
        private Rect _TableRect = new Rect(0, 0, 0, 0);
        private Rect _FieldRect = new Rect(0, 0, 0, 0);
        private Rect _AdvertiseRect = new Rect(0, 0, 0, 0);
        private int _Focus = FocusTable;
        private string? _Error = null;
        private string? _Notice = null;
        private bool _Detached = false;
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the dialog and start reading the provider's models.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <param name="provider">Provider name.</param>
        /// <param name="changed">Called on the loop thread after the provider's models or advertisements change.</param>
        public ManageModelsModal(TuiContext context, string provider, Action changed)
        {
            _Context = context ?? throw new ArgumentNullException(nameof(context));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _Changed = changed ?? (() => { });
            Title = " " + XenoModelManagement.Title(provider) + " ";
            FooterHint = " Tab field  Enter pull  d delete  c cancel pull  r refresh  v columns  Esc close ";
            MinContentWidth = 80;
            MaxContentWidth = 112;

            Table = new DataTable<ProviderInventoryRow> { EmptyText = "No models on this provider yet.", KeySelector = row => row.Name, StretchLastColumn = false };
            Table.Column(new DataColumn<ProviderInventoryRow>("Model", row => row.Name, true));
            Table.Column(new DataColumn<ProviderInventoryRow>("Size", row => row.Size, true) { Width = 9 });
            Table.Column(new DataColumn<ProviderInventoryRow>("Parameters", row => row.ParameterSize, true) { Width = 11 });
            Table.Column(new DataColumn<ProviderInventoryRow>("Quantization", row => row.Quantization, true) { Width = 13 });
            Table.Column(new DataColumn<ProviderInventoryRow>("Modified", row => row.Modified, true) { Width = 17 });
            Table.Column(new DataColumn<ProviderInventoryRow>("Advertised", row => row.Advertised, true) { Width = 11 });
            Table.SetLoading();
            TuiTableColumns<ProviderInventoryRow> columns = new TuiTableColumns<ProviderInventoryRow>(Table, "terminal:ManageModels:0") { Title = "Models" };
            Columns = columns;
            columns.Apply(XenoTableColumns.Visible(_Context.Preferences, columns.Id, columns.Columns, columns.Defaults.ToList()));
            PullField.ValueChanged += (sender, args) => _Error = null;
            ApplyFocus();
            _Context.Workspace.ModelPulls.JobChanged += OnJobChanged;
            RefreshJobs();
            Reload();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Stop following pulls. Call after the dialog closes; pulls themselves keep running.
        /// </summary>
        public void Detach()
        {
            if (_Detached) return;
            _Detached = true;
            _Context.Workspace.ModelPulls.JobChanged -= OnJobChanged;
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
            ApplyFocusTheme(_Theme);
            Table.ApplyTheme(_Theme);
            PullField.ApplyTheme(_Theme);
            Advertise.ApplyTheme(_Theme);
        }

        /// <summary>
        /// Read the provider's models again.
        /// </summary>
        public void Reload()
        {
            _ = _Context.LoadAsync(token => _Context.Workspace.GetProviderInventoryAsync(Provider, token), inventory =>
            {
                Inventory = inventory;
                Table.EmptyText = inventory.Reachable ? "No models on this provider yet." : "Could not read models: " + (inventory.Error ?? "no answer") + ".";
                Table.Bind(XenoModelManagement.Rows(inventory));
                if (!inventory.CanPull && _Focus != FocusTable)
                {
                    _Focus = FocusTable;
                    ApplyFocus();
                }

                _Context.App.RequestRender();
            }, message => Table.SetError(message));
        }

        /// <summary>
        /// Pull the model named in the pull field.
        /// </summary>
        public void StartPull()
        {
            string model = PullField.Value.Trim();
            if (model.Length == 0)
            {
                _Error = "Enter the model to pull, for example " + XenoModelManagement.PullPlaceholder + ".";
                return;
            }

            bool advertise = Advertise.Checked;
            _ = _Context.LoadAsync(token => _Context.Workspace.StartModelPullAsync(Provider, model, advertise, token), job =>
            {
                PullField.Value = String.Empty;
                _Error = null;
                _Notice = XenoModelManagement.PullLine(job) + ". It keeps going if you close this dialog" + (_Context.Workspace.ModelPulls.RunsInWorker ? String.Empty : " (but stops if you quit the app, because the Xeno client is not running)") + ".";
                _Context.Notify(XenoModelManagement.PullLine(job) + " on " + Provider + ".", NotificationSeverity.Info);
                RefreshJobs();
            }, message => _Error = message);
        }

        /// <summary>
        /// Ask, then delete the selected model from the provider's disk and remove its advertisement.
        /// </summary>
        /// <returns>Task that completes after the delete.</returns>
        public async Task DeleteSelectedAsync()
        {
            ProviderInventoryRow? row = Table.SelectedRow;
            if (row == null) return;
            if (Inventory == null || !Inventory.CanDelete)
            {
                _Error = XenoModelManagement.OllamaOnlyNote;
                return;
            }

            bool confirmed = await _Context.ConfirmAsync("Delete Model", XenoModelManagement.DeleteConfirmation(Provider, row.Name, row.IsAdvertised), "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await _Context.LoadAsync(token => _Context.Workspace.DeleteProviderModelAsync(Provider, row.Name, token), removed =>
            {
                string message = XenoModelManagement.Deleted(Provider, row.Name, removed);
                _Notice = message;
                _Error = null;
                _Context.Notify(message, NotificationSeverity.Success);
                _Changed();
                Reload();
            }, message => _Error = message).ConfigureAwait(false);
        }

        /// <summary>
        /// Cancel a pull.
        /// </summary>
        /// <param name="id">Pull id.</param>
        public void CancelPull(string id)
        {
            ModelPullJob? job = _Jobs.FirstOrDefault(item => item.Id == id);
            _ = _Context.LoadAsync(token => _Context.Workspace.CancelModelPullAsync(id, token), cancelled =>
            {
                if (cancelled && job != null) _Notice = "Cancelling the pull of " + job.Model + "...";
            });
        }

        /// <summary>
        /// Route keys to the focused part, then the dialog's own keys.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape)
            {
                RequestClose(null);
                return true;
            }

            if (key.Code == KeyCode.Tab)
            {
                MoveFocus((key.Modifiers & KeyModifiers.Shift) == 0);
                return true;
            }

            if (_Focus == FocusField)
            {
                if (key.Code == KeyCode.Enter)
                {
                    StartPull();
                    return true;
                }

                PullField.HandleKey(key);
                return true;
            }

            if (_Focus == FocusAdvertise)
            {
                if (key.Code == KeyCode.Enter) StartPull();
                else Advertise.HandleKey(key);
                return true;
            }

            if (key.Code == KeyCode.Delete || PageKeys.Is(key, 'd'))
            {
                _ = DeleteSelectedAsync();
                return true;
            }

            if (PageKeys.Is(key, 'r'))
            {
                Reload();
                return true;
            }

            if (PageKeys.Is(key, 'c'))
            {
                ModelPullJob? active = _Jobs.LastOrDefault(item => item.Active);
                if (active != null) CancelPull(active.Id);
                return true;
            }

            if (PageKeys.Is(key, TuiPage.ColumnsKey))
            {
                _ = _Context.ChooseColumnsAsync(Columns);
                return true;
            }

            Table.HandleKey(key);
            return true;
        }

        /// <summary>
        /// Route clicks to the table, the field, the option, and the buttons.
        /// </summary>
        /// <param name="mouse">Mouse event in screen coordinates.</param>
        /// <returns>True.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null) return true;
            Point at = new Point(mouse.X, mouse.Y);
            bool press = mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left;
            if (press)
            {
                foreach (KeyValuePair<string, Rect> cancel in _CancelButtons)
                {
                    if (!cancel.Value.Contains(at)) continue;
                    CancelPull(cancel.Key);
                    return true;
                }

                foreach (KeyValuePair<string, Rect> button in _Buttons)
                {
                    if (!button.Value.Contains(at)) continue;
                    Press(button.Key);
                    return true;
                }

                if (_FieldRect.Contains(at))
                {
                    SetFocus(FocusField);
                    PullField.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _FieldRect.X, mouse.Y - _FieldRect.Y, mouse.Modifiers, mouse.ClickCount));
                    return true;
                }

                if (_AdvertiseRect.Contains(at))
                {
                    SetFocus(FocusAdvertise);
                    Advertise.Checked = !Advertise.Checked;
                    return true;
                }
            }

            if (_TableRect.Contains(at))
            {
                if (press) SetFocus(FocusTable);
                Table.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _TableRect.X, mouse.Y - _TableRect.Y, mouse.Modifiers, mouse.ClickCount));
            }

            return true;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Content width.
        /// </summary>
        /// <param name="availableWidth">Available width.</param>
        /// <returns>Width.</returns>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(availableWidth, 108);
        }

        /// <summary>
        /// Content height.
        /// </summary>
        /// <param name="contentWidth">Width.</param>
        /// <returns>Height.</returns>
        protected override int MeasureContentHeight(int contentWidth)
        {
            int pulls = CanPull ? 2 + Math.Max(1, _Jobs.Count) * 2 : 0;
            return SummaryLines(contentWidth).Count + 1 + TableRows + 1 + pulls + 2;
        }

        /// <summary>
        /// Render the summary, the table, the pull area, the message, and the buttons.
        /// </summary>
        /// <param name="content">Surface.</param>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            int height = content.Size.Height;
            Rect origin = ContentBounds;
            int row = 0;
            foreach (StyledText line in SummaryLines(width)) content.DrawStyledText(0, row++, line);
            _Buttons.Clear();
            _CancelButtons.Clear();
            string columnsLabel = "[" + XenoTableColumns.ControlText + "]";
            content.DrawText(0, row, "Models", _Theme.Accent.WithAttribute(CellAttributes.Bold, true));
            int columnsX = Math.Max(0, width - columnsLabel.Length);
            content.DrawText(columnsX, row, columnsLabel, _Theme.Accent);
            _Buttons["columns"] = new Rect(origin.X + columnsX, origin.Y + row, columnsLabel.Length, 1);
            row++;

            int pullRows = CanPull ? 2 + Math.Max(1, _Jobs.Count) * 2 : 0;
            int tableHeight = Math.Max(3, height - row - pullRows - 2);
            Table.Render(new SurfaceView(content, new Rect(0, row, width, tableHeight)));
            _TableRect = new Rect(origin.X, origin.Y + row, width, tableHeight);
            row += tableHeight;

            _FieldRect = new Rect(0, 0, 0, 0);
            _AdvertiseRect = new Rect(0, 0, 0, 0);
            if (CanPull)
            {
                row++;
                string label = "Pull: ";
                content.DrawText(0, row, label, _Focus == FocusField ? _Theme.Accent : _Theme.Muted);
                string advertise = (Advertise.Checked ? "[x] " : "[ ] ") + XenoModelManagement.AdvertiseLabel;
                string pull = "[ Pull ]";
                int fieldWidth = Math.Max(10, width - label.Length - advertise.Length - pull.Length - 4);
                PullField.Render(new SurfaceView(content, new Rect(label.Length, row, fieldWidth, 1)));
                _FieldRect = new Rect(origin.X + label.Length, origin.Y + row, fieldWidth, 1);
                int x = label.Length + fieldWidth + 2;
                content.DrawText(x, row, advertise, _Focus == FocusAdvertise ? _Theme.Selection : _Theme.Text);
                _AdvertiseRect = new Rect(origin.X + x, origin.Y + row, advertise.Length, 1);
                x += advertise.Length + 2;
                content.DrawText(x, row, pull, _Theme.Text);
                _Buttons["pull"] = new Rect(origin.X + x, origin.Y + row, pull.Length, 1);
                row++;
                if (_Jobs.Count == 0)
                {
                    content.DrawText(0, row, "No pulls on this provider yet.", _Theme.Muted);
                    row += 2;
                }

                foreach (ModelPullJob job in _Jobs)
                {
                    RenderJob(content, origin, job, row, width);
                    row += 2;
                }
            }

            string message = _Error ?? _Notice ?? String.Empty;
            if (message.Length > 0) content.DrawText(0, height - 2, Fit(message, width), _Error != null ? _Theme.Error : _Theme.Success);
            List<string> labels = new List<string>();
            if (Inventory != null && Inventory.CanDelete) labels.Add("Delete");
            labels.Add("Refresh");
            labels.Add("Close");
            int bx = Math.Max(0, (width - (labels.Sum(item => item.Length + 5) - 1)) / 2);
            foreach (string item in labels)
            {
                int drawn = content.DrawText(bx, height - 1, "[ " + item + " ]", _Theme.Text);
                _Buttons[item.ToLowerInvariant()] = new Rect(origin.X + bx, origin.Y + height - 1, drawn, 1);
                bx += drawn + 1;
            }
        }

        #endregion

        #region Private-Methods

        private bool CanPull
        {
            get { return Inventory == null || Inventory.CanPull; }
        }

        private void RenderJob(ISurface content, Rect origin, ModelPullJob job, int row, int width)
        {
            string cancel = "[ Cancel ]";
            int barWidth = Math.Min(30, Math.Max(10, width / 4));
            string line = XenoModelManagement.PullLine(job);
            int lineWidth = Math.Max(10, width - barWidth - cancel.Length - 3);
            CellStyle lineStyle = job.State == ModelPullStateEnum.Failed ? _Theme.Error : job.State == ModelPullStateEnum.Succeeded ? _Theme.Success : _Theme.Text;
            content.DrawText(0, row, Fit(line, lineWidth), lineStyle);
            int filled = (int)Math.Round(XenoModelManagement.Fraction(job) * barWidth);
            int barX = lineWidth + 1;
            content.DrawText(barX, row, new string('█', filled), _Theme.Accent);
            content.DrawText(barX + filled, row, new string('░', barWidth - filled), _Theme.Muted);
            if (job.Active)
            {
                int cx = barX + barWidth + 2;
                content.DrawText(cx, row, cancel, _Theme.Text);
                _CancelButtons[job.Id] = new Rect(origin.X + cx, origin.Y + row, cancel.Length, 1);
            }

            string detail = job.Active ? XenoModelManagement.PullDetail(job) : (job.Error ?? String.Empty);
            if (detail.Length > 0) content.DrawText(2, row + 1, Fit(detail, width - 2), _Theme.Muted);
        }

        private void Press(string button)
        {
            switch (button)
            {
                case "columns":
                    _ = _Context.ChooseColumnsAsync(Columns);
                    break;
                case "pull":
                    StartPull();
                    break;
                case "delete":
                    _ = DeleteSelectedAsync();
                    break;
                case "refresh":
                    Reload();
                    break;
                case "close":
                    RequestClose(null);
                    break;
            }
        }

        private void OnJobChanged(object? sender, ModelPullJob job)
        {
            // Raised on a background thread: copy the pulls on the loop thread.
            if (!String.Equals(job.Provider, Provider, StringComparison.OrdinalIgnoreCase)) return;
            _Context.Post(() =>
            {
                if (_Detached) return;
                RefreshJobs();
                if (!job.Active)
                {
                    _Notice = XenoModelManagement.FinishedMessage(job);
                    if (job.State == ModelPullStateEnum.Succeeded)
                    {
                        Reload();
                        _Changed();
                    }
                }

                _Context.App.RequestRender();
            });
        }

        private void RefreshJobs()
        {
            _Jobs = XenoModelManagement.PullsFor(_Context.Workspace.ListModelPulls(), Provider);
        }

        private void MoveFocus(bool forward)
        {
            if (!CanPull)
            {
                SetFocus(FocusTable);
                return;
            }

            SetFocus((_Focus + (forward ? 1 : 2)) % 3);
        }

        private void SetFocus(int focus)
        {
            _Focus = focus;
            ApplyFocus();
        }

        private void ApplyFocus()
        {
            Table.OnFocusChanged(_Focus == FocusTable);
            PullField.OnFocusChanged(_Focus == FocusField);
            Advertise.OnFocusChanged(_Focus == FocusAdvertise);
        }

        private IReadOnlyList<StyledText> SummaryLines(int width)
        {
            string text = Inventory == null ? "Reading models from " + Provider + "..." : XenoModelManagement.Summary(Inventory);
            return TextWrapper.Wrap(StyledText.From(text, Inventory != null && !Inventory.CanPull ? _Theme.Info : _Theme.Muted), Math.Max(1, width));
        }

        private static string Fit(string text, int width)
        {
            if (width <= 0) return String.Empty;
            return TextFit.Width(text) <= width ? text : Truncate(text, width);
        }

        #endregion
    }
}
