namespace Xeno.Core.Chat
{
    /// <summary>
    /// Limits the chat engine applies to every turn.
    /// </summary>
    public interface IChatLimits
    {
        /// <summary>
        /// Days chat request history and tool calls are kept.
        /// </summary>
        int HistoryRetentionDays { get; }

        /// <summary>
        /// Most model calls in one turn when the model uses tools.
        /// </summary>
        int MaxToolIterations { get; }

        /// <summary>
        /// Most tool calls in one turn.
        /// </summary>
        int MaxToolCallsPerTurn { get; }

        /// <summary>
        /// Seconds to wait for Allow or Deny on a tool that changes something.
        /// </summary>
        int ToolApprovalTimeoutSeconds { get; }
    }
}
