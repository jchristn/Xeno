namespace Xeno.Email.Templates
{
    using System;

    /// <summary>
    /// Label/value field used in email templates.
    /// </summary>
    public class EmailTemplateField
    {
        /// <summary>
        /// Field label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// Field value.
        /// </summary>
        public string Value { get; set; } = String.Empty;

        /// <summary>
        /// Create a field.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <param name="value">Value.</param>
        /// <returns>Field.</returns>
        public static EmailTemplateField Create(string label, string? value)
        {
            return new EmailTemplateField
            {
                Label = label ?? String.Empty,
                Value = value ?? String.Empty
            };
        }
    }
}
