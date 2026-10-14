namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A field in a <see cref="XenoFormDialog"/>. Values are exchanged as strings; check boxes use "true" and "false".
    /// </summary>
    public sealed class XenoFormField
    {
        /// <summary>
        /// Key used in the result dictionary.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// Input kind.
        /// </summary>
        public XenoFormFieldKindEnum Kind { get; set; } = XenoFormFieldKindEnum.Text;

        /// <summary>
        /// Initial value.
        /// </summary>
        public string Value { get; set; } = String.Empty;

        /// <summary>
        /// Placeholder shown in empty text inputs.
        /// </summary>
        public string Placeholder { get; set; } = String.Empty;

        /// <summary>
        /// Help text under the input.
        /// </summary>
        public string Help { get; set; } = String.Empty;

        /// <summary>
        /// Choices for <see cref="XenoFormFieldKindEnum.Choice"/>.
        /// </summary>
        public List<string> Choices { get; set; } = new List<string>();

        /// <summary>
        /// True when an empty value is rejected.
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// True to show the field read-only.
        /// </summary>
        public bool ReadOnly { get; set; } = false;
    }
}
