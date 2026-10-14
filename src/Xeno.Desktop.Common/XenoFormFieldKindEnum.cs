namespace Xeno.Desktop.Common
{
    /// <summary>
    /// Kind of input in a <see cref="XenoFormDialog"/>.
    /// </summary>
    public enum XenoFormFieldKindEnum
    {
        /// <summary>
        /// Single-line text.
        /// </summary>
        Text,

        /// <summary>
        /// Masked secret.
        /// </summary>
        Password,

        /// <summary>
        /// Whole number.
        /// </summary>
        Number,

        /// <summary>
        /// Check box ("true" or "false").
        /// </summary>
        Check,

        /// <summary>
        /// One of a fixed set of choices.
        /// </summary>
        Choice,

        /// <summary>
        /// Multi-line text.
        /// </summary>
        MultiLine
    }
}
