namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Telemetry for one answer and the turn that produced it, shown in the answer's information tooltip.
    /// </summary>
    public sealed class ChatMessageStats
    {
        /// <summary>Model.</summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>Route: "local:&lt;provider&gt;" or "network".</summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>Provider or client that answered.</summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>True when the answer was streamed.</summary>
        public bool Streamed { get; set; } = true;

        /// <summary>Milliseconds from sending the message to the first answer text.</summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>Milliseconds from the first to the last answer text.</summary>
        public double? StreamingMs { get; set; } = null;

        /// <summary>Milliseconds this answer's model call took.</summary>
        public double? ModelMs { get; set; } = null;

        /// <summary>Milliseconds spent running tools in the turn.</summary>
        public double ToolMs { get; set; } = 0;

        /// <summary>Tool calls in the turn.</summary>
        public int ToolCalls { get; set; } = 0;

        /// <summary>Model calls in the turn (more than one when the model used tools).</summary>
        public int ModelCalls { get; set; } = 1;

        /// <summary>Milliseconds for the whole turn.</summary>
        public double? TurnMs { get; set; } = null;

        /// <summary>Prompt tokens for this answer.</summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>Prompt tokens served from the provider cache.</summary>
        public int? CachedPromptTokens { get; set; } = null;

        /// <summary>Completion tokens for this answer.</summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>Reasoning tokens inside the completion tokens.</summary>
        public int? ReasoningTokens { get; set; } = null;

        /// <summary>Prompt tokens summed over every model call in the turn.</summary>
        public int? TurnPromptTokens { get; set; } = null;

        /// <summary>Completion tokens summed over every model call in the turn.</summary>
        public int? TurnCompletionTokens { get; set; } = null;

        /// <summary>Completion tokens per second while streaming, or null.</summary>
        public double? TokensPerSecond { get; set; } = null;
    }
}
