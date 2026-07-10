namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Xeno open ai chat completion DTO.
    /// </summary>
    public class OpenAiChatCompletion
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the object.
        /// </summary>
        public string Object { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the choices.
        /// </summary>
        public List<RawJsonValue> Choices { get; set; } = new List<RawJsonValue>();

        /// <summary>
        /// Gets or sets the usage.
        /// </summary>
        public RawJsonValue Usage { get; set; } = new RawJsonValue();
    }
}
