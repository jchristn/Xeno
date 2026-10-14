namespace Xeno.Desktop.Common
{
    using Xeno.Client.Presentation;

    /// <summary>
    /// Executes menu commands and reports their state for <see cref="XenoNativeMenuBuilder"/>.
    /// </summary>
    public interface IXenoMenuHost
    {
        /// <summary>
        /// Text for status-line entries.
        /// </summary>
        string StatusLine { get; }

        /// <summary>
        /// Determine whether a command is enabled.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>True when enabled.</returns>
        bool CanExecute(XenoCommandEnum command);

        /// <summary>
        /// Determine whether a radio command is selected.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>True when selected.</returns>
        bool IsChecked(XenoCommandEnum command);

        /// <summary>
        /// Execute a command.
        /// </summary>
        /// <param name="command">Command.</param>
        void Execute(XenoCommandEnum command);
    }
}
