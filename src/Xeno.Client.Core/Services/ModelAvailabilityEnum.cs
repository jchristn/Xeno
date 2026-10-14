namespace Xeno.Client.Services
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Where a model can be served from, from this machine's point of view.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ModelAvailabilityEnum
    {
        /// <summary>
        /// Served only by a provider on this machine.
        /// </summary>
        ThisMachine,

        /// <summary>
        /// Offered only by other machines on the Xeno network.
        /// </summary>
        XenoNetwork,

        /// <summary>
        /// Served by this machine and also offered by the Xeno network.
        /// </summary>
        Both
    }
}
