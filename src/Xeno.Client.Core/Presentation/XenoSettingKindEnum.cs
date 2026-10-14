namespace Xeno.Client.Presentation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Kind of value an editable setting holds.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoSettingKindEnum
    {
        /// <summary>
        /// On or off ("true" or "false").
        /// </summary>
        Toggle,

        /// <summary>
        /// Whole number within a range.
        /// </summary>
        Number,

        /// <summary>
        /// Free text.
        /// </summary>
        Text,

        /// <summary>
        /// One of a fixed set of choices.
        /// </summary>
        Choice
    }
}
