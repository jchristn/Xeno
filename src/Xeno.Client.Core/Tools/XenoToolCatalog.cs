namespace Xeno.Client.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Core.Chat.Wire;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Xeno's tools: one list shared by the MCP servers (HTTP in the client process, stdio through "xeno mcp") and the
    /// in-app chat, so the two never drift. Read-only tools look at status, models, routing, providers, and history;
    /// tools marked mutating change settings or the client process, or send a chat message. Thread safe.
    /// </summary>
    public sealed class XenoToolCatalog
    {
        #region Public-Members

        /// <summary>
        /// Name of the tool that sends a chat message. The in-app chat does not offer it to the model.
        /// </summary>
        public const string ChatToolName = "xeno_chat";

        /// <summary>
        /// Tools in display order.
        /// </summary>
        public IReadOnlyList<XenoTool> Tools
        {
            get { return _Tools; }
        }

        #endregion

        #region Private-Members

        private readonly XenoWorkspace _Workspace;
        private readonly Func<CancellationToken, Task<IChatStore>> _Chat;
        private readonly IXenoChatGateway? _Gateway;
        private readonly List<XenoTool> _Tools;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the catalog.
        /// </summary>
        /// <param name="workspace">Workspace for status, settings, routing, and the client process.</param>
        /// <param name="chat">Returns the chat store.</param>
        /// <param name="gateway">Sends chat messages for xeno_chat, or null to leave that tool out.</param>
        /// <exception cref="ArgumentNullException">workspace or chat is null.</exception>
        public XenoToolCatalog(XenoWorkspace workspace, Func<CancellationToken, Task<IChatStore>> chat, IXenoChatGateway? gateway)
        {
            _Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _Chat = chat ?? throw new ArgumentNullException(nameof(chat));
            _Gateway = gateway;
            _Tools = Build();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Find a tool.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Tool, or null.</returns>
        public XenoTool? Find(string? name)
        {
            return _Tools.FirstOrDefault(tool => String.Equals(tool.Name, name, StringComparison.Ordinal));
        }

        /// <summary>
        /// Run a tool and return its result as JSON text.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="arguments">Arguments as JSON text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result as JSON.</returns>
        /// <exception cref="ArgumentException">The tool is unknown or its arguments are invalid.</exception>
        public async Task<string> InvokeAsync(string name, string arguments, CancellationToken token)
        {
            XenoTool tool = Find(name) ?? throw new ArgumentException("There is no Xeno tool named '" + name + "'.");
            object result = await tool.Invoke(String.IsNullOrWhiteSpace(arguments) ? "{}" : arguments, token).ConfigureAwait(false);
            return JsonUtil.Serialize(result);
        }

        /// <summary>
        /// Read tool arguments.
        /// </summary>
        /// <param name="arguments">JSON text.</param>
        /// <returns>Arguments.</returns>
        /// <exception cref="ArgumentException">The arguments are not a JSON object.</exception>
        public static XenoToolArguments ReadArguments(string? arguments)
        {
            if (String.IsNullOrWhiteSpace(arguments)) return new XenoToolArguments();
            try
            {
                return JsonUtil.Deserialize<XenoToolArguments>(arguments) ?? new XenoToolArguments();
            }
            catch (InvalidDataException ex)
            {
                throw new ArgumentException("The tool arguments are not valid JSON: " + ex.Message);
            }
        }

        #endregion

        #region Private-Methods

        private List<XenoTool> Build()
        {
            List<XenoTool> tools = new List<XenoTool>
            {
                Tool("xeno_status", "Status of this machine's Xeno client: whether it is running and connected to the Xeno network, the server it uses, the local dashboard and local inference endpoint, routing mode, and provider counts.", false, Schema(), (args, token) =>
                {
                    return Task.FromResult<object>(_Workspace.GetStatus());
                }),
                Tool("xeno_list_models", "Models available to chat with: models served by providers on this machine, and (unless includeNetwork is false) models the Xeno network offers. Shows where a request for each model would go.", false,
                    Schema(Bool("includeNetwork", "Include Xeno network models. Default true.")), async (args, token) =>
                {
                    ModelCatalog catalog = await _Workspace.GetModelCatalogAsync(args.IncludeNetwork != false, token).ConfigureAwait(false);
                    return new XenoToolModelList
                    {
                        Models = catalog.Entries.Select(entry => new XenoToolModel
                        {
                            Name = entry.Name,
                            Availability = entry.AvailabilityText,
                            ServedLocally = entry.ServedLocally,
                            LocalProviders = entry.LocalProviders.ToList(),
                            OnNetwork = entry.OnNetwork,
                            RouteTarget = entry.RouteTarget,
                            SupportsChat = entry.SupportsCompletions || !entry.SupportsEmbeddings
                        }).ToList(),
                        NetworkMessage = String.IsNullOrWhiteSpace(catalog.NetworkMessage) ? null : catalog.NetworkMessage
                    };
                }),
                Tool("xeno_resolve_route", "Where a request for a model would go right now (a provider on this machine, or the Xeno network) and why.", false,
                    Schema(Text("model", "Model name, for example llama3.1:8b."), "model"), async (args, token) =>
                {
                    if (String.IsNullOrWhiteSpace(args.Model)) throw new ArgumentException("model is required.");
                    LocalInferenceRouteDecision decision = await _Workspace.ResolveRouteAsync(args.Model, token).ConfigureAwait(false);
                    return LocalInferenceRoutingService.ToResponse(decision);
                }),
                Tool("xeno_list_providers", "Model providers configured on this machine (Ollama, vLLM, OpenAI-compatible): endpoint, models, and whether each serves local requests and the Xeno network. Credentials are never included.", false, Schema(), (args, token) =>
                {
                    ClientSettings settings = _Workspace.LoadSettings();
                    return Task.FromResult<object>(new XenoToolProviderList
                    {
                        Providers = settings.Providers.Select(provider => new XenoToolProvider
                        {
                            Name = provider.Name,
                            ApiType = provider.ApiType,
                            Endpoint = provider.Endpoint,
                            Models = (provider.Models ?? new List<ClientModelSettings>()).Select(model => model.Name).ToList(),
                            HasCredential = !String.IsNullOrWhiteSpace(provider.Credential),
                            ShareWithNetwork = provider.ShareWithNetwork,
                            RouteLocalRequests = provider.RouteLocalRequests
                        }).ToList()
                    });
                }),
                Tool("xeno_provider_health", "Routing on this machine: routing mode, route rules, and each local provider's health, latency, requests in progress, and concurrency limit.", false, Schema(), async (args, token) =>
                {
                    return await _Workspace.GetLocalInferenceRoutingAsync(token).ConfigureAwait(false);
                }),
                Tool("xeno_check_providers", "Check every local provider now and report whether each answers.", false, Schema(), async (args, token) =>
                {
                    return new XenoToolBackendList { Providers = (await _Workspace.CheckBackendsAsync(token).ConfigureAwait(false)).ToList() };
                }),
                Tool("xeno_request_history_query", "Recent requests apps on this machine sent to the local inference endpoint: model, where each went, status, latency, and tokens. Metadata only, newest first.", false,
                    Schema(Integer("limit", "Most requests to return, 1 to 500. Default 50."), Integer("skip", "Requests to skip.")), async (args, token) =>
                {
                    List<LocalInferenceRequestRecord> records = await _Workspace.GetLocalInferenceRequestsAsync(Math.Max(0, args.Skip ?? 0), Math.Clamp(args.Limit ?? 50, 1, 500), token).ConfigureAwait(false);
                    return new XenoToolRequestList { Requests = records };
                }),
                ToolRaw("xeno_chat_history_query", "Search chat request history: one row per chat turn with model, route, outcome, timing, tokens, tool calls, and rating. Newest first.", false,
                    Schema(Text("model", "Model."), Choice("route", "local, network, or local:<provider>.", null), Choice("status", "Outcome.", new List<string> { "Success", "Error", "Cancelled" }),
                        Integer("rating", "1 for thumbs up, -1 for thumbs down, 0 for unrated."), Choice("surface", "Where the chat came from.", new List<string> { "Web", "Desktop", "Terminal", "Cli", "Mcp" }),
                        Text("search", "Text to find in the conversation title, model, or error."), Text("fromUtc", "Earliest start, ISO 8601."), Text("toUtc", "Latest start, ISO 8601."),
                        Integer("page", "Page, from 1."), Integer("pageSize", "Rows per page, 1 to 500.")), async (raw, token) =>
                {
                    ChatHistoryQuery query = Read<ChatHistoryQuery>(raw.RawJson);
                    IChatStore store = await _Chat(token).ConfigureAwait(false);
                    return await store.QueryTurnsAsync(query, token).ConfigureAwait(false);
                }),
                ToolRaw("xeno_feedback_query", "Search ratings people gave chat answers: thumbs up or down, comments, model, and route. Newest first.", false,
                    Schema(Integer("rating", "1 or -1."), Text("model", "Model."), Bool("hasComment", "True for ratings with a comment."), Text("search", "Text to find in comments and titles."),
                        Integer("page", "Page, from 1."), Integer("pageSize", "Rows per page, 1 to 500.")), async (raw, token) =>
                {
                    ChatFeedbackQuery query = Read<ChatFeedbackQuery>(raw.RawJson);
                    IChatStore store = await _Chat(token).ConfigureAwait(false);
                    return await store.QueryFeedbackAsync(query, token).ConfigureAwait(false);
                }),
                ToolRaw("xeno_usage", "Usage analytics for this machine over a range: requests and failures, latency, time to first token, streaming time, and throughput percentiles, tokens by type, totals by model, provider, or route, a token heatmap, and average time by stage. The same numbers the Usage page in every Xeno app shows.", false,
                    Schema(Choice("dataset", "served (network requests this machine answered, default), endpoint (the local inference endpoint), or chat.", new List<string> { "served", "endpoint", "chat" }),
                        Choice("range", "Last hour, day (default), week, or month.", new List<string> { "hour", "day", "week", "month" }),
                        Choice("groupBy", "Group by Model (default), Provider, or Route.", new List<string> { "Model", "Provider", "Route" }),
                        Choice("groupMetric", "Rank groups by Requests (default) or Tokens.", new List<string> { "Requests", "Tokens" }),
                        Text("model", "Only this model.")), async (raw, token) =>
                {
                    XenoToolUsageArguments args = Read<XenoToolUsageArguments>(raw.RawJson);
                    Xeno.Core.Analytics.UsageDatasetEnum dataset = (args.Dataset ?? "served").ToLowerInvariant() switch
                    {
                        "served" => Xeno.Core.Analytics.UsageDatasetEnum.ServedRequests,
                        "endpoint" => Xeno.Core.Analytics.UsageDatasetEnum.LocalEndpoint,
                        "chat" => Xeno.Core.Analytics.UsageDatasetEnum.Chat,
                        _ => throw new ArgumentException("dataset must be served, endpoint, or chat.")
                    };
                    Services.HistoryRangeEnum range = Services.HistoryRanges.Parse(args.Range ?? "day");
                    if (args.Range != null && !new[] { "hour", "day", "week", "month" }.Contains(args.Range.ToLowerInvariant())) throw new ArgumentException("range must be hour, day, week, or month.");
                    Xeno.Core.Analytics.UsageGroupByEnum groupBy = Enum.TryParse(args.GroupBy ?? "Model", true, out Xeno.Core.Analytics.UsageGroupByEnum parsedGroup) && (parsedGroup == Xeno.Core.Analytics.UsageGroupByEnum.Model || parsedGroup == Xeno.Core.Analytics.UsageGroupByEnum.Provider || parsedGroup == Xeno.Core.Analytics.UsageGroupByEnum.Route) ? parsedGroup : throw new ArgumentException("groupBy must be Model, Provider, or Route.");
                    Xeno.Core.Analytics.UsageGroupMetricEnum metric = Enum.TryParse(args.GroupMetric ?? "Requests", true, out Xeno.Core.Analytics.UsageGroupMetricEnum parsedMetric) ? parsedMetric : throw new ArgumentException("groupMetric must be Requests or Tokens.");
                    DateTime to = DateTime.UtcNow;
                    Xeno.Core.Analytics.UsageAnalyticsQuery query = new Xeno.Core.Analytics.UsageAnalyticsQuery
                    {
                        FromUtc = Services.HistoryRanges.StartUtc(range, to),
                        ToUtc = to,
                        BucketMinutes = Services.HistoryRanges.DefaultBucketMinutes(range),
                        GroupBy = groupBy,
                        GroupMetric = metric,
                        Model = args.Model,
                        UtcOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(to).TotalMinutes
                    };
                    return await _Workspace.Usage.GetUsageAsync(dataset, query, token).ConfigureAwait(false);
                }),
                Tool("xeno_list_conversations", "Saved chat conversations, pinned first, then most recent.", false,
                    Schema(Text("search", "Text to find in titles and models."), Integer("limit", "Most to return, 1 to 200. Default 50.")), async (args, token) =>
                {
                    IChatStore store = await _Chat(token).ConfigureAwait(false);
                    return new XenoToolConversationList { Conversations = await store.ListConversationsAsync(args.Search, null, Math.Clamp(args.Limit ?? 50, 1, 200), token).ConfigureAwait(false) };
                }),
                Tool("xeno_get_conversation", "One chat conversation with its messages, tool calls, and ratings.", false,
                    Schema(Text("conversationId", "Conversation id (cnv_...)."), "conversationId"), async (args, token) =>
                {
                    if (String.IsNullOrWhiteSpace(args.ConversationId)) throw new ArgumentException("conversationId is required.");
                    IChatStore store = await _Chat(token).ConfigureAwait(false);
                    return await store.GetConversationAsync(args.ConversationId, true, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation '" + args.ConversationId + "' not found.");
                }),
                Tool("xeno_set_route_rule", "Add or replace the route rule for a model: send it to a provider on this machine (Local, optionally falling back to the network) or always to the Xeno network.", true,
                    Schema(Text("model", "Model name."), Choice("target", "Local or Network.", new List<string> { "Local", "Network" }), Text("provider", "Local provider, required for Local."), Bool("fallbackToNetwork", "For Local: retry on the network when the provider fails before answering."), "model", "target"), async (args, token) =>
                {
                    if (String.IsNullOrWhiteSpace(args.Model)) throw new ArgumentException("model is required.");
                    if (!Enum.TryParse(args.Target, true, out LocalInferenceRouteTargetEnum target)) throw new ArgumentException("target must be Local or Network.");
                    LocalInferenceRouteRule saved = await _Workspace.SaveRouteRuleAsync(new LocalInferenceRouteRule
                    {
                        Model = args.Model,
                        Target = target,
                        Provider = target == LocalInferenceRouteTargetEnum.Local ? args.Provider : null,
                        FallbackToNetwork = target == LocalInferenceRouteTargetEnum.Local && args.FallbackToNetwork == true
                    }, null, token).ConfigureAwait(false);
                    return saved;
                }),
                Tool("xeno_remove_route_rule", "Remove the route rule for a model, so it follows the routing mode again.", true,
                    Schema(Text("model", "Model name."), "model"), async (args, token) =>
                {
                    if (String.IsNullOrWhiteSpace(args.Model)) throw new ArgumentException("model is required.");
                    int removed = await _Workspace.DeleteRouteRuleAsync(args.Model, token).ConfigureAwait(false);
                    return new XenoToolMessage { Success = removed > 0, Message = removed > 0 ? "Removed the route rule for " + args.Model + "." : "There was no route rule for " + args.Model + "." };
                }),
                Tool("xeno_start_client", "Start this machine's Xeno client so it connects to the Xeno network and serves its providers.", true, Schema(), async (args, token) =>
                {
                    ClientStartResult result = await _Workspace.StartClientAsync(token).ConfigureAwait(false);
                    return new XenoToolMessage { Success = result.Connected || result.Outcome == ClientStartOutcomeEnum.AlreadyRunning, Message = "Start: " + result.Outcome + "." };
                }),
                Tool("xeno_stop_client", "Stop this machine's Xeno client. Chat and the local endpoint stop working until it starts again.", true, Schema(), async (args, token) =>
                {
                    ClientStopResult result = await _Workspace.StopClientAsync(false, token).ConfigureAwait(false);
                    return new XenoToolMessage { Success = result.Stopped || !result.WasRunning, Message = result.WasRunning ? (result.Stopped ? "Stopped the client." : "The client did not stop.") : "The client was not running." };
                })
            };

            if (_Gateway != null)
            {
                tools.Add(ToolRaw(ChatToolName, "Ask a model on this machine or the Xeno network a question and get its answer. Saved as a chat conversation; pass conversationId to continue one.", true,
                    Schema(Text("model", "Model to ask. Required for a new conversation."), Text("message", "The message."), Text("conversationId", "Conversation to continue."), Text("systemPrompt", "Instructions for a new conversation."), "message"), async (raw, token) =>
                {
                    XenoChatToolArguments arguments = Read<XenoChatToolArguments>(raw.RawJson);
                    if (String.IsNullOrWhiteSpace(arguments.Message)) throw new ArgumentException("message is required.");
                    return await _Gateway.AskAsync(arguments, token).ConfigureAwait(false);
                }));
            }

            return tools;
        }

        private static XenoTool Tool(string name, string description, bool mutating, ChatToolSchemaDto schema, Func<XenoToolArguments, CancellationToken, Task<object>> handler)
        {
            return new XenoTool
            {
                Name = name,
                Description = description,
                Mutating = mutating,
                Schema = schema,
                Invoke = async (json, token) => await handler(ReadArguments(json), token).ConfigureAwait(false)
            };
        }

        private static XenoTool ToolRaw(string name, string description, bool mutating, ChatToolSchemaDto schema, Func<RawArguments, CancellationToken, Task<object>> handler)
        {
            return new XenoTool
            {
                Name = name,
                Description = description,
                Mutating = mutating,
                Schema = schema,
                Invoke = async (json, token) => await handler(new RawArguments(json), token).ConfigureAwait(false)
            };
        }

        private static T Read<T>(string json) where T : class, new()
        {
            try
            {
                return JsonUtil.Deserialize<T>(String.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new T();
            }
            catch (InvalidDataException ex)
            {
                throw new ArgumentException("The tool arguments are not valid: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        private static ChatToolSchemaDto Schema(params object[] parts)
        {
            ChatToolSchemaDto schema = new ChatToolSchemaDto();
            foreach (object part in parts)
            {
                if (part is KeyValuePair<string, ChatToolPropertyDto> property) schema.Properties[property.Key] = property.Value;
                else if (part is string required) schema.Required.Add(required);
            }

            return schema;
        }

        private static KeyValuePair<string, ChatToolPropertyDto> Text(string name, string description)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "string", Description = description });
        }

        private static KeyValuePair<string, ChatToolPropertyDto> Integer(string name, string description)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "integer", Description = description });
        }

        private static KeyValuePair<string, ChatToolPropertyDto> Bool(string name, string description)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "boolean", Description = description });
        }

        private static KeyValuePair<string, ChatToolPropertyDto> Choice(string name, string description, List<string>? values)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "string", Description = description, Enum = values });
        }

        #endregion

        #region Private-Classes

        private sealed class RawArguments
        {
            internal RawArguments(string json)
            {
                RawJson = json;
            }

            internal string RawJson { get; }
        }

        #endregion
    }
}
