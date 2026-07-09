namespace Xeno.Email.Templates
{
    using System;
    using System.Net;

    /// <summary>
    /// Email HTML encoding helpers.
    /// </summary>
    public static class EmailHtmlEncoder
    {
        /// <summary>
        /// Encode a value for HTML.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Encoded value.</returns>
        public static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? String.Empty);
        }

        /// <summary>
        /// Encode a value for an HTML attribute.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Encoded value.</returns>
        public static string Attribute(string? value)
        {
            return WebUtility.HtmlEncode(value ?? String.Empty).Replace("\"", "&quot;", StringComparison.Ordinal);
        }
    }
}
