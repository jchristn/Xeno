namespace Xeno.Client.Hosting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Xeno client executables that launch one another.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoApplicationEnum
    {
        /// <summary>
        /// The xeno command-line tool, which also runs the background worker and hosts the terminal app (xeno tui).
        /// </summary>
        Cli,

        /// <summary>
        /// The xeno-desktop desktop application.
        /// </summary>
        Desktop,

        /// <summary>
        /// The xeno-tray tray agent.
        /// </summary>
        Tray
    }
}
