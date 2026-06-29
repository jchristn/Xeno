namespace Xeno.Sdk
{
    using System;
    using System.Text.Json;

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
        public JsonElement Choices { get; set; }

        /// <summary>
        /// Gets or sets the usage.
        /// </summary>
        public JsonElement Usage { get; set; }
    }
}
