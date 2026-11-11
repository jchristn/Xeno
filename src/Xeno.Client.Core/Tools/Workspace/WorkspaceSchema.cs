namespace Xeno.Client.Tools.Workspace
{
    using System.Collections.Generic;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// Builds tool argument schemas: <c>WorkspaceSchema.Object(WorkspaceSchema.Text("path", "..."), "path")</c> makes an
    /// object schema with a required path property. Strings among the parts name required properties.
    /// </summary>
    public static class WorkspaceSchema
    {
        /// <summary>
        /// Top-level argument schema.
        /// </summary>
        /// <param name="parts">Properties (from the builders below) and required property names.</param>
        /// <returns>Schema.</returns>
        public static ChatToolSchemaDto Object(params object[] parts)
        {
            ChatToolSchemaDto schema = new ChatToolSchemaDto();
            foreach (object part in parts)
            {
                if (part is KeyValuePair<string, ChatToolPropertyDto> property) schema.Properties[property.Key] = property.Value;
                else if (part is string required) schema.Required.Add(required);
            }

            return schema;
        }

        /// <summary>
        /// A nested object property.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="parts">Properties and required property names.</param>
        /// <returns>Property.</returns>
        public static KeyValuePair<string, ChatToolPropertyDto> ObjectProperty(string name, string description, params object[] parts)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, Nested(description, parts));
        }

        /// <summary>
        /// A nested object schema without a name, for array items.
        /// </summary>
        /// <param name="description">Description.</param>
        /// <param name="parts">Properties and required property names.</param>
        /// <returns>Schema.</returns>
        public static ChatToolPropertyDto Nested(string description, params object[] parts)
        {
            ChatToolPropertyDto schema = new ChatToolPropertyDto { Type = "object", Description = description, Properties = new Dictionary<string, ChatToolPropertyDto>(), Required = new List<string>() };
            foreach (object part in parts)
            {
                if (part is KeyValuePair<string, ChatToolPropertyDto> property) schema.Properties[property.Key] = property.Value;
                else if (part is string required) schema.Required.Add(required);
            }

            if (schema.Required.Count == 0) schema.Required = null;
            return schema;
        }

        /// <summary>
        /// A string property.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <returns>Property.</returns>
        public static KeyValuePair<string, ChatToolPropertyDto> Text(string name, string description)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "string", Description = description });
        }

        /// <summary>
        /// An integer property.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <returns>Property.</returns>
        public static KeyValuePair<string, ChatToolPropertyDto> Integer(string name, string description)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "integer", Description = description });
        }

        /// <summary>
        /// A boolean property.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <returns>Property.</returns>
        public static KeyValuePair<string, ChatToolPropertyDto> Bool(string name, string description)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "boolean", Description = description });
        }

        /// <summary>
        /// A string property limited to some values.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="values">Allowed values.</param>
        /// <returns>Property.</returns>
        public static KeyValuePair<string, ChatToolPropertyDto> Choice(string name, string description, params string[] values)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "string", Description = description, Enum = new List<string>(values) });
        }

        /// <summary>
        /// An array property.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        /// <param name="items">Schema of each element, for example <c>new ChatToolPropertyDto { Type = "string" }</c> or <see cref="Nested"/>.</param>
        /// <returns>Property.</returns>
        public static KeyValuePair<string, ChatToolPropertyDto> Array(string name, string description, ChatToolPropertyDto items)
        {
            return new KeyValuePair<string, ChatToolPropertyDto>(name, new ChatToolPropertyDto { Type = "array", Description = description, Items = items });
        }
    }
}
