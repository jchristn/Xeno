namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Chat routes of the local dashboard API (/api/v1/chat/...).
    /// </summary>
    public sealed partial class LocalDashboardService
    {
        #region Private-Members

        private readonly IChatStore? _Chat;
        private readonly ChatEngine? _ChatEngine;
        private XenoServerClient? _ChatServer;
        private Xeno.Client.LocalInference.ChatModelPreparer? _ChatPreparer;

        #endregion

        #region Private-Methods

        private async Task HandleChatAsync(HttpContextBase context, string method, string path, CancellationToken token)
        {
            if (_Chat == null || _ChatEngine == null) throw new InvalidOperationException("Chat is not available in this client.");
            string rest = path.Substring("/api/v1/chat/".Length).Trim('/');
            string[] parts = rest.Split('/');
            for (int i = 0; i < parts.Length; i++) parts[i] = Uri.UnescapeDataString(parts[i]);

            if (parts[0] == "models" && parts.Length == 1 && method == "GET")
            {
                _ChatServer ??= new XenoServerClient();
                ModelCatalog catalog = await ModelCatalogBuilder.BuildAsync(_Settings, _ChatServer, token).ConfigureAwait(false);
                catalog.Entries = catalog.Entries.Where(entry => !ChatModelNames.IsEmbeddingOnly(entry.Name, entry.SupportsCompletions, entry.SupportsEmbeddings)).ToList();
                await SendJsonAsync(context, catalog).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "models" && parts.Length == 2 && parts[1] == "prepare" && method == "POST")
            {
                ChatModelPrepareRequest prepare = ReadJson<ChatModelPrepareRequest>(context);
                _ChatPreparer ??= new Xeno.Client.LocalInference.ChatModelPreparer(() => _Settings);
                await SendJsonAsync(context, await _ChatPreparer.PrepareAsync(prepare.Model, token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "mcp-servers" && parts.Length == 1 && (method == "GET" || method == "POST"))
            {
                Xeno.Client.Tools.Mcp.IMcpToolSource? mcp = LocalDashboardRuntime.McpTools;
                if (mcp == null)
                {
                    await SendJsonAsync(context, new List<Xeno.Client.Tools.Mcp.McpServerStatus>()).ConfigureAwait(false);
                    return;
                }

                if (method == "POST") await mcp.RefreshAsync(token).ConfigureAwait(false);
                await SendJsonAsync(context, mcp.Servers).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "tools" && parts.Length == 1 && method == "GET")
            {
                Xeno.Client.Tools.XenoToolCatalog? catalog = LocalDashboardRuntime.Tools;
                List<ChatToolInfo> tools = new List<ChatToolInfo>();
                if (LocalDashboardRuntime.ChatTools != null)
                {
                    tools.AddRange(LocalDashboardRuntime.ChatTools.Describe());
                }
                else if (catalog != null)
                {
                    foreach (Xeno.Client.Tools.XenoTool tool in catalog.Tools)
                    {
                        if (String.Equals(tool.Name, Xeno.Client.Tools.XenoToolCatalog.ChatToolName, StringComparison.Ordinal)) continue;
                        tools.Add(new ChatToolInfo { Name = tool.Name, Description = tool.Description, Mutating = tool.Mutating });
                    }
                }

                await SendJsonAsync(context, tools).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "conversations")
            {
                await HandleConversationsAsync(context, method, parts, token).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "turns" && parts.Length == 3 && parts[2] == "cancel" && method == "POST")
            {
                await SendJsonAsync(context, new ChatMutationResponse { Changed = _ChatEngine.Cancel(parts[1]) }).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "turns" && parts.Length == 5 && parts[2] == "tool-calls" && parts[4] == "approval" && method == "POST")
            {
                ChatToolApprovalRequest approval = ReadJson<ChatToolApprovalRequest>(context);
                bool answered = _ChatEngine.Approve(parts[1], parts[3], approval.Allow);
                if (!answered) throw new KeyNotFoundException("That tool call is not waiting for approval.");
                await SendJsonAsync(context, new ChatMutationResponse { Changed = true }).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "messages" && parts.Length == 3 && parts[2] == "feedback")
            {
                if (method == "PUT" || method == "POST")
                {
                    await SendJsonAsync(context, await _Chat.SetFeedbackAsync(parts[1], ReadJson<ChatFeedbackRequest>(context), token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }

                if (method == "DELETE")
                {
                    await SendJsonAsync(context, new ChatMutationResponse { Changed = await _Chat.DeleteFeedbackAsync(parts[1], token).ConfigureAwait(false) }).ConfigureAwait(false);
                    return;
                }
            }

            if (parts[0] == "feedback" && parts.Length == 1 && method == "GET")
            {
                await SendJsonAsync(context, await _Chat.QueryFeedbackAsync(ReadFeedbackQuery(context), token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "history" && parts.Length == 1)
            {
                if (method == "GET")
                {
                    await SendJsonAsync(context, await _Chat.QueryTurnsAsync(ReadHistoryQuery(context), token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }

                if (method == "DELETE")
                {
                    int removed = await _Chat.DeleteTurnsAsync(ReadHistoryQuery(context), token).ConfigureAwait(false);
                    await SendJsonAsync(context, new ChatMutationResponse { Changed = removed > 0, Removed = removed }).ConfigureAwait(false);
                    return;
                }
            }

            if (parts[0] == "history" && parts.Length == 2 && method == "GET")
            {
                ChatTurn turn = await _Chat.GetTurnAsync(parts[1], token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Turn not found.");
                await SendJsonAsync(context, turn).ConfigureAwait(false);
                return;
            }

            if (parts[0] == "analytics" && parts.Length == 1 && method == "GET")
            {
                DateTime to = ParseUtc(Query(context, "toUtc")) ?? DateTime.UtcNow;
                DateTime from = ParseUtc(Query(context, "fromUtc")) ?? to.AddHours(-24);
                if (to <= from) throw new ArgumentException("toUtc must be after fromUtc.");
                string? seconds = Query(context, "bucketSeconds");
                int bucket = !String.IsNullOrWhiteSpace(seconds) ? ParseInt(seconds, 3600) : ParseInt(Query(context, "bucketMinutes"), 60) * 60;
                await SendJsonAsync(context, await _Chat.AnalyticsAsync(from, to, bucket, token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            throw new KeyNotFoundException("Chat route not found.");
        }

        private async Task HandleConversationsAsync(HttpContextBase context, string method, string[] parts, CancellationToken token)
        {
            IChatStore chat = _Chat!;
            if (parts.Length == 1 && method == "GET")
            {
                ChatSurfaceEnum? surface = ParseEnum<ChatSurfaceEnum>(Query(context, "surface"));
                await SendJsonAsync(context, await chat.ListConversationsAsync(Query(context, "search"), surface, ParseInt(Query(context, "limit"), 200), token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (parts.Length == 1 && method == "POST")
            {
                ChatConversationRequest request = ReadJson<ChatConversationRequest>(context);
                ChatConversation conversation = new ChatConversation
                {
                    Title = String.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim(),
                    Model = request.Model?.Trim() ?? String.Empty,
                    SystemPrompt = request.SystemPrompt ?? _Settings.Chat.DefaultSystemPrompt,
                    ToolsEnabled = request.ToolsEnabled ?? false,
                    Pinned = request.Pinned ?? false,
                    Surface = request.Surface ?? ChatSurfaceEnum.Web,
                    WorkingDirectory = ChatWorkingDirectory(request.WorkingDirectory)
                };
                context.Response.StatusCode = 201;
                await SendJsonAsync(context, await chat.CreateConversationAsync(conversation, token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            if (parts.Length == 2 && method == "GET")
            {
                ChatConversation conversation = await chat.GetConversationAsync(parts[1], true, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation not found.");
                await SendJsonAsync(context, conversation).ConfigureAwait(false);
                return;
            }

            if (parts.Length == 2 && method == "PUT")
            {
                ChatConversationRequest request = ReadJson<ChatConversationRequest>(context);
                ChatConversation conversation = await chat.GetConversationAsync(parts[1], false, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation not found.");
                if (request.Title != null) conversation.Title = String.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim();
                if (request.Model != null) conversation.Model = request.Model.Trim();
                if (request.SystemPrompt != null) conversation.SystemPrompt = String.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt;
                if (request.ToolsEnabled.HasValue) conversation.ToolsEnabled = request.ToolsEnabled.Value;
                if (request.Pinned.HasValue) conversation.Pinned = request.Pinned.Value;
                if (request.WorkingDirectory != null) conversation.WorkingDirectory = ChatWorkingDirectory(request.WorkingDirectory);
                await chat.UpdateConversationAsync(conversation, token).ConfigureAwait(false);
                await SendJsonAsync(context, conversation).ConfigureAwait(false);
                return;
            }

            if (parts.Length == 2 && method == "DELETE")
            {
                LocalDashboardRuntime.ForgetConversation(parts[1]);
                await SendJsonAsync(context, new ChatMutationResponse { Changed = await chat.DeleteConversationAsync(parts[1], token).ConfigureAwait(false) }).ConfigureAwait(false);
                return;
            }

            if (parts.Length == 3 && parts[2] == "messages" && method == "POST")
            {
                await StreamTurnAsync(context, parts[1], ReadJson<ChatSendRequest>(context), token).ConfigureAwait(false);
                return;
            }

            throw new KeyNotFoundException("Chat route not found.");
        }

        private async Task StreamTurnAsync(HttpContextBase context, string conversationId, ChatSendRequest request, CancellationToken token)
        {
            // Validate before the stream starts so problems come back as ordinary JSON errors.
            if (String.IsNullOrWhiteSpace(request.Content)) throw new ArgumentException("Type a message to send.");
            ChatConversation conversation = await _Chat!.GetConversationAsync(conversationId, false, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation not found.");
            if (String.IsNullOrWhiteSpace(request.Model) && String.IsNullOrWhiteSpace(conversation.Model)) throw new ArgumentException("Choose a model for this conversation.");

            context.Response.StatusCode = 200;
            context.Response.ContentType = ChatSse.ContentType;
            context.Response.Headers.Add("Cache-Control", "no-store");
            context.Response.Headers.Add("X-Accel-Buffering", "no");
            context.Response.ChunkedTransfer = true;
            SemaphoreSlim gate = new SemaphoreSlim(1, 1);
            try
            {
                await _ChatEngine!.SendAsync(conversationId, request, async item =>
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(ChatSse.Format(item));
                    await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        await context.Response.SendChunk(bytes, false, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, token).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await context.Response.SendChunk(Array.Empty<byte>(), true, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is ObjectDisposedException || ex is InvalidOperationException || ex is OperationCanceledException)
                {
                }

                gate.Dispose();
            }
        }

        private static ChatHistoryQuery ReadHistoryQuery(HttpContextBase context)
        {
            return new ChatHistoryQuery
            {
                Model = Query(context, "model"),
                Route = Query(context, "route"),
                Provider = Query(context, "provider"),
                Status = ParseEnum<ChatTurnStatusEnum>(Query(context, "status")),
                Surface = ParseEnum<ChatSurfaceEnum>(Query(context, "surface")),
                Rating = ParseNullableInt(Query(context, "rating")),
                Fallback = ParseBool(Query(context, "fallback")),
                ConversationId = Query(context, "conversationId"),
                Search = Query(context, "search"),
                FromUtc = ParseUtc(Query(context, "fromUtc")),
                ToUtc = ParseUtc(Query(context, "toUtc")),
                Page = ParseInt(Query(context, "page"), 1),
                PageSize = ParseInt(Query(context, "pageSize"), 50)
            };
        }

        private static ChatFeedbackQuery ReadFeedbackQuery(HttpContextBase context)
        {
            return new ChatFeedbackQuery
            {
                Rating = ParseNullableInt(Query(context, "rating")),
                Model = Query(context, "model"),
                Route = Query(context, "route"),
                HasComment = ParseBool(Query(context, "hasComment")),
                Search = Query(context, "search"),
                FromUtc = ParseUtc(Query(context, "fromUtc")),
                ToUtc = ParseUtc(Query(context, "toUtc")),
                Page = ParseInt(Query(context, "page"), 1),
                PageSize = ParseInt(Query(context, "pageSize"), 50)
            };
        }

        private static string? Query(HttpContextBase context, string name)
        {
            string? value = context.Request.Query.Elements.Get(name);
            if (String.IsNullOrWhiteSpace(value)) return null;
            string decoded = Uri.UnescapeDataString(value).Trim();
            return decoded.Length == 0 ? null : decoded;
        }

        private static int? ParseNullableInt(string? value)
        {
            if (value == null) return null;
            if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not a number.");
        }

        private static bool? ParseBool(string? value)
        {
            if (value == null) return null;
            if (Boolean.TryParse(value, out bool parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not true or false.");
        }

        private static DateTime? ParseUtc(string? value)
        {
            if (value == null) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            throw new ArgumentException("'" + value + "' is not a date and time.");
        }

        private static T? ParseEnum<T>(string? value) where T : struct, Enum
        {
            if (value == null) return null;
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not one of: " + String.Join(", ", Enum.GetNames<T>()) + ".");
        }

        #endregion

        // The working directory for a conversation: the requested one (it must exist), or the default when the request
        // gives none or an empty string.
        private string ChatWorkingDirectory(string? requested)
        {
            string directory = Xeno.Client.Tools.XenoChatToolHost.EffectiveWorkingDirectory(String.IsNullOrWhiteSpace(requested) ? null : requested.Trim(), _Settings.Chat);
            if (!System.IO.Directory.Exists(directory)) throw new ArgumentException("The working directory does not exist: " + directory);
            return directory;
        }
    }
}
