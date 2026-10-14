namespace Xeno.Client.Presentation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Light or dark appearance for the desktop app and the terminal app.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoAppearanceEnum
    {
        /// <summary>
        /// Follow the operating system (desktop) or use the dark theme (terminal).
        /// </summary>
        System,

        /// <summary>
        /// Light.
        /// </summary>
        Light,

        /// <summary>
        /// Dark.
        /// </summary>
        Dark
    }
}
