namespace Xeno.Core.Chat
{
    /// <summary>
    /// Default chat limits: 30 days of history, 8 model calls and 16 tool calls per turn, 120 seconds to approve a tool.
    /// </summary>
    public sealed class DefaultChatLimits : IChatLimits
    {
        /// <inheritdoc />
        public int HistoryRetentionDays { get; set; } = 30;

        /// <inheritdoc />
        public int MaxToolIterations { get; set; } = 8;

        /// <inheritdoc />
        public int MaxToolCallsPerTurn { get; set; } = 16;

        /// <inheritdoc />
        public int ToolApprovalTimeoutSeconds { get; set; } = 120;
    }
}
