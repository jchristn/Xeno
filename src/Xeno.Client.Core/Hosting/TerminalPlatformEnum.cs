namespace Xeno.Client.Hosting
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Desktop platform used when planning terminal and shell launches.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TerminalPlatformEnum
    {
        /// <summary>
        /// Windows.
        /// </summary>
        Windows,

        /// <summary>
        /// macOS.
        /// </summary>
        MacOS,

        /// <summary>
        /// Linux and other Unix-like systems.
        /// </summary>
        Linux
    }
}
