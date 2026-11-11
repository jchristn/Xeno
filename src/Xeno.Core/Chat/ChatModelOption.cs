namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// A model offered for chat. Field names match the client's model catalog entries so the same chat page lists both.
    /// </summary>
    public sealed class ChatModelOption
    {
        /// <summary>Name.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>True when a provider on the asking machine serves it (always false on the server).</summary>
        public bool ServedLocally { get; set; } = false;

        /// <summary>True when the Xeno network offers it.</summary>
        public bool OnNetwork { get; set; } = true;

        /// <summary>True when it answers chat.</summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>True when it makes embeddings.</summary>
        public bool SupportsEmbeddings { get; set; } = false;
    }
}
