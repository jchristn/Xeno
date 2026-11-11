namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Server chat storage shared by every database driver. Rows keep the columns queries filter on plus the whole
    /// object as JSON, like the server's other tables; a small dialect covers types, paging, and table creation.
    /// Thread safe: every call opens its own connection.
    /// </summary>
    public sealed class DbChatMethods : IChatMethods
    {
        #region Private-Members

        private readonly ChatSqlDialectEnum _Dialect;
        private readonly Func<DbConnection> _Connect;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the store.
        /// </summary>
        /// <param name="dialect">SQL dialect.</param>
        /// <param name="connect">Creates an unopened connection.</param>
        /// <exception cref="ArgumentNullException">connect is null.</exception>
        public DbChatMethods(ChatSqlDialectEnum dialect, Func<DbConnection> connect)
        {
            _Dialect = dialect;
            _Connect = connect ?? throw new ArgumentNullException(nameof(connect));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the chat tables when missing.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task InitializeAsync(CancellationToken token)
        {
            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                foreach (string statement in SchemaStatements())
                {
                    try
                    {
                        using (DbCommand command = connection.CreateCommand())
                        {
                            command.CommandText = statement;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }
                    catch (DbException ex) when (_Dialect == ChatSqlDialectEnum.Mysql && ex.Message.Contains("Duplicate key name", StringComparison.OrdinalIgnoreCase))
                    {
                    }
                }

                await EnsureTurnUsageColumnsAsync(connection, token).ConfigureAwait(false);
                while (await BackfillTurnUsageAsync(connection, 2000, token).ConfigureAwait(false) > 0)
                {
                }
            }
        }

        /// <inheritdoc />
        public IChatStore For(string tenantId, string? userId)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("A tenant is required.", nameof(tenantId));
            return new Scoped(this, tenantId, String.IsNullOrWhiteSpace(userId) ? null : userId);
        }

        /// <inheritdoc />
        public async Task DeleteByTenantAsync(string tenantId, CancellationToken token)
        {
            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                foreach (string table in new[] { "chat_feedback", "chat_messages", "chat_turns", "chat_conversations" })
                {
                    await ExecuteAsync(connection, null, "DELETE FROM " + table + " WHERE tenant_id = @tenant", token, P("@tenant", tenantId)).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private async Task<DbConnection> OpenAsync(CancellationToken token)
        {
            DbConnection connection = _Connect();
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

        private async Task EnsureTurnUsageColumnsAsync(DbConnection connection, CancellationToken token)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string sql = _Dialect switch
            {
                ChatSqlDialectEnum.Sqlite => "SELECT name FROM pragma_table_info('chat_turns')",
                ChatSqlDialectEnum.Postgresql => "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'chat_turns'",
                ChatSqlDialectEnum.Mysql => "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'chat_turns'",
                _ => "SELECT column_name FROM information_schema.columns WHERE table_schema = 'dbo' AND table_name = 'chat_turns'"
            };
            foreach (object?[] row in await RowsAsync(connection, sql, token, Array.Empty<KeyValuePair<string, object?>>()).ConfigureAwait(false))
            {
                existing.Add(Convert.ToString(row[0], CultureInfo.InvariantCulture) ?? String.Empty);
            }

            string real = _Dialect switch { ChatSqlDialectEnum.Sqlite => "REAL", ChatSqlDialectEnum.Postgresql => "DOUBLE PRECISION", ChatSqlDialectEnum.Mysql => "DOUBLE", _ => "FLOAT" };
            string integer = _Dialect == ChatSqlDialectEnum.Sqlite ? "INTEGER" : "INT";
            string add = _Dialect == ChatSqlDialectEnum.SqlServer ? "ALTER TABLE dbo.chat_turns ADD " : "ALTER TABLE chat_turns ADD COLUMN ";
            foreach (string column in ChatTurnUsageColumns.All)
            {
                if (existing.Contains(column)) continue;
                string type = ChatTurnUsageColumns.RealColumns.Contains(column) ? real : integer;
                await ExecuteAsync(connection, null, add + column + " " + type + " NULL", token).ConfigureAwait(false);
            }
        }

        private async Task<int> BackfillTurnUsageAsync(DbConnection connection, int batchSize, CancellationToken token)
        {
            string select = _Dialect == ChatSqlDialectEnum.SqlServer
                ? "SELECT TOP (" + batchSize.ToString(CultureInfo.InvariantCulture) + ") id, turn_json FROM chat_turns WHERE usage_version IS NULL"
                : "SELECT id, turn_json FROM chat_turns WHERE usage_version IS NULL LIMIT " + batchSize.ToString(CultureInfo.InvariantCulture);
            List<object?[]> rows = await RowsAsync(connection, select, token, Array.Empty<KeyValuePair<string, object?>>()).ConfigureAwait(false);
            foreach (object?[] row in rows)
            {
                string json = Convert.ToString(row[1], CultureInfo.InvariantCulture) ?? String.Empty;
                ChatTurn turn = JsonUtil.TryDeserialize<ChatTurn>(json, out ChatTurn? parsed) && parsed != null ? parsed : new ChatTurn();
                List<KeyValuePair<string, object?>> values = ChatTurnUsageColumns.Values(turn);
                values.Add(P("@id", row[0]));
                await ExecuteAsync(connection, null, "UPDATE chat_turns SET " + ChatTurnUsageColumns.SetList + " WHERE id = @id", token, values.ToArray()).ConfigureAwait(false);
            }

            return rows.Count;
        }

        private static KeyValuePair<string, object?> P(string name, object? value)
        {
            return new KeyValuePair<string, object?>(name, value);
        }

        private static void Bind(DbCommand command, IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            foreach (KeyValuePair<string, object?> pair in parameters)
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = pair.Key;
                parameter.Value = pair.Value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        private static async Task<int> ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken token, params KeyValuePair<string, object?>[] parameters)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                Bind(command, parameters);
                return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task<object?> ScalarAsync(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken token, IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                Bind(command, parameters);
                object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return value == DBNull.Value ? null : value;
            }
        }

        private static Task<List<object?[]>> RowsAsync(DbConnection connection, string sql, CancellationToken token, IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            return RowsAsync(connection, null, sql, token, parameters);
        }

        private static async Task<List<object?[]>> RowsAsync(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken token, IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            List<object?[]> rows = new List<object?[]>();
            using (DbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                Bind(command, parameters);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        object?[] row = new object?[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        private string Page(string orderedSql)
        {
            return _Dialect == ChatSqlDialectEnum.SqlServer
                ? orderedSql + " OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY"
                : orderedSql + " LIMIT @limit OFFSET @offset";
        }

        private string Top(string orderedSql, int limit)
        {
            return _Dialect == ChatSqlDialectEnum.SqlServer
                ? orderedSql + " OFFSET 0 ROWS FETCH NEXT " + limit.ToString(CultureInfo.InvariantCulture) + " ROWS ONLY"
                : orderedSql + " LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        }

        private static string Format(DateTime value)
        {
            return DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseDate(object? value)
        {
            string? text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text != null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return DateTime.MinValue;
        }

        private static string Like(string value)
        {
            StringBuilder builder = new StringBuilder("%");
            foreach (char c in value.Trim().ToLowerInvariant())
            {
                if (c == '%' || c == '_' || c == '!' || c == '[') builder.Append('!');
                builder.Append(c);
            }

            return builder.Append('%').ToString();
        }

        private static T Read<T>(object? json) where T : class, new()
        {
            string text = json as string ?? String.Empty;
            if (text.Length == 0) return new T();
            try
            {
                return JsonUtil.Deserialize<T>(text) ?? new T();
            }
            catch (InvalidDataException)
            {
                return new T();
            }
        }

        private IEnumerable<string> SchemaStatements()
        {
            string id, key, text, json, integer;
            switch (_Dialect)
            {
                case ChatSqlDialectEnum.Mysql:
                    id = "VARCHAR(64)"; key = "VARCHAR(255)"; text = "TEXT"; json = "LONGTEXT"; integer = "INT";
                    break;
                case ChatSqlDialectEnum.SqlServer:
                    id = "NVARCHAR(64)"; key = "NVARCHAR(255)"; text = "NVARCHAR(MAX)"; json = "NVARCHAR(MAX)"; integer = "INT";
                    break;
                default:
                    id = "TEXT"; key = "TEXT"; text = "TEXT"; json = "TEXT"; integer = "INTEGER";
                    break;
            }

            Dictionary<string, string> tables = new Dictionary<string, string>
            {
                ["chat_conversations"] = "id " + id + " NOT NULL PRIMARY KEY, tenant_id " + id + " NOT NULL, user_id " + id + " NOT NULL, title " + key + " NOT NULL, model " + key + " NOT NULL, pinned " + integer + " NOT NULL, surface " + id + " NOT NULL, created_utc " + id + " NOT NULL, updated_utc " + id + " NOT NULL, conversation_json " + json + " NOT NULL",
                ["chat_messages"] = "id " + id + " NOT NULL PRIMARY KEY, tenant_id " + id + " NOT NULL, user_id " + id + " NOT NULL, conversation_id " + id + " NOT NULL, sequence " + integer + " NOT NULL, role " + id + " NOT NULL, created_utc " + id + " NOT NULL, message_json " + json + " NOT NULL",
                ["chat_feedback"] = "id " + id + " NOT NULL PRIMARY KEY, tenant_id " + id + " NOT NULL, user_id " + id + " NOT NULL, conversation_id " + id + " NOT NULL, message_id " + id + " NOT NULL, rating " + integer + " NOT NULL, comment_text " + text + " NULL, has_comment " + integer + " NOT NULL, model " + key + " NULL, route " + key + " NULL, updated_utc " + id + " NOT NULL, feedback_json " + json + " NOT NULL",
                ["chat_turns"] = "id " + id + " NOT NULL PRIMARY KEY, tenant_id " + id + " NOT NULL, user_id " + id + " NOT NULL, conversation_id " + id + " NOT NULL, model " + key + " NOT NULL, route " + key + " NOT NULL, provider " + key + " NOT NULL, status " + id + " NOT NULL, surface " + id + " NOT NULL, fallback " + integer + " NOT NULL, feedback_rating " + integer + " NULL, created_utc " + id + " NOT NULL, turn_json " + json + " NOT NULL"
            };
            Dictionary<string, string> indexes = new Dictionary<string, string>
            {
                ["idx_chat_conversations_owner"] = "chat_conversations (tenant_id, user_id, updated_utc)",
                ["idx_chat_messages_conversation"] = "chat_messages (conversation_id, sequence)",
                ["idx_chat_feedback_owner"] = "chat_feedback (tenant_id, user_id, updated_utc)",
                ["idx_chat_feedback_message"] = "chat_feedback (message_id)",
                ["idx_chat_turns_owner"] = "chat_turns (tenant_id, user_id, created_utc)"
            };

            foreach (KeyValuePair<string, string> table in tables)
            {
                if (_Dialect == ChatSqlDialectEnum.SqlServer) yield return "IF OBJECT_ID(N'dbo." + table.Key + "', N'U') IS NULL CREATE TABLE dbo." + table.Key + " (" + table.Value + ")";
                else yield return "CREATE TABLE IF NOT EXISTS " + table.Key + " (" + table.Value + ")";
            }

            foreach (KeyValuePair<string, string> index in indexes)
            {
                string tableName = index.Value.Substring(0, index.Value.IndexOf(' '));
                switch (_Dialect)
                {
                    case ChatSqlDialectEnum.SqlServer:
                        yield return "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'" + index.Key + "' AND object_id = OBJECT_ID(N'dbo." + tableName + "')) CREATE INDEX " + index.Key + " ON dbo." + index.Value;
                        break;
                    case ChatSqlDialectEnum.Mysql:
                        // MySQL has no CREATE INDEX IF NOT EXISTS; InitializeAsync ignores the duplicate-name error.
                        yield return "CREATE INDEX " + index.Key + " ON " + index.Value;
                        break;
                    default:
                        yield return "CREATE INDEX IF NOT EXISTS " + index.Key + " ON " + index.Value;
                        break;
                }
            }
        }

        #endregion

        #region Private-Classes

        /// <summary>
        /// A chat store scoped to one user, or to a whole tenant.
        /// </summary>
        private sealed class Scoped : IChatStore
        {
            private readonly DbChatMethods _Owner;
            private readonly string _TenantId;
            private readonly string? _UserId;

            internal Scoped(DbChatMethods owner, string tenantId, string? userId)
            {
                _Owner = owner;
                _TenantId = tenantId;
                _UserId = userId;
            }

            public Task InitializeAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            public async Task<ChatConversation> CreateConversationAsync(ChatConversation conversation, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(conversation);
                string user = RequireUser();
                if (String.IsNullOrWhiteSpace(conversation.Id)) conversation.Id = IdGenerator.NewId("chconv_");
                DateTime now = DateTime.UtcNow;
                conversation.CreatedUtc = now;
                conversation.UpdatedUtc = now;
                if (String.IsNullOrWhiteSpace(conversation.Title)) conversation.Title = "New chat";
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    await ExecuteAsync(connection, null,
                        "INSERT INTO chat_conversations (id, tenant_id, user_id, title, model, pinned, surface, created_utc, updated_utc, conversation_json) VALUES (@id, @tenant, @user, @title, @model, @pinned, @surface, @created, @updated, @json)",
                        token, P("@id", conversation.Id), P("@tenant", _TenantId), P("@user", user), P("@title", Clip(conversation.Title, 255)), P("@model", Clip(conversation.Model ?? String.Empty, 255)),
                        P("@pinned", conversation.Pinned ? 1 : 0), P("@surface", conversation.Surface.ToString()), P("@created", Format(now)), P("@updated", Format(now)), P("@json", ConversationJson(conversation))).ConfigureAwait(false);
                }

                return conversation;
            }

            public async Task<ChatConversation?> GetConversationAsync(string id, bool includeMessages, CancellationToken token)
            {
                if (String.IsNullOrWhiteSpace(id)) return null;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>> { P("@id", id) });
                    List<object?[]> rows = await RowsAsync(connection, "SELECT conversation_json, updated_utc, (SELECT COUNT(*) FROM chat_messages m WHERE m.conversation_id = c.id) FROM chat_conversations c WHERE c.id = @id AND " + OwnerClause("c"), token, parameters).ConfigureAwait(false);
                    if (rows.Count == 0) return null;
                    ChatConversation conversation = ReadConversation(rows[0]);
                    if (!includeMessages) return conversation;
                    conversation.Messages = await MessagesAsync(connection, "m.conversation_id = @id", parameters, token).ConfigureAwait(false);
                    return conversation;
                }
            }

            public async Task<List<ChatConversation>> ListConversationsAsync(string? search, ChatSurfaceEnum? surface, int limit, CancellationToken token)
            {
                List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>>());
                string where = OwnerClause("c");
                if (!String.IsNullOrWhiteSpace(search))
                {
                    where += " AND (LOWER(c.title) LIKE @search ESCAPE '!' OR LOWER(c.model) LIKE @search ESCAPE '!')";
                    parameters.Add(P("@search", Like(search)));
                }

                if (surface.HasValue)
                {
                    where += " AND c.surface = @surface";
                    parameters.Add(P("@surface", surface.Value.ToString()));
                }

                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    string sql = _Owner.Top("SELECT c.conversation_json, c.updated_utc, (SELECT COUNT(*) FROM chat_messages m WHERE m.conversation_id = c.id) FROM chat_conversations c WHERE " + where + " ORDER BY c.pinned DESC, c.updated_utc DESC", Math.Clamp(limit, 1, 1000));
                    List<object?[]> rows = await RowsAsync(connection, sql, token, parameters).ConfigureAwait(false);
                    return rows.Select(ReadConversation).ToList();
                }
            }

            public async Task<bool> UpdateConversationAsync(ChatConversation conversation, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(conversation);
                conversation.UpdatedUtc = DateTime.UtcNow;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>>
                    {
                        P("@id", conversation.Id), P("@title", Clip(String.IsNullOrWhiteSpace(conversation.Title) ? "New chat" : conversation.Title, 255)), P("@model", Clip(conversation.Model ?? String.Empty, 255)),
                        P("@pinned", conversation.Pinned ? 1 : 0), P("@updated", Format(conversation.UpdatedUtc)), P("@json", ConversationJson(conversation))
                    });
                    int changed = await ExecuteAsync(connection, null, "UPDATE chat_conversations SET title = @title, model = @model, pinned = @pinned, updated_utc = @updated, conversation_json = @json WHERE id = @id AND " + OwnerClause(null), token, parameters.ToArray()).ConfigureAwait(false);
                    return changed > 0;
                }
            }

            public async Task<bool> DeleteConversationAsync(string id, CancellationToken token)
            {
                if (String.IsNullOrWhiteSpace(id)) return false;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    KeyValuePair<string, object?>[] parameters = Owner(new List<KeyValuePair<string, object?>> { P("@id", id) }).ToArray();
                    int removed = await ExecuteAsync(connection, transaction, "DELETE FROM chat_conversations WHERE id = @id AND " + OwnerClause(null), token, parameters).ConfigureAwait(false);
                    if (removed > 0)
                    {
                        await ExecuteAsync(connection, transaction, "DELETE FROM chat_feedback WHERE conversation_id = @id AND " + OwnerClause(null), token, parameters).ConfigureAwait(false);
                        await ExecuteAsync(connection, transaction, "DELETE FROM chat_messages WHERE conversation_id = @id AND " + OwnerClause(null), token, parameters).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return removed > 0;
                }
            }

            public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(message);
                string user = RequireUser();
                if (String.IsNullOrWhiteSpace(message.Id)) message.Id = IdGenerator.NewId("chmsg_");
                message.CreatedUtc = DateTime.UtcNow;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    object? next = await ScalarAsync(connection, transaction, "SELECT COALESCE(MAX(sequence), 0) + 1 FROM chat_messages WHERE conversation_id = @conversation", token, new[] { P("@conversation", message.ConversationId) }).ConfigureAwait(false);
                    message.Sequence = Convert.ToInt32(next ?? 1, CultureInfo.InvariantCulture);
                    await ExecuteAsync(connection, transaction,
                        "INSERT INTO chat_messages (id, tenant_id, user_id, conversation_id, sequence, role, created_utc, message_json) VALUES (@id, @tenant, @user, @conversation, @sequence, @role, @created, @json)",
                        token, P("@id", message.Id), P("@tenant", _TenantId), P("@user", user), P("@conversation", message.ConversationId), P("@sequence", message.Sequence), P("@role", message.Role.ToString()), P("@created", Format(message.CreatedUtc)), P("@json", MessageJson(message))).ConfigureAwait(false);
                    await ExecuteAsync(connection, transaction, "UPDATE chat_conversations SET updated_utc = @updated WHERE id = @conversation AND tenant_id = @tenant", token, P("@updated", Format(DateTime.UtcNow)), P("@conversation", message.ConversationId), P("@tenant", _TenantId)).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }

                return message;
            }

            public async Task UpdateMessageAsync(ChatMessage message, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(message);
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    await ExecuteAsync(connection, null, "UPDATE chat_messages SET message_json = @json WHERE id = @id AND tenant_id = @tenant", token, P("@json", MessageJson(message)), P("@id", message.Id), P("@tenant", _TenantId)).ConfigureAwait(false);
                }
            }

            public async Task<ChatMessage?> GetMessageAsync(string id, CancellationToken token)
            {
                if (String.IsNullOrWhiteSpace(id)) return null;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    List<ChatMessage> found = await MessagesAsync(connection, "m.id = @id", Owner(new List<KeyValuePair<string, object?>> { P("@id", id) }), token).ConfigureAwait(false);
                    return found.FirstOrDefault();
                }
            }

            public async Task UpsertToolCallAsync(ChatToolCallRecord call, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(call);
                if (String.IsNullOrWhiteSpace(call.Id)) call.Id = IdGenerator.NewId("chtool_");
                ChatMessage? message = await GetMessageAsync(call.MessageId, token).ConfigureAwait(false);
                if (message == null) return;
                message.ToolCalls.RemoveAll(item => item.Id == call.Id);
                message.ToolCalls.Add(call);
                await UpdateMessageAsync(message, token).ConfigureAwait(false);
            }

            public async Task UpsertTurnAsync(ChatTurn turn, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(turn);
                string user = RequireUser();
                if (String.IsNullOrWhiteSpace(turn.Id)) turn.Id = IdGenerator.NewId("chturn_");
                List<ChatToolCallRecord> calls = turn.ToolCalls;
                turn.ToolCalls = new List<ChatToolCallRecord>();
                string json = JsonUtil.Serialize(turn);
                turn.ToolCalls = calls;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    KeyValuePair<string, object?>[] values =
                    {
                        P("@id", turn.Id), P("@tenant", _TenantId), P("@user", user), P("@conversation", turn.ConversationId), P("@model", Clip(turn.Model ?? String.Empty, 255)),
                        P("@route", Clip(turn.Route ?? String.Empty, 255)), P("@provider", Clip(turn.Provider ?? String.Empty, 255)), P("@status", turn.Status.ToString()), P("@surface", turn.Surface.ToString()),
                        P("@fallback", turn.Fallback ? 1 : 0), P("@created", Format(turn.CreatedUtc)), P("@json", json)
                    };
                    values = values.Concat(ChatTurnUsageColumns.Values(turn)).ToArray();
                    int changed = await ExecuteAsync(connection, transaction, "UPDATE chat_turns SET model = @model, route = @route, provider = @provider, status = @status, surface = @surface, fallback = @fallback, turn_json = @json, " + ChatTurnUsageColumns.SetList + " WHERE id = @id AND tenant_id = @tenant AND user_id = @user", token, values).ConfigureAwait(false);
                    if (changed == 0)
                    {
                        await ExecuteAsync(connection, transaction, "INSERT INTO chat_turns (id, tenant_id, user_id, conversation_id, model, route, provider, status, surface, fallback, feedback_rating, created_utc, turn_json" + ChatTurnUsageColumns.InsertColumns + ") VALUES (@id, @tenant, @user, @conversation, @model, @route, @provider, @status, @surface, @fallback, NULL, @created, @json" + ChatTurnUsageColumns.InsertValues + ")", token, values).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            public async Task<ChatTurn?> GetTurnAsync(string id, CancellationToken token)
            {
                if (String.IsNullOrWhiteSpace(id)) return null;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    List<ChatTurn> found = await TurnsAsync(connection, "t.id = @id AND " + OwnerClause("t"), Owner(new List<KeyValuePair<string, object?>> { P("@id", id) }), null, token).ConfigureAwait(false);
                    return found.FirstOrDefault();
                }
            }

            public async Task<ChatPage<ChatTurn>> QueryTurnsAsync(ChatHistoryQuery query, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(query);
                int page = Math.Max(1, query.Page);
                int size = Math.Clamp(query.PageSize, 1, 500);
                List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>>());
                string where = TurnWhere(query, parameters);
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    object? total = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM chat_turns t LEFT JOIN chat_conversations c ON c.id = t.conversation_id WHERE " + where, token, parameters).ConfigureAwait(false);
                    parameters.Add(P("@limit", size));
                    parameters.Add(P("@offset", (page - 1) * size));
                    List<ChatTurn> items = await TurnsAsync(connection, where, parameters, "paged", token).ConfigureAwait(false);
                    return new ChatPage<ChatTurn> { Items = items, Total = Convert.ToInt32(total ?? 0, CultureInfo.InvariantCulture), Page = page, PageSize = size };
                }
            }

            public async Task<int> DeleteTurnsAsync(ChatHistoryQuery query, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(query);
                List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>>());
                string where = TurnWhere(query, parameters);
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    List<object?[]> ids = await RowsAsync(connection, "SELECT t.id FROM chat_turns t LEFT JOIN chat_conversations c ON c.id = t.conversation_id WHERE " + where, token, parameters).ConfigureAwait(false);
                    int removed = 0;
                    foreach (object?[] row in ids)
                    {
                        removed += await ExecuteAsync(connection, null, "DELETE FROM chat_turns WHERE id = @id AND tenant_id = @tenant", token, P("@id", row[0]), P("@tenant", _TenantId)).ConfigureAwait(false);
                    }

                    return removed;
                }
            }

            public async Task<int> PruneTurnsAsync(DateTime olderThanUtc, CancellationToken token)
            {
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    return await ExecuteAsync(connection, null, "DELETE FROM chat_turns WHERE tenant_id = @tenant AND created_utc < @cutoff", token, P("@tenant", _TenantId), P("@cutoff", Format(olderThanUtc))).ConfigureAwait(false);
                }
            }

            public async Task<ChatFeedback> SetFeedbackAsync(string messageId, ChatFeedbackRequest request, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(request);
                string user = RequireUser();
                if (request.Rating != 1 && request.Rating != -1) throw new ArgumentException("Rating must be 1 (thumbs up) or -1 (thumbs down).");
                ChatMessage message = await GetMessageAsync(messageId, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Message not found.");
                if (message.Role != ChatRoleEnum.Assistant) throw new ArgumentException("Only answers from the model can be rated.");
                ChatConversation? conversation = await GetConversationAsync(message.ConversationId, false, token).ConfigureAwait(false);
                string? comment = String.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
                if (comment != null && comment.Length > 4000) comment = comment.Substring(0, 4000);
                DateTime now = DateTime.UtcNow;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    List<object?[]> existing = await RowsAsync(connection, transaction, "SELECT feedback_json FROM chat_feedback WHERE message_id = @message AND tenant_id = @tenant", token, new[] { P("@message", message.Id), P("@tenant", _TenantId) }).ConfigureAwait(false);
                    ChatFeedback feedback = existing.Count > 0 ? Read<ChatFeedback>(existing[0][0]) : new ChatFeedback { Id = IdGenerator.NewId("chfb_"), CreatedUtc = now };
                    feedback.ConversationId = message.ConversationId;
                    feedback.MessageId = message.Id;
                    feedback.TurnId = message.TurnId;
                    feedback.Rating = request.Rating;
                    feedback.Comment = comment;
                    feedback.Model = message.Model;
                    feedback.Route = message.Route;
                    feedback.ConversationTitle = conversation?.Title;
                    feedback.Excerpt = message.Content.Length > 240 ? message.Content.Substring(0, 240) : message.Content;
                    feedback.CompletionTokens = message.CompletionTokens;
                    feedback.TotalMs = message.TotalMs;
                    feedback.UpdatedUtc = now;
                    KeyValuePair<string, object?>[] values =
                    {
                        P("@id", feedback.Id), P("@tenant", _TenantId), P("@user", user), P("@conversation", feedback.ConversationId), P("@message", feedback.MessageId), P("@rating", feedback.Rating),
                        P("@comment", comment), P("@hasComment", comment == null ? 0 : 1), P("@model", Clip(feedback.Model, 255)), P("@route", Clip(feedback.Route, 255)), P("@updated", Format(now)), P("@json", JsonUtil.Serialize(feedback))
                    };
                    if (existing.Count > 0)
                    {
                        await ExecuteAsync(connection, transaction, "UPDATE chat_feedback SET rating = @rating, comment_text = @comment, has_comment = @hasComment, updated_utc = @updated, feedback_json = @json WHERE id = @id AND tenant_id = @tenant", token, values).ConfigureAwait(false);
                    }
                    else
                    {
                        await ExecuteAsync(connection, transaction, "INSERT INTO chat_feedback (id, tenant_id, user_id, conversation_id, message_id, rating, comment_text, has_comment, model, route, updated_utc, feedback_json) VALUES (@id, @tenant, @user, @conversation, @message, @rating, @comment, @hasComment, @model, @route, @updated, @json)", token, values).ConfigureAwait(false);
                    }

                    if (!String.IsNullOrWhiteSpace(message.TurnId))
                    {
                        await ExecuteAsync(connection, transaction, "UPDATE chat_turns SET feedback_rating = @rating WHERE id = @turn AND tenant_id = @tenant", token, P("@rating", feedback.Rating), P("@turn", message.TurnId), P("@tenant", _TenantId)).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return feedback;
                }
            }

            public async Task<bool> DeleteFeedbackAsync(string messageId, CancellationToken token)
            {
                ChatMessage? message = await GetMessageAsync(messageId, token).ConfigureAwait(false);
                if (message == null) return false;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    int removed = await ExecuteAsync(connection, null, "DELETE FROM chat_feedback WHERE message_id = @message AND tenant_id = @tenant", token, P("@message", message.Id), P("@tenant", _TenantId)).ConfigureAwait(false);
                    if (!String.IsNullOrWhiteSpace(message.TurnId)) await ExecuteAsync(connection, null, "UPDATE chat_turns SET feedback_rating = NULL WHERE id = @turn AND tenant_id = @tenant", token, P("@turn", message.TurnId), P("@tenant", _TenantId)).ConfigureAwait(false);
                    return removed > 0;
                }
            }

            public async Task<ChatPage<ChatFeedback>> QueryFeedbackAsync(ChatFeedbackQuery query, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(query);
                int page = Math.Max(1, query.Page);
                int size = Math.Clamp(query.PageSize, 1, 500);
                List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>>());
                string where = OwnerClause("f");
                if (query.Rating.HasValue)
                {
                    where += " AND f.rating = @rating";
                    parameters.Add(P("@rating", query.Rating.Value));
                }

                if (!String.IsNullOrWhiteSpace(query.Model))
                {
                    where += " AND LOWER(f.model) = @model";
                    parameters.Add(P("@model", query.Model.Trim().ToLowerInvariant()));
                }

                where += RouteClause("f.route", query.Route, parameters);
                if (query.HasComment.HasValue) where += query.HasComment.Value ? " AND f.has_comment = 1" : " AND f.has_comment = 0";
                if (!String.IsNullOrWhiteSpace(query.Search))
                {
                    where += " AND LOWER(f.comment_text) LIKE @search ESCAPE '!'";
                    parameters.Add(P("@search", Like(query.Search)));
                }

                if (query.FromUtc.HasValue)
                {
                    where += " AND f.updated_utc >= @fromUtc";
                    parameters.Add(P("@fromUtc", Format(query.FromUtc.Value)));
                }

                if (query.ToUtc.HasValue)
                {
                    where += " AND f.updated_utc < @toUtc";
                    parameters.Add(P("@toUtc", Format(query.ToUtc.Value)));
                }

                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    object? total = await ScalarAsync(connection, null, "SELECT COUNT(*) FROM chat_feedback f WHERE " + where, token, parameters).ConfigureAwait(false);
                    parameters.Add(P("@limit", size));
                    parameters.Add(P("@offset", (page - 1) * size));
                    List<object?[]> rows = await RowsAsync(connection, _Owner.Page("SELECT f.feedback_json FROM chat_feedback f WHERE " + where + " ORDER BY f.updated_utc DESC, f.id DESC"), token, parameters).ConfigureAwait(false);
                    return new ChatPage<ChatFeedback> { Items = rows.Select(row => Read<ChatFeedback>(row[0])).ToList(), Total = Convert.ToInt32(total ?? 0, CultureInfo.InvariantCulture), Page = page, PageSize = size };
                }
            }

            public async Task<ChatAnalytics> AnalyticsAsync(DateTime fromUtc, DateTime toUtc, int bucketSeconds, CancellationToken token)
            {
                List<KeyValuePair<string, object?>> parameters = Owner(new List<KeyValuePair<string, object?>> { P("@fromUtc", Format(fromUtc)), P("@toUtc", Format(toUtc)) });
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                {
                    List<ChatTurn> turns = await TurnsAsync(connection, OwnerClause("t") + " AND t.created_utc >= @fromUtc AND t.created_utc < @toUtc", parameters, null, token).ConfigureAwait(false);
                    return ChatAnalyticsBuilder.Build(turns, fromUtc, toUtc, bucketSeconds);
                }
            }

            /// <inheritdoc />
            public async IAsyncEnumerable<Xeno.Core.Analytics.UsageRecord> EnumerateUsageAsync(DateTime fromUtc, DateTime toUtc, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            {
                List<KeyValuePair<string, object?>> parameters = new List<KeyValuePair<string, object?>> { P("@tenant", _TenantId), P("@from", Format(fromUtc)), P("@to", Format(toUtc)) };
                string where = "tenant_id = @tenant AND created_utc >= @from AND created_utc < @to AND status <> 'Running'";
                if (_UserId != null)
                {
                    where += " AND user_id = @user";
                    parameters.Add(P("@user", _UserId));
                }

                string sql = "SELECT created_utc, user_id, model, route, provider, status, fallback, feedback_rating, " + String.Join(", ", ChatTurnUsageColumns.All) + " FROM chat_turns WHERE " + where;
                using (DbConnection connection = await _Owner.OpenAsync(token).ConfigureAwait(false))
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    Bind(command, parameters);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            ChatTurn turn = new ChatTurn
                            {
                                CreatedUtc = ParseDate(reader.GetValue(0)),
                                Model = Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? String.Empty,
                                Route = Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? String.Empty,
                                Provider = Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? String.Empty,
                                Status = Enum.TryParse(Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture), out ChatTurnStatusEnum status) ? status : ChatTurnStatusEnum.Error,
                                Fallback = Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture) == 1,
                                FeedbackRating = reader.IsDBNull(7) ? null : (int)Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture)
                            };
                            object?[] usage = new object?[ChatTurnUsageColumns.RealColumns.Count + ChatTurnUsageColumns.IntegerColumns.Count];
                            for (int i = 0; i < usage.Length; i++) usage[i] = reader.GetValue(8 + i);
                            ChatTurnUsageColumns.Read(turn, usage);
                            yield return Xeno.Core.Analytics.UsageRecordMapper.FromChatTurn(turn, _TenantId, Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            private string RequireUser()
            {
                return _UserId ?? throw new InvalidOperationException("A tenant-wide chat view cannot write.");
            }

            private List<KeyValuePair<string, object?>> Owner(List<KeyValuePair<string, object?>> parameters)
            {
                parameters.Add(P("@tenant", _TenantId));
                if (_UserId != null) parameters.Add(P("@user", _UserId));
                return parameters;
            }

            private string OwnerClause(string? alias)
            {
                string prefix = alias == null ? String.Empty : alias + ".";
                return prefix + "tenant_id = @tenant" + (_UserId == null ? String.Empty : " AND " + prefix + "user_id = @user");
            }

            private string TurnWhere(ChatHistoryQuery query, List<KeyValuePair<string, object?>> parameters)
            {
                string where = OwnerClause("t");
                if (!String.IsNullOrWhiteSpace(query.Model))
                {
                    where += " AND LOWER(t.model) = @model";
                    parameters.Add(P("@model", query.Model.Trim().ToLowerInvariant()));
                }

                where += RouteClause("t.route", query.Route, parameters);
                if (!String.IsNullOrWhiteSpace(query.Provider))
                {
                    where += " AND LOWER(t.provider) = @provider";
                    parameters.Add(P("@provider", query.Provider.Trim().ToLowerInvariant()));
                }

                if (query.Status.HasValue)
                {
                    where += " AND t.status = @status";
                    parameters.Add(P("@status", query.Status.Value.ToString()));
                }

                if (query.Surface.HasValue)
                {
                    where += " AND t.surface = @surface";
                    parameters.Add(P("@surface", query.Surface.Value.ToString()));
                }

                if (query.Rating.HasValue)
                {
                    if (query.Rating.Value == 0)
                    {
                        where += " AND t.feedback_rating IS NULL";
                    }
                    else
                    {
                        where += " AND t.feedback_rating = @rating";
                        parameters.Add(P("@rating", query.Rating.Value));
                    }
                }

                if (query.Fallback.HasValue) where += query.Fallback.Value ? " AND t.fallback = 1" : " AND t.fallback = 0";
                if (!String.IsNullOrWhiteSpace(query.ConversationId))
                {
                    where += " AND t.conversation_id = @conversationFilter";
                    parameters.Add(P("@conversationFilter", query.ConversationId.Trim()));
                }

                if (!String.IsNullOrWhiteSpace(query.Search))
                {
                    where += " AND (LOWER(c.title) LIKE @search ESCAPE '!' OR LOWER(t.model) LIKE @search ESCAPE '!')";
                    parameters.Add(P("@search", Like(query.Search)));
                }

                if (query.FromUtc.HasValue)
                {
                    where += " AND t.created_utc >= @fromUtc";
                    parameters.Add(P("@fromUtc", Format(query.FromUtc.Value)));
                }

                if (query.ToUtc.HasValue)
                {
                    where += " AND t.created_utc < @toUtc";
                    parameters.Add(P("@toUtc", Format(query.ToUtc.Value)));
                }

                return where;
            }

            private static string RouteClause(string column, string? route, List<KeyValuePair<string, object?>> parameters)
            {
                if (String.IsNullOrWhiteSpace(route)) return String.Empty;
                string value = route.Trim().ToLowerInvariant();
                if (value == "local") return " AND (LOWER(" + column + ") = 'local' OR LOWER(" + column + ") LIKE 'local:%')";
                parameters.Add(P("@route", value));
                return " AND LOWER(" + column + ") = @route";
            }

            private async Task<List<ChatTurn>> TurnsAsync(DbConnection connection, string where, List<KeyValuePair<string, object?>> parameters, string? paged, CancellationToken token)
            {
                string sql = "SELECT t.turn_json, t.feedback_rating, c.title FROM chat_turns t LEFT JOIN chat_conversations c ON c.id = t.conversation_id WHERE " + where + " ORDER BY t.created_utc DESC, t.id DESC";
                if (paged != null) sql = _Owner.Page(sql);
                List<object?[]> rows = await RowsAsync(connection, sql, token, parameters).ConfigureAwait(false);
                return rows.Select(row =>
                {
                    ChatTurn turn = Read<ChatTurn>(row[0]);
                    turn.FeedbackRating = row[1] == null ? null : Convert.ToInt32(row[1], CultureInfo.InvariantCulture);
                    turn.ConversationTitle = row[2] as string;
                    return turn;
                }).ToList();
            }

            private async Task<List<ChatMessage>> MessagesAsync(DbConnection connection, string where, List<KeyValuePair<string, object?>> parameters, CancellationToken token)
            {
                List<object?[]> rows = await RowsAsync(connection, "SELECT m.message_json, f.feedback_json FROM chat_messages m LEFT JOIN chat_feedback f ON f.message_id = m.id WHERE " + where + " AND " + OwnerClause("m") + " ORDER BY m.sequence ASC", token, parameters).ConfigureAwait(false);
                return rows.Select(row =>
                {
                    ChatMessage message = Read<ChatMessage>(row[0]);
                    message.Feedback = row[1] == null ? null : Read<ChatFeedback>(row[1]);
                    return message;
                }).ToList();
            }

            private static ChatConversation ReadConversation(object?[] row)
            {
                ChatConversation conversation = Read<ChatConversation>(row[0]);
                conversation.UpdatedUtc = ParseDate(row[1]);
                conversation.MessageCount = Convert.ToInt32(row[2] ?? 0, CultureInfo.InvariantCulture);
                conversation.Messages = new List<ChatMessage>();
                return conversation;
            }

            private static string ConversationJson(ChatConversation conversation)
            {
                List<ChatMessage> messages = conversation.Messages;
                conversation.Messages = new List<ChatMessage>();
                string json = JsonUtil.Serialize(conversation);
                conversation.Messages = messages;
                return json;
            }

            private static string MessageJson(ChatMessage message)
            {
                ChatFeedback? feedback = message.Feedback;
                message.Feedback = null;
                string json = JsonUtil.Serialize(message);
                message.Feedback = feedback;
                return json;
            }

            private static string? Clip(string? value, int length)
            {
                if (value == null) return null;
                return value.Length <= length ? value : value.Substring(0, length);
            }
        }

        #endregion
    }
}
