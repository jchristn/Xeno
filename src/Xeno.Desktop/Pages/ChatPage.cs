namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Input;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Avalonia.Threading;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Chat: conversations on the left; on the right the selected conversation's messages, a model picker grouped by
    /// this machine and the Xeno network, a tools switch, and a composer. Answers stream in; answers can be rated with a
    /// comment; tools that change something show Allow and Deny. Talks to the running client through
    /// <see cref="XenoWorkspace.Chat"/>.
    /// </summary>
    public sealed class ChatPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Chat; }
        }

        /// <inheritdoc />
        public override bool Scrolls
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private Button _RenameButton = new Button();
        private Button _DeleteButton = new Button();
        private readonly ListBox _List = new ListBox { Background = Avalonia.Media.Brushes.Transparent };
        private readonly TextBox _Search = new TextBox { PlaceholderText = "Search chats" };
        private readonly ComboBox _Model = new ComboBox { MinWidth = 240 };
        private readonly ToggleButton _Tools = new ToggleButton { Content = "Tools" };
        private readonly TextBlock _Title = new TextBlock { FontSize = 16, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel _Messages = new StackPanel { Spacing = 14, Margin = new Thickness(18, 16) };
        private readonly ScrollViewer _Scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly TextBox _Input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 36, MaxHeight = 200, VerticalContentAlignment = VerticalAlignment.Center, PlaceholderText = "Message" };
        private readonly StackPanel _Notice = new StackPanel { Spacing = 6 };
        private Button? _Send;
        private List<ChatConversation> _Conversations = new List<ChatConversation>();
        private List<string> _ModelNames = new List<string>();
        private ChatConversation? _Conversation;
        private string? _NewChatDirectory;
        private readonly ChatWaitMessages _WaitMessages = new ChatWaitMessages();
        private string? _LiveTurn;
        private CancellationTokenSource? _LiveCancel;
        private bool _Loading;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Desktop session.</param>
        /// <param name="window">Main window.</param>
        public ChatPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control>
            {
                Ui.Button("Refresh", () => _ = RefreshAsync()),
                Ui.Primary("New Chat", () => _ = NewChatAsync())
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            XenoPalette palette = XenoTheme.Current;
            _List.SelectionChanged += (sender, args) =>
            {
                if (_Loading || _List.SelectedItem is not ListBoxItem item || item.Tag is not ChatConversation conversation) return;
                _ = OpenAsync(conversation.Id);
            };
            _RenameButton = Ui.Button("Rename...", () => _ = RenameAsync(), "Rename this chat.");
            _DeleteButton = Ui.Button("Delete...", () => _ = DeleteAsync(), "Delete this chat and its messages.");
            _Search.TextChanged += (sender, args) => _ = LoadListAsync();
            _List.KeyDown += (sender, args) =>
            {
                if (_List.SelectedItem is not ListBoxItem selected || selected.Tag is not ChatConversation conversation) return;
                if (args.Key == Key.F2)
                {
                    args.Handled = true;
                    _ = RenameAsync(conversation);
                }
                else if (args.Key == Key.Delete || (args.Key == Key.Back && args.KeyModifiers.HasFlag(KeyModifiers.Meta)))
                {
                    args.Handled = true;
                    _ = DeleteAsync(conversation);
                }
            };
            _Model.SelectionChanged += (sender, args) =>
            {
                if (_Loading || _Model.SelectedItem is not ComboBoxItem item || item.Tag is not string model) return;
                _ = ChooseModelAsync(model);
            };
            _Tools.IsCheckedChanged += (sender, args) =>
            {
                if (_Loading) return;
                _ = SetToolsAsync(_Tools.IsChecked == true);
            };
            ToolTip.SetTip(_Tools, "Let the model use Xeno tools (status, models, routing, history). Tools that change something ask first.");
            ToolTip.SetTip(_Model, "Models on this machine are answered here; the rest go to the Xeno network.");
            // The text box handles Enter itself (it inserts a new line), so listen before it does: Enter sends, and Shift+Enter
            // or Ctrl+Enter adds a line.
            _Input.AddHandler(InputElement.KeyDownEvent, (sender, args) =>
            {
                if (args.Key != Key.Enter && args.Key != Key.Return) return;
                if (args.KeyModifiers.HasFlag(KeyModifiers.Control) || args.KeyModifiers.HasFlag(KeyModifiers.Meta))
                {
                    args.Handled = true;
                    InsertNewLine();
                    return;
                }

                if (args.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
                args.Handled = true;
                _ = SendAsync();
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            _Scroll.Content = _Messages;

            DockPanel left = new DockPanel { Width = 260 };
            Button newChat = Ui.Primary("New Chat", () => _ = NewChatAsync());
            newChat.HorizontalAlignment = HorizontalAlignment.Stretch;
            StackPanel leftTop = Ui.Stack(8, newChat, _Search);
            leftTop.Margin = new Thickness(10);
            DockPanel.SetDock(leftTop, Dock.Top);
            left.Children.Add(leftTop);
            left.Children.Add(Ui.Adopt(_List));

            _Send = Ui.Primary("Send", () => _ = SendOrStopAsync());
            _Send.MinWidth = 90;
            _Send.MinHeight = 36;
            _Send.VerticalAlignment = VerticalAlignment.Bottom;
            _Send.HorizontalContentAlignment = HorizontalAlignment.Center;
            _Send.VerticalContentAlignment = VerticalAlignment.Center;
            DockPanel composer = new DockPanel { Margin = new Thickness(14, 10, 14, 4) };
            DockPanel.SetDock(_Send, Dock.Right);
            composer.Children.Add(Ui.Adopt(_Send));
            _Input.Margin = new Thickness(0, 0, 10, 0);
            composer.Children.Add(Ui.Adopt(_Input));

            StackPanel header = new StackPanel { Margin = new Thickness(14, 10), Spacing = 8 };
            WrapPanel headerActions = Ui.Wrap(_Model, _Tools,
                Ui.Button("Tools...", () => _ = ShowToolsAsync(), "The Xeno tools the model can use."),
                Ui.Button("System Prompt...", () => _ = EditSystemPromptAsync(), "Instructions sent before every message in this conversation."),
                Ui.Button("Folder...", () => _ = EditWorkingDirectoryAsync(), "The folder this chat's file and process tools work in. They cannot reach outside it."),
                _RenameButton,
                _DeleteButton);
            header.Children.Add(Ui.Adopt(_Title));
            header.Children.Add(headerActions);

            DockPanel right = new DockPanel();
            Border headerBar = new Border { BorderBrush = palette.Border, BorderThickness = new Thickness(0, 0, 0, 1), Child = header };
            TextBlock disclaimer = new TextBlock { Text = ChatFormat.Disclaimer + "  Enter sends; Shift+Enter or Ctrl+Enter adds a line.", Foreground = palette.Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(14, 0, 14, 8) };
            Border composerBar = new Border { BorderBrush = palette.Border, BorderThickness = new Thickness(0, 1, 0, 0), Child = new StackPanel { Children = { composer, disclaimer } } };
            DockPanel.SetDock(headerBar, Dock.Top);
            DockPanel.SetDock(_Notice, Dock.Top);
            DockPanel.SetDock(composerBar, Dock.Bottom);
            _Notice.Margin = new Thickness(14, 8, 14, 0);
            right.Children.Add(headerBar);
            right.Children.Add(Ui.Adopt(_Notice));
            right.Children.Add(composerBar);
            right.Children.Add(Ui.Adopt(_Scroll));

            Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Border leftBorder = new Border { Background = palette.PanelSoft, BorderBrush = palette.Border, BorderThickness = new Thickness(0, 0, 1, 0), Child = left };
            Grid.SetColumn(right, 1);
            grid.Children.Add(leftBorder);
            grid.Children.Add(right);
            Border frame = new Border { Background = palette.Panel, BorderBrush = palette.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = grid };
            RenderConversation();
            return frame;
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            _Notice.Children.Clear();
            if (!Workspace.Chat.IsAvailable)
            {
                ShowNotRunning();
                return;
            }

            try
            {
                ModelCatalog catalog = await Workspace.Chat.ListModelsAsync(None).ConfigureAwait(true);
                FillModels(catalog);
                await LoadListAsync().ConfigureAwait(true);
                if (_Conversation != null) await OpenAsync(_Conversation.Id).ConfigureAwait(true);
            }
            catch (InvalidOperationException ex) when (ex.Message == XenoChatClient.NotRunningMessage)
            {
                ShowNotRunning();
            }
        }

        /// <summary>
        /// Open a conversation, for example from Chat history.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <returns>Task.</returns>
        public async Task OpenAsync(string conversationId)
        {
            await GuardAsync("Chat", async () =>
            {
                _Conversation = await Workspace.Chat.GetConversationAsync(conversationId, None).ConfigureAwait(true);
                RenderConversation();
                SelectInList();
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void ShowNotRunning()
        {
            _Notice.Children.Clear();
            _Notice.Children.Add(Ui.Notice(XenoChatClient.NotRunningMessage, XenoToneEnum.Warning));
            _Notice.Children.Add(Ui.Row(Ui.Primary("Start Client", () =>
            {
                if (Application.Current is IXenoMenuHost host) host.Execute(XenoCommandEnum.StartClient);
            }), Ui.Button("Try Again", () => _ = RefreshAsync())));
        }

        private void FillModels(ModelCatalog catalog)
        {
            _Loading = true;
            try
            {
                _Model.Items.Clear();
                _ModelNames.Clear();
                List<ModelCatalogEntry> usable = catalog.Entries.Where(entry => entry.SupportsCompletions || !entry.SupportsEmbeddings).ToList();
                AddGroup("This machine", usable.Where(entry => entry.ServedLocally));
                AddGroup("Xeno network", usable.Where(entry => !entry.ServedLocally && entry.OnNetwork));
                if (!String.IsNullOrWhiteSpace(catalog.NetworkMessage) && !usable.Any(entry => !entry.ServedLocally))
                {
                    _Notice.Children.Add(Ui.Notice("Xeno network models are unavailable: " + catalog.NetworkMessage, XenoToneEnum.Info));
                }

                SelectModel(_Conversation?.Model ?? _ModelNames.FirstOrDefault());
            }
            finally
            {
                _Loading = false;
            }
        }

        private void AddGroup(string title, IEnumerable<ModelCatalogEntry> entries)
        {
            List<ModelCatalogEntry> list = entries.ToList();
            if (list.Count == 0) return;
            _Model.Items.Add(new ComboBoxItem { Content = title.ToUpperInvariant(), IsEnabled = false, FontSize = 11, FontWeight = FontWeight.SemiBold });
            foreach (ModelCatalogEntry entry in list)
            {
                _Model.Items.Add(new ComboBoxItem { Content = "  " + entry.Name, Tag = entry.Name });
                _ModelNames.Add(entry.Name);
            }
        }

        private void SelectModel(string? model)
        {
            if (String.IsNullOrWhiteSpace(model)) return;
            ComboBoxItem? match = _Model.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag is string name && String.Equals(name, model, StringComparison.Ordinal));
            if (match == null)
            {
                match = new ComboBoxItem { Content = "  " + model, Tag = model };
                _Model.Items.Add(match);
            }

            bool loading = _Loading;
            _Loading = true;
            _Model.SelectedItem = match;
            _Loading = loading;
        }

        private string? CurrentModel()
        {
            return _Model.SelectedItem is ComboBoxItem item && item.Tag is string model ? model : _Conversation?.Model;
        }

        private async Task LoadListAsync()
        {
            if (!Workspace.Chat.IsAvailable) return;
            try
            {
                _Conversations = await Workspace.Chat.ListConversationsAsync(String.IsNullOrWhiteSpace(_Search.Text) ? null : _Search.Text, 200, None).ConfigureAwait(true);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            _Loading = true;
            try
            {
                _List.Items.Clear();
                foreach (ChatConversation conversation in _Conversations)
                {
                    StackPanel content = Ui.Stack(2,
                        new TextBlock { Text = (conversation.Pinned ? "📌 " : String.Empty) + conversation.Title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = conversation.Model + " · " + ChatFormat.When(conversation.UpdatedUtc) + (conversation.Surface == ChatSurfaceEnum.Desktop ? String.Empty : " · " + ChatFormat.Surface(conversation.Surface)), FontSize = 11.5, Foreground = XenoTheme.Current.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
                    _List.Items.Add(ConversationItem(conversation, content));
                }

                SelectInList();
            }
            finally
            {
                _Loading = false;
            }
        }

        private ListBoxItem ConversationItem(ChatConversation conversation, Control content)
        {
            // Each conversation has a ... button and a right-click menu to rename or delete it, whether or not it is open.
            Func<ContextMenu> menu = () =>
            {
                MenuItem rename = new MenuItem { Header = "Rename..." };
                rename.Click += (sender, args) => _ = RenameAsync(conversation);
                MenuItem delete = new MenuItem { Header = "Delete..." };
                delete.Click += (sender, args) => _ = DeleteAsync(conversation);
                return new ContextMenu { ItemsSource = new List<MenuItem> { rename, delete } };
            };
            Button more = new Button { Content = "\u22ef", Padding = new Thickness(8, 0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center, Background = Avalonia.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(more, "Rename or delete this chat");
            Avalonia.Automation.AutomationProperties.SetName(more, "Chat actions for " + conversation.Title);
            more.Click += (sender, args) =>
            {
                ContextMenu popup = menu();
                popup.PlacementTarget = more;
                popup.Open(more);
            };
            DockPanel row = new DockPanel();
            DockPanel.SetDock(more, Dock.Right);
            row.Children.Add(more);
            row.Children.Add(content);
            ListBoxItem item = new ListBoxItem { Content = row, Tag = conversation, Padding = new Thickness(10, 7, 16, 7), ContextMenu = menu() };
            return item;
        }

        private void InsertNewLine()
        {
            string text = _Input.Text ?? String.Empty;
            int start = Math.Clamp(Math.Min(_Input.SelectionStart, _Input.SelectionEnd), 0, text.Length);
            int end = Math.Clamp(Math.Max(_Input.SelectionStart, _Input.SelectionEnd), 0, text.Length);
            _Input.Text = text.Substring(0, start) + Environment.NewLine + text.Substring(end);
            _Input.CaretIndex = start + Environment.NewLine.Length;
        }

        private void SelectInList()
        {
            bool loading = _Loading;
            _Loading = true;
            _List.SelectedItem = _List.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.Tag is ChatConversation conversation && _Conversation != null && conversation.Id == _Conversation.Id);
            _Loading = loading;
        }

        private async Task NewChatAsync()
        {
            await GuardAsync("New chat", async () =>
            {
                ChatConversation created = await Workspace.Chat.CreateConversationAsync(new ChatConversationRequest { Model = CurrentModel(), Surface = ChatSurfaceEnum.Desktop, WorkingDirectory = _NewChatDirectory }, None).ConfigureAwait(true);
                _Conversation = created;
                RenderConversation();
                await LoadListAsync().ConfigureAwait(true);
                _Input.Focus();
            }).ConfigureAwait(true);
        }

        private async Task ChooseModelAsync(string model)
        {
            if (_Conversation == null) return;
            await GuardAsync("Choose model", async () =>
            {
                _Conversation = await Workspace.Chat.UpdateConversationAsync(_Conversation.Id, new ChatConversationRequest { Model = model }, None).ConfigureAwait(true);
                await LoadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task SetToolsAsync(bool on)
        {
            if (_Conversation == null) return;
            await GuardAsync("Tools", async () =>
            {
                ChatConversation updated = await Workspace.Chat.UpdateConversationAsync(_Conversation.Id, new ChatConversationRequest { ToolsEnabled = on }, None).ConfigureAwait(true);
                _Conversation.ToolsEnabled = updated.ToolsEnabled;
                Report(on ? "Tools on. Tools that change something will ask first." : "Tools off.");
            }).ConfigureAwait(true);
        }

        private async Task ShowToolsAsync()
        {
            await GuardAsync("Xeno tools", async () =>
            {
                List<ChatToolInfo> tools = await Workspace.Chat.ListToolsAsync(None).ConfigureAwait(true);
                StringBuilder text = new StringBuilder("With tools on, the model can call these. Read-only tools run right away; the others wait for you to allow them.\n\n");
                foreach (ChatToolInfo tool in tools) text.Append(tool.Name).Append(tool.Mutating ? "  (asks first)" : "  (read-only)").Append('\n').Append("    ").Append(tool.Description).Append("\n\n");
                await XenoDialogs.MessageAsync(Window, "Xeno tools", text.ToString().TrimEnd(), XenoToneEnum.Info).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task RenameAsync(ChatConversation? target = null)
        {
            ChatConversation? conversation = target ?? _Conversation;
            if (conversation == null) return;
            string? title = await XenoDialogs.PromptAsync(Window, "Rename chat", "Title", conversation.Title, false).ConfigureAwait(true);
            if (title == null || String.IsNullOrWhiteSpace(title)) return;
            await GuardAsync("Rename chat", async () =>
            {
                ChatConversation updated = await Workspace.Chat.UpdateConversationAsync(conversation.Id, new ChatConversationRequest { Title = title.Trim() }, None).ConfigureAwait(true);
                if (_Conversation != null && _Conversation.Id == updated.Id)
                {
                    _Conversation = updated;
                    _Title.Text = updated.Title;
                }

                await LoadListAsync().ConfigureAwait(true);
                Report("Renamed the chat to \"" + updated.Title + "\".");
            }).ConfigureAwait(true);
        }

        private async Task EditSystemPromptAsync()
        {
            if (_Conversation == null) return;
            Dictionary<string, string>? values = await XenoFormDialog.ShowAsync(Window, "System prompt", "Instructions sent before every message in this conversation. Leave empty for none.",
                new List<XenoFormField> { new XenoFormField { Key = "prompt", Label = "System prompt", Kind = XenoFormFieldKindEnum.MultiLine, Value = _Conversation.SystemPrompt ?? String.Empty } }, "Save").ConfigureAwait(true);
            if (values == null) return;
            await GuardAsync("System prompt", async () =>
            {
                _Conversation = await Workspace.Chat.UpdateConversationAsync(_Conversation.Id, new ChatConversationRequest { SystemPrompt = values["prompt"] }, None).ConfigureAwait(true);
                Report("Saved the system prompt.");
            }).ConfigureAwait(true);
        }

        private async Task EditWorkingDirectoryAsync()
        {
            string current = _Conversation != null ? _Conversation.WorkingDirectory ?? String.Empty : _NewChatDirectory ?? String.Empty;
            Dictionary<string, string>? values = await XenoFormDialog.ShowAsync(Window, "Working folder", "File and process tools work in this folder and cannot reach outside it. Changing files or running commands always asks first. Leave empty for your home folder.",
                new List<XenoFormField> { new XenoFormField { Key = "folder", Label = "Folder", Value = current } }, "Save").ConfigureAwait(true);
            if (values == null) return;
            string value = values["folder"].Trim();
            if (_Conversation == null)
            {
                _NewChatDirectory = value.Length == 0 ? null : value;
                Report("New chats will work in " + (_NewChatDirectory ?? "your home folder") + ".");
                return;
            }

            await GuardAsync("Working folder", async () =>
            {
                ChatConversation updated = await Workspace.Chat.UpdateConversationAsync(_Conversation.Id, new ChatConversationRequest { WorkingDirectory = value }, None).ConfigureAwait(true);
                _Conversation.WorkingDirectory = updated.WorkingDirectory;
                Report("Tools in this chat now work in " + updated.WorkingDirectory + ".");
            }).ConfigureAwait(true);
        }

        private async Task DeleteAsync(ChatConversation? target = null)
        {
            ChatConversation? conversation = target ?? _Conversation;
            if (conversation == null) return;
            bool confirmed = await XenoDialogs.ConfirmAsync(Window, "Delete chat", "Delete \"" + conversation.Title + "\" and its messages and ratings? Chat history keeps the timing records.", "Delete").ConfigureAwait(true);
            if (!confirmed) return;
            await GuardAsync("Delete chat", async () =>
            {
                await Workspace.Chat.DeleteConversationAsync(conversation.Id, None).ConfigureAwait(true);
                if (_Conversation != null && _Conversation.Id == conversation.Id)
                {
                    _Conversation = null;
                    RenderConversation();
                }

                await LoadListAsync().ConfigureAwait(true);
                Report("Deleted \"" + conversation.Title + "\".");
            }).ConfigureAwait(true);
        }

        private async Task SendOrStopAsync()
        {
            if (_LiveTurn != null || _LiveCancel != null)
            {
                if (_LiveTurn != null)
                {
                    try
                    {
                        await Workspace.Chat.CancelAsync(_LiveTurn, None).ConfigureAwait(true);
                    }
                    catch (InvalidOperationException)
                    {
                        _LiveCancel?.Cancel();
                    }
                }
                else
                {
                    _LiveCancel?.Cancel();
                }

                return;
            }

            await SendAsync().ConfigureAwait(true);
        }

        private async Task SendAsync()
        {
            string content = (_Input.Text ?? String.Empty).Trim();
            string? model = CurrentModel();
            if (content.Length == 0 || _LiveCancel != null) return;
            if (String.IsNullOrWhiteSpace(model))
            {
                await XenoDialogs.MessageAsync(Window, "Chat", "Choose a model first.", XenoToneEnum.Warning).ConfigureAwait(true);
                return;
            }

            await GuardAsync("Chat", async () =>
            {
                if (_Conversation == null)
                {
                    _Conversation = await Workspace.Chat.CreateConversationAsync(new ChatConversationRequest { Model = model, Surface = ChatSurfaceEnum.Desktop, ToolsEnabled = _Tools.IsChecked == true, WorkingDirectory = _NewChatDirectory }, None).ConfigureAwait(true);
                    _Conversation.Messages = new List<ChatMessage>();
                }

                _Input.Text = String.Empty;
                _Messages.Children.Add(UserBubble(content));
                LiveAnswer live = new LiveAnswer(this);
                _Messages.Children.Add(live.Root);
                ScrollToEnd();
                SetSending(true);
                _LiveCancel = new CancellationTokenSource();
                try
                {
                    await Workspace.Chat.SendAsync(_Conversation.Id, new ChatSendRequest { Content = content, Model = model, Surface = ChatSurfaceEnum.Desktop }, item =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (item.Kind == ChatStreamEventKindEnum.Turn) _LiveTurn = item.TurnId;
                            live.Apply(item);
                            ScrollToEnd();
                        });
                        return Task.CompletedTask;
                    }, _LiveCancel.Token).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    live.StopWaiting();
                    _LiveCancel.Dispose();
                    _LiveCancel = null;
                    _LiveTurn = null;
                    SetSending(false);
                }

                string id = _Conversation.Id;
                _Conversation = await Workspace.Chat.GetConversationAsync(id, None).ConfigureAwait(true);
                RenderConversation();
                await LoadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private void SetSending(bool sending)
        {
            if (_Send == null) return;
            _Send.Content = sending ? "Stop" : "Send";
            _Send.Classes.Set("accent", !sending);
            _Send.Classes.Set("danger", sending);
            _Input.IsEnabled = !sending;
            _Model.IsEnabled = !sending;
        }

        private void RenderConversation()
        {
            _Loading = true;
            try
            {
                _Title.Text = _Conversation?.Title ?? "New chat";
                _RenameButton.IsEnabled = _Conversation != null;
                _DeleteButton.IsEnabled = _Conversation != null;
                _Tools.IsChecked = _Conversation?.ToolsEnabled == true;
                if (_Conversation != null) SelectModel(_Conversation.Model);
            }
            finally
            {
                _Loading = false;
            }

            _Messages.Children.Clear();
            List<ChatMessage> messages = _Conversation?.Messages ?? new List<ChatMessage>();
            Dictionary<string, string> toolResults = messages.Where(item => item.Role == ChatRoleEnum.Tool && item.ToolCallId != null).GroupBy(item => item.ToolCallId!).ToDictionary(group => group.Key, group => group.Last().Content);
            if (messages.Count(item => item.Role == ChatRoleEnum.User || item.Role == ChatRoleEnum.Assistant) == 0)
            {
                StackPanel empty = Ui.Stack(6, Ui.Heading("Start a conversation"), Ui.Muted("Pick a model above and type a message. Answers stream in as the model writes them."));
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Margin = new Thickness(0, 60, 0, 0);
                _Messages.Children.Add(empty);
                return;
            }

            foreach (ChatMessage message in messages)
            {
                if (message.Role == ChatRoleEnum.User) _Messages.Children.Add(UserBubble(message.Content));
                else if (message.Role == ChatRoleEnum.Assistant) _Messages.Children.Add(AssistantBlock(message, toolResults));
            }

            ScrollToEnd();
        }

        private Control UserBubble(string text)
        {
            XenoPalette palette = XenoTheme.Current;
            return new Border
            {
                Background = palette.AccentFill,
                CornerRadius = new CornerRadius(12, 12, 3, 12),
                Padding = new Thickness(12, 9),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 640,
                Child = new SelectableTextBlock { Text = text, Foreground = palette.AccentText, TextWrapping = TextWrapping.Wrap }
            };
        }

        private static Control FailureBox(string text, ChatFailureKindEnum kind)
        {
            XenoPalette palette = XenoTheme.Current;
            IBrush tone = kind == ChatFailureKindEnum.Stopped ? palette.Muted : palette.Danger;
            return new Border
            {
                BorderBrush = tone,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(10, 4),
                Child = new SelectableTextBlock { Text = "⚠ " + text, Foreground = tone, TextWrapping = TextWrapping.Wrap }
            };
        }

        private Control AssistantBlock(ChatMessage message, Dictionary<string, string> toolResults)
        {
            XenoPalette palette = XenoTheme.Current;
            StackPanel stack = new StackPanel { Spacing = 6, MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
            if (!String.IsNullOrWhiteSpace(message.Thinking)) stack.Children.Add(Thinking(message.Thinking));
            foreach (ChatToolCallRecord call in message.ToolCalls)
            {
                if (call.Result == null && toolResults.TryGetValue(call.CallId, out string? result)) call.Result = result;
                stack.Children.Add(ToolRow(call, null));
            }

            if (!String.IsNullOrEmpty(message.Content)) stack.Children.Add(new SelectableTextBlock { Text = message.Content, TextWrapping = TextWrapping.Wrap, Foreground = palette.Text, LineHeight = 21 });
            string? failure = ChatFormat.FailureLine(message);
            if (failure != null) stack.Children.Add(FailureBox(failure, message.Failure!.Kind));
            StackPanel footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            footer.Children.Add(new TextBlock { Text = ChatFormat.AnswerMeta(message), FontSize = 11.5, Foreground = palette.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            if (!String.IsNullOrEmpty(message.Content))
            {
                footer.Children.Add(Ui.CopyButton("Copy answer", () => _ = XenoClipboard.CopyAsync(Window, message.Content)));
                Button up = Ui.Button(message.Feedback?.Rating == 1 ? "👍 Rated" : "👍", () => _ = RateAsync(message, 1), "Good answer");
                Button down = Ui.Button(message.Feedback?.Rating == -1 ? "👎 Rated" : "👎", () => _ = RateAsync(message, -1), "Bad answer");
                footer.Children.Add(up);
                footer.Children.Add(down);
            }

            stack.Children.Add(footer);
            if (!String.IsNullOrWhiteSpace(message.Feedback?.Comment))
            {
                stack.Children.Add(new Border { BorderBrush = palette.Border, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(10, 2), Child = Ui.Muted(message.Feedback!.Comment!) });
            }

            return stack;
        }

        private static Control Thinking(string text)
        {
            return new Expander
            {
                Header = "Thinking",
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = XenoTheme.Current.Muted }
            };
        }

        private Control ToolRow(ChatToolCallRecord call, string? turnId)
        {
            XenoPalette palette = XenoTheme.Current;
            bool pending = call.Approval == ChatToolApprovalEnum.Pending;
            string state = pending ? "needs approval" : call.Success ? "done" : call.Error != null ? "failed" : "running";
            XenoToneEnum tone = pending ? XenoToneEnum.Warning : call.Success ? XenoToneEnum.Success : call.Error != null ? XenoToneEnum.Danger : XenoToneEnum.Info;
            StackPanel body = new StackPanel { Spacing = 6 };
            body.Children.Add(Ui.Row(new TextBlock { Text = "🔧 " + call.ToolName, FontFamily = new FontFamily("Menlo, Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center }, Ui.Badge(state, tone),
                Ui.Muted(call.DurationMs > 0 ? ChatFormat.Duration(call.DurationMs) : String.Empty)));
            if (pending && turnId != null)
            {
                body.Children.Add(Ui.Row(Ui.Muted("This tool changes something. Run it?"),
                    Ui.Primary("Allow", () => _ = ApproveAsync(turnId, call.Id, true)),
                    Ui.Button("Deny", () => _ = ApproveAsync(turnId, call.Id, false))));
            }

            string details = "Arguments: " + call.Arguments + (call.Result != null ? "\n\nResult: " + call.Result : String.Empty) + (call.Error != null ? "\n\nError: " + call.Error : String.Empty);
            body.Children.Add(new Expander { Header = "Details", Content = new SelectableTextBlock { Text = details, TextWrapping = TextWrapping.Wrap, FontSize = 11.5, FontFamily = new FontFamily("Menlo, Consolas, monospace") } });
            return new Border { BorderBrush = palette.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Background = pending ? palette.WarningSoft : palette.PanelSoft, Padding = new Thickness(10, 7), Child = body };
        }

        private async Task ApproveAsync(string turnId, string toolCallId, bool allow)
        {
            await GuardAsync("Tool approval", async () =>
            {
                await Workspace.Chat.ApproveAsync(turnId, toolCallId, allow, None).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task RateAsync(ChatMessage message, int rating)
        {
            Dictionary<string, string>? values = await XenoFormDialog.ShowAsync(Window, rating > 0 ? "Good answer" : "Bad answer",
                rating > 0 ? "What was good about this answer? A comment is optional." : "What was wrong with this answer? A comment is optional and helps compare models.",
                new List<XenoFormField> { new XenoFormField { Key = "comment", Label = "Comment", Kind = XenoFormFieldKindEnum.MultiLine, Value = message.Feedback?.Rating == rating ? message.Feedback?.Comment ?? String.Empty : String.Empty } },
                "Save Rating").ConfigureAwait(true);
            if (values == null) return;
            await GuardAsync("Rate answer", async () =>
            {
                await Workspace.Chat.SetFeedbackAsync(message.Id, new ChatFeedbackRequest { Rating = rating, Comment = values["comment"] }, None).ConfigureAwait(true);
                Report("Saved your rating.");
                if (_Conversation != null) await OpenAsync(_Conversation.Id).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private void ScrollToEnd()
        {
            Dispatcher.UIThread.Post(() => _Scroll.ScrollToEnd(), DispatcherPriority.Background);
        }

        #endregion

        #region Private-Classes

        private sealed class LiveAnswer
        {
            private readonly ChatPage _Page;
            private readonly StackPanel _Tools = new StackPanel { Spacing = 6 };
            private readonly SelectableTextBlock _Text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
            private readonly TextBlock _Status = new TextBlock { FontSize = 11.5 };
            private readonly SelectableTextBlock _Thinking = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
            private readonly Expander _ThinkingBox = new Expander { Header = "Thinking", IsExpanded = true, IsVisible = false };
            private readonly Dictionary<string, Control> _ToolRows = new Dictionary<string, Control>(StringComparer.Ordinal);
            private readonly StringBuilder _Answer = new StringBuilder();
            private readonly StringBuilder _Reasoning = new StringBuilder();
            private string? _TurnId;
            private readonly DispatcherTimer _Quips = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ChatWaitMessages.RotateMs) };

            internal LiveAnswer(ChatPage page)
            {
                _Page = page;
                XenoPalette palette = XenoTheme.Current;
                _Text.Foreground = palette.Text;
                _Status.Foreground = palette.Muted;
                _Thinking.Foreground = palette.Muted;
                // A short quip under the pending answer until something arrives, changed every few seconds.
                _Status.Text = page._WaitMessages.Next();
                _Status.FontStyle = FontStyle.Italic;
                _Quips.Tick += (sender, args) => _Status.Text = _Page._WaitMessages.Next();
                _Quips.Start();
                _ThinkingBox.Content = _Thinking;
                Root = new StackPanel { Spacing = 6, MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, Children = { _ThinkingBox, _Tools, _Text, _Status } };
            }

            internal StackPanel Root { get; }

            internal void StopWaiting()
            {
                _Quips.Stop();
                _Status.FontStyle = FontStyle.Normal;
            }

            internal void Apply(ChatStreamEvent item)
            {
                if (item.Kind != ChatStreamEventKindEnum.Turn) StopWaiting();
                switch (item.Kind)
                {
                    case ChatStreamEventKindEnum.Turn:
                        _TurnId = item.TurnId;
                        break;
                    case ChatStreamEventKindEnum.Thinking:
                        _Reasoning.Append(item.Text);
                        _Thinking.Text = _Reasoning.ToString();
                        _ThinkingBox.IsVisible = true;
                        break;
                    case ChatStreamEventKindEnum.Chunk:
                        _Answer.Append(item.Text);
                        _Text.Text = _Answer.ToString();
                        _Status.Text = "Answering...";
                        _ThinkingBox.IsExpanded = false;
                        break;
                    case ChatStreamEventKindEnum.ToolCall:
                        if (item.ToolCall == null) break;
                        Control row = _Page.ToolRow(item.ToolCall, _TurnId);
                        if (_ToolRows.TryGetValue(item.ToolCall.Id, out Control? existing)) _Tools.Children[_Tools.Children.IndexOf(existing)] = row;
                        else _Tools.Children.Add(row);
                        _ToolRows[item.ToolCall.Id] = row;
                        break;
                    case ChatStreamEventKindEnum.Error:
                        _Status.Text = "⚠ " + ChatFormat.FailureLine(item, _Answer.Length > 0);
                        _Status.Foreground = XenoTheme.Current.Danger;
                        break;
                    case ChatStreamEventKindEnum.Done:
                        if (item.Turn != null && item.Turn.Status == ChatTurnStatusEnum.Success) _Status.Text = ChatFormat.Route(item.Turn.Route, item.Turn.Fallback) + " · " + ChatFormat.Duration(item.Turn.TotalMs);
                        break;
                }
            }
        }

        #endregion
    }
}
