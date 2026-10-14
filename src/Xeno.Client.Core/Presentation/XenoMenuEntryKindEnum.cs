namespace Xeno.Client.Presentation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Kind of menu entry.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoMenuEntryKindEnum
    {
        /// <summary>
        /// A command item.
        /// </summary>
        Item,

        /// <summary>
        /// A command item that shows a check mark when selected (one of a group).
        /// </summary>
        Radio,

        /// <summary>
        /// A submenu.
        /// </summary>
        Submenu,

        /// <summary>
        /// A separator line.
        /// </summary>
        Separator,

        /// <summary>
        /// A disabled line showing the current client status.
        /// </summary>
        StatusLine
    }
}
