namespace Xeno.Core.Settings
{
    /// <summary>
    /// File email provider settings.
    /// </summary>
    public class EmailFileSettings
    {
        /// <summary>
        /// Directory used by the file email provider. Default value is <c>data/email-outbox</c>.
        /// </summary>
        public string Directory { get; set; } = "data/email-outbox";
    }
}
