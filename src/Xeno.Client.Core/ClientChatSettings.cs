namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Chat settings: history retention, tool limits, and the workspace tools (files, processes, web, planning, memory,
    /// and external MCP servers).
    /// </summary>
    public class ClientChatSettings : Xeno.Core.Chat.IChatLimits
    {
        private int _HistoryRetentionDays = 30;
        private int _MaxToolIterations = 8;
        private int _MaxToolCallsPerTurn = 16;
        private int _ToolApprovalTimeoutSeconds = 120;
        private int _ProcessTimeoutSeconds = 120;
        private int _MaxToolOutputCharacters = 50000;
        private ChatWebSearchSettings _WebSearch = new ChatWebSearchSettings();
        private List<ChatMcpServerSettings> _McpServers = new List<ChatMcpServerSettings>();

        /// <summary>
        /// Days chat request history and tool calls are kept, 1 to 3650. Conversations are kept until deleted.
        /// </summary>
        public int HistoryRetentionDays
        {
            get { return _HistoryRetentionDays; }
            set { _HistoryRetentionDays = Math.Clamp(value, 1, 3650); }
        }

        /// <summary>
        /// Most model calls in one turn when the model uses tools, 1 to 20.
        /// </summary>
        public int MaxToolIterations
        {
            get { return _MaxToolIterations; }
            set { _MaxToolIterations = Math.Clamp(value, 1, 20); }
        }

        /// <summary>
        /// Most tool calls in one turn, 1 to 50.
        /// </summary>
        public int MaxToolCallsPerTurn
        {
            get { return _MaxToolCallsPerTurn; }
            set { _MaxToolCallsPerTurn = Math.Clamp(value, 1, 50); }
        }

        /// <summary>
        /// Seconds to wait for Allow or Deny on a tool that changes something, 10 to 3600. No answer denies the call.
        /// </summary>
        public int ToolApprovalTimeoutSeconds
        {
            get { return _ToolApprovalTimeoutSeconds; }
            set { _ToolApprovalTimeoutSeconds = Math.Clamp(value, 10, 3600); }
        }

        /// <summary>
        /// System prompt for new conversations, or null for none.
        /// </summary>
        public string? DefaultSystemPrompt { get; set; } = null;

        /// <summary>
        /// Working directory for new conversations, or null for the user's home folder. File and process tools are
        /// confined to a conversation's working directory.
        /// </summary>
        public string? DefaultWorkingDirectory { get; set; } = null;

        /// <summary>
        /// Seconds a run_process call may take before it is stopped, 5 to 3600.
        /// </summary>
        public int ProcessTimeoutSeconds
        {
            get { return _ProcessTimeoutSeconds; }
            set { _ProcessTimeoutSeconds = Math.Clamp(value, 5, 3600); }
        }

        /// <summary>
        /// Most characters of one tool result sent back to the model, 1000 to 1000000; longer output is truncated.
        /// </summary>
        public int MaxToolOutputCharacters
        {
            get { return _MaxToolOutputCharacters; }
            set { _MaxToolOutputCharacters = Math.Clamp(value, 1000, 1000000); }
        }

        /// <summary>
        /// Offer plan_tasks and update_task, which keep a task list for the turn.
        /// </summary>
        public bool TaskPlanningEnabled { get; set; } = true;

        /// <summary>
        /// Offer the memory tools (remember, forget, recall), which keep facts across conversations under the client home's memory folder.
        /// </summary>
        public bool MemoryEnabled { get; set; } = true;

        /// <summary>
        /// Let web_retrieve fetch loopback and private network addresses. Off by default.
        /// </summary>
        public bool WebRetrieveAllowPrivateNetworks { get; set; } = false;

        /// <summary>
        /// Web search provider for the web_search tool. The tool is offered only when a provider is configured.
        /// </summary>
        public ChatWebSearchSettings WebSearch
        {
            get { return _WebSearch; }
            set { _WebSearch = value ?? new ChatWebSearchSettings(); }
        }

        /// <summary>
        /// External MCP servers whose tools chat may use.
        /// </summary>
        public List<ChatMcpServerSettings> McpServers
        {
            get { return _McpServers; }
            set { _McpServers = value ?? new List<ChatMcpServerSettings>(); }
        }
    }
}
