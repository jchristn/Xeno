namespace Xeno.Email
{
    using System;

    /// <summary>
    /// Email attachment.
    /// </summary>
    public class EmailAttachment
    {
        /// <summary>
        /// Attachment filename.
        /// </summary>
        public string Filename { get; set; } = String.Empty;

        /// <summary>
        /// Attachment content type.
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// Attachment content.
        /// </summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
