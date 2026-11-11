namespace Xeno.Client.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Tools.Mcp;
    using Xeno.Client.Tools.Workspace;
    using Xeno.Core.Chat;
    using Xeno.Core.Chat.Wire;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Every tool the in-app chat model may call: Xeno's own tools (status, models, routing, history; xeno_chat is left
    /// out so a chat cannot call itself), the workspace tools ported from Mux (files, search, processes, web, task
    /// planning, memory), and tools from external MCP servers. Workspace tools run in the conversation's working
    /// directory and cannot reach outside it. Thread safe.
    /// </summary>
    public sealed class XenoChatToolHost : IChatToolHost
    {
        #region Private-Members

        private readonly XenoToolCatalog _Catalog;
        private readonly Func<ClientSettings> _Settings;
        private readonly Dictionary<string, IWorkspaceTool> _Workspace;
        private readonly IMcpToolSource? _Mcp;
        private readonly List<ChatCompletionToolDto> _XenoDefinitions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the host with only Xeno's own tools.
        /// </summary>
        /// <param name="catalog">Tool catalog.</param>
        /// <exception cref="ArgumentNullException">catalog is null.</exception>
        public XenoChatToolHost(XenoToolCatalog catalog) : this(catalog, () => new ClientSettings(), new List<IWorkspaceTool>(), null)
        {
        }

        /// <summary>
        /// Create the host.
        /// </summary>
        /// <param name="catalog">Xeno tool catalog.</param>
        /// <param name="settings">Returns the current client settings.</param>
        /// <param name="workspace">Workspace tools.</param>
        /// <param name="mcp">External MCP tools, or null for none.</param>
        /// <exception cref="ArgumentNullException">catalog, settings, or workspace is null.</exception>
        public XenoChatToolHost(XenoToolCatalog catalog, Func<ClientSettings> settings, IEnumerable<IWorkspaceTool> workspace, IMcpToolSource? mcp)
        {
            _Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ArgumentNullException.ThrowIfNull(workspace);
            _Workspace = new Dictionary<string, IWorkspaceTool>(StringComparer.Ordinal);
            foreach (IWorkspaceTool tool in workspace) _Workspace[tool.Name] = tool;
            _Mcp = mcp;
            _XenoDefinitions = catalog.Tools
                .Where(tool => !String.Equals(tool.Name, XenoToolCatalog.ChatToolName, StringComparison.Ordinal))
                .Select(tool => Definition(tool.Name, tool.Description, tool.Schema))
                .ToList();
        }

        #endregion

        #region Public-Members

        /// <inheritdoc />
        public IReadOnlyList<ChatCompletionToolDto> Definitions
        {
            get
            {
                ClientChatSettings chat = _Settings().Chat;
                List<ChatCompletionToolDto> definitions = new List<ChatCompletionToolDto>(_XenoDefinitions);
                definitions.AddRange(_Workspace.Values.Where(tool => IsAvailable(tool.Name, chat)).Select(tool => Definition(tool.Name, tool.Description, tool.Schema)));
                if (_Mcp != null) definitions.AddRange(_Mcp.Tools.Select(tool => Definition(tool.Name, tool.Description, tool.Schema)));
                return definitions;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// True when a workspace tool is offered under these settings: web_search needs a configured provider,
        /// plan_tasks and update_task need task planning on, and remember, forget, and recall need memory on.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="chat">Chat settings.</param>
        /// <returns>True when offered.</returns>
        public static bool IsAvailable(string name, ClientChatSettings chat)
        {
            ArgumentNullException.ThrowIfNull(chat);
            switch (name)
            {
                case "web_search": return chat.WebSearch.IsConfigured;
                case "plan_tasks":
                case "update_task": return chat.TaskPlanningEnabled;
                case "remember":
                case "forget":
                case "recall": return chat.MemoryEnabled;
                default: return true;
            }
        }

        /// <summary>
        /// The working directory a conversation's tools use: its own, else <see cref="ClientChatSettings.DefaultWorkingDirectory"/>,
        /// else the user's home folder.
        /// </summary>
        /// <param name="conversationDirectory">The conversation's directory, or null.</param>
        /// <param name="chat">Chat settings.</param>
        /// <returns>Absolute path.</returns>
        public static string EffectiveWorkingDirectory(string? conversationDirectory, ClientChatSettings chat)
        {
            ArgumentNullException.ThrowIfNull(chat);
            string? chosen = !String.IsNullOrWhiteSpace(conversationDirectory) ? conversationDirectory : chat.DefaultWorkingDirectory;
            if (String.IsNullOrWhiteSpace(chosen)) chosen = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (chosen == "~" || chosen.StartsWith("~/", StringComparison.Ordinal)) chosen = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), chosen.Length > 2 ? chosen.Substring(2) : String.Empty);
            return Path.GetFullPath(chosen);
        }

        /// <summary>
        /// Every tool chat offers right now, with its group and whether it asks before running.
        /// </summary>
        /// <returns>Tools in offer order.</returns>
        public IReadOnlyList<ChatToolInfo> Describe()
        {
            ClientChatSettings chat = _Settings().Chat;
            List<ChatToolInfo> tools = _Catalog.Tools
                .Where(tool => !String.Equals(tool.Name, XenoToolCatalog.ChatToolName, StringComparison.Ordinal))
                .Select(tool => new ChatToolInfo { Name = tool.Name, Description = tool.Description, Mutating = tool.Mutating, Group = "Xeno" })
                .ToList();
            foreach (IWorkspaceTool tool in _Workspace.Values.Where(item => IsAvailable(item.Name, chat)))
            {
                string? space = tool.GetType().Namespace;
                string group = space == null ? "Workspace" : space.Substring(space.LastIndexOf('.') + 1);
                tools.Add(new ChatToolInfo { Name = tool.Name, Description = tool.Description, Mutating = tool.Mutating, Group = group });
            }

            if (_Mcp != null) tools.AddRange(_Mcp.Tools.Select(tool => new ChatToolInfo { Name = tool.Name, Description = tool.Description, Mutating = tool.Mutating, Group = "MCP: " + tool.Server }));
            return tools;
        }

        /// <inheritdoc />
        public bool IsMutating(string name)
        {
            if (_Workspace.TryGetValue(name, out IWorkspaceTool? workspace)) return workspace.Mutating;
            McpChatTool? external = _Mcp?.Tools.FirstOrDefault(tool => String.Equals(tool.Name, name, StringComparison.Ordinal));
            if (external != null) return external.Mutating;
            XenoTool? tool = _Catalog.Find(name);
            return tool == null || tool.Mutating;
        }

        /// <inheritdoc />
        public async Task<string> InvokeAsync(string name, string arguments, ChatToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (String.Equals(name, XenoToolCatalog.ChatToolName, StringComparison.Ordinal)) throw new ArgumentException("A chat cannot call xeno_chat.");
            ClientChatSettings chat = _Settings().Chat;
            string result;
            if (_Workspace.TryGetValue(name, out IWorkspaceTool? workspace))
            {
                if (!IsAvailable(name, chat)) throw new ArgumentException("The " + name + " tool is turned off in chat settings.");
                string directory = EffectiveWorkingDirectory(context.WorkingDirectory, chat);
                if (!Directory.Exists(directory)) throw new ArgumentException("This conversation's working directory does not exist: " + directory);
                WorkspaceToolContext toolContext = new WorkspaceToolContext { ConversationId = context.ConversationId, TurnId = context.TurnId, WorkingDirectory = directory, Settings = chat };
                object value = await workspace.InvokeAsync(arguments, toolContext, token).ConfigureAwait(false);
                result = value as string ?? JsonUtil.Serialize(value);
            }
            else if (_Mcp != null && _Mcp.Tools.Any(tool => String.Equals(tool.Name, name, StringComparison.Ordinal)))
            {
                result = await _Mcp.InvokeAsync(name, arguments, token).ConfigureAwait(false);
            }
            else
            {
                result = await _Catalog.InvokeAsync(name, arguments, token).ConfigureAwait(false);
            }

            return Truncate(result, chat.MaxToolOutputCharacters);
        }

        #endregion

        #region Private-Methods

        private static ChatCompletionToolDto Definition(string name, string description, ChatToolSchemaDto schema)
        {
            return new ChatCompletionToolDto { Function = new ChatCompletionFunctionDto { Name = name, Description = description, Parameters = schema } };
        }

        private static string Truncate(string result, int limit)
        {
            if (result.Length <= limit) return result;
            return result.Substring(0, limit) + "\n[Output truncated: " + (result.Length - limit) + " more characters. Ask for a smaller range or a narrower search.]";
        }

        #endregion
    }
}
