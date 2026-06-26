namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Raw API response returned by Xeno SDK methods that expose JSON payloads.
    /// </summary>
    public class XenoApiResponse
    {
        /// <summary>
        /// Response body text.
        /// </summary>
        public string Body { get; set; } = String.Empty;

        /// <summary>
        /// Returns the response body.
        /// </summary>
        /// <returns>Response body text.</returns>
        public override string ToString()
        {
            return Body;
        }
    }
}
