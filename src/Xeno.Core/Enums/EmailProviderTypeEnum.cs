namespace Xeno.Core.Enums
{
    /// <summary>
    /// Email delivery provider type.
    /// </summary>
    public enum EmailProviderTypeEnum
    {
        /// <summary>
        /// Email delivery is disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// Email messages are written to local files.
        /// </summary>
        File,

        /// <summary>
        /// Email messages are delivered through Mailgun.
        /// </summary>
        Mailgun
    }
}
