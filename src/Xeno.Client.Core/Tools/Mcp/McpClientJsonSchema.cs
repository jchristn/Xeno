namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// The parts of a JSON Schema that chat tool definitions carry, read from an MCP tool's inputSchema.
    /// </summary>
    internal sealed class McpClientJsonSchema
    {
        private const int MaxDepth = 32;

        private static readonly HashSet<string> _KnownTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "string", "integer", "number", "boolean", "array", "object"
        };

        /// <summary>
        /// Type names; JSON Schema allows one name or several.
        /// </summary>
        [JsonPropertyName("type")]
        [JsonConverter(typeof(McpClientStringListConverter))]
        public List<string>? Type { get; set; } = null;

        /// <summary>
        /// Title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Allowed values as text.
        /// </summary>
        [JsonPropertyName("enum")]
        [JsonConverter(typeof(McpClientStringListConverter))]
        public List<string>? Enum { get; set; } = null;

        /// <summary>
        /// Element schema for arrays.
        /// </summary>
        [JsonPropertyName("items")]
        public McpClientJsonSchema? Items { get; set; } = null;

        /// <summary>
        /// Properties for objects.
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, McpClientJsonSchema>? Properties { get; set; } = null;

        /// <summary>
        /// Required property names for objects.
        /// </summary>
        [JsonPropertyName("required")]
        public List<string>? Required { get; set; } = null;

        /// <summary>
        /// Alternatives; the first with a usable type is used.
        /// </summary>
        [JsonPropertyName("anyOf")]
        public List<McpClientJsonSchema>? AnyOf { get; set; } = null;

        /// <summary>
        /// Alternatives; the first with a usable type is used.
        /// </summary>
        [JsonPropertyName("oneOf")]
        public List<McpClientJsonSchema>? OneOf { get; set; } = null;

        /// <summary>
        /// Combined schemas; the first with a usable type is used.
        /// </summary>
        [JsonPropertyName("allOf")]
        public List<McpClientJsonSchema>? AllOf { get; set; } = null;

        /// <summary>
        /// Convert a tool's top-level schema.
        /// </summary>
        /// <returns>Chat tool schema.</returns>
        public ChatToolSchemaDto ToToolSchema()
        {
            ChatToolSchemaDto schema = new ChatToolSchemaDto();
            McpClientJsonSchema effective = Effective(0);
            if (effective.Properties != null)
            {
                foreach (KeyValuePair<string, McpClientJsonSchema> property in effective.Properties)
                {
                    if (property.Value == null) continue;
                    schema.Properties[property.Key] = property.Value.ToProperty(1);
                }
            }

            if (effective.Required != null)
            {
                foreach (string name in effective.Required)
                {
                    if (!String.IsNullOrEmpty(name) && !schema.Required.Contains(name)) schema.Required.Add(name);
                }
            }

            return schema;
        }

        private ChatToolPropertyDto ToProperty(int depth)
        {
            McpClientJsonSchema effective = Effective(depth);
            string type = effective.ResolveType() ?? (effective.Properties != null ? "object" : effective.Items != null ? "array" : "string");
            ChatToolPropertyDto property = new ChatToolPropertyDto
            {
                Type = type,
                Description = FirstText(Description, effective.Description, Title, effective.Title)
            };

            if (effective.Enum != null && effective.Enum.Count > 0) property.Enum = new List<string>(effective.Enum);
            if (depth >= MaxDepth) return property;

            if (type == "array")
            {
                property.Items = effective.Items != null ? effective.Items.ToProperty(depth + 1) : new ChatToolPropertyDto();
            }
            else if (type == "object" && effective.Properties != null)
            {
                property.Properties = new Dictionary<string, ChatToolPropertyDto>();
                foreach (KeyValuePair<string, McpClientJsonSchema> child in effective.Properties)
                {
                    if (child.Value == null) continue;
                    property.Properties[child.Key] = child.Value.ToProperty(depth + 1);
                }

                if (effective.Required != null && effective.Required.Count > 0) property.Required = new List<string>(effective.Required);
            }

            return property;
        }

        // A schema with no usable type of its own takes the first alternative that has one (for example the
        // non-null branch of anyOf: [{"type": "string"}, {"type": "null"}]).
        private McpClientJsonSchema Effective(int depth)
        {
            if (ResolveType() != null || depth >= MaxDepth) return this;
            foreach (List<McpClientJsonSchema>? alternatives in new List<List<McpClientJsonSchema>?> { AnyOf, OneOf, AllOf })
            {
                if (alternatives == null) continue;
                foreach (McpClientJsonSchema alternative in alternatives)
                {
                    if (alternative == null) continue;
                    McpClientJsonSchema candidate = alternative.Effective(depth + 1);
                    if (candidate.ResolveType() != null) return candidate;
                }
            }

            return this;
        }

        private string? ResolveType()
        {
            if (Type == null) return null;
            foreach (string name in Type)
            {
                if (_KnownTypes.Contains(name)) return name;
            }

            return null;
        }

        private static string FirstText(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }

            return String.Empty;
        }
    }
}
