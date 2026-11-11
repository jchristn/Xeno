namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Keeps chat secrets (web search API keys, MCP server header and environment values) out of settings responses:
    /// they go out as <see cref="Placeholder"/>, and a save that sends the placeholder back keeps the stored value.
    /// </summary>
    public static class ChatSettingsSecrets
    {
        /// <summary>
        /// Value sent in place of a stored secret.
        /// </summary>
        public const string Placeholder = "configured";

        /// <summary>
        /// A copy of chat settings with secrets replaced by <see cref="Placeholder"/>.
        /// </summary>
        /// <param name="chat">Chat settings.</param>
        /// <returns>Copy.</returns>
        /// <exception cref="ArgumentNullException">chat is null.</exception>
        public static ClientChatSettings Redact(ClientChatSettings chat)
        {
            ArgumentNullException.ThrowIfNull(chat);
            ClientChatSettings copy = JsonUtil.Deserialize<ClientChatSettings>(JsonUtil.Serialize(chat)) ?? new ClientChatSettings();
            copy.WebSearch.TavilyApiKey = Mask(copy.WebSearch.TavilyApiKey);
            copy.WebSearch.YouApiKey = Mask(copy.WebSearch.YouApiKey);
            foreach (ChatMcpServerSettings server in copy.McpServers)
            {
                foreach (string key in server.Headers.Keys.ToList()) server.Headers[key] = Mask(server.Headers[key]) ?? String.Empty;
                foreach (string key in server.Environment.Keys.ToList()) server.Environment[key] = Mask(server.Environment[key]) ?? String.Empty;
            }

            return copy;
        }

        /// <summary>
        /// Put stored secrets back where a saved copy still holds <see cref="Placeholder"/>.
        /// </summary>
        /// <param name="incoming">Settings being saved; changed in place.</param>
        /// <param name="existing">Settings stored now.</param>
        /// <exception cref="ArgumentNullException">incoming or existing is null.</exception>
        public static void Restore(ClientChatSettings incoming, ClientChatSettings existing)
        {
            ArgumentNullException.ThrowIfNull(incoming);
            ArgumentNullException.ThrowIfNull(existing);
            if (incoming.WebSearch.TavilyApiKey == Placeholder) incoming.WebSearch.TavilyApiKey = existing.WebSearch.TavilyApiKey;
            if (incoming.WebSearch.YouApiKey == Placeholder) incoming.WebSearch.YouApiKey = existing.WebSearch.YouApiKey;
            foreach (ChatMcpServerSettings server in incoming.McpServers)
            {
                ChatMcpServerSettings? stored = existing.McpServers.FirstOrDefault(item => String.Equals(item.Name, server.Name, StringComparison.OrdinalIgnoreCase));
                RestoreValues(server.Headers, stored?.Headers);
                RestoreValues(server.Environment, stored?.Environment);
            }
        }

        private static string? Mask(string? value)
        {
            return String.IsNullOrEmpty(value) ? value : Placeholder;
        }

        private static void RestoreValues(Dictionary<string, string> incoming, Dictionary<string, string>? stored)
        {
            foreach (string key in incoming.Keys.ToList())
            {
                if (incoming[key] != Placeholder) continue;
                string? value = null;
                if (stored != null) stored.TryGetValue(key, out value);
                if (value == null && stored != null) value = stored.FirstOrDefault(item => String.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
                if (value == null) incoming.Remove(key);
                else incoming[key] = value;
            }
        }
    }
}
