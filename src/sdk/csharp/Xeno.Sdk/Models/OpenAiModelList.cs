namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

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
        public List<RawJsonValue> Data { get; set; } = new List<RawJsonValue>();
    }
}
