namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Whether a model is loaded and what it can do, from preparing it for chat.
    /// </summary>
    public sealed class ChatModelReadiness
    {
        /// <summary>Model.</summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>Where requests for it go: "local:&lt;provider&gt;", "network", or "unavailable".</summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>True when the model is loaded on this machine and ready.</summary>
        public bool Loaded { get; set; } = false;

        /// <summary>True when the model can call tools.</summary>
        public bool SupportsTools { get; set; } = false;

        /// <summary>True when the model reports separate reasoning text.</summary>
        public bool SupportsThinking { get; set; } = false;

        /// <summary>Milliseconds loading took.</summary>
        public double LoadMs { get; set; } = 0;

        /// <summary>What happened, in words.</summary>
        public string Message { get; set; } = String.Empty;
    }
}
