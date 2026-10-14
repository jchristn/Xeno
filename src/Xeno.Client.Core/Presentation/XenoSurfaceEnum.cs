namespace Xeno.Client.Presentation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Front end a menu is built for.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoSurfaceEnum
    {
        /// <summary>
        /// The desktop app.
        /// </summary>
        Desktop,

        /// <summary>
        /// The terminal app.
        /// </summary>
        Terminal,

        /// <summary>
        /// The tray agent.
        /// </summary>
        Tray
    }
}
