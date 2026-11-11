namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Chat;
    using Xeno.Client.Presentation;
    using Xeno.Core.Chat;
    using Xeno.Client.Services;
    using Xeno.Client.Tools;

    /// <summary>
    /// The "xeno chat" command: interactive chat, one-shot questions, and conversation, history, and feedback queries,
    /// all through the running client's chat API.
    /// </summary>
    public static partial class Program
    {
        private static string? _ChatDirectory;

        private static async Task<int> HandleChatAsync(string[] args)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintChatHelp();
                return 0;
            }

            using (XenoChatClient chat = new XenoChatClient())
            {
                string? sub = args.Length > 1 && !args[1].StartsWith("-", StringComparison.Ordinal) ? args[1].ToLowerInvariant() : null;
                try
                {
                    switch (sub)
                    {
                        case "list":
                        case "ls":
                            return await ChatListAsync(chat, args).ConfigureAwait(false);
                        case "show":
                            return await ChatShowAsync(chat, args).ConfigureAwait(false);
                        case "delete":
                        case "rm":
                            return await ChatDeleteAsync(chat, args).ConfigureAwait(false);
                        case "history":
                            return await ChatHistoryAsync(chat, args).ConfigureAwait(false);
                        case "feedback":
                            return await ChatFeedbackAsync(chat, args).ConfigureAwait(false);
                        case "rate":
                            return await ChatRateAsync(chat, args).ConfigureAwait(false);
                        case "models":
                            return await ChatModelsAsync(chat).ConfigureAwait(false);
                        default:
                            return await ChatSendAsync(chat, args).ConfigureAwait(false);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message == XenoChatClient.NotRunningMessage)
                {
                    WriteError(ex.Message + " Run 'xeno start'.");
                    return 1;
                }
                catch (KeyNotFoundException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }
                catch (ArgumentException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }
            }
        }

        private static async Task<int> ChatSendAsync(XenoChatClient chat, string[] args)
        {
            string? model = ChatOption(args, "--model", "-m");
            string? conversationId = ChatOption(args, "--conversation", "-c");
            string? system = ChatOption(args, "--system", null);
            _ChatDirectory = System.IO.Path.GetFullPath(ChatOption(args, "--cwd", null) ?? Environment.CurrentDirectory);
            bool tools = args.Any(arg => String.Equals(arg, "--tools", StringComparison.OrdinalIgnoreCase));
            bool yes = args.Any(arg => String.Equals(arg, "--yes", StringComparison.OrdinalIgnoreCase) || String.Equals(arg, "-y", StringComparison.Ordinal));
            List<string> words = ChatPositional(args, 1, new[] { "--model", "-m", "--conversation", "-c", "--system", "--cwd" });
            string prompt = String.Join(" ", words).Trim();
            if (prompt.Length == 0 && Console.IsInputRedirected) prompt = (await Console.In.ReadToEndAsync().ConfigureAwait(false)).Trim();

            if (prompt.Length == 0)
            {
                if (_UseJson || Console.IsInputRedirected)
                {
                    WriteError("Give a message, or run 'xeno chat' in a terminal for an interactive chat.");
                    return 1;
                }

                return await ChatInteractiveAsync(chat, model, conversationId, system, tools).ConfigureAwait(false);
            }

            ChatConversation conversation = await OpenConversationAsync(chat, model, conversationId, system, tools).ConfigureAwait(false);
            StringBuilder answer = new StringBuilder();
            string? error = null;
            ChatFailure? failure = null;
            ChatTurn? turn = await chat.SendAsync(conversation.Id, new ChatSendRequest { Content = prompt, Model = model, Surface = ChatSurfaceEnum.Cli }, async item =>
            {
                if (item.Kind == ChatStreamEventKindEnum.Chunk)
                {
                    answer.Append(item.Text);
                    if (!_UseJson) Console.Write(item.Text);
                }
                else if (item.Kind == ChatStreamEventKindEnum.Error)
                {
                    error = ChatFormat.FailureLine(item, answer.Length > 0);
                    failure = item.Failure;
                }
                else if (item.Kind == ChatStreamEventKindEnum.ToolCall && item.ToolCall != null)
                {
                    await HandleToolEventAsync(chat, item, yes && !_UseJson ? true : (bool?)null).ConfigureAwait(false);
                }
            }, CancellationToken.None).ConfigureAwait(false);

            if (_UseJson)
            {
                WriteJson(XenoChatResults.Build(conversation.Id, turn, answer.ToString(), error, failure));
            }
            else
            {
                Console.WriteLine();
                if (error != null) WriteError(error);
                Console.Error.WriteLine(DescribeTurn(turn) + " Conversation " + conversation.Id + ".");
            }

            return turn?.Status == ChatTurnStatusEnum.Success ? 0 : 1;
        }

        private static async Task<int> ChatInteractiveAsync(XenoChatClient chat, string? model, string? conversationId, string? system, bool tools)
        {
            ChatConversation conversation = await OpenConversationAsync(chat, model, conversationId, system, tools).ConfigureAwait(false);
            WriteInfoHeading("Xeno chat with " + conversation.Model);
            Console.WriteLine("Type a message and press Enter. Commands: /model <name>, /models, /new, /tools on|off, /cwd <folder>, /system <text>, /rate up|down [comment], /exit.");
            if (conversation.ToolsEnabled) WriteMuted("Tools work in " + conversation.WorkingDirectory + ".");
            WriteMuted(ChatFormat.Disclaimer);
            Console.WriteLine();
            string? lastAnswerId = null;
            while (true)
            {
                Console.Write("> ");
                string? line = Console.ReadLine();
                if (line == null) return 0;
                line = line.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("/", StringComparison.Ordinal))
                {
                    string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string argument = parts.Length > 1 ? parts[1].Trim() : String.Empty;
                    switch (parts[0].ToLowerInvariant())
                    {
                        case "/exit":
                        case "/quit":
                            return 0;
                        case "/new":
                            conversation = await chat.CreateConversationAsync(new ChatConversationRequest { Model = conversation.Model, Surface = ChatSurfaceEnum.Cli, ToolsEnabled = conversation.ToolsEnabled, WorkingDirectory = conversation.WorkingDirectory }, CancellationToken.None).ConfigureAwait(false);
                            lastAnswerId = null;
                            WriteSuccess("New conversation with " + conversation.Model + ".");
                            break;
                        case "/model":
                            if (argument.Length == 0)
                            {
                                Console.WriteLine("Model: " + conversation.Model);
                                break;
                            }

                            conversation = await chat.UpdateConversationAsync(conversation.Id, new ChatConversationRequest { Model = argument }, CancellationToken.None).ConfigureAwait(false);
                            WriteSuccess("Now chatting with " + conversation.Model + ".");
                            break;
                        case "/models":
                            await ChatModelsAsync(chat).ConfigureAwait(false);
                            break;
                        case "/tools":
                            bool on = !String.Equals(argument, "off", StringComparison.OrdinalIgnoreCase);
                            conversation = await chat.UpdateConversationAsync(conversation.Id, new ChatConversationRequest { ToolsEnabled = on }, CancellationToken.None).ConfigureAwait(false);
                            WriteSuccess(on ? "Tools on. Tools that change something will ask first." : "Tools off.");
                            break;
                        case "/cwd":
                            conversation = await chat.UpdateConversationAsync(conversation.Id, new ChatConversationRequest { WorkingDirectory = argument.Length == 0 ? String.Empty : System.IO.Path.GetFullPath(argument) }, CancellationToken.None).ConfigureAwait(false);
                            WriteSuccess("Tools now work in " + conversation.WorkingDirectory + ".");
                            break;
                        case "/system":
                            conversation = await chat.UpdateConversationAsync(conversation.Id, new ChatConversationRequest { SystemPrompt = argument }, CancellationToken.None).ConfigureAwait(false);
                            WriteSuccess(argument.Length == 0 ? "Removed the system prompt." : "Set the system prompt.");
                            break;
                        case "/rate":
                            if (lastAnswerId == null)
                            {
                                WriteError("There is no answer to rate yet.");
                                break;
                            }

                            string[] rating = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            int? value = rating.Length > 0 ? ParseRating(rating[0]) : null;
                            if (value == null)
                            {
                                WriteError("Use /rate up or /rate down, optionally followed by a comment.");
                                break;
                            }

                            await chat.SetFeedbackAsync(lastAnswerId, new ChatFeedbackRequest { Rating = value.Value, Comment = rating.Length > 1 ? rating[1] : null }, CancellationToken.None).ConfigureAwait(false);
                            WriteSuccess("Thanks. Rated the last answer " + (value.Value > 0 ? "up" : "down") + ".");
                            break;
                        default:
                            WriteError("Unknown command " + parts[0] + ".");
                            break;
                    }

                    continue;
                }

                using (CancellationTokenSource stop = new CancellationTokenSource())
                {
                    string? turnId = null;
                    ConsoleCancelEventHandler cancel = (sender, e) =>
                    {
                        e.Cancel = true;
                        if (turnId != null) _ = chat.CancelAsync(turnId, CancellationToken.None);
                        else stop.Cancel();
                    };
                    Console.CancelKeyPress += cancel;
                    try
                    {
                        bool thinking = false;
                        bool answered = false;
                        ChatTurn? turn = await chat.SendAsync(conversation.Id, new ChatSendRequest { Content = line, Surface = ChatSurfaceEnum.Cli }, async item =>
                        {
                            switch (item.Kind)
                            {
                                case ChatStreamEventKindEnum.Turn:
                                    turnId = item.TurnId;
                                    break;
                                case ChatStreamEventKindEnum.Thinking:
                                    if (!thinking)
                                    {
                                        thinking = true;
                                        WriteMuted("(thinking) ");
                                    }

                                    break;
                                case ChatStreamEventKindEnum.Chunk:
                                    if (thinking)
                                    {
                                        thinking = false;
                                        Console.WriteLine();
                                    }

                                    answered = true;
                                    Console.Write(item.Text);
                                    break;
                                case ChatStreamEventKindEnum.ToolCall:
                                    await HandleToolEventAsync(chat, item, null).ConfigureAwait(false);
                                    break;
                                case ChatStreamEventKindEnum.Error:
                                    Console.WriteLine();
                                    WriteError(ChatFormat.FailureLine(item, answered));
                                    break;
                            }
                        }, stop.Token).ConfigureAwait(false);
                        Console.WriteLine();
                        lastAnswerId = turn?.AssistantMessageId ?? lastAnswerId;
                        WriteMuted(DescribeTurn(turn));
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine();
                        WriteMuted("Stopped.");
                        Console.WriteLine();
                    }
                    finally
                    {
                        Console.CancelKeyPress -= cancel;
                    }
                }
            }
        }

        private static async Task HandleToolEventAsync(XenoChatClient chat, ChatStreamEvent item, bool? autoAnswer)
        {
            ChatToolCallRecord call = item.ToolCall!;
            if (call.Approval == ChatToolApprovalEnum.Pending)
            {
                Console.WriteLine();
                bool allow;
                if (autoAnswer.HasValue)
                {
                    allow = autoAnswer.Value;
                }
                else if (Console.IsInputRedirected)
                {
                    allow = false;
                }
                else
                {
                    Console.Write("The model wants to run " + call.ToolName + " " + call.Arguments + ", which changes something. Allow? [y/N] ");
                    string? answer = Console.ReadLine();
                    allow = String.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) || String.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
                }

                await chat.ApproveAsync(item.TurnId!, call.Id, allow, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (_UseJson) return;
            if (call.Success) WriteMuted("[" + call.ToolName + " done in " + FormatMs(call.DurationMs) + "]");
            else if (call.Error != null) WriteMuted("[" + call.ToolName + ": " + call.Error + "]");
            else WriteMuted("[running " + call.ToolName + "]");
            Console.WriteLine();
        }

        private static async Task<ChatConversation> OpenConversationAsync(XenoChatClient chat, string? model, string? conversationId, string? system, bool tools)
        {
            if (!String.IsNullOrWhiteSpace(conversationId))
            {
                ChatConversation existing = await chat.GetConversationAsync(conversationId, CancellationToken.None).ConfigureAwait(false);
                if (tools && !existing.ToolsEnabled) existing = await chat.UpdateConversationAsync(existing.Id, new ChatConversationRequest { ToolsEnabled = true }, CancellationToken.None).ConfigureAwait(false);
                return existing;
            }

            if (String.IsNullOrWhiteSpace(model))
            {
                ModelCatalog catalog = await chat.ListModelsAsync(CancellationToken.None).ConfigureAwait(false);
                ModelCatalogEntry? first = catalog.Entries.FirstOrDefault(entry => entry.ServedLocally && (entry.SupportsCompletions || !entry.SupportsEmbeddings))
                    ?? catalog.Entries.FirstOrDefault(entry => entry.SupportsCompletions || !entry.SupportsEmbeddings);
                if (first == null) throw new ArgumentException("No models are available. Add a provider with 'xeno provider add', or sign in to use Xeno network models.");
                model = first.Name;
            }

            return await chat.CreateConversationAsync(new ChatConversationRequest { Model = model, SystemPrompt = system, ToolsEnabled = tools, Surface = ChatSurfaceEnum.Cli, WorkingDirectory = _ChatDirectory }, CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task<int> ChatModelsAsync(XenoChatClient chat)
        {
            ModelCatalog catalog = await chat.ListModelsAsync(CancellationToken.None).ConfigureAwait(false);
            if (_UseJson)
            {
                WriteJson(catalog);
                return 0;
            }

            foreach (ModelCatalogEntry entry in catalog.Entries.Where(entry => entry.SupportsCompletions || !entry.SupportsEmbeddings))
            {
                Console.WriteLine("  " + entry.Name.PadRight(40) + entry.AvailabilityText);
            }

            if (!String.IsNullOrWhiteSpace(catalog.NetworkMessage)) WriteMuted("  Xeno network models are unavailable: " + catalog.NetworkMessage + Environment.NewLine);
            return 0;
        }

        private static async Task<int> ChatListAsync(XenoChatClient chat, string[] args)
        {
            List<ChatConversation> conversations = await chat.ListConversationsAsync(ChatOption(args, "--search", null), ParseLimit(args, 50), CancellationToken.None).ConfigureAwait(false);
            if (_UseJson)
            {
                WriteJson(conversations);
                return 0;
            }

            if (conversations.Count == 0)
            {
                Console.WriteLine("No conversations.");
                return 0;
            }

            foreach (ChatConversation item in conversations)
            {
                Console.WriteLine(item.Id + "  " + Clip(item.Title, 40).PadRight(40) + "  " + Clip(item.Model, 24).PadRight(24) + "  " + item.MessageCount.ToString(CultureInfo.InvariantCulture).PadLeft(4) + " msgs  " + item.UpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
            }

            return 0;
        }

        private static async Task<int> ChatShowAsync(XenoChatClient chat, string[] args)
        {
            string id = ChatPositional(args, 2, Array.Empty<string>()).FirstOrDefault() ?? throw new ArgumentException("Usage: xeno chat show <conversation-id>");
            ChatConversation conversation = await chat.GetConversationAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (_UseJson)
            {
                WriteJson(conversation);
                return 0;
            }

            WriteInfoHeading(conversation.Title + " (" + conversation.Model + ")");
            foreach (ChatMessage message in conversation.Messages.Where(item => item.Role == ChatRoleEnum.User || item.Role == ChatRoleEnum.Assistant))
            {
                Console.WriteLine();
                Console.WriteLine(message.Role == ChatRoleEnum.User ? "You:" : "Model (" + (message.Model ?? "?") + ", " + (message.Route ?? "?") + ", " + message.Id + "):");
                if (!String.IsNullOrEmpty(message.Content)) Console.WriteLine(message.Content);
                string? failure = message.Role == ChatRoleEnum.Assistant ? ChatFormat.FailureLine(message) : null;
                if (failure != null) WriteError(failure);
                if (message.Feedback != null) WriteMuted("  Rated " + (message.Feedback.Rating > 0 ? "up" : "down") + (String.IsNullOrWhiteSpace(message.Feedback.Comment) ? String.Empty : ": " + message.Feedback.Comment) + Environment.NewLine);
            }

            return 0;
        }

        private static async Task<int> ChatDeleteAsync(XenoChatClient chat, string[] args)
        {
            string id = ChatPositional(args, 2, Array.Empty<string>()).FirstOrDefault() ?? throw new ArgumentException("Usage: xeno chat delete <conversation-id>");
            bool removed = await chat.DeleteConversationAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (!removed) throw new KeyNotFoundException("Conversation '" + id + "' not found.");
            WriteSuccess("Deleted conversation " + id + ".");
            return 0;
        }

        private static async Task<int> ChatRateAsync(XenoChatClient chat, string[] args)
        {
            List<string> words = ChatPositional(args, 2, Array.Empty<string>());
            int? rating = words.Count > 1 ? ParseRating(words[1]) : null;
            if (words.Count < 2 || rating == null) throw new ArgumentException("Usage: xeno chat rate <message-id> up|down [comment]");
            ChatFeedback feedback = await chat.SetFeedbackAsync(words[0], new ChatFeedbackRequest { Rating = rating.Value, Comment = words.Count > 2 ? String.Join(" ", words.Skip(2)) : null }, CancellationToken.None).ConfigureAwait(false);
            if (_UseJson) WriteJson(feedback);
            else WriteSuccess("Rated " + words[0] + " " + (rating.Value > 0 ? "up" : "down") + ".");
            return 0;
        }

        private static async Task<int> ChatHistoryAsync(XenoChatClient chat, string[] args)
        {
            ChatHistoryQuery query = new ChatHistoryQuery
            {
                Model = ChatOption(args, "--model", "-m"),
                Route = ChatOption(args, "--route", null),
                Status = ParseChatEnum<ChatTurnStatusEnum>(ChatOption(args, "--status", null)),
                Surface = ParseChatEnum<ChatSurfaceEnum>(ChatOption(args, "--surface", null)),
                Rating = ChatOption(args, "--rating", null) is string rating ? (rating == "none" ? 0 : ParseRating(rating) ?? throw new ArgumentException("--rating must be up, down, or none.")) : null,
                Search = ChatOption(args, "--search", null),
                PageSize = ParseLimit(args, 50)
            };
            ChatPage<ChatTurn> page = await chat.QueryHistoryAsync(query, CancellationToken.None).ConfigureAwait(false);
            if (_UseJson)
            {
                WriteJson(page);
                return 0;
            }

            Console.WriteLine(page.Total.ToString(CultureInfo.InvariantCulture) + " chat turns" + (page.Total > page.Items.Count ? " (showing " + page.Items.Count + ")" : String.Empty) + ".");
            foreach (ChatTurn turn in page.Items)
            {
                string rated = turn.FeedbackRating == 1 ? "up" : turn.FeedbackRating == -1 ? "down" : "-";
                Console.WriteLine(turn.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture).PadRight(20) + Clip(turn.Model, 24).PadRight(26) + Clip(turn.Route, 22).PadRight(24) + turn.Status.ToString().PadRight(11) + FormatMs(turn.TotalMs).PadLeft(9) + "  " + rated.PadRight(5) + Clip(turn.ConversationTitle ?? "(deleted)", 40));
            }

            return 0;
        }

        private static async Task<int> ChatFeedbackAsync(XenoChatClient chat, string[] args)
        {
            ChatFeedbackQuery query = new ChatFeedbackQuery
            {
                Rating = ChatOption(args, "--rating", null) is string rating ? ParseRating(rating) ?? throw new ArgumentException("--rating must be up or down.") : null,
                Model = ChatOption(args, "--model", "-m"),
                HasComment = args.Any(arg => String.Equals(arg, "--comments", StringComparison.OrdinalIgnoreCase)) ? true : null,
                Search = ChatOption(args, "--search", null),
                PageSize = ParseLimit(args, 50)
            };
            ChatPage<ChatFeedback> page = await chat.QueryFeedbackAsync(query, CancellationToken.None).ConfigureAwait(false);
            if (_UseJson)
            {
                WriteJson(page);
                return 0;
            }

            Console.WriteLine(page.Total.ToString(CultureInfo.InvariantCulture) + " ratings.");
            foreach (ChatFeedback item in page.Items)
            {
                Console.WriteLine(item.UpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture).PadRight(20) + (item.Rating > 0 ? "up  " : "down") + "  " + Clip(item.Model ?? "-", 24).PadRight(26) + Clip(item.ConversationTitle ?? "(deleted)", 30).PadRight(32) + (item.Comment ?? String.Empty));
            }

            return 0;
        }

        private static string DescribeTurn(ChatTurn? turn)
        {
            if (turn == null) return "No answer.";
            List<string> parts = new List<string> { turn.Model };
            if (!String.IsNullOrWhiteSpace(turn.Route)) parts.Add(turn.Route.StartsWith("local:", StringComparison.Ordinal) ? "this machine (" + turn.Route.Substring(6) + ")" : turn.Route == "network" ? "Xeno network" : turn.Route);
            if (turn.Fallback) parts.Add("fallback");
            parts.Add(FormatMs(turn.TotalMs));
            if (turn.CompletionTokens.HasValue) parts.Add(turn.CompletionTokens.Value.ToString(CultureInfo.InvariantCulture) + " tokens");
            if (turn.ToolCallCount > 0) parts.Add(turn.ToolCallCount.ToString(CultureInfo.InvariantCulture) + " tool calls");
            if (turn.Status != ChatTurnStatusEnum.Success) parts.Add(turn.Status.ToString().ToLowerInvariant());
            return String.Join(" · ", parts) + ".";
        }

        private static string FormatMs(double ms)
        {
            return ms < 1000 ? Math.Round(ms).ToString(CultureInfo.InvariantCulture) + " ms" : (ms / 1000).ToString(ms < 10000 ? "0.0" : "0", CultureInfo.InvariantCulture) + " s";
        }

        private static int? ParseRating(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "up":
                case "1":
                case "+1":
                case "good":
                    return 1;
                case "down":
                case "-1":
                case "bad":
                    return -1;
                default:
                    return null;
            }
        }

        private static T? ParseChatEnum<T>(string? value) where T : struct, Enum
        {
            if (value == null) return null;
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not one of: " + String.Join(", ", Enum.GetNames<T>()) + ".");
        }

        private static int ParseLimit(string[] args, int fallback)
        {
            string? value = ChatOption(args, "--limit", "-n");
            if (value == null) return fallback;
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit < 1 || limit > 500) throw new ArgumentException("--limit must be from 1 to 500.");
            return limit;
        }

        private static string? ChatOption(string[] args, string name, string? alias)
        {
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) || (alias != null && String.Equals(args[i], alias, StringComparison.Ordinal))) return args[i + 1];
            }

            return null;
        }

        private static List<string> ChatPositional(string[] args, int start, IEnumerable<string> valueOptions)
        {
            HashSet<string> withValues = new HashSet<string>(valueOptions.Concat(new[] { "--search", "--limit", "-n", "--route", "--status", "--surface", "--rating", "--model", "-m" }), StringComparer.OrdinalIgnoreCase);
            List<string> words = new List<string>();
            for (int i = start; i < args.Length; i++)
            {
                if (withValues.Contains(args[i]))
                {
                    i++;
                    continue;
                }

                if (args[i].StartsWith("-", StringComparison.Ordinal) && args[i].Length > 1) continue;
                words.Add(args[i]);
            }

            return words;
        }

        private static string Clip(string value, int length)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
        }

        private static void WriteMuted(string text)
        {
            ConsoleColor previous = Console.ForegroundColor;
            if (_UseColors) Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(text);
            if (_UseColors) Console.ForegroundColor = previous;
        }

        private static void PrintChatHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  xeno chat [-m <model>] [-c <conversation-id>] [--tools] [--cwd <folder>] [--system <text>]   Interactive chat");
            Console.WriteLine("  xeno chat [-m <model>] [-c <conversation-id>] [--tools [--yes]] [--cwd <folder>] \"message\"   Ask once (also reads a piped message)");
            Console.WriteLine("    Tools work in --cwd (default: the current folder) and cannot reach outside it.");
            Console.WriteLine("  xeno chat models                                List models you can chat with");
            Console.WriteLine("  xeno chat list [--search <text>] [--limit <n>]  List conversations");
            Console.WriteLine("  xeno chat show <conversation-id>                Show a conversation");
            Console.WriteLine("  xeno chat delete <conversation-id>              Delete a conversation");
            Console.WriteLine("  xeno chat rate <message-id> up|down [comment]   Rate an answer");
            Console.WriteLine("  xeno chat history [--model <m>] [--route local|network] [--status Success|Error|Cancelled]");
            Console.WriteLine("                    [--surface Web|Desktop|Terminal|Cli|Mcp] [--rating up|down|none] [--search <text>] [--limit <n>]");
            Console.WriteLine("  xeno chat feedback [--rating up|down] [--model <m>] [--comments] [--search <text>] [--limit <n>]");
            Console.WriteLine();
            Console.WriteLine("Add --json for machine-readable output. Chat needs the client running ('xeno start'). Models served on");
            Console.WriteLine("this machine answer here; the rest go to the Xeno network. --tools lets the model use Xeno's tools;");
            Console.WriteLine("tools that change something ask first (--yes allows them in one-shot mode).");
        }
    }
}
