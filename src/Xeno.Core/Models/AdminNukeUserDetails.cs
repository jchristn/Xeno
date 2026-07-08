namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// User details included in an administrative nuke response.
    /// </summary>
    public class AdminNukeUserDetails
    {
        /// <summary>
        /// User identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// User email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User status for the response.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
