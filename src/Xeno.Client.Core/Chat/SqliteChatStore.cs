namespace Xeno.Client.Chat
{
    using System;
    using Xeno.Core.Chat;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using SQLitePCL;
    using Xeno.Core.Helpers;

    /// <summary>
    /// SQLite chat storage in the local dashboard database. Thread safe: every call opens its own connection.
    /// </summary>
    public sealed class SqliteChatStore : IChatStore
    {
        #region Public-Members

        /// <summary>
        /// Longest tool result kept, in characters.
        /// </summary>
        public const int MaxToolResultChars = 16000;

        #endregion

        #region Private-Members

        private readonly ClientLocalDatabaseSettings _Settings;
        private string _ConnectionString = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the store.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public SqliteChatStore(ClientLocalDatabaseSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task InitializeAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string filename = _Settings.ResolveFilename();
            string? directory = Path.GetDirectoryName(filename);
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            Batteries_V2.Init();
            _ConnectionString = String.IsNullOrWhiteSpace(_Settings.ConnectionString)
                ? "Data Source=" + filename + ";Pooling=False;Foreign Keys=True;"
                : _Settings.ConnectionString;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, null, SchemaSql, null, token).ConfigureAwait(false);
                foreach (string migration in new[] { "ALTER TABLE chat_messages ADD COLUMN stats_json TEXT NULL;", "ALTER TABLE chat_conversations ADD COLUMN working_directory TEXT NULL;", "ALTER TABLE chat_messages ADD COLUMN failure_json TEXT NULL;", "ALTER TABLE chat_turns ADD COLUMN model_ms REAL NULL;", "ALTER TABLE chat_turns ADD COLUMN streaming_ms REAL NULL;", "ALTER TABLE chat_turns ADD COLUMN approval_ms REAL NULL;", "ALTER TABLE chat_turns ADD COLUMN tool_failures INTEGER NULL;", "ALTER TABLE chat_turns ADD COLUMN tool_denied INTEGER NULL;", "ALTER TABLE chat_turns ADD COLUMN cached_prompt_tokens INTEGER NULL;", "ALTER TABLE chat_tool_calls ADD COLUMN approval_ms REAL NULL;" })
                {
                    try
                    {
                        await ExecuteAsync(connection, null, migration, null, token).ConfigureAwait(false);
                    }
                    catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
                    {
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<ChatConversation> CreateConversationAsync(ChatConversation conversation, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (String.IsNullOrWhiteSpace(conversation.Id)) conversation.Id = IdGenerator.NewId("cnv_");
            DateTime now = DateTime.UtcNow;
            conversation.CreatedUtc = now;
            conversation.UpdatedUtc = now;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, null,
                    "INSERT INTO chat_conversations (id, title, model, system_prompt, tools_enabled, pinned, surface, created_utc, updated_utc, working_directory) VALUES ($id, $title, $model, $system, $tools, $pinned, $surface, $created, $updated, $directory);",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", conversation.Id);
                        AddConversationValues(command, conversation);
                        command.Parameters.AddWithValue("$surface", conversation.Surface.ToString());
                        command.Parameters.AddWithValue("$created", Format(conversation.CreatedUtc));
                    }, token).ConfigureAwait(false);
            }

            return conversation;
        }

        /// <inheritdoc />
        public async Task<ChatConversation?> GetConversationAsync(string id, bool includeMessages, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                List<ChatConversation> found = await ReadConversationsAsync(connection, "WHERE c.id = $id", command => command.Parameters.AddWithValue("$id", id), 1, token).ConfigureAwait(false);
                if (found.Count == 0) return null;
                ChatConversation conversation = found[0];
                if (!includeMessages) return conversation;
                conversation.Messages = await ReadMessagesAsync(connection, "WHERE m.conversation_id = $id", command => command.Parameters.AddWithValue("$id", id), token).ConfigureAwait(false);
                List<ChatToolCallRecord> calls = await ReadToolCallsAsync(connection, "WHERE message_id IN (SELECT id FROM chat_messages WHERE conversation_id = $id)", command => command.Parameters.AddWithValue("$id", id), token).ConfigureAwait(false);
                foreach (ChatMessage message in conversation.Messages)
                {
                    message.ToolCalls = calls.Where(call => String.Equals(call.MessageId, message.Id, StringComparison.Ordinal)).ToList();
                }

                return conversation;
            }
        }

        /// <inheritdoc />
        public async Task<List<ChatConversation>> ListConversationsAsync(string? search, ChatSurfaceEnum? surface, int limit, CancellationToken token)
        {
            List<string> where = new List<string>();
            if (!String.IsNullOrWhiteSpace(search)) where.Add("(c.title LIKE $search ESCAPE '\\' OR c.model LIKE $search ESCAPE '\\')");
            if (surface.HasValue) where.Add("c.surface = $surface");
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                return await ReadConversationsAsync(connection, where.Count == 0 ? String.Empty : "WHERE " + String.Join(" AND ", where), command =>
                {
                    if (!String.IsNullOrWhiteSpace(search)) command.Parameters.AddWithValue("$search", LikePattern(search));
                    if (surface.HasValue) command.Parameters.AddWithValue("$surface", surface.Value.ToString());
                }, Math.Clamp(limit, 1, 1000), token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateConversationAsync(ChatConversation conversation, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            conversation.UpdatedUtc = DateTime.UtcNow;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                int changed = await ExecuteAsync(connection, null,
                    "UPDATE chat_conversations SET title = $title, model = $model, system_prompt = $system, tools_enabled = $tools, pinned = $pinned, updated_utc = $updated, working_directory = $directory WHERE id = $id;",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", conversation.Id);
                        AddConversationValues(command, conversation);
                    }, token).ConfigureAwait(false);
                return changed > 0;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConversationAsync(string id, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                Action<SqliteCommand> bind = command => command.Parameters.AddWithValue("$id", id);
                await ExecuteAsync(connection, transaction, "DELETE FROM chat_feedback WHERE conversation_id = $id;", bind, token).ConfigureAwait(false);
                await ExecuteAsync(connection, transaction, "DELETE FROM chat_tool_calls WHERE message_id IN (SELECT id FROM chat_messages WHERE conversation_id = $id);", bind, token).ConfigureAwait(false);
                await ExecuteAsync(connection, transaction, "DELETE FROM chat_messages WHERE conversation_id = $id;", bind, token).ConfigureAwait(false);
                int removed = await ExecuteAsync(connection, transaction, "DELETE FROM chat_conversations WHERE id = $id;", bind, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return removed > 0;
            }
        }

        /// <inheritdoc />
        public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (String.IsNullOrWhiteSpace(message.ConversationId)) throw new ArgumentException("A message needs a conversation.");
            if (String.IsNullOrWhiteSpace(message.Id)) message.Id = IdGenerator.NewId("msg_");
            message.CreatedUtc = DateTime.UtcNow;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                object? next = await ScalarAsync(connection, transaction, "SELECT COALESCE(MAX(sequence), 0) + 1 FROM chat_messages WHERE conversation_id = $conversation;", command => command.Parameters.AddWithValue("$conversation", message.ConversationId), token).ConfigureAwait(false);
                message.Sequence = Convert.ToInt32(next, CultureInfo.InvariantCulture);
                await ExecuteAsync(connection, transaction,
                    "INSERT INTO chat_messages (id, conversation_id, sequence, role, content, thinking, model, route, fallback, prompt_tokens, completion_tokens, ttft_ms, total_ms, incomplete, turn_id, tool_call_id, created_utc, stats_json, failure_json) "
                    + "VALUES ($id, $conversation, $sequence, $role, $content, $thinking, $model, $route, $fallback, $prompt, $completion, $ttft, $total, $incomplete, $turn, $toolCall, $created, $stats, $failure);",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", message.Id);
                        command.Parameters.AddWithValue("$conversation", message.ConversationId);
                        command.Parameters.AddWithValue("$sequence", message.Sequence);
                        command.Parameters.AddWithValue("$role", message.Role.ToString());
                        command.Parameters.AddWithValue("$turn", Db(message.TurnId));
                        command.Parameters.AddWithValue("$toolCall", Db(message.ToolCallId));
                        command.Parameters.AddWithValue("$created", Format(message.CreatedUtc));
                        AddMessageValues(command, message);
                    }, token).ConfigureAwait(false);
                await ExecuteAsync(connection, transaction, "UPDATE chat_conversations SET updated_utc = $now WHERE id = $conversation;", command =>
                {
                    command.Parameters.AddWithValue("$now", Format(DateTime.UtcNow));
                    command.Parameters.AddWithValue("$conversation", message.ConversationId);
                }, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
            }

            return message;
        }

        /// <inheritdoc />
        public async Task UpdateMessageAsync(ChatMessage message, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(message);
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, null,
                    "UPDATE chat_messages SET content = $content, thinking = $thinking, model = $model, route = $route, fallback = $fallback, prompt_tokens = $prompt, completion_tokens = $completion, ttft_ms = $ttft, total_ms = $total, incomplete = $incomplete, stats_json = $stats, failure_json = $failure WHERE id = $id;",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", message.Id);
                        AddMessageValues(command, message);
                    }, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<ChatMessage?> GetMessageAsync(string id, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                List<ChatMessage> found = await ReadMessagesAsync(connection, "WHERE m.id = $id", command => command.Parameters.AddWithValue("$id", id), token).ConfigureAwait(false);
                if (found.Count == 0) return null;
                found[0].ToolCalls = await ReadToolCallsAsync(connection, "WHERE message_id = $id", command => command.Parameters.AddWithValue("$id", id), token).ConfigureAwait(false);
                return found[0];
            }
        }

        /// <inheritdoc />
        public async Task UpsertToolCallAsync(ChatToolCallRecord call, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(call);
            if (String.IsNullOrWhiteSpace(call.Id)) call.Id = IdGenerator.NewId("tcl_");
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, null,
                    "INSERT OR REPLACE INTO chat_tool_calls (id, call_id, turn_id, message_id, tool_name, arguments, result, mutating, approval, success, error, duration_ms, created_utc, approval_ms) "
                    + "VALUES ($id, $callId, $turn, $message, $name, $arguments, $result, $mutating, $approval, $success, $error, $duration, $created, $approvalMs);",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", call.Id);
                        command.Parameters.AddWithValue("$callId", call.CallId ?? String.Empty);
                        command.Parameters.AddWithValue("$turn", call.TurnId);
                        command.Parameters.AddWithValue("$message", call.MessageId);
                        command.Parameters.AddWithValue("$name", call.ToolName);
                        command.Parameters.AddWithValue("$arguments", call.Arguments ?? "{}");
                        command.Parameters.AddWithValue("$result", Db(Truncate(call.Result, MaxToolResultChars)));
                        command.Parameters.AddWithValue("$mutating", call.Mutating ? 1 : 0);
                        command.Parameters.AddWithValue("$approval", call.Approval.ToString());
                        command.Parameters.AddWithValue("$success", call.Success ? 1 : 0);
                        command.Parameters.AddWithValue("$error", Db(call.Error));
                        command.Parameters.AddWithValue("$duration", call.DurationMs);
                        command.Parameters.AddWithValue("$created", Format(call.CreatedUtc));
                        command.Parameters.AddWithValue("$approvalMs", call.ApprovalMs);
                    }, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task UpsertTurnAsync(ChatTurn turn, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(turn);
            if (String.IsNullOrWhiteSpace(turn.Id)) turn.Id = IdGenerator.NewId("trn_");
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, null,
                    "INSERT OR REPLACE INTO chat_turns (id, conversation_id, user_message_id, assistant_message_id, model, route, provider, fallback, status, error, status_code, prompt_tokens, completion_tokens, ttft_ms, total_ms, model_calls, tool_call_count, tool_ms, surface, created_utc, completed_utc, model_ms, streaming_ms, approval_ms, tool_failures, tool_denied, cached_prompt_tokens) "
                    + "VALUES ($id, $conversation, $user, $assistant, $model, $route, $provider, $fallback, $status, $error, $statusCode, $prompt, $completion, $ttft, $total, $calls, $tools, $toolMs, $surface, $created, $completed, $modelMs, $streamingMs, $approvalMs, $toolFailures, $toolDenied, $cached);",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", turn.Id);
                        command.Parameters.AddWithValue("$conversation", turn.ConversationId);
                        command.Parameters.AddWithValue("$user", Db(turn.UserMessageId));
                        command.Parameters.AddWithValue("$assistant", Db(turn.AssistantMessageId));
                        command.Parameters.AddWithValue("$model", turn.Model ?? String.Empty);
                        command.Parameters.AddWithValue("$route", turn.Route ?? String.Empty);
                        command.Parameters.AddWithValue("$provider", turn.Provider ?? String.Empty);
                        command.Parameters.AddWithValue("$fallback", turn.Fallback ? 1 : 0);
                        command.Parameters.AddWithValue("$status", turn.Status.ToString());
                        command.Parameters.AddWithValue("$error", Db(turn.Error));
                        command.Parameters.AddWithValue("$statusCode", Db(turn.StatusCode));
                        command.Parameters.AddWithValue("$prompt", Db(turn.PromptTokens));
                        command.Parameters.AddWithValue("$completion", Db(turn.CompletionTokens));
                        command.Parameters.AddWithValue("$ttft", Db(turn.TimeToFirstTokenMs));
                        command.Parameters.AddWithValue("$total", turn.TotalMs);
                        command.Parameters.AddWithValue("$calls", turn.ModelCalls);
                        command.Parameters.AddWithValue("$tools", turn.ToolCallCount);
                        command.Parameters.AddWithValue("$toolMs", turn.ToolMs);
                        command.Parameters.AddWithValue("$surface", turn.Surface.ToString());
                        command.Parameters.AddWithValue("$created", Format(turn.CreatedUtc));
                        command.Parameters.AddWithValue("$completed", turn.CompletedUtc.HasValue ? Format(turn.CompletedUtc.Value) : DBNull.Value);
                        command.Parameters.AddWithValue("$modelMs", turn.ModelMs);
                        command.Parameters.AddWithValue("$streamingMs", Db(turn.StreamingMs));
                        command.Parameters.AddWithValue("$approvalMs", turn.ApprovalMs);
                        command.Parameters.AddWithValue("$toolFailures", turn.ToolFailures);
                        command.Parameters.AddWithValue("$toolDenied", turn.ToolDenied);
                        command.Parameters.AddWithValue("$cached", Db(turn.CachedPromptTokens));
                    }, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<ChatTurn?> GetTurnAsync(string id, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                List<ChatTurn> found = await ReadTurnsAsync(connection, "WHERE t.id = $id", command => command.Parameters.AddWithValue("$id", id), String.Empty, token).ConfigureAwait(false);
                if (found.Count == 0) return null;
                found[0].ToolCalls = await ReadToolCallsAsync(connection, "WHERE turn_id = $id", command => command.Parameters.AddWithValue("$id", id), token).ConfigureAwait(false);
                return found[0];
            }
        }

        /// <inheritdoc />
        public async Task<ChatPage<ChatTurn>> QueryTurnsAsync(ChatHistoryQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            int page = Math.Max(1, query.Page);
            int size = Math.Clamp(query.PageSize, 1, 500);
            string where = TurnWhere(query, out Action<SqliteCommand> bind);
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                object? total = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM chat_turns t LEFT JOIN chat_conversations c ON c.id = t.conversation_id LEFT JOIN chat_feedback f ON f.message_id = t.assistant_message_id " + where + ";", bind, token).ConfigureAwait(false);
                List<ChatTurn> items = await ReadTurnsAsync(connection, where, command =>
                {
                    bind(command);
                    command.Parameters.AddWithValue("$limit", size);
                    command.Parameters.AddWithValue("$offset", (page - 1) * size);
                }, "LIMIT $limit OFFSET $offset", token).ConfigureAwait(false);
                return new ChatPage<ChatTurn> { Items = items, Total = Convert.ToInt32(total, CultureInfo.InvariantCulture), Page = page, PageSize = size };
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteTurnsAsync(ChatHistoryQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            string where = TurnWhere(query, out Action<SqliteCommand> bind);
            string ids = "SELECT t.id FROM chat_turns t LEFT JOIN chat_conversations c ON c.id = t.conversation_id LEFT JOIN chat_feedback f ON f.message_id = t.assistant_message_id " + where;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                await ExecuteAsync(connection, transaction, "DELETE FROM chat_tool_calls WHERE turn_id IN (" + ids + ") AND message_id NOT IN (SELECT id FROM chat_messages);", bind, token).ConfigureAwait(false);
                int removed = await ExecuteAsync(connection, transaction, "DELETE FROM chat_turns WHERE id IN (" + ids + ");", bind, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return removed;
            }
        }

        /// <inheritdoc />
        public async Task<int> PruneTurnsAsync(DateTime olderThanUtc, CancellationToken token)
        {
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                Action<SqliteCommand> bind = command => command.Parameters.AddWithValue("$cutoff", Format(olderThanUtc));
                await ExecuteAsync(connection, transaction, "DELETE FROM chat_tool_calls WHERE turn_id IN (SELECT id FROM chat_turns WHERE created_utc < $cutoff) AND message_id NOT IN (SELECT id FROM chat_messages);", bind, token).ConfigureAwait(false);
                int removed = await ExecuteAsync(connection, transaction, "DELETE FROM chat_turns WHERE created_utc < $cutoff;", bind, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return removed;
            }
        }

        /// <inheritdoc />
        public async Task<ChatFeedback> SetFeedbackAsync(string messageId, ChatFeedbackRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Rating != 1 && request.Rating != -1) throw new ArgumentException("Rating must be 1 (thumbs up) or -1 (thumbs down).");
            ChatMessage message = await GetMessageAsync(messageId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Message not found.");
            if (message.Role != ChatRoleEnum.Assistant) throw new ArgumentException("Only answers from the model can be rated.");
            string? comment = String.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (comment != null && comment.Length > 4000) comment = comment.Substring(0, 4000);
            DateTime now = DateTime.UtcNow;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                object? existingId = await ScalarAsync(connection, null, "SELECT id FROM chat_feedback WHERE message_id = $message;", command => command.Parameters.AddWithValue("$message", message.Id), token).ConfigureAwait(false);
                string id = existingId is string text && text.Length > 0 ? text : IdGenerator.NewId("fbk_");
                await ExecuteAsync(connection, null,
                    "INSERT INTO chat_feedback (id, conversation_id, message_id, turn_id, rating, comment, model, route, completion_tokens, total_ms, created_utc, updated_utc) "
                    + "VALUES ($id, $conversation, $message, $turn, $rating, $comment, $model, $route, $completion, $total, $now, $now) "
                    + "ON CONFLICT(message_id) DO UPDATE SET rating = excluded.rating, comment = excluded.comment, updated_utc = excluded.updated_utc;",
                    command =>
                    {
                        command.Parameters.AddWithValue("$id", id);
                        command.Parameters.AddWithValue("$conversation", message.ConversationId);
                        command.Parameters.AddWithValue("$message", message.Id);
                        command.Parameters.AddWithValue("$turn", Db(message.TurnId));
                        command.Parameters.AddWithValue("$rating", request.Rating);
                        command.Parameters.AddWithValue("$comment", Db(comment));
                        command.Parameters.AddWithValue("$model", Db(message.Model));
                        command.Parameters.AddWithValue("$route", Db(message.Route));
                        command.Parameters.AddWithValue("$completion", Db(message.CompletionTokens));
                        command.Parameters.AddWithValue("$total", Db(message.TotalMs));
                        command.Parameters.AddWithValue("$now", Format(now));
                    }, token).ConfigureAwait(false);
                List<ChatFeedback> saved = await ReadFeedbackAsync(connection, "WHERE f.message_id = $message", command => command.Parameters.AddWithValue("$message", message.Id), String.Empty, token).ConfigureAwait(false);
                return saved[0];
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteFeedbackAsync(string messageId, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(messageId)) return false;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                return await ExecuteAsync(connection, null, "DELETE FROM chat_feedback WHERE message_id = $message;", command => command.Parameters.AddWithValue("$message", messageId), token).ConfigureAwait(false) > 0;
            }
        }

        /// <inheritdoc />
        public async Task<ChatPage<ChatFeedback>> QueryFeedbackAsync(ChatFeedbackQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            int page = Math.Max(1, query.Page);
            int size = Math.Clamp(query.PageSize, 1, 500);
            List<string> where = new List<string>();
            List<Action<SqliteCommand>> binds = new List<Action<SqliteCommand>>();
            if (query.Rating.HasValue)
            {
                where.Add("f.rating = $rating");
                binds.Add(command => command.Parameters.AddWithValue("$rating", query.Rating.Value));
            }

            if (!String.IsNullOrWhiteSpace(query.Model))
            {
                where.Add("f.model = $model COLLATE NOCASE");
                binds.Add(command => command.Parameters.AddWithValue("$model", query.Model.Trim()));
            }

            AddRouteFilter("f.route", query.Route, where, binds);
            if (query.HasComment.HasValue) where.Add(query.HasComment.Value ? "COALESCE(f.comment, '') <> ''" : "COALESCE(f.comment, '') = ''");
            if (!String.IsNullOrWhiteSpace(query.Search))
            {
                where.Add("(f.comment LIKE $search ESCAPE '\\' OR c.title LIKE $search ESCAPE '\\')");
                binds.Add(command => command.Parameters.AddWithValue("$search", LikePattern(query.Search)));
            }

            AddRangeFilter("f.updated_utc", query.FromUtc, query.ToUtc, where, binds);
            string clause = where.Count == 0 ? String.Empty : "WHERE " + String.Join(" AND ", where);
            Action<SqliteCommand> bind = command => { foreach (Action<SqliteCommand> item in binds) item(command); };
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                object? total = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM chat_feedback f LEFT JOIN chat_conversations c ON c.id = f.conversation_id " + clause + ";", bind, token).ConfigureAwait(false);
                List<ChatFeedback> items = await ReadFeedbackAsync(connection, clause, command =>
                {
                    bind(command);
                    command.Parameters.AddWithValue("$limit", size);
                    command.Parameters.AddWithValue("$offset", (page - 1) * size);
                }, "LIMIT $limit OFFSET $offset", token).ConfigureAwait(false);
                return new ChatPage<ChatFeedback> { Items = items, Total = Convert.ToInt32(total, CultureInfo.InvariantCulture), Page = page, PageSize = size };
            }
        }

        /// <inheritdoc />
        public async Task<ChatAnalytics> AnalyticsAsync(DateTime fromUtc, DateTime toUtc, int bucketSeconds, CancellationToken token)
        {
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                List<ChatTurn> turns = await ReadTurnsAsync(connection, "WHERE t.created_utc >= $from AND t.created_utc < $to", command =>
                {
                    command.Parameters.AddWithValue("$from", Format(fromUtc));
                    command.Parameters.AddWithValue("$to", Format(toUtc));
                }, String.Empty, token).ConfigureAwait(false);
                return ChatAnalyticsBuilder.Build(turns, fromUtc, toUtc, bucketSeconds);
            }
        }

        #endregion

        #region Private-Methods

        private async Task<SqliteConnection> OpenAsync(CancellationToken token)
        {
            if (String.IsNullOrEmpty(_ConnectionString)) throw new InvalidOperationException("The chat store is not initialized.");
            SqliteConnection connection = new SqliteConnection(_ConnectionString);
            try
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static async Task<int> ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, Action<SqliteCommand>? bind, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                bind?.Invoke(command);
                return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task<object?> ScalarAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, Action<SqliteCommand>? bind, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                bind?.Invoke(command);
                object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return value == DBNull.Value ? null : value;
            }
        }

        private static async Task<List<ChatConversation>> ReadConversationsAsync(SqliteConnection connection, string where, Action<SqliteCommand> bind, int limit, CancellationToken token)
        {
            List<ChatConversation> list = new List<ChatConversation>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT c.*, (SELECT COUNT(*) FROM chat_messages m WHERE m.conversation_id = c.id) AS message_count FROM chat_conversations c "
                    + where + " ORDER BY c.pinned DESC, c.updated_utc DESC LIMIT $limit;";
                bind(command);
                command.Parameters.AddWithValue("$limit", limit);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        list.Add(new ChatConversation
                        {
                            Id = Text(reader, "id"),
                            Title = Text(reader, "title"),
                            Model = Text(reader, "model"),
                            SystemPrompt = NullableText(reader, "system_prompt"),
                            ToolsEnabled = Int(reader, "tools_enabled") == 1,
                            WorkingDirectory = NullableText(reader, "working_directory"),
                            Pinned = Int(reader, "pinned") == 1,
                            Surface = ParseEnum(Text(reader, "surface"), ChatSurfaceEnum.Web),
                            MessageCount = Int(reader, "message_count") ?? 0,
                            CreatedUtc = Date(reader, "created_utc"),
                            UpdatedUtc = Date(reader, "updated_utc")
                        });
                    }
                }
            }

            return list;
        }

        private static async Task<List<ChatMessage>> ReadMessagesAsync(SqliteConnection connection, string where, Action<SqliteCommand> bind, CancellationToken token)
        {
            List<ChatMessage> list = new List<ChatMessage>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT m.*, f.id AS f_id, f.rating AS f_rating, f.comment AS f_comment, f.created_utc AS f_created, f.updated_utc AS f_updated "
                    + "FROM chat_messages m LEFT JOIN chat_feedback f ON f.message_id = m.id " + where + " ORDER BY m.sequence ASC;";
                bind(command);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        ChatMessage message = new ChatMessage
                        {
                            Id = Text(reader, "id"),
                            ConversationId = Text(reader, "conversation_id"),
                            Sequence = Int(reader, "sequence") ?? 0,
                            Role = ParseEnum(Text(reader, "role"), ChatRoleEnum.User),
                            Content = Text(reader, "content"),
                            Thinking = NullableText(reader, "thinking"),
                            Model = NullableText(reader, "model"),
                            Route = NullableText(reader, "route"),
                            Fallback = Int(reader, "fallback") == 1,
                            PromptTokens = Int(reader, "prompt_tokens"),
                            CompletionTokens = Int(reader, "completion_tokens"),
                            TimeToFirstTokenMs = Double(reader, "ttft_ms"),
                            TotalMs = Double(reader, "total_ms"),
                            Incomplete = Int(reader, "incomplete") == 1,
                            TurnId = NullableText(reader, "turn_id"),
                            ToolCallId = NullableText(reader, "tool_call_id"),
                            CreatedUtc = Date(reader, "created_utc"),
                            Stats = ReadStats(NullableText(reader, "stats_json")),
                            Failure = ReadFailure(NullableText(reader, "failure_json"))
                        };
                        string? feedbackId = NullableText(reader, "f_id");
                        if (feedbackId != null)
                        {
                            message.Feedback = new ChatFeedback
                            {
                                Id = feedbackId,
                                ConversationId = message.ConversationId,
                                MessageId = message.Id,
                                TurnId = message.TurnId,
                                Rating = Int(reader, "f_rating") ?? 0,
                                Comment = NullableText(reader, "f_comment"),
                                Model = message.Model,
                                Route = message.Route,
                                CreatedUtc = Date(reader, "f_created"),
                                UpdatedUtc = Date(reader, "f_updated")
                            };
                        }

                        list.Add(message);
                    }
                }
            }

            return list;
        }

        private static async Task<List<ChatToolCallRecord>> ReadToolCallsAsync(SqliteConnection connection, string where, Action<SqliteCommand> bind, CancellationToken token)
        {
            List<ChatToolCallRecord> list = new List<ChatToolCallRecord>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM chat_tool_calls " + where + " ORDER BY created_utc ASC;";
                bind(command);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        list.Add(new ChatToolCallRecord
                        {
                            Id = Text(reader, "id"),
                            CallId = Text(reader, "call_id"),
                            TurnId = Text(reader, "turn_id"),
                            MessageId = Text(reader, "message_id"),
                            ToolName = Text(reader, "tool_name"),
                            Arguments = Text(reader, "arguments"),
                            Result = NullableText(reader, "result"),
                            Mutating = Int(reader, "mutating") == 1,
                            Approval = ParseEnum(Text(reader, "approval"), ChatToolApprovalEnum.Auto),
                            Success = Int(reader, "success") == 1,
                            Error = NullableText(reader, "error"),
                            DurationMs = Double(reader, "duration_ms") ?? 0,
                            ApprovalMs = Double(reader, "approval_ms") ?? 0,
                            CreatedUtc = Date(reader, "created_utc")
                        });
                    }
                }
            }

            return list;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<Xeno.Core.Analytics.UsageRecord> EnumerateUsageAsync(DateTime fromUtc, DateTime toUtc, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            List<ChatTurn> turns;
            using (SqliteConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                turns = await ReadTurnsAsync(connection, "WHERE t.created_utc >= $from AND t.created_utc < $to AND t.status <> 'Running'", command =>
                {
                    command.Parameters.AddWithValue("$from", Format(fromUtc));
                    command.Parameters.AddWithValue("$to", Format(toUtc));
                }, String.Empty, token).ConfigureAwait(false);
            }

            foreach (ChatTurn turn in turns)
            {
                token.ThrowIfCancellationRequested();
                yield return Xeno.Core.Analytics.UsageRecordMapper.FromChatTurn(turn);
            }
        }

        private static async Task<List<ChatTurn>> ReadTurnsAsync(SqliteConnection connection, string where, Action<SqliteCommand> bind, string limit, CancellationToken token)
        {
            List<ChatTurn> list = new List<ChatTurn>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT t.*, c.title AS conversation_title, f.rating AS feedback_rating FROM chat_turns t "
                    + "LEFT JOIN chat_conversations c ON c.id = t.conversation_id LEFT JOIN chat_feedback f ON f.message_id = t.assistant_message_id "
                    + where + " ORDER BY t.created_utc DESC " + limit + ";";
                bind(command);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string? completed = NullableText(reader, "completed_utc");
                        list.Add(new ChatTurn
                        {
                            Id = Text(reader, "id"),
                            ConversationId = Text(reader, "conversation_id"),
                            ConversationTitle = NullableText(reader, "conversation_title"),
                            UserMessageId = NullableText(reader, "user_message_id"),
                            AssistantMessageId = NullableText(reader, "assistant_message_id"),
                            Model = Text(reader, "model"),
                            Route = Text(reader, "route"),
                            Provider = Text(reader, "provider"),
                            Fallback = Int(reader, "fallback") == 1,
                            Status = ParseEnum(Text(reader, "status"), ChatTurnStatusEnum.Error),
                            Error = NullableText(reader, "error"),
                            StatusCode = Int(reader, "status_code"),
                            PromptTokens = Int(reader, "prompt_tokens"),
                            CompletionTokens = Int(reader, "completion_tokens"),
                            TimeToFirstTokenMs = Double(reader, "ttft_ms"),
                            TotalMs = Double(reader, "total_ms") ?? 0,
                            ModelCalls = Int(reader, "model_calls") ?? 0,
                            ToolCallCount = Int(reader, "tool_call_count") ?? 0,
                            ToolMs = Double(reader, "tool_ms") ?? 0,
                            ModelMs = Double(reader, "model_ms") ?? 0,
                            StreamingMs = Double(reader, "streaming_ms"),
                            ApprovalMs = Double(reader, "approval_ms") ?? 0,
                            ToolFailures = Int(reader, "tool_failures") ?? 0,
                            ToolDenied = Int(reader, "tool_denied") ?? 0,
                            CachedPromptTokens = Int(reader, "cached_prompt_tokens"),
                            Surface = ParseEnum(Text(reader, "surface"), ChatSurfaceEnum.Web),
                            FeedbackRating = Int(reader, "feedback_rating"),
                            CreatedUtc = Date(reader, "created_utc"),
                            CompletedUtc = completed == null ? null : Date(reader, "completed_utc")
                        });
                    }
                }
            }

            return list;
        }

        private static async Task<List<ChatFeedback>> ReadFeedbackAsync(SqliteConnection connection, string where, Action<SqliteCommand> bind, string limit, CancellationToken token)
        {
            List<ChatFeedback> list = new List<ChatFeedback>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT f.*, c.title AS conversation_title, substr(m.content, 1, 240) AS excerpt FROM chat_feedback f "
                    + "LEFT JOIN chat_conversations c ON c.id = f.conversation_id LEFT JOIN chat_messages m ON m.id = f.message_id "
                    + where + " ORDER BY f.updated_utc DESC " + limit + ";";
                bind(command);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        list.Add(new ChatFeedback
                        {
                            Id = Text(reader, "id"),
                            ConversationId = Text(reader, "conversation_id"),
                            MessageId = Text(reader, "message_id"),
                            TurnId = NullableText(reader, "turn_id"),
                            Rating = Int(reader, "rating") ?? 0,
                            Comment = NullableText(reader, "comment"),
                            Model = NullableText(reader, "model"),
                            Route = NullableText(reader, "route"),
                            ConversationTitle = NullableText(reader, "conversation_title"),
                            Excerpt = NullableText(reader, "excerpt"),
                            CompletionTokens = Int(reader, "completion_tokens"),
                            TotalMs = Double(reader, "total_ms"),
                            CreatedUtc = Date(reader, "created_utc"),
                            UpdatedUtc = Date(reader, "updated_utc")
                        });
                    }
                }
            }

            return list;
        }

        private static string TurnWhere(ChatHistoryQuery query, out Action<SqliteCommand> bind)
        {
            List<string> where = new List<string>();
            List<Action<SqliteCommand>> binds = new List<Action<SqliteCommand>>();
            if (!String.IsNullOrWhiteSpace(query.Model))
            {
                where.Add("t.model = $model COLLATE NOCASE");
                binds.Add(command => command.Parameters.AddWithValue("$model", query.Model.Trim()));
            }

            AddRouteFilter("t.route", query.Route, where, binds);
            if (!String.IsNullOrWhiteSpace(query.Provider))
            {
                where.Add("t.provider = $provider COLLATE NOCASE");
                binds.Add(command => command.Parameters.AddWithValue("$provider", query.Provider.Trim()));
            }

            if (query.Status.HasValue)
            {
                where.Add("t.status = $status");
                binds.Add(command => command.Parameters.AddWithValue("$status", query.Status.Value.ToString()));
            }

            if (query.Surface.HasValue)
            {
                where.Add("t.surface = $surface");
                binds.Add(command => command.Parameters.AddWithValue("$surface", query.Surface.Value.ToString()));
            }

            if (query.Rating.HasValue)
            {
                if (query.Rating.Value == 0)
                {
                    where.Add("f.rating IS NULL");
                }
                else
                {
                    where.Add("f.rating = $rating");
                    binds.Add(command => command.Parameters.AddWithValue("$rating", query.Rating.Value));
                }
            }

            if (query.Fallback.HasValue) where.Add(query.Fallback.Value ? "t.fallback = 1" : "t.fallback = 0");
            if (!String.IsNullOrWhiteSpace(query.ConversationId))
            {
                where.Add("t.conversation_id = $conversation");
                binds.Add(command => command.Parameters.AddWithValue("$conversation", query.ConversationId.Trim()));
            }

            if (!String.IsNullOrWhiteSpace(query.Search))
            {
                where.Add("(c.title LIKE $search ESCAPE '\\' OR t.model LIKE $search ESCAPE '\\' OR t.error LIKE $search ESCAPE '\\')");
                binds.Add(command => command.Parameters.AddWithValue("$search", LikePattern(query.Search)));
            }

            AddRangeFilter("t.created_utc", query.FromUtc, query.ToUtc, where, binds);
            bind = command => { foreach (Action<SqliteCommand> item in binds) item(command); };
            return where.Count == 0 ? String.Empty : "WHERE " + String.Join(" AND ", where);
        }

        private static void AddRouteFilter(string column, string? route, List<string> where, List<Action<SqliteCommand>> binds)
        {
            if (String.IsNullOrWhiteSpace(route)) return;
            string value = route.Trim();
            if (String.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                where.Add("(" + column + " = 'local' OR " + column + " LIKE 'local:%')");
                return;
            }

            where.Add(column + " = $route COLLATE NOCASE");
            binds.Add(command => command.Parameters.AddWithValue("$route", value));
        }

        private static void AddRangeFilter(string column, DateTime? fromUtc, DateTime? toUtc, List<string> where, List<Action<SqliteCommand>> binds)
        {
            if (fromUtc.HasValue)
            {
                where.Add(column + " >= $fromUtc");
                binds.Add(command => command.Parameters.AddWithValue("$fromUtc", Format(fromUtc.Value)));
            }

            if (toUtc.HasValue)
            {
                where.Add(column + " < $toUtc");
                binds.Add(command => command.Parameters.AddWithValue("$toUtc", Format(toUtc.Value)));
            }
        }

        private static void AddConversationValues(SqliteCommand command, ChatConversation conversation)
        {
            command.Parameters.AddWithValue("$title", String.IsNullOrWhiteSpace(conversation.Title) ? "New chat" : conversation.Title.Trim());
            command.Parameters.AddWithValue("$model", conversation.Model ?? String.Empty);
            command.Parameters.AddWithValue("$system", Db(String.IsNullOrWhiteSpace(conversation.SystemPrompt) ? null : conversation.SystemPrompt));
            command.Parameters.AddWithValue("$tools", conversation.ToolsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$pinned", conversation.Pinned ? 1 : 0);
            command.Parameters.AddWithValue("$directory", Db(String.IsNullOrWhiteSpace(conversation.WorkingDirectory) ? null : conversation.WorkingDirectory));
            command.Parameters.AddWithValue("$updated", Format(conversation.UpdatedUtc));
        }

        private static void AddMessageValues(SqliteCommand command, ChatMessage message)
        {
            command.Parameters.AddWithValue("$content", message.Content ?? String.Empty);
            command.Parameters.AddWithValue("$thinking", Db(message.Thinking));
            command.Parameters.AddWithValue("$model", Db(message.Model));
            command.Parameters.AddWithValue("$route", Db(message.Route));
            command.Parameters.AddWithValue("$fallback", message.Fallback ? 1 : 0);
            command.Parameters.AddWithValue("$prompt", Db(message.PromptTokens));
            command.Parameters.AddWithValue("$completion", Db(message.CompletionTokens));
            command.Parameters.AddWithValue("$ttft", Db(message.TimeToFirstTokenMs));
            command.Parameters.AddWithValue("$total", Db(message.TotalMs));
            command.Parameters.AddWithValue("$incomplete", message.Incomplete ? 1 : 0);
            command.Parameters.AddWithValue("$stats", message.Stats == null ? DBNull.Value : Xeno.Core.Serialization.JsonUtil.Serialize(message.Stats));
            command.Parameters.AddWithValue("$failure", message.Failure == null ? DBNull.Value : Xeno.Core.Serialization.JsonUtil.Serialize(message.Failure));
        }

        private static ChatFailure? ReadFailure(string? json)
        {
            if (String.IsNullOrWhiteSpace(json)) return null;
            return Xeno.Core.Serialization.JsonUtil.TryDeserialize<ChatFailure>(json, out ChatFailure? failure) ? failure : null;
        }

        private static ChatMessageStats? ReadStats(string? json)
        {
            if (String.IsNullOrWhiteSpace(json)) return null;
            return Xeno.Core.Serialization.JsonUtil.TryDeserialize<ChatMessageStats>(json, out ChatMessageStats? stats) ? stats : null;
        }

        private static string LikePattern(string value)
        {
            StringBuilder escaped = new StringBuilder();
            foreach (char c in value.Trim())
            {
                if (c == '%' || c == '_' || c == '\\') escaped.Append('\\');
                escaped.Append(c);
            }

            return "%" + escaped + "%";
        }

        private static string? Truncate(string? value, int max)
        {
            if (value == null || value.Length <= max) return value;
            return value.Substring(0, max) + "...(truncated)";
        }

        private static object Db(string? value)
        {
            return value == null ? DBNull.Value : value;
        }

        private static object Db(int? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static object Db(double? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static string Format(DateTime value)
        {
            return DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
        }

        private static string Text(IDataRecord reader, string name)
        {
            return NullableText(reader, name) ?? String.Empty;
        }

        private static string? NullableText(IDataRecord reader, string name)
        {
            object value = reader.GetValue(reader.GetOrdinal(name));
            return value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int? Int(IDataRecord reader, string name)
        {
            object value = reader.GetValue(reader.GetOrdinal(name));
            return value == DBNull.Value ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static double? Double(IDataRecord reader, string name)
        {
            object value = reader.GetValue(reader.GetOrdinal(name));
            return value == DBNull.Value ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static DateTime Date(IDataRecord reader, string name)
        {
            string? value = NullableText(reader, name);
            if (value != null && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return DateTime.MinValue;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
        {
            return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
        }

        private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS chat_conversations (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    model TEXT NOT NULL,
    system_prompt TEXT NULL,
    tools_enabled INTEGER NOT NULL DEFAULT 0,
    pinned INTEGER NOT NULL DEFAULT 0,
    surface TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    working_directory TEXT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_conversations_updated ON chat_conversations(updated_utc);
CREATE TABLE IF NOT EXISTS chat_messages (
    id TEXT PRIMARY KEY,
    conversation_id TEXT NOT NULL,
    sequence INTEGER NOT NULL,
    role TEXT NOT NULL,
    content TEXT NOT NULL,
    thinking TEXT NULL,
    model TEXT NULL,
    route TEXT NULL,
    fallback INTEGER NOT NULL DEFAULT 0,
    prompt_tokens INTEGER NULL,
    completion_tokens INTEGER NULL,
    ttft_ms REAL NULL,
    total_ms REAL NULL,
    incomplete INTEGER NOT NULL DEFAULT 0,
    turn_id TEXT NULL,
    tool_call_id TEXT NULL,
    created_utc TEXT NOT NULL,
    stats_json TEXT NULL,
    failure_json TEXT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_messages_conversation ON chat_messages(conversation_id, sequence);
CREATE TABLE IF NOT EXISTS chat_tool_calls (
    id TEXT PRIMARY KEY,
    call_id TEXT NOT NULL,
    turn_id TEXT NOT NULL,
    message_id TEXT NOT NULL,
    tool_name TEXT NOT NULL,
    arguments TEXT NOT NULL,
    result TEXT NULL,
    mutating INTEGER NOT NULL DEFAULT 0,
    approval TEXT NOT NULL,
    success INTEGER NOT NULL DEFAULT 0,
    error TEXT NULL,
    duration_ms REAL NOT NULL DEFAULT 0,
    approval_ms REAL NULL,
    created_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_tool_calls_turn ON chat_tool_calls(turn_id);
CREATE INDEX IF NOT EXISTS idx_chat_tool_calls_message ON chat_tool_calls(message_id);
CREATE TABLE IF NOT EXISTS chat_feedback (
    id TEXT PRIMARY KEY,
    conversation_id TEXT NOT NULL,
    message_id TEXT NOT NULL UNIQUE,
    turn_id TEXT NULL,
    rating INTEGER NOT NULL,
    comment TEXT NULL,
    model TEXT NULL,
    route TEXT NULL,
    completion_tokens INTEGER NULL,
    total_ms REAL NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_feedback_updated ON chat_feedback(updated_utc);
CREATE TABLE IF NOT EXISTS chat_turns (
    id TEXT PRIMARY KEY,
    conversation_id TEXT NOT NULL,
    user_message_id TEXT NULL,
    assistant_message_id TEXT NULL,
    model TEXT NOT NULL,
    route TEXT NOT NULL,
    provider TEXT NOT NULL,
    fallback INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL,
    error TEXT NULL,
    status_code INTEGER NULL,
    prompt_tokens INTEGER NULL,
    completion_tokens INTEGER NULL,
    ttft_ms REAL NULL,
    total_ms REAL NOT NULL DEFAULT 0,
    model_calls INTEGER NOT NULL DEFAULT 0,
    tool_call_count INTEGER NOT NULL DEFAULT 0,
    tool_ms REAL NOT NULL DEFAULT 0,
    surface TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    completed_utc TEXT NULL,
    model_ms REAL NULL,
    streaming_ms REAL NULL,
    approval_ms REAL NULL,
    tool_failures INTEGER NULL,
    tool_denied INTEGER NULL,
    cached_prompt_tokens INTEGER NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_turns_created ON chat_turns(created_utc);
CREATE INDEX IF NOT EXISTS idx_chat_turns_conversation ON chat_turns(conversation_id);
";

        #endregion
    }
}
