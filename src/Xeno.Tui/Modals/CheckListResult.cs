namespace Xeno.Tui.Modals
{
    using System.Collections.Generic;

    /// <summary>
    /// How a <see cref="CheckListDialog"/> closed: the button chosen and every row's checked state.
    /// </summary>
    internal sealed class CheckListResult
    {
        /// <summary>
        /// Index of the button chosen.
        /// </summary>
        public int Button { get; set; } = 0;

        /// <summary>
        /// Checked state of each row, in order.
        /// </summary>
        public List<bool> Checked { get; set; } = new List<bool>();
    }
}
