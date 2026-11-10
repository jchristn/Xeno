namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Response for a local dashboard acknowledgement, such as dismissing a one-time notice.
    /// </summary>
    public class LocalAcknowledgeResponse
    {
        /// <summary>
        /// Whether the notice was acknowledged. Always true on success.
        /// </summary>
        public bool Acknowledged { get; set; } = true;
    }
}
