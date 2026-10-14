namespace Xeno.Desktop.Common
{
    using Avalonia.Controls;

    /// <summary>
    /// Base window for Xeno dialogs. Records the close result so it is available whether the dialog was shown
    /// modally with an owner or unowned (as from the tray).
    /// </summary>
    public class XenoDialogWindow : Window
    {
        /// <summary>
        /// Result passed to <see cref="CloseWith(object?)"/>, or null.
        /// </summary>
        public object? Result { get; private set; } = null;

        /// <summary>
        /// Close the dialog with a result.
        /// </summary>
        /// <param name="result">Result.</param>
        public void CloseWith(object? result)
        {
            Result = result;
            Close(result);
        }
    }
}
