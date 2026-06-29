namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Xeno open ai embedding list DTO.
    /// </summary>
    public class OpenAiEmbeddingList
    {
        /// <summary>
        /// Gets or sets the object.
        /// </summary>
        public string Object { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        public List<JsonElement> Data { get; set; } = new List<JsonElement>();

        /// <summary>
        /// Gets or sets the usage.
        /// </summary>
        public JsonElement Usage { get; set; }
    }
}
