namespace Xeno.Client.LocalInference.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provider request value that accepts either one string or a string array.
    /// </summary>
    [JsonConverter(typeof(StringOrStringArrayJsonConverter))]
    public class StringOrStringArray
    {
        /// <summary>
        /// Values supplied by the caller.
        /// </summary>
        public List<string> Values { get; set; } = new List<string>();

        /// <summary>
        /// Whether the caller supplied an array.
        /// </summary>
        public bool IsArray { get; set; } = false;
    }
}
