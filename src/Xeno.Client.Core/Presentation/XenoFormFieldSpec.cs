namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A field in a <see cref="XenoFormSpec"/>. Front ends render it with their own input controls.
    /// </summary>
    public sealed class XenoFormFieldSpec
    {
        /// <summary>
        /// Result key.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// Value kind.
        /// </summary>
        public XenoSettingKindEnum Kind { get; set; } = XenoSettingKindEnum.Text;

        /// <summary>
        /// Initial value. Toggles use "true" or "false".
        /// </summary>
        public string Value { get; set; } = String.Empty;

        /// <summary>
        /// Placeholder for empty text inputs.
        /// </summary>
        public string Placeholder { get; set; } = String.Empty;

        /// <summary>
        /// Help text.
        /// </summary>
        public string Help { get; set; } = String.Empty;

        /// <summary>
        /// Choices for <see cref="XenoSettingKindEnum.Choice"/>.
        /// </summary>
        public List<string> Choices { get; set; } = new List<string>();

        /// <summary>
        /// True when an empty value is rejected.
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// True to mask the input (passwords, credentials).
        /// </summary>
        public bool Secret { get; set; } = false;
    }
}
