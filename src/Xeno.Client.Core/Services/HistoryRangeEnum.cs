namespace Xeno.Client.Services
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Time range for request history and telemetry views.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HistoryRangeEnum
    {
        /// <summary>
        /// The last hour.
        /// </summary>
        Hour,

        /// <summary>
        /// The last day.
        /// </summary>
        Day,

        /// <summary>
        /// The last seven days.
        /// </summary>
        Week,

        /// <summary>
        /// The last thirty days.
        /// </summary>
        Month
    }
}
