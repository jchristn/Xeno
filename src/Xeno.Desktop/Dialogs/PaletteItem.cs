namespace Xeno.Desktop.Dialogs
{
    using Xeno.Client.Presentation;

    /// <summary>
    /// A command palette entry.
    /// </summary>
    public sealed class PaletteItem
    {
        /// <summary>
        /// Command.
        /// </summary>
        public XenoCommandEnum Command { get; }

        /// <summary>
        /// Display title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Create an entry.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="title">Display title.</param>
        public PaletteItem(XenoCommandEnum command, string title)
        {
            Command = command;
            Title = title;
        }

        /// <summary>
        /// Display text.
        /// </summary>
        /// <returns>Title.</returns>
        public override string ToString()
        {
            return Title;
        }
    }
}
