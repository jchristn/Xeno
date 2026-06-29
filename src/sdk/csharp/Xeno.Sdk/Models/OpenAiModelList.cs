namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Xeno open ai model list DTO.
    /// </summary>
    public class OpenAiModelList
    {
        /// <summary>
        /// Gets or sets the object.
        /// </summary>
        public string Object { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        public List<JsonElement> Data { get; set; } = new List<JsonElement>();
    }
}
