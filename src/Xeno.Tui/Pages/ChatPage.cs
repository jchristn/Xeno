namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Chat in the terminal app: pick a conversation and a model, type a message, and read the answer as it streams.
    /// Answers can be rated with a comment; tools that change something ask through a choice dialog. Messages are
    /// shown literally (escaped Markdown). Works with the mouse and the keyboard.
    /// </summary>
    internal sealed class ChatPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Chat; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("Enter", "Send"), new KeyHint("Ctrl+N", "New chat"), new KeyHint("Esc", "Stop") }; }
        }

        #endregion

        #region Private-Members

        private const string NewChatLabel = "(new chat)";
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _Conversation = new Dropdown<string>(new List<string> { NewChatLabel });
        private readonly Dropdown<string> _Model = new Dropdown<string>(new List<string>());
        private readonly ButtonRow _Actions = new ButtonRow();
        private readonly MarkdownView _Transcript = new MarkdownView();
        private readonly TextField _Input = new TextField { Placeholder = "Type a message and press Enter" };
        private List<ChatConversation> _Conversations = new List<ChatConversation>();
        private List<string> _Models = new List<string>();
        private ChatConversation? _Current;
        private string? _LiveTurn;
        private CancellationTokenSource? _LiveCancel;
        private bool _Binding;
        private bool _Streaming = true;
        private readonly ChatWaitMessages _WaitMessages = new ChatWaitMessages();
        private string? _NewChatDirectory;
        private string? _SelectedModel;
        private readonly Dictionary<string, ChatModelReadiness> _Readiness = new Dictionary<string, ChatModelReadiness>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _Preparing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Button _SendButton = new Button("Send", () => { });
        private Button _StreamingButton = new Button("Stream: on", () => { });
        private readonly Label _Disclaimer = new Label(TUIKit.StyledText.From(ChatFormat.Disclaimer).Dim());

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">App context.</param>
        public ChatPage(TuiContext context) : base(context)
        {
            _Conversation.SelectionChanged += (sender, args) =>
            {
                if (_Binding) return;
                int index = _Conversation.SelectedIndex - 1;
                if (index < 0 || index >= _Conversations.Count)
                {
                    _Current = null;
                    Render(null, null, null, null);
                    return;
                }

                _ = OpenAsync(_Conversations[index].Id);
            };
            _Model.SelectionChanged += (sender, args) =>
            {
                if (_Binding || _Model.SelectedItem == null) return;
                _ = ChooseModelAsync(_Model.SelectedItem);
            };
            _Input.Submitted += (sender, args) => _ = SendAsync();
            _SendButton = new Button("Send", () => _ = SendOrStopAsync());
            _StreamingButton = new Button("Stream: on", () => ToggleStreaming());
            _Actions.Add(_StreamingButton);
            _Actions.Add("Stats", () => _ = ShowTelemetryAsync());
            _Actions.Add("New Chat", () => NewChat());
            _Actions.Add("Rate Up", () => _ = RateAsync(1));
            _Actions.Add("Rate Down", () => _ = RateAsync(-1));
            _Actions.Add("Tools", () => _ = ToggleToolsAsync());
            _Actions.Add("Prompt", () => _ = SystemPromptAsync());
            _Actions.Add("Folder", () => _ = WorkingDirectoryAsync());
            _Actions.Add("Delete", () => _ = DeleteAsync());
            _Actions.Add("Delete Chats", () => _ = DeleteManyAsync());
            FramedStack pickers = new FramedStack(SplitOrientation.Horizontal);
            Frame(pickers, _Conversation, StackSize.Weighted(3, 20), "Conversation");
            Frame(pickers, _Model, StackSize.Weighted(2, 16), "Model");
            Frame(_Root, pickers, StackSize.Fixed(1));
            Frame(_Root, _Actions, StackSize.Fixed(1), "Actions");
            Frame(_Root, _Transcript, StackSize.Weighted(1, 6), "Messages");
            // Send sits beside the message box on the same row, so the two are the same height.
            FramedStack composer = new FramedStack(SplitOrientation.Horizontal);
            Frame(composer, _Input, StackSize.Weighted(1, 20), "Message");
            Frame(composer, _SendButton, StackSize.Fixed(10));
            Frame(_Root, composer, StackSize.Fixed(1));
            Frame(_Root, _Disclaimer, StackSize.Fixed(1));
            Render(null, null, null, null);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            if (!Workspace.Chat.IsAvailable)
            {
                _Transcript.Markdown = XenoMarkdown.Escape(XenoChatClient.NotRunningMessage) + "\n\nStart it from the Client menu, then refresh.";
                return Task.CompletedTask;
            }

            return LoadAsync(async token =>
            {
                ModelCatalog catalog = await Workspace.Chat.ListModelsAsync(token).ConfigureAwait(false);
                List<ChatConversation> conversations = await Workspace.Chat.ListConversationsAsync(null, 100, token).ConfigureAwait(false);
                ChatConversation? current = _Current == null ? null : await Workspace.Chat.GetConversationAsync(_Current.Id, token).ConfigureAwait(false);
                return new ChatPageData(catalog, conversations, current);
            }, data =>
            {
                _Models = data.Catalog.Entries.Where(entry => entry.SupportsCompletions || !entry.SupportsEmbeddings)
                    .OrderBy(entry => entry.ServedLocally ? 0 : 1).Select(entry => entry.Name).ToList();
                _Conversations = data.Conversations;
                _Current = data.Current;
                Bind();
                Render(null, null, null, null);
                if (_SelectedModel != null) _ = PrepareAsync(_SelectedModel, false);
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Character && (key.Modifiers & KeyModifiers.Ctrl) != 0 && char.ToLowerInvariant((char)key.Rune) == 'n')
            {
                NewChat();
                return true;
            }

            if (key.Code == KeyCode.Escape && _LiveCancel != null)
            {
                _ = StopAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private void Bind()
        {
            _Binding = true;
            try
            {
                _Conversation.SetItems(new List<string> { NewChatLabel }.Concat(_Conversations.Select(item => item.Title + " · " + item.Model)).ToList());
                int index = _Current == null ? 0 : _Conversations.FindIndex(item => item.Id == _Current.Id) + 1;
                _Conversation.SelectedIndex = Math.Max(0, index);
                List<string> models = _Models.ToList();
                if (_Current != null && !String.IsNullOrWhiteSpace(_Current.Model) && !models.Contains(_Current.Model)) models.Insert(0, _Current.Model);
                _Model.SetItems(models);
                string? selected = !String.IsNullOrWhiteSpace(_Current?.Model) ? _Current!.Model : (_SelectedModel != null && models.Contains(_SelectedModel) ? _SelectedModel : models.FirstOrDefault());
                _SelectedModel = selected;
                if (selected != null) _Model.SelectedIndex = Math.Max(0, models.IndexOf(selected));
            }
            finally
            {
                _Binding = false;
            }
        }

        private void Render(string? pendingUser, string? live, string? status, IEnumerable<string>? tools)
        {
            string folder = _Current != null ? _Current.WorkingDirectory ?? "home folder" : _NewChatDirectory ?? "home folder";
            string header = (_Current == null
                ? "**New chat**"
                : "**" + XenoMarkdown.Escape(_Current.Title) + "** · tools " + (_Current.ToolsEnabled ? "on" : "off") + (String.IsNullOrWhiteSpace(_Current.SystemPrompt) ? String.Empty : " · system prompt set"))
                + " · folder " + XenoMarkdown.Escape(folder);
            _Transcript.Markdown = header + "\n\n" + ChatTranscript.Build(_Current, pendingUser, live, status, tools);
            _Transcript.ScrollToEnd();
        }

        private async Task OpenAsync(string id)
        {
            await LoadAsync(token => Workspace.Chat.GetConversationAsync(id, token), conversation =>
            {
                _Current = conversation;
                Bind();
                Render(null, null, null, null);
            }).ConfigureAwait(false);
        }

        private void NewChat()
        {
            _Current = null;
            Bind();
            Render(null, null, null, null);
            Context.Notify("New chat. Type a message to start it.", NotificationSeverity.Info);
        }

        private async Task SendAsync()
        {
            string content = (_Input.Value ?? String.Empty).Trim();
            string? model = _Current?.Model ?? _SelectedModel ?? _Model.SelectedItem;
            if (content.Length == 0 || _LiveCancel != null) return;
            if (String.IsNullOrWhiteSpace(model))
            {
                Context.Notify("Choose a model first.", NotificationSeverity.Warning);
                return;
            }

            _Input.Value = String.Empty;
            CancellationTokenSource cancel = new CancellationTokenSource();
            _LiveCancel = cancel;
            StringBuilder answer = new StringBuilder();
            Dictionary<string, string> tools = new Dictionary<string, string>(StringComparer.Ordinal);
            // A short quip under the pending answer until something arrives, changed every few seconds.
            string status = _WaitMessages.Next();
            bool started = false;
            Render(content, String.Empty, status, null);
            _SendButton.Label = "Stop";
            _ = Task.Run(async () =>
            {
                while (!cancel.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(ChatWaitMessages.RotateMs, cancel.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (started || _LiveCancel != cancel) return;
                    string quip = _WaitMessages.Next();
                    status = quip;
                    Context.Post(() =>
                    {
                        if (!started && _LiveCancel == cancel) Render(content, String.Empty, quip, null);
                    });
                }
            });
            Context.Progress("Asking " + model + "...");
            try
            {
                if (_Current == null)
                {
                    bool toolsOn = _Readiness.TryGetValue(model, out ChatModelReadiness? ready) && ready.SupportsTools;
                    _Current = await Workspace.Chat.CreateConversationAsync(new ChatConversationRequest { Model = model, Surface = ChatSurfaceEnum.Terminal, ToolsEnabled = toolsOn, WorkingDirectory = _NewChatDirectory }, cancel.Token).ConfigureAwait(false);
                }

                ChatTurn? turn = await Workspace.Chat.SendAsync(_Current.Id, new ChatSendRequest { Content = content, Model = model, Stream = _Streaming, Surface = ChatSurfaceEnum.Terminal }, async item =>
                {
                    if (item.Kind != ChatStreamEventKindEnum.Turn) started = true;
                    switch (item.Kind)
                    {
                        case ChatStreamEventKindEnum.Turn:
                            _LiveTurn = item.TurnId;
                            break;
                        case ChatStreamEventKindEnum.Chunk:
                            answer.Append(item.Text);
                            status = "Answering...";
                            break;
                        case ChatStreamEventKindEnum.Thinking:
                            status = "Thinking...";
                            break;
                        case ChatStreamEventKindEnum.Error:
                            status = ChatTranscript.FailureMarker + " " + ChatFormat.FailureLine(item, answer.Length > 0);
                            break;
                        case ChatStreamEventKindEnum.ToolCall:
                            if (item.ToolCall == null) break;
                            tools[item.ToolCall.Id] = ChatTranscript.ToolLine(item.ToolCall);
                            if (item.ToolCall.Approval == ChatToolApprovalEnum.Pending) await AskApprovalAsync(item.TurnId!, item.ToolCall).ConfigureAwait(false);
                            break;
                    }

                    string text = answer.ToString();
                    string line = status;
                    List<string> toolLines = tools.Values.ToList();
                    Context.Post(() => Render(content, text, line, toolLines));
                }, cancel.Token).ConfigureAwait(false);
                Context.Notify(turn == null ? "No answer." : ChatFormat.Outcome(turn.Status) + " · " + ChatFormat.Route(turn.Route, turn.Fallback) + " · " + ChatFormat.Duration(turn.TotalMs), turn?.Status == ChatTurnStatusEnum.Success ? NotificationSeverity.Success : NotificationSeverity.Warning);
            }
            catch (OperationCanceledException)
            {
                Context.Notify("Stopped.", NotificationSeverity.Info);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is KeyNotFoundException || ex is System.Net.Http.HttpRequestException || ex is System.IO.IOException)
            {
                Context.Notify(ex.Message, NotificationSeverity.Error);
            }
            finally
            {
                started = true;
                _LiveCancel = null;
                _LiveTurn = null;
                cancel.Cancel();
                cancel.Dispose();
                Context.Post(() => _SendButton.Label = "Send");
            }

            await RefreshAsync().ConfigureAwait(false);
        }

        private Task SendOrStopAsync()
        {
            return _LiveCancel != null ? StopAsync() : SendAsync();
        }

        private void ToggleStreaming()
        {
            _Streaming = !_Streaming;
            _StreamingButton.Label = _Streaming ? "Stream: on" : "Stream: off";
            Context.Notify(_Streaming ? "Streaming on: answers appear as the model writes them." : "Streaming off: answers appear when the model finishes.", NotificationSeverity.Info);
        }

        private async Task ChooseModelAsync(string model)
        {
            _SelectedModel = model;
            ChatConversation? current = _Current;
            try
            {
                if (current != null)
                {
                    _Current = await Workspace.Chat.UpdateConversationAsync(current.Id, new ChatConversationRequest { Model = model }, CancellationToken.None).ConfigureAwait(false);
                    _Current.Messages = current.Messages;
                }

                ChatModelReadiness? ready = await PrepareAsync(model, true).ConfigureAwait(false);
                bool toolsUnusable = ready != null && ready.Loaded && ready.Route != "network" && !ready.SupportsTools && _Current != null && _Current.ToolsEnabled;
                if (_Current != null && ready != null && (_Current.Messages.Count == 0 || toolsUnusable))
                {
                    ChatConversation updated = await Workspace.Chat.UpdateConversationAsync(_Current.Id, new ChatConversationRequest { ToolsEnabled = ready.SupportsTools }, CancellationToken.None).ConfigureAwait(false);
                    _Current.ToolsEnabled = updated.ToolsEnabled;
                }

                Context.Post(() => Render(null, null, null, null));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is KeyNotFoundException || ex is System.Net.Http.HttpRequestException)
            {
                Context.Notify(ex.Message, NotificationSeverity.Error);
            }
        }

        private async Task<ChatModelReadiness?> PrepareAsync(string model, bool report)
        {
            if (_Readiness.TryGetValue(model, out ChatModelReadiness? known)) return known;
            lock (_Preparing)
            {
                if (!_Preparing.Add(model)) return null;
            }

            try
            {
                Context.Progress("Loading " + model + "...");
                ChatModelReadiness ready = await Workspace.Chat.PrepareModelAsync(model, CancellationToken.None).ConfigureAwait(false);
                if (ready.Route != "unavailable") _Readiness[model] = ready;
                Context.Notify(model + ": " + (ready.Loaded || ready.Route == "network" ? "ready. " : "not ready. ") + ready.Message + (ready.SupportsTools ? " Tools on for new chats." : String.Empty), ready.Loaded || ready.Route == "network" ? NotificationSeverity.Success : NotificationSeverity.Warning);
                return ready;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is System.Net.Http.HttpRequestException)
            {
                if (report) Context.Notify("Could not load " + model + ": " + ex.Message, NotificationSeverity.Warning);
                return null;
            }
            finally
            {
                lock (_Preparing) _Preparing.Remove(model);
            }
        }

        private Task ShowTelemetryAsync()
        {
            ChatMessage? answer = _Current?.Messages.LastOrDefault(item => item.Role == ChatRoleEnum.Assistant);
            if (answer == null)
            {
                Context.Notify("There is no answer yet.", NotificationSeverity.Info);
                return Task.CompletedTask;
            }

            return Context.ShowMarkdownAsync("Answer telemetry", XenoMarkdown.Details(ChatFormat.Telemetry(answer)));
        }

        private async Task AskApprovalAsync(string turnId, ChatToolCallRecord call)
        {
            TaskCompletionSource<bool> answered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Context.Post(() => _ = Task.Run(async () =>
            {
                bool allow = await Context.ConfirmAsync("Allow tool?", "The model wants to run " + call.ToolName + " " + call.Arguments + ", which changes something.", "Allow").ConfigureAwait(false);
                answered.TrySetResult(allow);
            }));
            bool allowed = await answered.Task.ConfigureAwait(false);
            try
            {
                await Workspace.Chat.ApproveAsync(turnId, call.Id, allowed, CancellationToken.None).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                Context.Notify("The tool call is no longer waiting.", NotificationSeverity.Warning);
            }
        }

        private async Task StopAsync()
        {
            if (_LiveTurn != null)
            {
                try
                {
                    await Workspace.Chat.CancelAsync(_LiveTurn, CancellationToken.None).ConfigureAwait(false);
                    return;
                }
                catch (InvalidOperationException)
                {
                }
            }

            _LiveCancel?.Cancel();
        }

        private async Task RateAsync(int rating)
        {
            ChatMessage? answer = _Current?.Messages.LastOrDefault(item => item.Role == ChatRoleEnum.Assistant && !String.IsNullOrEmpty(item.Content));
            if (answer == null)
            {
                Context.Notify("There is no answer to rate yet.", NotificationSeverity.Warning);
                return;
            }

            Dictionary<string, string>? values = await Context.FormAsync(rating > 0 ? "Good Answer" : "Bad Answer",
                rating > 0 ? "What was good about the last answer? A comment is optional." : "What was wrong with the last answer? A comment is optional.",
                new List<XenoFormFieldSpec> { new XenoFormFieldSpec { Key = "comment", Label = "Comment", Value = answer.Feedback?.Rating == rating ? answer.Feedback?.Comment ?? String.Empty : String.Empty } },
                null, "Save Rating", "Saving...").ConfigureAwait(false);
            if (values == null) return;
            string id = answer.Id;
            await Context.RunAsync(async token =>
            {
                await Workspace.Chat.SetFeedbackAsync(id, new ChatFeedbackRequest { Rating = rating, Comment = values.TryGetValue("comment", out string? comment) ? comment : null }, token).ConfigureAwait(false);
                return "Rated the last answer " + (rating > 0 ? "up." : "down.");
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private async Task ToggleToolsAsync()
        {
            if (_Current == null)
            {
                Context.Notify("Send a message first; tools are set per conversation.", NotificationSeverity.Info);
                return;
            }

            bool on = !_Current.ToolsEnabled;
            await Context.RunAsync(async token =>
            {
                ChatConversation updated = await Workspace.Chat.UpdateConversationAsync(_Current.Id, new ChatConversationRequest { ToolsEnabled = on }, token).ConfigureAwait(false);
                _Current.ToolsEnabled = updated.ToolsEnabled;
                return on ? "Tools on. Tools that change something will ask first." : "Tools off.";
            }, () => Context.Post(() => Render(null, null, null, null))).ConfigureAwait(false);
        }

        private async Task SystemPromptAsync()
        {
            if (_Current == null)
            {
                Context.Notify("Send a message first; the system prompt is set per conversation.", NotificationSeverity.Info);
                return;
            }

            bool hasPrompt = !String.IsNullOrWhiteSpace(_Current.SystemPrompt);
            string intro = hasPrompt
                ? "Instructions sent before every message in this conversation. Clear it to send none."
                : "This chat has no system prompt. Xeno's default is filled in: save to use it, edit it, or clear it to keep sending none.";
            Dictionary<string, string>? values = await Context.FormAsync("System Prompt", intro,
                new List<XenoFormFieldSpec> { new XenoFormFieldSpec { Key = "prompt", Label = "System prompt", Value = hasPrompt ? _Current.SystemPrompt! : ChatDefaults.SystemPrompt } }, null, "Save", "Saving...").ConfigureAwait(false);
            if (values == null) return;
            string id = _Current.Id;
            await Context.RunAsync(async token =>
            {
                _Current = await Workspace.Chat.UpdateConversationAsync(id, new ChatConversationRequest { SystemPrompt = values.TryGetValue("prompt", out string? prompt) ? prompt : String.Empty }, token).ConfigureAwait(false);
                return "Saved the system prompt.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private async Task WorkingDirectoryAsync()
        {
            string current = _Current != null ? _Current.WorkingDirectory ?? String.Empty : _NewChatDirectory ?? String.Empty;
            Dictionary<string, string>? values = await Context.FormAsync("Working Folder", "File and process tools work in this folder and cannot reach outside it. Changing files or running commands always asks first. Leave empty for your home folder.",
                new List<XenoFormFieldSpec> { new XenoFormFieldSpec { Key = "folder", Label = "Folder", Value = current } }, null, "Save", "Saving...").ConfigureAwait(false);
            if (values == null) return;
            string value = values.TryGetValue("folder", out string? folder) ? folder.Trim() : String.Empty;
            if (_Current == null)
            {
                _NewChatDirectory = value.Length == 0 ? null : value;
                Context.Post(() => Render(null, null, null, null));
                Context.Notify("New chats will work in " + (_NewChatDirectory ?? "your home folder") + ".", NotificationSeverity.Info);
                return;
            }

            string id = _Current.Id;
            await Context.RunAsync(async token =>
            {
                List<ChatMessage> messages = _Current.Messages;
                _Current = await Workspace.Chat.UpdateConversationAsync(id, new ChatConversationRequest { WorkingDirectory = value }, token).ConfigureAwait(false);
                _Current.Messages = messages;
                return "Tools in this chat now work in " + _Current.WorkingDirectory + ".";
            }, () => Render(null, null, null, null)).ConfigureAwait(false);
        }

        private async Task DeleteAsync()
        {
            if (_Current == null) return;
            ChatConversation target = _Current;
            bool confirmed = await Context.ConfirmAsync("Delete Chat", "Delete \"" + target.Title + "\" and its messages and ratings?", "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                await Workspace.Chat.DeleteConversationAsync(target.Id, token).ConfigureAwait(false);
                _Current = null;
                return "Deleted \"" + target.Title + "\".";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private async Task DeleteManyAsync()
        {
            // A checkbox per conversation; the ticked ones are deleted together after one confirmation.
            List<ChatConversation> conversations = _Conversations.Take(200).ToList();
            if (conversations.Count == 0)
            {
                Context.Notify("There are no chats to delete.", NotificationSeverity.Info);
                return;
            }

            List<XenoFormFieldSpec> fields = conversations.Select((item, index) => new XenoFormFieldSpec
            {
                Key = "c" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Label = item.Title + "  (" + item.Model + ", " + ChatFormat.When(item.UpdatedUtc) + ")",
                Kind = XenoSettingKindEnum.Toggle,
                Value = _Current != null && _Current.Id == item.Id ? "true" : "false"
            }).ToList();
            Dictionary<string, string>? values = await Context.FormAsync("Delete Chats", "Tick the chats to delete, then press Ctrl+S.", fields, null, "Next", "Checking...").ConfigureAwait(false);
            if (values == null) return;
            List<ChatConversation> chosen = conversations.Where((item, index) => values.TryGetValue("c" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), out string? value) && value == "true").ToList();
            if (chosen.Count == 0)
            {
                Context.Notify("No chats were ticked.", NotificationSeverity.Info);
                return;
            }

            bool confirmed = await Context.ConfirmAsync("Delete Chats", "Delete " + chosen.Count + " chat" + (chosen.Count == 1 ? String.Empty : "s") + " and their messages and ratings?", "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                foreach (ChatConversation conversation in chosen) await Workspace.Chat.DeleteConversationAsync(conversation.Id, token).ConfigureAwait(false);
                if (_Current != null && chosen.Any(item => item.Id == _Current.Id)) _Current = null;
                return "Deleted " + chosen.Count + " chat" + (chosen.Count == 1 ? String.Empty : "s") + ".";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        #endregion

        #region Private-Classes

        private sealed class ChatPageData
        {
            internal ChatPageData(ModelCatalog catalog, List<ChatConversation> conversations, ChatConversation? current)
            {
                Catalog = catalog;
                Conversations = conversations;
                Current = current;
            }

            internal ModelCatalog Catalog { get; }

            internal List<ChatConversation> Conversations { get; }

            internal ChatConversation? Current { get; }
        }

        #endregion
    }
}
