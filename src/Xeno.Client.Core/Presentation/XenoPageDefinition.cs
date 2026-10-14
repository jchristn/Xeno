namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// A page in the shared navigation catalog.
    /// </summary>
    public class XenoPageDefinition
    {
        private string _LongTitle = String.Empty;

        /// <summary>
        /// Page.
        /// </summary>
        public XenoPageEnum Page { get; set; } = XenoPageEnum.Overview;

        /// <summary>
        /// Title shown in the navigation sidebar, under its section heading.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Title used where the navigation section is not shown: menus, the command palette, and page headers.
        /// Falls back to <see cref="Title"/> when empty.
        /// </summary>
        public string LongTitle
        {
            get { return String.IsNullOrEmpty(_LongTitle) ? Title : _LongTitle; }
            set { _LongTitle = value ?? String.Empty; }
        }

        /// <summary>
        /// Navigation section the page belongs to.
        /// </summary>
        public string Section { get; set; } = String.Empty;

        /// <summary>
        /// One-sentence description shown under the page title.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Shortcut digit (1 through 9) used with Cmd/Ctrl in the desktop app and Alt in the terminal app.
        /// </summary>
        public int ShortcutDigit { get; set; } = 0;

        /// <summary>
        /// Menu command that navigates to the page.
        /// </summary>
        public XenoCommandEnum Command { get; set; } = XenoCommandEnum.GoOverview;
    }
}
