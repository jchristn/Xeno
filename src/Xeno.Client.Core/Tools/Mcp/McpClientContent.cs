namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Globalization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One content block of a tool result: text, image, audio, resource_link, or resource.
    /// </summary>
    internal sealed class McpClientContent
    {
        /// <summary>
        /// Block type.
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; } = null;

        /// <summary>
        /// Text, for text blocks.
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; } = null;

        /// <summary>
        /// Base64 data, for image and audio blocks.
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; } = null;

        /// <summary>
        /// MIME type, for image, audio, and resource_link blocks.
        /// </summary>
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; } = null;

        /// <summary>
        /// URI, for resource_link blocks.
        /// </summary>
        [JsonPropertyName("uri")]
        public string? Uri { get; set; } = null;

        /// <summary>
        /// Name, for resource_link blocks.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Embedded resource, for resource blocks.
        /// </summary>
        [JsonPropertyName("resource")]
        public McpClientResourceContents? Resource { get; set; } = null;

        /// <summary>
        /// The block as text: text as is, other blocks described in one line.
        /// </summary>
        /// <returns>Text, or null for an empty block.</returns>
        public string? ToText()
        {
            string type = (Type ?? "text").Trim().ToLowerInvariant();
            switch (type)
            {
                case "text":
                    return Text;
                case "image":
                case "audio":
                    return "[" + type + Join(MimeType, Size(Data)) + "]";
                case "resource_link":
                    return "[resource link" + Join(Name, Uri, MimeType) + "]";
                case "resource":
                    if (Resource != null && Resource.Text != null) return Resource.Text;
                    return "[resource" + Join(Resource?.Uri, Resource?.MimeType, Size(Resource?.Blob)) + "]";
                default:
                    return "[" + type + " content]";
            }
        }

        private static string? Size(string? base64)
        {
            if (String.IsNullOrEmpty(base64)) return null;
            long bytes = (long)base64.Length * 3 / 4;
            return bytes.ToString(CultureInfo.InvariantCulture) + " bytes";
        }

        private static string Join(params string?[] parts)
        {
            string text = String.Empty;
            foreach (string? part in parts)
            {
                if (String.IsNullOrWhiteSpace(part)) continue;
                text += (text.Length == 0 ? " " : ", ") + part;
            }

            return text;
        }
    }
}
